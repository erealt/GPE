namespace GPE.Datos;

// Una fila de la hoja MICRO & FYQ del plan de análisis
public record Analisis(
    int Id,
    string Tipo,
    string Descripcion,
    string? Lote,
    DateTime? Caducidad,       // si solo se sabe el mes, el día 1
    string? AnalisisTotales,
    string? Parametro,
    string? ParametroDetalle,
    DateTime? FechaAnalisis,
    string? Resultado,
    string? CreadoPor = null,
    byte[]? Version = null);
