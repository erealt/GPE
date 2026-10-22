using System.Data.Common;
using System.Globalization;
using GPE.Datos;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GPE.Logica;

// Envía los avisos por correo apuntados en AVISO_CORREO y el resumen diario al almacén.
// La cuenta de envío se configura en cada ordenador con variables de entorno
// (la contraseña nunca va en el código ni en la base de datos):
//   GPE_SMTP_USUARIO   cuenta que envía, p. ej. avisos@empresa.com
//   GPE_SMTP_CLAVE     contraseña de aplicación de esa cuenta
//   GPE_SMTP_SERVIDOR  opcional, por defecto smtp.gmail.com
//   GPE_SMTP_PUERTO    opcional, por defecto 465 (SSL directo; con 587 se usa STARTTLS)
// Sin configurar, los avisos se quedan pendientes y no se envía nada.
// Se usa MailKit y no System.Net.Mail.SmtpClient, que Microsoft ya no recomienda
// (con Gmail cortaba la conexión sin dar el motivo).
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

    // Resumen diario al almacén: un correo por persona con todas las devoluciones que
    // tienen la mercancía recibida y sin revisar. Lo lanza GPE.Avisos (tarea programada
    // de Windows), sin necesidad de abrir GPE. A quien ya lo recibió hoy no se le repite.
    // Devuelve cuántos correos se han enviado; lanza la excepción si no se puede conectar.
    public static int EnviarResumenDiario()
    {
        if (!Configurado(out string remitente, out _))
            throw new InvalidOperationException("Falta configurar GPE_SMTP_USUARIO y GPE_SMTP_CLAVE.");

        var porPersona = repositorio.ObtenerRevisionesPendientes().GroupBy(p => p.Destinatario).ToList();
        if (porPersona.Count == 0) return 0;

        using var cliente = Conectar();
        foreach (var grupo in porPersona)
        {
            var (asunto, cuerpo) = RedactarResumenRevision(grupo.ToList());
            cliente.Send(Mensaje(remitente, grupo.Key, asunto, cuerpo));
            repositorio.MarcarResumenEnviado(TipoResumen.RevisionPendiente, grupo.Key);
        }
        cliente.Disconnect(true);
        return porPersona.Count;
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
        _ => throw new InvalidOperationException($"Tipo de aviso desconocido: {aviso.Tipo}")
    };

    // Primero las que más tiempo llevan esperando
    private static (string Asunto, string Cuerpo) RedactarResumenRevision(List<DevolucionPendiente> pendientes)
    {
        int n = pendientes.Count;
        var lineas = pendientes.Select(p =>
        {
            string llegada = p.FechaLlegada is DateTime f
                ? $"llegó el {f.ToString("dd/MM/yyyy", Es)} ({Dias((DateTime.Today - f.Date).Days)})"
                : "sin fecha de llegada";
            return $"  · {p.Numero} · {p.CodigoCliente} · {p.Cliente} · {llegada}";
        });

        return (
            n == 1 ? "Tienes 1 devolución pendiente de revisar" : $"Tienes {n} devoluciones pendientes de revisar",
            $"""
            Buenos días.

            {(n == 1 ? "Esta devolución tiene" : "Estas devoluciones tienen")} la mercancía en el almacén y falta revisarla:

            {string.Join(Environment.NewLine, lineas)}

            Abre GPE para revisar la mercancía. Recibirás este resumen cada día mientras quede alguna pendiente.

            Este correo se ha enviado automáticamente; no hace falta responder.
            """);
    }

    private static string Dias(int dias) => dias switch
    {
        0 => "hoy",
        1 => "hace 1 día",
        _ => $"hace {dias} días"
    };
}
