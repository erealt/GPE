using System.Security.Cryptography;
using System.Text;

namespace GPE.Escritorio;

// "Recordarme": guarda usuario y contraseña en este ordenador, cifrados con la cuenta de Windows
// (DPAPI), así que solo se pueden leer desde esta misma cuenta y este mismo equipo
public static class Recordar
{
    private static readonly string Archivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GPE", "recordar.dat");

    public static void Guardar(string usuario, string clave)
    {
        try
        {
            var datos = Encoding.UTF8.GetBytes(usuario.Trim() + "\n" + clave);
            var cifrado = ProtectedData.Protect(datos, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(Archivo)!);
            File.WriteAllBytes(Archivo, cifrado);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // Si no se puede guardar, simplemente la próxima vez pedirá la contraseña
        }
    }

    // Usuario y contraseña guardados, o null si no hay o no se pueden leer
    public static (string Usuario, string Clave)? Leer()
    {
        try
        {
            if (!File.Exists(Archivo)) return null;
            var datos = ProtectedData.Unprotect(File.ReadAllBytes(Archivo), null, DataProtectionScope.CurrentUser);
            var partes = Encoding.UTF8.GetString(datos).Split('\n', 2);
            return partes.Length == 2 ? (partes[0], partes[1]) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return null;
        }
    }

    public static void Olvidar()
    {
        try { File.Delete(Archivo); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
