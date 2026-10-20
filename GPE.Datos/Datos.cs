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

// ---------- DeCA ----------

// Remitente o destinatario de un DeCA (texto libre; se sugieren los ya usados)
public record ParteEnvio(
    string Nombre,
    string Nif,
    string Direccion,
    string CodigoPostal,
    string Poblacion,
    string? Provincia,
    string Pais,
    string? Telefono)
{
    public override string ToString() => $"{Nombre} · {Poblacion}";
}

// Transportista ya usado en otro DeCA, con la última matrícula que llevó
public record TransportistaDeca(string Nombre, string Nif, string Matricula)
{
    public override string ToString() => $"{Nombre} · {Nif}";
}

public record PaletDeca(decimal PesoKg, int NumCajas);

// Lo que el usuario rellena al crear o modificar un DeCA
public record DatosDeca(
    ParteEnvio Remitente,
    ParteEnvio Destinatario,
    DateTime FechaCarga,
    string TransportistaNombre,
    string TransportistaNif,
    string Matricula,
    string Mercancia,
    string? Observaciones,
    List<PaletDeca> Palets);

// Fila de la lista de DeCA, con los totales de sus palets
public record DecaFila(
    int Id,
    string Numero,
    DateTime FechaCarga,
    string Remitente,
    string Destinatario,
    string Transportista,
    int Palets,
    int Cajas,
    decimal PesoKg,
    bool PdfAlDia);   // false si no se ha generado o se modificó después

// DeCA completo. CodigoPublico da nombre al PDF publicado (y por tanto a la dirección del QR).
// PdfGeneradoEn es null si nunca se generó o si se modificó después de generarlo.
public record DecaGuardado(
    int Id,
    string Numero,
    Guid CodigoPublico,
    DatosDeca Datos,
    string? ArchivoPdf,
    DateTime? PdfGeneradoEn,
    string CreadoPor,
    DateTime CreadoEn,
    string? ModificadoPor,
    DateTime? ModificadoEn);
