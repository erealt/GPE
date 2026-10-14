namespace GPE.Datos;

public record DevolucionFila(
    int Id,
    string Numero,
    string Cliente,
    string? Albaran,
    string Motivo,
    DateTime FechaSolicitud,
    DateTime? FechaRecogida,
    DateTime? FechaLlegada,
    string Estado,
    int Fase);

public record Cliente(int Id, string Nombre)
{
    public override string ToString() => Nombre;
}

public record Producto(int Id, string Codigo, string Nombre)
{
    public override string ToString() => Nombre;
}

public record LineaNuevaDevolucion(int ProductoId, int Cantidad, string? Lote);

public record DatosNuevaDevolucion(
    int FabricaId,
    int ClienteId,
    string Motivo,
    string? Albaran,
    string? Observaciones,
    DateTime FechaSolicitud,
    List<LineaNuevaDevolucion> Lineas);