namespace GPE.Datos;

// Fases del proceso (columna ESTADO_DEVOLUCION.fase). Abono y cierre se registran
// a la vez, así que se pasa de la 4 a la 6: la 5 solo aparece en la barra de progreso.
public static class Fase
{
    public const int SolicitudRecibida = 1;
    public const int RecogidaSolicitada = 2;
    public const int MercanciaRecibida = 3;
    public const int RevisadaAlmacen = 4;
    public const int AbonoRealizado = 5;
    public const int Cierre = 6;

    // El paso en curso es el siguiente al último completado; si está cerrada, el cierre
    public static int PasoEnCurso(int fase) => Math.Min(fase + 1, Cierre);
}

public record DevolucionFila(
    int Id,
    string Numero,
    string Cliente,
    string CodigoCliente,
    string Motivo,
    DateTime FechaSolicitud,
    DateTime? FechaRecogida,   // fecha de recogida prevista
    DateTime? FechaLlegada,
    string Estado,
    int Fase);

public record Cliente(int Id, string Codigo, string Nombre)
{
    public override string ToString() => $"{Codigo} · {Nombre}";
}

public record Producto(int Id, string Codigo, string Nombre, string? Formato)
{
    public override string ToString() => Nombre;
}

public record LineaNuevaDevolucion(int ProductoId, int Cantidad, string? Lote);

public record DatosNuevaDevolucion(
    int FabricaId,
    int ClienteId,
    string Motivo,
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
    DateTime CreadaEn);   // cuándo se registró el transportista

// Producto que el cliente dijo que iba a devolver (al crear la devolución)
public record LineaSolicitada(
    int ProductoId,
    string Codigo,
    string Producto,
    string? Formato,
    string? Lote,
    int Cantidad);

// Línea apuntada por el almacén al revisar la mercancía
public record LineaRevision(
    int ProductoId,
    string Codigo,
    string Producto,
    string? Formato,
    string? Lote,
    DateTime? Caducidad,
    int Cantidad,
    bool Apto);

// Cabecera de la revisión. Agencia y RevisadaPor son null si aún no se ha guardado;
// Transportista es el de la recogida, para proponerlo como agencia.
public record CabeceraRevision(
    string? RevisadaPor,
    string? Agencia,
    string? Observaciones,
    string? Transportista);

// Lo que se guarda al finalizar la revisión
public record LineaRevisada(int ProductoId, string? Lote, DateTime? Caducidad, int Cantidad, bool Apto);

public record DatosRevision(
    int DevolucionId,
    string RevisadaPor,
    string? Agencia,
    string? Observaciones,
    List<LineaRevisada> Lineas);

public static class TipoAdjunto
{
    public const string Foto = "FOTO";
    public const string Abono = "ABONO";
    public const string Recibo = "RECIBO";   // recibo de la agencia al llegar la mercancía
}

// Datos de un adjunto sin su contenido (para listarlo)
public record AdjuntoInfo(int Id, string NombreArchivo, int TamanoBytes, DateTime SubidoEn, string Usuario);

public record ArchivoNuevo(string NombreArchivo, byte[] Contenido);

public static class TipoAviso
{
    public const string NuevaDevolucion = "NUEVA_DEVOLUCION";
    public const string RecogidaSolicitada = "RECOGIDA_SOLICITADA";
}

// Aviso por correo pendiente, con los datos de la devolución para escribir el mensaje
public record AvisoPendiente(
    int Id,
    string Tipo,
    string Destinatario,
    string NumeroDevolucion,
    string Cliente,
    string CodigoCliente,
    string Motivo,
    DateTime FechaSolicitud,
    string? Observaciones,
    // Datos de la recogida (null si todavía no se ha programado)
    string? Transportista,
    DateTime? FechaRecogidaPrevista,
    string? DireccionRecogida);

// Resúmenes diarios por correo (tabla AVISO_RESUMEN, una vez al día por destinatario)
public static class TipoResumen
{
    public const string RevisionPendiente = "REVISION_PENDIENTE";
}

// Devolución que alguien tiene pendiente, con el correo de esa persona
public record DevolucionPendiente(
    string Destinatario,
    string Numero,
    string Cliente,
    string CodigoCliente,
    DateTime? FechaLlegada);

public static class ResultadoAbono
{
    public const string Total = "TOTAL";
    public const string Parcial = "PARCIAL";
    public const string Ninguno = "NINGUNO";
}

// Lo que se indica al registrar el abono (numero e importe son null si no hay abono)
public record DatosAbono(
    int DevolucionId,
    string Resultado,
    string? Numero,
    DateTime Fecha,
    decimal? Importe,
    string? Observaciones);

// Abono ya registrado; FechaCierre es null si se reabrió
public record AbonoGuardado(
    string Resultado,
    string? Numero,
    DateTime Fecha,
    decimal? Importe,
    string? Observaciones,
    DateTime? FechaCierre);
