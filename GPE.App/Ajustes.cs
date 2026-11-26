using Android.Content;
using Android.Webkit;

namespace GPE.App;

// Lo que la app guarda en el móvil: la dirección del servidor y qué acciones ya ha avisado.
public static class Ajustes
{
    public const string ServidorPorDefecto = "http://192.168.2.249";

    private static ISharedPreferences Preferencias(Context contexto) =>
        contexto.GetSharedPreferences("gpe", FileCreationMode.Private)!;

    public static string Servidor(Context contexto) =>
        Preferencias(contexto).GetString("servidor", ServidorPorDefecto) ?? ServidorPorDefecto;

    public static void GuardarServidor(Context contexto, string servidor)
    {
        servidor = servidor.Trim().TrimEnd('/');
        if (!servidor.StartsWith("http://") && !servidor.StartsWith("https://"))
            servidor = "http://" + servidor;
        Preferencias(contexto).Edit()!.PutString("servidor", servidor)!.Apply();
    }

    // La sesión de la web (cookie), para que el servicio pregunte como la persona que ha entrado
    public static string? Cookie(Context contexto) => CookieManager.Instance?.GetCookie(Servidor(contexto));

    // Acciones ya avisadas y de quién son. Si entra otra persona se empieza de nuevo.
    public static (string? Usuario, HashSet<int> Avisadas) Avisadas(Context contexto)
    {
        var preferencias = Preferencias(contexto);
        string? usuario = preferencias.GetString("avisadas_usuario", null);
        var ids = (preferencias.GetString("avisadas", "") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => int.TryParse(t, out int id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();
        return (usuario, ids);
    }

    public static void GuardarAvisadas(Context contexto, string usuario, IEnumerable<int> ids) =>
        Preferencias(contexto).Edit()!
            .PutString("avisadas_usuario", usuario)!
            .PutString("avisadas", string.Join(",", ids))!
            .Apply();
}
