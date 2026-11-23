using System.Security.Cryptography;
using System.Text;

namespace GPE.Logica;

// Contraseñas cifradas con PBKDF2: "PBKDF2-SHA256$iteraciones$sal$resultado" (Base64).
// Mismo formato que tenían las contraseñas de la web de trabajadores (TRABAJADOR_WEB).
public static class Clave
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
