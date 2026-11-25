using GPE.Datos;

namespace GPE.Logica;

// Aviso en el escritorio a calidad cuando un trabajador marca una acción hecha desde la web
public class AccionHechaWebServicio
{
    private readonly AccionHechaWebRepositorio repositorio = new();

    // Solo salen las del último mes
    public const int Dias = 30;

    public List<AccionHechaWeb> ObtenerSinVer(int usuarioId) => repositorio.ObtenerSinVer(usuarioId, Dias);

    public void MarcarVistos(int usuarioId, IEnumerable<int> avisos) => repositorio.MarcarVistos(usuarioId, avisos);

    // Lo que dice el recuadro de la esquina
    public static (string Titulo, string Texto) TextoAviso(List<AccionHechaWeb> avisos)
    {
        if (avisos.Count == 1)
        {
            var a = avisos[0];
            return ("Acción hecha", $"{a.Persona} ha hecho su {a.NombreClase}.\n{a.Registro}");
        }
        var personas = avisos.Select(a => a.Persona).Distinct().ToList();
        string quien = personas.Count == 1
            ? personas[0]
            : string.Join(", ", personas.Take(personas.Count - 1)) + " y " + personas[^1];
        return ($"{avisos.Count} acciones hechas", $"Las han marcado hechas desde la web: {quien}.");
    }
}
