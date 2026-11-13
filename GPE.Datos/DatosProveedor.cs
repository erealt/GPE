namespace GPE.Datos;

// Columna TIPO PRODUCTO (los valores que hay en el Excel CP-RG05)
public static class TipoProductoProveedor
{
    public static readonly string[] Todos = { "MMPP", "M. C. DIRECTO", "M. C. INDIRECTO", "PRODUCTO FINAL" };
}

// Columna TIPO del proveedor (hoja LISTA del Excel CP-RG05, tabla TIPO PROVEEDOR)
public static class TipoProveedor
{
    public static readonly string[] Todos = { "MMPP", "PROD. ACABADO", "CONT. DIRECTO", "CONT. INDIRECTO", "DISTRIBUIDOR" };
}

// Columnas A-N de la hoja PRODUCTOS, más HISTORIAL y P. SUBCONTRATADO. Las notas no se guardan: ver ProveedorServicio.
public record Proveedor(
    int Id,
    int? Numero,
    string Nombre,
    DateTime? FechaAlta,
    bool Baja,
    string? Tipo,
    DateTime? FechaRevision,
    byte? Impacto,
    byte? Estatus,
    byte? Reclamaciones,
    string? Certificados,
    string? EnsayoTrazabilidad,
    string? Auditoria,
    string? Rfa,
    byte? Relacion,    // null = igual que el estatus
    byte? Historial,
    byte? Subcontratado,
    string? Revisar,   // datos que en el Excel eran distintos según la fila
    byte[]? Version);  // null = proveedor nuevo, sin guardar

// Columnas O-AY de la hoja PRODUCTOS, sin las calculadas
public record ProductoProveedor(
    int Id,
    int ProveedorId,
    string? Codigo,
    string? Descripcion,
    string? TipoProducto,
    DateTime? FechaAlta,
    bool Baja,
    string? Alergenos,
    string? PosiblesAlergenos,
    DateTime? FechaRevision,
    string? FichaTecnica,
    string? AnalisisQuimicos,
    string? AnalisisMicro,
    string? AnalisisAlergenos,
    DateTime? EurochocFecha,
    string? EurochocAlcance,
    string? EurochocResultado,
    byte? Impacto,     // null = el del proveedor
    byte? ControlRecepcion,
    byte? Reclamaciones,
    string? Parametro,
    byte? PHistorial,
    byte? PMotivacion,
    byte? PFacilidad,
    byte? DOrigen,
    byte? DCadena,
    byte? DMedidas,
    string? MedidasPreventivas,
    string? Observaciones,
    byte[]? Version);  // null = producto nuevo, sin guardar

// Un proveedor con todos sus productos (también los de baja)
public record ProveedorConProductos(Proveedor Proveedor, List<ProductoProveedor> Productos);
