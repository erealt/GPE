using System.Text.RegularExpressions;

namespace GPE.Logica;

// NIF español (DNI, NIE o CIF): el último carácter es de control y se calcula con los
// demás, así que sirve para detectar errores al teclearlo. No comprueba que exista.
public static partial class Nif
{
    private const string LetrasDni = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string LetrasCif = "JABCDEFGHI";   // control del CIF en letra: J = 0, A = 1...

    [GeneratedRegex(@"^\d{8}[A-Z]$")] private static partial Regex FormatoDni();
    [GeneratedRegex(@"^[XYZ]\d{7}[A-Z]$")] private static partial Regex FormatoNie();
    [GeneratedRegex(@"^[KLM]\d{7}[A-Z]$")] private static partial Regex FormatoKlm();
    [GeneratedRegex(@"^[ABCDEFGHJNPQRSUVW]\d{7}[0-9A-J]$")] private static partial Regex FormatoCif();

    // Mayúsculas, sin espacios, guiones ni puntos: "b-12.345.674" → "B12345674"
    public static string Normalizar(string nif) =>
        string.Concat(nif.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    // Si tiene la forma de un NIF español (aunque el carácter de control esté mal)
    public static bool PareceEspanol(string nif)
    {
        nif = Normalizar(nif);
        return FormatoDni().IsMatch(nif) || FormatoNie().IsMatch(nif)
            || FormatoKlm().IsMatch(nif) || FormatoCif().IsMatch(nif);
    }

    public static bool EsValido(string nif)
    {
        nif = Normalizar(nif);

        // DNI: el número entre 23; el resto es la posición de la letra
        if (FormatoDni().IsMatch(nif))
            return LetraDni(int.Parse(nif[..8])) == nif[8];

        // NIE: X, Y, Z valen 0, 1, 2 y después se calcula como un DNI
        if (FormatoNie().IsMatch(nif))
            return LetraDni(int.Parse("XYZ".IndexOf(nif[0]) + nif[1..8])) == nif[8];

        // K, L y M (españoles sin DNI, menores...): como un DNI con los 7 números
        if (FormatoKlm().IsMatch(nif))
            return LetraDni(int.Parse(nif[1..8])) == nif[8];

        if (FormatoCif().IsMatch(nif))
            return CifValido(nif);

        return false;
    }

    private static char LetraDni(int numero) => LetrasDni[numero % 23];

    // CIF: se suman las cifras pares, y las impares multiplicadas por 2 (sumando las
    // cifras del resultado: 7 → 14 → 1 + 4 = 5). El control es 10 menos la última cifra.
    private static bool CifValido(string cif)
    {
        int suma = 0;
        for (int i = 0; i < 7; i++)
        {
            int cifra = cif[i + 1] - '0';
            if (i % 2 == 0)   // posiciones 1, 3, 5 y 7
            {
                int doble = cifra * 2;
                suma += doble / 10 + doble % 10;
            }
            else
                suma += cifra;
        }

        int control = (10 - suma % 10) % 10;
        char final = cif[8];

        // Según el tipo de sociedad el control va en número, en letra o vale cualquiera
        return cif[0] switch
        {
            'A' or 'B' or 'E' or 'H' => final == (char)('0' + control),
            'N' or 'P' or 'Q' or 'R' or 'S' or 'W' => final == LetrasCif[control],
            _ => final == (char)('0' + control) || final == LetrasCif[control]
        };
    }
}
