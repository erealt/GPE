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
    int Id,
    long? TransaccionId,
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

public record Transportista(int Id, string Nombre, string? Telefono)
{
    public override string ToString() => Nombre;
}

public record DatosRecogida(
    int DevolucionId,
    int TransportistaId,
    DateTime FechaRecogida,
    string Direccion,
    string? TelefonoContacto,
    string? Observaciones);

public record RecogidaGuardada(
    int Id,
    int TransportistaId,
    DateTime FechaRecogida,
    string Direccion,
    string? TelefonoContacto,
    string? Observaciones,
    DateTime CreadaEn);

// Línea de producto tal como la ve el almacén al revisar
public record LineaRevision(
    int Id,
    string Producto,
    string? Lote,
    int Solicitada,
    int? Recibida,
    int? Apta,
    string? Observaciones);

// Lo que el almacén ha anotado de cada línea al finalizar la revisión
public record LineaRevisada(int Id, int Recibida, int Apta, string? Observaciones);

public static class TipoAdjunto
{
    public const string Foto = "FOTO";
    public const string Abono = "ABONO";
}

// Datos de un adjunto sin su contenido (para listarlo)
public record AdjuntoInfo(int Id, string NombreArchivo, int TamanoBytes, DateTime SubidoEn, string Usuario);

public record ArchivoNuevo(string NombreArchivo, byte[] Contenido);
