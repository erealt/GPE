namespace GPE.Datos;

public record AguaToma(int Id, string Nombre, decimal? CloroMin, decimal? CloroMax);

// Un control con su punto, para las consultas de varios días (el punto puede estar ya dado de baja)
public record ControlAguaConToma(ControlAgua Control, AguaToma Toma);

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
    TimeSpan? Hora = null,   // null en los controles pasados del Excel
    string? CreadoPor = null,
    byte[]? Version = null,
    int? RepiteId = null);   // la toma que esta repite porque no convenció
