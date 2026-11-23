using GPE.Datos;

namespace GPE.Logica;

// Plan de análisis de calidad (Excel PLAN DE ANALISIS 2026, hoja MICRO & FYQ)
public class AnalisisServicio
{
    private readonly AnalisisRepositorio repositorio = new();

    public List<Analisis> ObtenerTodos() => repositorio.ObtenerTodos();

    // "12 análisis", para la tarjeta del inicio
    public string Resumen()
    {
        int total = repositorio.Contar();
        return total == 0 ? "Sin análisis apuntados" : $"{total} análisis";
    }

    // Largos de las columnas de ANALISIS
    public const int LargoDescripcion = 300;
    public const int LargoLote = 300;
    public const int LargoTotales = 500;
    public const int LargoDetalle = 100;
    public const int LargoResultado = 500;

    private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    // Quita espacios y deja en null lo vacío
    public static Analisis Normalizar(Analisis a) => a with
    {
        Descripcion = a.Descripcion?.Trim() ?? "",
        Lote = Limpio(a.Lote),
        Caducidad = a.Caducidad?.Date,
        AnalisisTotales = Limpio(a.AnalisisTotales),
        Parametro = Limpio(a.Parametro),
        ParametroDetalle = Limpio(a.ParametroDetalle),
        FechaAnalisis = a.FechaAnalisis?.Date,
        Resultado = Limpio(a.Resultado)
    };

    private const string MensajeModificado =
        "Otra persona ha cambiado o borrado este análisis mientras lo tenías abierto. " +
        "Para no borrar sus cambios, los tuyos no se han guardado.\n\n" +
        "Apunta lo que hayas escrito y vuelve a abrirlo para ver lo último.";

    // Nuevo (Id 0) o corregido. Devuelve el análisis tal como se ha guardado (con su id).
    public Analisis Guardar(Analisis analisis)
    {
        if (Sesion.UsuarioActual is not Usuario usuario)
            throw new ReglaNegocioException("No hay ningún usuario elegido.");

        analisis = Normalizar(analisis);
        if (!ListasAnalisis.Tipos.Contains(analisis.Tipo))
            throw new ReglaNegocioException("Elige el tipo: PRODUCTO, MMPP o ENVUELTA.");
        if (analisis.Descripcion == "")
            throw new ReglaNegocioException("Escribe la descripción.");
        if (analisis.FechaAnalisis > DateTime.Today)
            throw new ReglaNegocioException("La fecha del análisis no puede ser de un día que todavía no ha llegado.");

        if (analisis.Id == 0)
            return analisis with { Id = repositorio.Insertar(analisis, usuario.Id) };
        if (!repositorio.Actualizar(analisis))
            throw new ReglaNegocioException(MensajeModificado);
        return analisis;
    }

    public void Borrar(Analisis analisis)
    {
        if (!repositorio.Borrar(analisis))
            throw new ReglaNegocioException(MensajeModificado);
    }
}
