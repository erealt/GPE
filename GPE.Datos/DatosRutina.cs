namespace GPE.Datos;

// Columna RUTINA_RESULTADO.estado: lo que se marca en cada casilla de la rutina
public static class EstadoCasilla
{
    public const string Correcto = "OK";
    public const string NoCorrecto = "NO";
    public const string NoAplica = "NA";        // hoy no se pudo revisar (máquina parada...)
    public const string Mantenimiento = "MTO";  // se ha avisado a mantenimiento

    public static readonly string[] Todos = { Correcto, NoCorrecto, NoAplica, Mantenimiento };

    public static string Nombre(string estado) => estado switch
    {
        Correcto => "Correcto",
        NoCorrecto => "No correcto",
        NoAplica => "No aplica",
        Mantenimiento => "Mantenimiento",
        _ => estado
    };
}

// Columna RUTINA_SECCION.planta
public static class Planta
{
    public const string Baja = "BAJA";
    public const string Superior = "SUPERIOR";

    public static string Nombre(string planta) => planta == Baja ? "Planta baja" : "Planta superior";
}

// Columna de la hoja (máquina o sección). SeccionCalidad: la de sus incidencias, o null.
public record RutinaSeccion(int Id, string Nombre, string Planta, string? SeccionCalidad);

// Fila de la hoja. Los numéricos llevan valor (PESOS); McPccBpm se propone al abrir una incidencia.
public record RutinaPunto(int Id, string Nombre, bool Numerico, string? McPccBpm);

// Qué se revisa: secciones y puntos activos en su orden, y en qué secciones toca cada punto
public record PlantillaRutina(
    List<RutinaSeccion> Secciones,
    List<RutinaPunto> Puntos,
    HashSet<(int PuntoId, int SeccionId)> Aplica);

// Ronda de un día (Id = 0 si no se ha guardado). Version: para no pisar lo que haya guardado otro.
// CajaOperario, CajaEstado y Depositos son las observaciones del pie de la hoja.
public record Rutina(
    int Id,
    DateTime Fecha,
    string? CajaOperario,
    string? CajaEstado,
    string? Depositos,
    string? CreadoPor,
    byte[]? Version = null);

// Una pasada por la fábrica: la 1 es la ronda completa, las siguientes son repasos.
// Hora: cuándo se empezó (null si aún no se ha tocado). CreadoPor: null si no se ha guardado.
public record RevisionRutina(int Numero, TimeSpan? Hora, string? CreadoPor = null);

// Observación de una sección en el pie de la hoja ("EMB2 · Sigue dando el fallo de envío")
public record ObservacionRutina(int SeccionId, string Texto);

// Casilla rellenada: Estado en los puntos normales, Valor en los numéricos.
// NoConformidadId: incidencia abierta desde la casilla (NumeroIncidencia solo para mostrarla).
public record ResultadoRutina(
    int PuntoId,
    int SeccionId,
    string? Estado,
    decimal? Valor,
    string? Comentario,
    int? NoConformidadId = null,
    string? NumeroIncidencia = null,
    int Revision = 1);
