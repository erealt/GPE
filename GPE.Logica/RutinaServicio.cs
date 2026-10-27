using GPE.Datos;

namespace GPE.Logica;

// Rutina diaria del supervisor de calidad: la hoja "CHEQUEOS DIARIOS - PCC" que se
// rellena al recorrer la fábrica. Una ronda por día; cada casilla es un punto de
// control (fila) en una máquina o sección (columna).
public class RutinaServicio
{
    private readonly RutinaRepositorio repositorio = new();

    // ---------- Consultas ----------

    public PlantillaRutina ObtenerPlantilla() => repositorio.ObtenerPlantilla();

    // La ronda del día con sus casillas y sus observaciones por sección. Si aún no hay, una en blanco (Id = 0).
    public (Rutina Rutina, List<ResultadoRutina> Resultados, List<ObservacionRutina> Observaciones) ObtenerRutina(DateTime fecha)
    {
        var rutina = repositorio.ObtenerRutina(fecha.Date);
        if (rutina == null)
            return (new Rutina(0, fecha.Date, null, null, null, null), new(), new());
        return (rutina, repositorio.ObtenerResultados(rutina.Id), repositorio.ObtenerObservaciones(rutina.Id));
    }

    // "Hoy: sin empezar" o "Hoy: empezada", para la tarjeta del inicio
    public string ResumenHoy() =>
        repositorio.ObtenerRutina(DateTime.Today) == null ? "Hoy: sin empezar" : "Hoy: empezada";

    // ---------- Validación ----------

    public const int LargoComentario = 1000;
    public const int LargoCaja = 100;   // operario y estado de la caja de herramientas

    // Quita espacios y deja en null lo vacío
    public static ResultadoRutina Normalizar(ResultadoRutina resultado) => resultado with
    {
        Comentario = string.IsNullOrWhiteSpace(resultado.Comentario) ? null : resultado.Comentario.Trim()
    };

    // Una casilla sin nada no se guarda
    public static bool Vacia(ResultadoRutina resultado) =>
        resultado.Estado == null && resultado.Valor == null && resultado.Comentario == null;

    // Error de una casilla ya normalizada (null si está bien)
    public static string? Error(ResultadoRutina resultado, RutinaPunto punto)
    {
        if (resultado.Estado != null && !EstadoCasilla.Todos.Contains(resultado.Estado))
            return "Estado no válido.";
        if (punto.Numerico && resultado.Estado != null)
            return $"\"{punto.Nombre}\" lleva un número, no correcto / no correcto.";
        if (!punto.Numerico && resultado.Valor != null)
            return $"\"{punto.Nombre}\" no lleva número.";
        if (resultado.Valor < 0)
            return "El valor no puede ser negativo.";
        if (resultado.Valor >= 100_000_000)
            return "El valor es demasiado grande.";
        if (resultado.Comentario?.Length > LargoComentario)
            return $"El comentario no puede pasar de {LargoComentario} caracteres.";
        return null;
    }

    // ---------- Guardado ----------

    private const string MensajeModificado =
        "Otra persona ha guardado esta ronda mientras la tenías abierta. " +
        "Para no borrar sus cambios, los tuyos no se han guardado.\n\n" +
        "Apunta lo que hayas marcado y pulsa \"Recargar\" para ver lo último.";

    // Crea la ronda o guarda sus cambios con todas sus casillas y observaciones. Devuelve su id.
    // Las observaciones sin texto no se guardan.
    public int Guardar(Rutina rutina, IEnumerable<ResultadoRutina> resultados,
        IEnumerable<ObservacionRutina> observaciones, PlantillaRutina plantilla)
    {
        if (Sesion.UsuarioActual is not Usuario usuario)
            throw new ReglaNegocioException("No hay ningún usuario elegido.");
        if (rutina.Fecha.Date > DateTime.Today)
            throw new ReglaNegocioException("No se puede rellenar la ronda de un día que todavía no ha llegado.");

        var puntos = plantilla.Puntos.ToDictionary(p => p.Id);
        var lista = new List<ResultadoRutina>();
        foreach (var resultado in resultados.Select(Normalizar).Where(r => !Vacia(r)))
        {
            // Un punto desactivado en la plantilla conserva lo que ya tenía
            if (puntos.TryGetValue(resultado.PuntoId, out var punto) && Error(resultado, punto) is string error)
                throw new ReglaNegocioException(error);
            lista.Add(resultado);
        }

        static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        rutina = rutina with
        {
            CajaOperario = Limpio(rutina.CajaOperario),
            CajaEstado = Limpio(rutina.CajaEstado),
            Depositos = Limpio(rutina.Depositos)
        };
        if (rutina.CajaOperario?.Length > LargoCaja || rutina.CajaEstado?.Length > LargoCaja)
            throw new ReglaNegocioException($"El operario y el estado de la caja de herramientas no pueden pasar de {LargoCaja} caracteres.");
        if (rutina.Depositos?.Length > LargoComentario)
            throw new ReglaNegocioException($"La observación de depósitos no puede pasar de {LargoComentario} caracteres.");

        var notas = new List<ObservacionRutina>();
        foreach (var observacion in observaciones)
        {
            if (Limpio(observacion.Texto) is not string texto) continue;
            if (!plantilla.Secciones.Any(s => s.Id == observacion.SeccionId))
                throw new ReglaNegocioException("Elige la sección de cada observación.");
            if (texto.Length > LargoComentario)
                throw new ReglaNegocioException($"Una observación no puede pasar de {LargoComentario} caracteres.");
            notas.Add(new ObservacionRutina(observacion.SeccionId, texto));
        }

        return repositorio.Guardar(rutina, lista, notas, usuario.Id)
            ?? throw new ReglaNegocioException(MensajeModificado);
    }

    public void GuardarPlantilla(IReadOnlyCollection<(int PuntoId, int SeccionId)> aplica) =>
        repositorio.GuardarPlantilla(aplica);

    // ---------- Incidencias ----------

    // Incidencia en blanco con lo que ya se sabe de la casilla, para completarla en su ficha
    public static NoConformidad BorradorIncidencia(DateTime fecha, RutinaPunto punto, RutinaSeccion seccion,
        ResultadoRutina? resultado)
    {
        string problema = $"Rutina de calidad: {punto.Nombre} en {seccion.Nombre} no correcto.";
        if (resultado?.Comentario != null)
            problema += "\n" + resultado.Comentario;

        return new NoConformidad(
            Id: 0, TipoRegistro: TipoRegistro.Incidencia, Numero: null, FechaDetectado: fecha.Date,
            Origen: null, Seccion: seccion.SeccionCalidad, Tipo: null, Problema: problema,
            DescripcionCausa: null, Responsable: null, FechaPrevista: null, FechaFin: null,
            Cliente: null, Proveedor: null, McPccBpm: punto.McPccBpm, Parametro: null, Operario: null,
            CapituloNorma: null, AnalisisCausaRaiz: null, CosteEconomico: null, HorasManoObra: null);
    }

    public void Vincular(int rutinaId, int puntoId, int seccionId, int noConformidadId)
    {
        if (!repositorio.Vincular(rutinaId, puntoId, seccionId, noConformidadId))
            throw new ReglaNegocioException(
                "La incidencia se ha guardado, pero la casilla ya no está en la ronda: puede que otra persona la haya vaciado.");
    }
}
