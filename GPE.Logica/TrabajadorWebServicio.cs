using GPE.Datos;
using System.Security.Cryptography;
using System.Text;

namespace GPE.Logica;

// Web de los trabajadores: entrar con usuario y contraseña, ver sus acciones pendientes y marcarlas hechas
public class TrabajadorWebServicio
{
    private readonly TrabajadorWebRepositorio repositorio = new();

    // El trabajador si el usuario existe, está activo y la contraseña es la suya; si no, null
    // (sin decir cuál de las dos cosas falla)
    public TrabajadorWeb? Entrar(string usuario, string clave)
    {
        usuario = usuario.Trim();
        if (usuario == "" || clave == "") return null;

        var trabajador = repositorio.ObtenerPorUsuario(usuario);
        if (trabajador == null || !trabajador.Activo || !ClaveWeb.Comprobar(clave, trabajador.Clave))
            return null;
        return trabajador;
    }

    public List<AccionTrabajador> ObtenerPendientes(string persona) => repositorio.ObtenerPendientes(persona);

    public void MarcarHecha(int accionId, string persona, string? comentario)
    {
        comentario = comentario?.Trim();
        string nota = $"Hecha por {persona} desde la web el {DateTime.Now:dd/MM/yyyy HH:mm}"
            + (string.IsNullOrEmpty(comentario) ? "." : $": {comentario}");

        if (!repositorio.MarcarHecha(accionId, persona, nota))
            throw new ReglaNegocioException(
                "Esta acción ya no está pendiente: puede que calidad la haya cambiado o cerrado. Se ha actualizado la lista.");
    }
}

// Contraseñas de la web cifradas con PBKDF2: "PBKDF2-SHA256$iteraciones$sal$resultado" (Base64)
public static class ClaveWeb
{
    private const string Prefijo = "PBKDF2-SHA256";
    private const int Iteraciones = 100_000;

    public static string Cifrar(string clave)
    {
        byte[] sal = RandomNumberGenerator.GetBytes(16);
        byte[] resultado = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(clave), sal, Iteraciones,
            HashAlgorithmName.SHA256, 32);
        return $"{Prefijo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(resultado)}";
    }

    public static bool Comprobar(string clave, string guardada)
    {
        string[] partes = guardada.Split('$');
        if (partes.Length != 4 || partes[0] != Prefijo || !int.TryParse(partes[1], out int iteraciones))
            return false;

        byte[] sal = Convert.FromBase64String(partes[2]);
        byte[] esperado = Convert.FromBase64String(partes[3]);
        byte[] resultado = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(clave), sal, iteraciones,
            HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(resultado, esperado);
    }
}
