using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GPE.Logica;

// Sube archivos a la biblioteca de medios de la web (WordPress) con su API REST. Se entra con
// un usuario de WordPress y una "contraseña de aplicación" (Usuarios → Perfil), nunca con la
// contraseña normal. Se configura con variables de entorno, como el correo.
public static class WordPress
{
    public static readonly string Sitio =
        (Environment.GetEnvironmentVariable("GPE_WP_SITIO") ?? "https://eurochoc.es").TrimEnd('/');

    private static readonly string usuario = Environment.GetEnvironmentVariable("GPE_WP_USUARIO") ?? "";
    private static readonly string clave = Environment.GetEnvironmentVariable("GPE_WP_CLAVE") ?? "";

    private static readonly HttpClient cliente = new() { Timeout = TimeSpan.FromSeconds(60) };

    // WordPress guarda lo que se sube en wp-content/uploads/AAAA/MM/ (el mes de la subida)
    public static string CarpetaDelMes(DateTime fecha) => $"{Sitio}/wp-content/uploads/{fecha:yyyy}/{fecha:MM}/";

    public record Medio(int Id, string Url);

    public static Medio Subir(string nombreArchivo, byte[] contenido, string tipo)
    {
        using var peticion = Peticion(HttpMethod.Post, "media");
        peticion.Content = new ByteArrayContent(contenido);
        peticion.Content.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        peticion.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = $"\"{nombreArchivo}\"" };

        using var respuesta = Enviar(peticion);
        using var json = JsonDocument.Parse(respuesta.Content.ReadAsStream());
        return new Medio(
            json.RootElement.GetProperty("id").GetInt32(),
            json.RootElement.GetProperty("source_url").GetString() ?? "");
    }

    // Borra el archivo del todo (sin pasar por la papelera)
    public static void Borrar(int id)
    {
        using var peticion = Peticion(HttpMethod.Delete, $"media/{id}?force=true");
        using var respuesta = Enviar(peticion);
    }

    // Todos los archivos de este tipo (p. ej. "application/pdf") subidos antes de esa fecha.
    // WordPress los devuelve de 100 en 100; se piden todas las páginas antes de borrar nada.
    public static List<Medio> Anteriores(string tipo, DateTime fecha)
    {
        var medios = new List<Medio>();
        for (int pagina = 1; ; pagina++)
        {
            using var peticion = Peticion(HttpMethod.Get,
                $"media?mime_type={Uri.EscapeDataString(tipo)}&before={fecha:yyyy-MM-ddTHH:mm:ss}" +
                $"&per_page=100&page={pagina}&_fields=id,source_url");
            using var respuesta = Enviar(peticion);
            using var json = JsonDocument.Parse(respuesta.Content.ReadAsStream());
            foreach (var medio in json.RootElement.EnumerateArray())
                medios.Add(new Medio(medio.GetProperty("id").GetInt32(), medio.GetProperty("source_url").GetString() ?? ""));

            int paginas = respuesta.Headers.TryGetValues("X-WP-TotalPages", out var valores)
                && int.TryParse(valores.FirstOrDefault(), out int total) ? total : 1;
            if (pagina >= paginas) return medios;
        }
    }

    private static HttpRequestMessage Peticion(HttpMethod metodo, string ruta)
    {
        if (usuario == "" || clave == "")
            throw new ReglaNegocioException(
                "Falta configurar el acceso a la web (variables GPE_WP_USUARIO y GPE_WP_CLAVE).");

        var peticion = new HttpRequestMessage(metodo, $"{Sitio}/wp-json/wp/v2/{ruta}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:{clave}")));
        return peticion;
    }

    private static HttpResponseMessage Enviar(HttpRequestMessage peticion)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = cliente.Send(peticion);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ReglaNegocioException($"No se pudo conectar con {Sitio}. Comprueba la conexión a internet.\n\n{ex.Message}");
        }

        if (respuesta.IsSuccessStatusCode) return respuesta;

        string detalle = respuesta.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        respuesta.Dispose();
        string motivo = Motivo(detalle);
        throw new ReglaNegocioException(respuesta.StatusCode switch
        {
            HttpStatusCode.Unauthorized when motivo.Contains("invalid_username") =>
                $"La web no conoce el usuario \"{usuario}\" (GPE_WP_USUARIO). Pon el nombre de usuario con el que entras en WordPress.",
            HttpStatusCode.Unauthorized when motivo.Contains("incorrect_password") =>
                "La contraseña de aplicación (GPE_WP_CLAVE) no es correcta o se ha revocado. Crea otra en WordPress → Usuarios → Perfil.",
            HttpStatusCode.Unauthorized =>
                $"La web no ha recibido el usuario y la contraseña de aplicación (GPE_WP_USUARIO y GPE_WP_CLAVE).\n\n{motivo}",
            HttpStatusCode.Forbidden =>
                $"El usuario de WordPress no tiene permiso para subir o borrar archivos.\n\n{motivo}",
            _ => $"La web devolvió un error ({(int)respuesta.StatusCode}).\n\n{motivo}"
        });
    }

    // WordPress explica el error en JSON: {"code": "...", "message": "..."}
    private static string Motivo(string detalle)
    {
        try
        {
            using var json = JsonDocument.Parse(detalle);
            return $"{json.RootElement.GetProperty("message").GetString()} ({json.RootElement.GetProperty("code").GetString()})";
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return detalle.Length > 300 ? detalle[..300] + "..." : detalle;
        }
    }
}
