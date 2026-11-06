namespace GPE.Datos;

// Fases (ESTADO_DEVOLUCION.fase). Abono y cierre van juntos: de la 4 se pasa a la 6; la 5 solo sale en la barra.
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

public record Usuario(int Id, string Nombre)
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

// Usuario de la aplicación; se envía a SQL Server para que los triggers sepan quién cambia qué.
public static class Sesion
{
    // En el escritorio hay un solo usuario por programa. La web atiende a varias personas a la
    // vez, así que cada operación indica el suyo con Como(...), que tiene preferencia.
    private static Usuario? usuarioPrograma;
    private static readonly AsyncLocal<Usuario?> usuarioOperacion = new();

    public static Usuario? UsuarioActual
    {
        get => usuarioOperacion.Value ?? usuarioPrograma;
        set => usuarioPrograma = value;
    }

    // using (Sesion.Como(usuario)) { ... }: lo que se haga dentro queda a nombre de ese usuario
    public static IDisposable Como(Usuario usuario)
    {
        var anterior = usuarioOperacion.Value;
        usuarioOperacion.Value = usuario;
        return new Restaurar(() => usuarioOperacion.Value = anterior);
    }

    private sealed class Restaurar(Action accion) : IDisposable
    {
        public void Dispose() => accion();
    }

    // Roles del usuario (tabla USUARIO_ROL): deciden qué pasos puede hacer
    public static HashSet<string> Roles { get; set; } = new();

    public static bool TieneRol(string rol) => Roles.Contains(rol);

    // Módulos del menú. Quien no tiene ningún rol puede consultar las devoluciones.
    public static bool VeDevoluciones => Roles.Count == 0 || Roles.Overlaps(Rol.Devoluciones);
    public static bool VeCalidad => TieneRol(Rol.Calidad);
}

// Nombres de la tabla ROL. Cada rol hace sus pasos; el resto solo consulta.
public static class Rol
{
    public const string Administracion = "Administración";   // nueva devolución, abono y cierre
    public const string Transporte = "Transporte";           // recogida
    public const string Almacen = "Almacén";                 // llegada y revisión de la mercancía
    public const string Calidad = "Calidad";                 // no conformidades y rutina del supervisor

    public static readonly string[] Devoluciones = { Administracion, Transporte, Almacen };
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

// Agencia y RevisadaPor son null si no se ha guardado; Transportista (el de la recogida) se propone como agencia.
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
    public const string RevisionFinalizada = "REVISION_FINALIZADA";
    public const string AbonoRegistrado = "ABONO_REGISTRADO";
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
    string? DireccionRecogida,
    // Resultado de la revisión del almacén (unidades aptas y no aptas)
    int UnidadesAptas,
    int UnidadesNoAptas,
    // Abono (null si todavía no se ha registrado)
    string? ResultadoAbono,
    string? NumeroAbono,
    decimal? ImporteAbono,
    string? ObservacionesAbono);

// Resúmenes por correo (tabla AVISO_RESUMEN, una vez al día por destinatario)
public static class TipoResumen
{
    public const string RecogidaPendiente = "RECOGIDA_PENDIENTE";
    public const string RevisionPendiente = "REVISION_PENDIENTE";
    public const string AbonoPendiente = "ABONO_PENDIENTE";
    public const string SinLlegar = "SIN_LLEGAR";   // solo los viernes
}

// Devolución que alguien tiene pendiente, con el correo de esa persona
public record DevolucionPendiente(
    string Destinatario,
    string Numero,
    string Cliente,
    string CodigoCliente,
    DateTime FechaSolicitud,
    DateTime? FechaLlegada,
    string? Transportista,
    DateTime? FechaRecogidaPrevista,
    DateTime? RecogidaGestionadaEn);

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
    public override string ToString() => $"{Nombre} · {Nif} · {Poblacion}";
}

// Transportista ya usado en otro DeCA, con la última matrícula que llevó
public record TransportistaDeca(string Nombre, string Nif, string Matricula)
{
    public override string ToString() => $"{Nombre} · {Nif}";
}

public record PaletDeca(decimal PesoKg, int NumCajas);

// Quién es el cargador contractual (quien contrata el transporte con el transportista)
public static class TipoCargador
{
    public const string Remitente = "REMITENTE";
    public const string Destinatario = "DESTINATARIO";
    public const string Otro = "OTRO";
}

// Lo que el usuario rellena al crear o modificar un DeCA. CargadorOtro solo se usa si
// CargadorTipo es Otro. LugarCarga y LugarDescarga son null si coinciden con las direcciones
// del remitente y del destinatario.
public record DatosDeca(
    ParteEnvio Remitente,
    ParteEnvio Destinatario,
    string CargadorTipo,
    ParteEnvio? CargadorOtro,
    DateTime FechaCarga,
    string TransportistaNombre,
    string TransportistaNif,
    string Matricula,
    string? MatriculaRemolque,
    string? LugarCarga,
    string? LugarDescarga,
    string Mercancia,
    string? Observaciones,
    List<PaletDeca> Palets)
{
    public ParteEnvio? Cargador => CargadorTipo switch
    {
        TipoCargador.Remitente => Remitente,
        TipoCargador.Destinatario => Destinatario,
        _ => CargadorOtro
    };
}

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
