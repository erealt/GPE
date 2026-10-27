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

// Una columna de la hoja: máquina o sección que se recorre. SeccionCalidad es la sección
// de las incidencias que le corresponde (null si no hay una clara).
public record RutinaSeccion(int Id, string Nombre, string Planta, string? SeccionCalidad);

// Una fila de la hoja: lo que se revisa. Los numéricos llevan un valor (PESOS).
// McPccBpm es el valor de esa lista que se propone al abrir una incidencia.
public record RutinaPunto(int Id, string Nombre, bool Numerico, string? McPccBpm);

// Qué se revisa: secciones y puntos activos en su orden, y en qué secciones toca cada punto
public record PlantillaRutina(
    List<RutinaSeccion> Secciones,
    List<RutinaPunto> Puntos,
    HashSet<(int PuntoId, int SeccionId)> Aplica);

// La ronda de un día. Id = 0 si todavía no se ha guardado nada ese día.
// Version: la de RUTINA tal como se leyó, para no pisar lo que haya guardado otro.
public record Rutina(int Id, DateTime Fecha, string? Observaciones, string? CreadoPor, byte[]? Version = null);

// Una casilla rellenada. Los puntos normales llevan Estado y los numéricos, Valor.
// NoConformidadId: incidencia abierta desde la casilla (NumeroIncidencia, su número, solo para enseñarlo).
public record ResultadoRutina(
    int PuntoId,
    int SeccionId,
    string? Estado,
    decimal? Valor,
    string? Comentario,
    int? NoConformidadId = null,
    string? NumeroIncidencia = null);
