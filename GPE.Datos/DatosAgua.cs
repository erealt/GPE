namespace GPE.Datos;

public record AguaToma(int Id, string Nombre, decimal? CloroMin, decimal? CloroMax);

public record ControlAgua(
    int Id,
    DateTime Fecha,
    int TomaId,
    decimal? Cloro,
    bool SaborOk,
    bool OlorOk,
    bool ColorOk,
    bool TurbidezOk,
    string? Comentario,
    string? CreadoPor = null,
    byte[]? Version = null);
