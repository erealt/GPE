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

// Columnas A-N de la hoja PRODUCTOS. Las notas no se guardan: ver ProveedorServicio.
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
    DateTime? EnsayoFecha,
    bool EnsayoNoAplica,
    string? EnsayoMotivo,      // solo con "No aplica"
    DateTime? AuditoriaFecha,
    bool AuditoriaNoAplica,
    string? AuditoriaMotivo,   // solo con "No aplica"
    short? RfaAnio,
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
    byte? Impacto,     // al crearlo, el del proveedor; luego se puede cambiar
    byte? ControlRecepcion,
    byte? Reclamaciones,
    string? Parametro,
    byte? PHistorial,
    byte? PMotivacion,
    byte? PFacilidad,
    byte? DOrigen,
    byte? DCadena,
    byte? DMedidas,
    // Riesgo del proveedor para cometer fraude: en el Excel está en cada fila, por eso es de cada producto
    byte? Relacion,
    byte? Historial,
    byte? Subcontratado,
    string? MedidasPreventivas,
    string? Observaciones,
    byte[]? Version);  // null = producto nuevo, sin guardar

// Un certificado del proveedor (BRC, IFS, ISO 9001...) con su fecha de caducidad
public record CertificadoProveedor(
    int Id,
    int ProveedorId,
    string Nombre,
    DateTime Caduca,
    byte[]? Version);  // null = certificado nuevo, sin guardar

// Un certificado que caduca pronto o ya ha caducado, con su proveedor (para el aviso al abrir GPE)
public record CertificadoPorCaducar(CertificadoProveedor Certificado, int? NumeroProveedor, string NombreProveedor);

// Un proveedor para elegirlo en un desplegable (en la ficha de la NC)
public record ProveedorDeLista(int Id, string Nombre, bool Baja)
{
    public override string ToString() => Baja ? $"{Nombre} (de baja)" : Nombre;
}

// Un proveedor con todos sus productos (también los de baja)
public record ProveedorConProductos(Proveedor Proveedor, List<ProductoProveedor> Productos);
