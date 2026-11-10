using System.Data.Common;
using System.Globalization;
using GPE.Datos;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GPE.Logica;

// Envía los avisos de AVISO_CORREO y los resúmenes diarios. Cuenta por variables de entorno:
//   GPE_SMTP_USUARIO, GPE_SMTP_CLAVE (contraseña de aplicación),
//   GPE_SMTP_SERVIDOR (smtp.gmail.com), GPE_SMTP_PUERTO (465 SSL; con 587, STARTTLS)
// Sin configurar no se envía nada. MailKit porque SmtpClient cortaba la conexión con Gmail.
public static class AvisoServicio
{
    private static readonly AvisoRepositorio repositorio = new();
    private static int enviando;   // 1 mientras hay un envío en marcha en esta aplicación

    // No bloquea la pantalla: un fallo del correo nunca impide seguir trabajando
    public static void EnviarPendientesEnSegundoPlano() => Task.Run(() =>
    {
        try { EnviarPendientes(); }
        catch (Exception) { /* se reintenta en el siguiente envío */ }
    });

    public static void EnviarPendientes()
    {
        if (Interlocked.Exchange(ref enviando, 1) == 1) return;
        try
        {
            if (!Configurado(out string remitente, out _)) return;

            var avisos = repositorio.ReservarPendientes();
            if (avisos.Count == 0) return;

            SmtpClient cliente;
            try
            {
                cliente = Conectar();
            }
            catch (Exception ex) when (ex is not DbException)
            {
                // Sin conexión o sin acceso a la cuenta: se anota en todos para poder ver el motivo
                foreach (var aviso in avisos)
                    repositorio.MarcarFallo(aviso.Id, DescribirError(ex));
                return;
            }

            using (cliente)
            {
                foreach (var aviso in avisos)
                {
                    try
                    {
                        var (asunto, cuerpo) = Redactar(aviso);
                        cliente.Send(Mensaje(remitente, aviso.Destinatario, asunto, cuerpo));
                        repositorio.MarcarEnviado(aviso.Id);
                    }
                    catch (Exception ex) when (ex is not DbException)
                    {
                        repositorio.MarcarFallo(aviso.Id, DescribirError(ex));
                    }
                }
                cliente.Disconnect(true);
            }
        }
        finally
        {
            Interlocked.Exchange(ref enviando, 0);
        }
    }

    // A transporte, las de la fase 1 sin recogida; al almacén, las de la 3 sin revisar; a administración,
    // las de la 4 sin abono. Los viernes, además, al almacén las de la 2 que aún no han llegado, para
    // poder reclamarlas (cada día no: no se sabe cuánto tardarán).
    // Uno por persona y tipo, sin repetir en el día. Devuelve cuántos ha enviado.
    public static int EnviarResumenDiario()
    {
        if (!Configurado(out string remitente, out _))
            throw new InvalidOperationException("Falta configurar GPE_SMTP_USUARIO y GPE_SMTP_CLAVE.");

        var resumenes = new List<(string Tipo, List<DevolucionPendiente> Pendientes)>
        {
            (TipoResumen.RecogidaPendiente, repositorio.ObtenerPendientes(Fase.SolicitudRecibida, TipoResumen.RecogidaPendiente)),
            (TipoResumen.RevisionPendiente, repositorio.ObtenerPendientes(Fase.MercanciaRecibida, TipoResumen.RevisionPendiente)),
            (TipoResumen.AbonoPendiente, repositorio.ObtenerPendientes(Fase.RevisadaAlmacen, TipoResumen.AbonoPendiente))
        };
        if (DateTime.Today.DayOfWeek == DayOfWeek.Friday)
            resumenes.Add((TipoResumen.SinLlegar, repositorio.ObtenerPendientes(Fase.RecogidaSolicitada, TipoResumen.SinLlegar)));
        if (resumenes.All(r => r.Pendientes.Count == 0)) return 0;

        int enviados = 0;
        using var cliente = Conectar();
        foreach (var (tipo, pendientes) in resumenes)
        {
            foreach (var grupo in pendientes.GroupBy(p => p.Destinatario))
            {
                var (asunto, cuerpo) = RedactarResumen(tipo, grupo.ToList());
                cliente.Send(Mensaje(remitente, grupo.Key, asunto, cuerpo));
                repositorio.MarcarResumenEnviado(tipo, grupo.Key);
                enviados++;
            }
        }
        cliente.Disconnect(true);
        return enviados;
    }

    // Acciones de calidad

    private static readonly AvisoAccionRepositorio repositorioAcciones = new();
    private const string ResumenAcciones = "ACCIONES_NC";   // tipo en AVISO_RESUMEN
    private const string NombreCalidad = "GPE · Calidad";
    private static int enviandoAcciones;

    // Al guardar una ficha: no bloquea la pantalla y un fallo del correo no impide seguir
    public static void EnviarAsignacionesEnSegundoPlano() => Task.Run(() =>
    {
        try { EnviarAsignaciones(); }
        catch (Exception) { /* se reintenta al guardar otra ficha o en la tarea diaria */ }
    });

    // Un correo a cada responsable con las acciones que se le acaban de poner. Devuelve cuántos ha enviado.
    public static int EnviarAsignaciones()
    {
        // Sin cuenta configurada no se reserva nada: si no, se darían por avisadas sin haberlo hecho
        if (!Configurado(out string remitente, out _)) return 0;
        if (Interlocked.Exchange(ref enviandoAcciones, 1) == 1) return 0;
        try
        {
            var acciones = repositorioAcciones.ReservarAsignaciones();
            if (acciones.Count == 0) return 0;

            SmtpClient cliente;
            try
            {
                cliente = Conectar();
            }
            catch (Exception)
            {
                // Sin conexión o sin acceso a la cuenta: se vuelven a intentar la próxima vez
                foreach (var accion in acciones) repositorioAcciones.QuitarAviso(accion.AccionId);
                throw;
            }

            int enviados = 0;
            using (cliente)
            {
                foreach (var grupo in acciones.GroupBy(a => a.Destinatario, StringComparer.OrdinalIgnoreCase))
                {
                    var lista = Ordenar(grupo);
                    try
                    {
                        var (fotos, sinAdjuntar) = FotosParaCorreo(lista);
                        var (asunto, cuerpo) = RedactarAsignacion(lista, fotos.Count, sinAdjuntar);
                        var mensaje = Mensaje(remitente, grupo.Key, asunto, cuerpo, NombreCalidad, fotos);
                        cliente.Send(mensaje);
                        enviados++;
                    }
                    catch (Exception ex) when (ex is not DbException)
                    {
                        foreach (var accion in lista) repositorioAcciones.QuitarAviso(accion.AccionId);
                    }
                }
                cliente.Disconnect(true);
            }
            return enviados;
        }
        finally
        {
            Interlocked.Exchange(ref enviandoAcciones, 0);
        }
    }

    // Cada día, a cada responsable con correo, todas sus acciones sin hacer. Una vez al día como mucho.
    public static int EnviarResumenAcciones()
    {
        if (!Configurado(out string remitente, out _))
            throw new InvalidOperationException("Falta configurar GPE_SMTP_USUARIO y GPE_SMTP_CLAVE.");

        var pendientes = repositorioAcciones.ObtenerPendientesResumen(ResumenAcciones);
        if (pendientes.Count == 0) return 0;

        int enviados = 0;
        using var cliente = Conectar();
        foreach (var grupo in pendientes.GroupBy(a => a.Destinatario, StringComparer.OrdinalIgnoreCase))
        {
            var (asunto, cuerpo) = RedactarResumenAcciones(Ordenar(grupo));
            cliente.Send(Mensaje(remitente, grupo.Key, asunto, cuerpo, NombreCalidad));
            repositorio.MarcarResumenEnviado(ResumenAcciones, grupo.Key);
            enviados++;
        }
        cliente.Disconnect(true);
        return enviados;
    }

    // Hasta 15 MB de fotos por correo (los servidores suelen rechazar los de más de 20-25 MB).
    // Las que no caben no se envían: se dice en el correo cuántas son.
    private const int MaximoFotosCorreo = 15 * 1024 * 1024;

    private static (List<ArchivoNuevo> Fotos, int SinAdjuntar) FotosParaCorreo(List<AccionPendienteAviso> acciones)
    {
        var todas = repositorioAcciones.ObtenerFotos(acciones.Select(a => a.NoConformidadId));
        var adjuntas = new List<ArchivoNuevo>();
        long total = 0;
        foreach (var foto in todas)
        {
            if (total + foto.Contenido.Length > MaximoFotosCorreo) continue;
            adjuntas.Add(foto);
            total += foto.Contenido.Length;
        }
        return (adjuntas, todas.Count - adjuntas.Count);
    }

    // Primero las de prioridad alta; dentro de cada prioridad, la de plazo más cercano (sin plazo, al final)
    private static List<AccionPendienteAviso> Ordenar(IEnumerable<AccionPendienteAviso> acciones) => acciones
        .OrderBy(a => PrioridadAccion.Orden(a.Prioridad))
        .ThenBy(a => a.Fecha ?? DateTime.MaxValue)
        .ThenBy(a => a.AccionId)
        .ToList();

    private static (string Asunto, string Cuerpo) RedactarAsignacion(List<AccionPendienteAviso> acciones,
        int fotos = 0, int fotosSinAdjuntar = 0)
    {
        int n = acciones.Count;
        string asunto = n == 1
            ? $"Tienes una acción de calidad nueva: {Registro(acciones[0])}"
            : $"Tienes {n} acciones de calidad nuevas";

        string textoFotos = (fotos, fotosSinAdjuntar) switch
        {
            (0, 0) => "",
            (_, 0) => fotos == 1 ? "Va adjunta la foto del problema." : $"Van adjuntas las {fotos} fotos del problema.",
            _ => $"Van adjuntas {fotos} fotos del problema; hay {fotosSinAdjuntar} más en GPE que no caben en el correo."
        };

        return (asunto, $"""
            Hola, {acciones[0].Responsable}.

            {(n == 1 ? "Te han puesto como responsable de esta acción en GPE:" : "Te han puesto como responsable de estas acciones en GPE:")}

            {string.Join(Environment.NewLine + Environment.NewLine, acciones.Select(DescribirAccion))}

            {(textoFotos == "" ? "" : textoFotos + Environment.NewLine + Environment.NewLine)}Cuando la termines, avisa a Calidad para que la marque como hecha. Mientras esté pendiente,
            te llegará cada día un resumen con todas tus acciones sin hacer.

            Este correo se ha enviado automáticamente; no hace falta responder.
            """);
    }

    private static (string Asunto, string Cuerpo) RedactarResumenAcciones(List<AccionPendienteAviso> acciones)
    {
        int n = acciones.Count;
        int fuera = acciones.Count(a => a.Fecha?.Date < DateTime.Today);
        string asunto = (n == 1 ? "Tienes 1 acción de calidad pendiente" : $"Tienes {n} acciones de calidad pendientes")
            + (fuera == 0 ? "" : fuera == 1 ? " (1 fuera de plazo)" : $" ({fuera} fuera de plazo)");

        return (asunto, $"""
            Buenos días, {acciones[0].Responsable}.

            {(n == 1 ? "Esta es la acción de calidad que tienes pendiente:" : "Estas son las acciones de calidad que tienes pendientes, de más a menos prioridad:")}

            {string.Join(Environment.NewLine + Environment.NewLine, acciones.Select(DescribirAccion))}

            Cuando termines una, avisa a Calidad para que la marque como hecha. Recibirás este resumen
            cada día mientras tengas alguna pendiente.

            Este correo se ha enviado automáticamente; no hace falta responder.
            """);
    }

    //   · [Prioridad alta] Acción correctora · No conformidad 558
    //     Qué hay que hacer: ...
    //     Plazo: 20/11/2026 (fuera de plazo)
    //     Problema: ...
    private static string DescribirAccion(AccionPendienteAviso a)
    {
        string prioridad = a.Prioridad == null ? "" : $"[Prioridad {a.Prioridad.ToLower(Es)}] ";
        string clase = a.Clase == ClaseAccion.Inmediata ? "Acción inmediata" : "Acción correctora";
        string plazo = a.Fecha is DateTime f
            ? FechaTexto(f) + (f.Date < DateTime.Today ? "  (FUERA DE PLAZO)" : f.Date == DateTime.Today ? "  (hoy)" : "")
            : "sin fecha";
        string problema = a.Problema.Split('\n')[0].Trim();
        if (problema.Length > 200) problema = problema[..200] + "…";

        return $"""
              · {prioridad}{clase} · {Registro(a)}
                Qué hay que hacer: {a.Accion.Replace("\n", "\n    ")}
                Plazo: {plazo}
                Problema: {problema}
            """;
    }

    // "No conformidad 558", "Incidencia 840"
    private static string Registro(AccionPendienteAviso a) =>
        $"{TipoRegistro.Nombre(a.TipoRegistro)} {a.NumeroNc ?? "(sin número)"}";

    private static bool Configurado(out string remitente, out string clave)
    {
        remitente = Environment.GetEnvironmentVariable("GPE_SMTP_USUARIO") ?? "";
        // Google muestra la contraseña de aplicación en grupos de 4 con espacios: se quitan
        clave = (Environment.GetEnvironmentVariable("GPE_SMTP_CLAVE") ?? "").Replace(" ", "");
        return remitente != "" && clave != "";
    }

    private static SmtpClient Conectar()
    {
        Configurado(out string remitente, out string clave);
        string servidor = Environment.GetEnvironmentVariable("GPE_SMTP_SERVIDOR") ?? "smtp.gmail.com";
        int puerto = int.TryParse(Environment.GetEnvironmentVariable("GPE_SMTP_PUERTO"), out int p) ? p : 465;

        var cliente = new SmtpClient { Timeout = 20000 };
        try
        {
            cliente.Connect(servidor, puerto, SecureSocketOptions.Auto);
            cliente.Authenticate(remitente, clave);
            return cliente;
        }
        catch
        {
            cliente.Dispose();
            throw;
        }
    }

    private static MimeMessage Mensaje(string remitente, string destinatario, string asunto, string cuerpo,
        string nombreRemitente = "GPE · Devoluciones", IEnumerable<ArchivoNuevo>? adjuntos = null)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(nombreRemitente, remitente));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = asunto;

        var cuerpoCorreo = new BodyBuilder { TextBody = cuerpo };
        foreach (var adjunto in adjuntos ?? Enumerable.Empty<ArchivoNuevo>())
            cuerpoCorreo.Attachments.Add(adjunto.NombreArchivo, adjunto.Contenido);
        mensaje.Body = cuerpoCorreo.ToMessageBody();
        return mensaje;
    }

    // La causa real puede estar en las excepciones internas
    public static string DescribirError(Exception ex)
    {
        var mensajes = new List<string>();
        for (Exception? e = ex; e != null; e = e.InnerException)
            mensajes.Add(e.Message);
        return string.Join(" → ", mensajes);
    }

    private static readonly CultureInfo Es = new("es-ES");

    private static (string Asunto, string Cuerpo) Redactar(AvisoPendiente aviso) => aviso.Tipo switch
    {
        TipoAviso.NuevaDevolucion => (
            $"Nueva devolución {aviso.NumeroDevolucion} pendiente de recogida",
            $"""
            Se ha registrado una nueva devolución y hay que programar la recogida.

            Devolución: {aviso.NumeroDevolucion}
            Cliente: {aviso.CodigoCliente} · {aviso.Cliente}
            Motivo: {aviso.Motivo}
            Fecha de solicitud: {aviso.FechaSolicitud.ToString("dd/MM/yyyy", Es)}
            Observaciones: {aviso.Observaciones ?? "—"}

            Abre GPE para programar la recogida.

            Este correo se ha enviado automáticamente; no hace falta responder.
            """),
        TipoAviso.RecogidaSolicitada => (
            $"Devolución {aviso.NumeroDevolucion}: mercancía en camino al almacén",
            $"""
            Se ha programado la recogida de una devolución. Cuando llegue la mercancía,
            márcala como recibida en GPE y adjunta el recibo de la agencia.

            Devolución: {aviso.NumeroDevolucion}
            Cliente: {aviso.CodigoCliente} · {aviso.Cliente}
            Motivo: {aviso.Motivo}
            Transportista: {aviso.Transportista ?? "—"}
            Fecha de recogida prevista: {aviso.FechaRecogidaPrevista?.ToString("dd/MM/yyyy", Es) ?? "—"}
            Dirección de recogida: {aviso.DireccionRecogida ?? "—"}

            Este correo se ha enviado automáticamente; no hace falta responder.
            """),
        TipoAviso.RevisionFinalizada => (
            $"Devolución {aviso.NumeroDevolucion}: revisada, pendiente de abono",
            $"""
            El almacén ha terminado de revisar la mercancía de esta devolución.
            Ya se puede registrar el abono.

            Devolución: {aviso.NumeroDevolucion}
            Cliente: {aviso.CodigoCliente} · {aviso.Cliente}
            Motivo: {aviso.Motivo}
            Unidades bien para servir: {aviso.UnidadesAptas}
            Unidades que no están bien: {aviso.UnidadesNoAptas}

            Abre GPE para ver la revisión y registrar el abono.

            Este correo se ha enviado automáticamente; no hace falta responder.
            """),
        TipoAviso.AbonoRegistrado => (
            $"Devolución {aviso.NumeroDevolucion}: abono registrado ({TextoResultado(aviso.ResultadoAbono)})",
            $"""
            Se ha registrado el abono de esta devolución y queda cerrada.

            Devolución: {aviso.NumeroDevolucion}
            Cliente: {aviso.CodigoCliente} · {aviso.Cliente}
            Resultado: {TextoResultado(aviso.ResultadoAbono)}
            Nº de abono: {aviso.NumeroAbono ?? "—"}
            Importe: {aviso.ImporteAbono?.ToString("C", Es) ?? "—"}
            Observaciones: {aviso.ObservacionesAbono ?? "—"}
            Unidades bien para servir: {aviso.UnidadesAptas}
            Unidades que no están bien: {aviso.UnidadesNoAptas}

            Este correo se ha enviado automáticamente; no hace falta responder.
            """),
        _ => throw new InvalidOperationException($"Tipo de aviso desconocido: {aviso.Tipo}")
    };

    // pendientes llega ordenado por fecha: primero las que más llevan esperando
    private static (string Asunto, string Cuerpo) RedactarResumen(string tipo, List<DevolucionPendiente> pendientes)
    {
        int n = pendientes.Count;
        var lineas = pendientes.Select(p => $"  · {p.Numero} · {p.CodigoCliente} · {p.Cliente} · {DetalleResumen(tipo, p)}");

        string pendientesDe(string que) =>
            n == 1 ? $"Tienes 1 devolución pendiente {que}" : $"Tienes {n} devoluciones pendientes {que}";

        var (asunto, situacion, cierre) = tipo switch
        {
            TipoResumen.RecogidaPendiente => (
                pendientesDe("de recogida"),
                n == 1 ? "Esta devolución está registrada y falta solicitar la recogida:"
                       : "Estas devoluciones están registradas y falta solicitar la recogida:",
                "Abre GPE para solicitar la recogida. Recibirás este resumen cada día mientras quede alguna pendiente."),
            TipoResumen.RevisionPendiente => (
                pendientesDe("de revisar"),
                n == 1 ? "Esta devolución tiene la mercancía en el almacén y falta revisarla:"
                       : "Estas devoluciones tienen la mercancía en el almacén y falta revisarlas:",
                "Abre GPE para revisar la mercancía. Recibirás este resumen cada día mientras quede alguna pendiente."),
            TipoResumen.AbonoPendiente => (
                pendientesDe("de abono"),
                n == 1 ? "Esta devolución ya está revisada por el almacén y falta registrar el abono:"
                       : "Estas devoluciones ya están revisadas por el almacén y falta registrar el abono:",
                "Abre GPE para registrar el abono. Recibirás este resumen cada día mientras quede alguna pendiente."),
            TipoResumen.SinLlegar => (
                n == 1 ? "1 devolución aún no ha llegado" : $"{n} devoluciones aún no han llegado",
                n == 1 ? "Esta devolución tiene la recogida gestionada y todavía no consta como recibida:"
                       : "Estas devoluciones tienen la recogida gestionada y todavía no constan como recibidas:",
                "Si ya ha llegado, márcala como recibida en GPE. Si tarda demasiado, avisa a Transporte para reclamarla. " +
                "Recibirás este resumen cada viernes mientras quede alguna sin llegar."),
            _ => throw new InvalidOperationException($"Tipo de resumen desconocido: {tipo}")
        };

        return (
            asunto,
            $"""
            Buenos días.

            {situacion}

            {string.Join(Environment.NewLine, lineas)}

            {cierre}

            Este correo se ha enviado automáticamente; no hace falta responder.
            """);
    }

    private static string DetalleResumen(string tipo, DevolucionPendiente p) => tipo switch
    {
        TipoResumen.RecogidaPendiente =>
            $"solicitada el {FechaTexto(p.FechaSolicitud)} ({Dias((DateTime.Today - p.FechaSolicitud.Date).Days)})",
        TipoResumen.SinLlegar =>
            $"{p.Transportista ?? "sin transportista"} · gestionada el {FechaTexto(p.RecogidaGestionadaEn)} · " +
            $"recogida prevista el {FechaTexto(p.FechaRecogidaPrevista)}",
        _ => p.FechaLlegada is DateTime f
            ? $"llegó el {FechaTexto(f)} ({Dias((DateTime.Today - f.Date).Days)})"
            : "sin fecha de llegada"
    };

    private static string FechaTexto(DateTime? fecha) => fecha?.ToString("dd/MM/yyyy", Es) ?? "—";

    private static string TextoResultado(string? resultado) => resultado switch
    {
        ResultadoAbono.Total => "abono total",
        ResultadoAbono.Parcial => "abono parcial",
        ResultadoAbono.Ninguno => "sin abono",
        _ => "—"
    };

    private static string Dias(int dias) => dias switch
    {
        0 => "hoy",
        1 => "hace 1 día",
        _ => $"hace {dias} días"
    };
}
