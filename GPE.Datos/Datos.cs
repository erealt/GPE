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

public record Usuario(int Id, string Nombre, string Email)
{
    public string Iniciales => string.Concat(Nombre
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2)
        .Select(p => char.ToUpper(p[0])));

    public override string ToString() => Nombre;
}

// Fila tal cual se guarda en HISTORIAL_CAMBIOS (los valores van en JSON)
public record CambioHistorial(
    DateTime Fecha,
    string Usuario,
    string Tabla,
    int RegistroId,
    string Accion,
    string? ValoresAnteriores,
    string? ValoresNuevos);

// Usuario que está usando la aplicación. Se envía a SQL Server al abrir cada
// conexión para que los triggers sepan quién hace cada cambio.
public static class Sesion
{
    public static Usuario? UsuarioActual { get; set; }
}
