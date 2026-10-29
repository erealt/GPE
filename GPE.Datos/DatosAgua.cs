namespace GPE.Datos;

// Un punto de agua que se puede elegir al apuntar un control.
// CloroMin / CloroMax: rango admitido en ppm (null: no se comprueba).
public record AguaToma(int Id, string Nombre, decimal? CloroMin, decimal? CloroMax);

// Una fila de la hoja: el control de una toma un día. Cloro es null si no se ha medido.
// Version: la de AGUA_CONTROL tal como se leyó, para no pisar lo que haya guardado otro.
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
