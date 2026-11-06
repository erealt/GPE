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

    private static MimeMessage Mensaje(string remitente, string destinatario, string asunto, string cuerpo)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress("GPE · Devoluciones", remitente));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart("plain") { Text = cuerpo };
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
