using GPE.Datos;
using System.Globalization;

namespace GPE.Logica;

// Control de agua de calidad (hoja AGUA, MT-RG09): cloro y organolépticos de cada punto revisado.
public class AguaServicio
{
    private readonly AguaRepositorio repositorio = new();
    private static readonly CultureInfo Es = new("es-ES");

    // Consultas

    public List<AguaToma> ObtenerTomas() => repositorio.ObtenerTomas();

    public List<ControlAgua> ObtenerDia(DateTime fecha) => repositorio.ObtenerControles(fecha.Date);

    // "Hoy: sin controles" o "Hoy: 2 controles", para la tarjeta del inicio
    public string ResumenHoy()
    {
        int controles = repositorio.ObtenerControles(DateTime.Today).Count;
        return controles == 0 ? "Hoy: sin controles" : $"Hoy: {controles} {(controles == 1 ? "control" : "controles")}";
    }

    // El cloro se sale del rango de la toma (las que no tienen rango nunca se salen)
    public static bool FueraDeRango(decimal? cloro, AguaToma toma) =>
        cloro is decimal c && (c < toma.CloroMin || c > toma.CloroMax);

    // "0,55 ppm"
    public static string Ppm(decimal cloro) => cloro.ToString("0.00", Es) + " ppm";

    // Validación

    public const int LargoComentario = 1000;
    public const decimal CloroMaximo = 9.99m;   // lo que cabe en AGUA_CONTROL.cloro

    // Quita espacios y deja en null lo vacío
    public static ControlAgua Normalizar(ControlAgua control) => control with
    {
        Fecha = control.Fecha.Date,
        Comentario = string.IsNullOrWhiteSpace(control.Comentario) ? null : control.Comentario.Trim()
    };

    // Error de un control ya normalizado (null si está bien)
    public static string? Error(ControlAgua control)
    {
        if (control.TomaId == 0)
            return "Elige el punto de agua de cada fila.";
        if (control.Cloro < 0)
            return "El cloro no puede ser negativo.";
        if (control.Cloro > CloroMaximo)
            return $"El cloro no puede pasar de {Ppm(CloroMaximo)}.";
        if (control.Comentario?.Length > LargoComentario)
            return $"El comentario no puede pasar de {LargoComentario} caracteres.";
        return null;
    }

    // Guardado

    private const string MensajeModificado =
        "Otra persona ha guardado controles de este día mientras lo tenías abierto. " +
        "Para no borrar sus cambios, los tuyos no se han guardado.\n\n" +
        "Apunta lo que hayas escrito y pulsa \"Recargar\" para ver lo último.";

    // Solo escribe las filas nuevas, las cambiadas y las quitadas respecto a originales.
    public void GuardarDia(DateTime fecha, IEnumerable<ControlAgua> controles, IReadOnlyList<ControlAgua> originales)
    {
        if (Sesion.UsuarioActual is not Usuario usuario)
            throw new ReglaNegocioException("No hay ningún usuario elegido.");
        if (fecha.Date > DateTime.Today)
            throw new ReglaNegocioException("No se pueden apuntar controles de un día que todavía no ha llegado.");

        var lista = controles.Select(c => Normalizar(c with { Fecha = fecha })).ToList();
        foreach (var control in lista)
            if (Error(control) is string error)
                throw new ReglaNegocioException(error);

        var porId = originales.ToDictionary(c => c.Id);
        var cambiados = lista.Where(c => c.Id == 0 || !porId.TryGetValue(c.Id, out var original) || !Iguales(c, original)).ToList();
        var borrados = originales.Where(o => !lista.Any(c => c.Id == o.Id)).ToList();
        if (cambiados.Count == 0 && borrados.Count == 0) return;

        if (!repositorio.GuardarDia(cambiados, borrados, usuario.Id))
            throw new ReglaNegocioException(MensajeModificado);
    }

    private static bool Iguales(ControlAgua a, ControlAgua b) =>
        a.TomaId == b.TomaId && a.Cloro == b.Cloro && a.SaborOk == b.SaborOk && a.OlorOk == b.OlorOk
        && a.ColorOk == b.ColorOk && a.TurbidezOk == b.TurbidezOk && a.Comentario == b.Comentario;
}
