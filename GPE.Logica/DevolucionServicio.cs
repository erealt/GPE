using GPE.Datos;

namespace GPE.Logica;

public class ReglaNegocioException : Exception
{
    public ReglaNegocioException(string mensaje) : base(mensaje) { }
}

public class DevolucionServicio
{
    private readonly DevolucionRepositorio repositorio = new();

    // Consultas

    public List<DevolucionFila> ObtenerDevoluciones() => repositorio.ObtenerDevoluciones();

    public DevolucionFila? ObtenerDevolucion(int devolucionId) => repositorio.ObtenerDevolucion(devolucionId);

    public string? ObtenerObservacionesSolicitud(int devolucionId) =>
        repositorio.ObtenerObservacionesSolicitud(devolucionId);

    public List<string> ObtenerEstados() => repositorio.ObtenerEstados();

    public List<Cliente> ObtenerClientes() => repositorio.ObtenerClientes();

    public List<Producto> ObtenerProductos() => repositorio.ObtenerProductos();

    public List<Usuario> ObtenerUsuarios() => repositorio.ObtenerUsuarios();

    public List<Transportista> ObtenerTransportistas() => repositorio.ObtenerTransportistas();

    public RecogidaGuardada? ObtenerRecogida(int devolucionId) => repositorio.ObtenerRecogida(devolucionId);

    public List<EntradaHistorial> ObtenerHistorial(int devolucionId)
    {
        var nombres = repositorio.ObtenerNombresPorCampo();
        return HistorialFormateador.Agrupar(repositorio.ObtenerHistorial(devolucionId), nombres);
    }

    // Operaciones

    public int CrearDevolucion(DatosNuevaDevolucion datos)
    {
        Validar(datos);
        int id = repositorio.InsertarDevolucion(datos);
        AvisoServicio.EnviarPendientesEnSegundoPlano();
        return id;
    }

    // Almacén

    private const string MensajeOtroUsuario =
        "Otro usuario ya ha cambiado el estado de esta devolución. Cierra la pantalla y vuelve a abrirla.";

    public List<LineaSolicitada> ObtenerProductosSolicitados(int devolucionId) =>
        repositorio.ObtenerProductosSolicitados(devolucionId);

    public List<LineaRevision> ObtenerLineasRevision(int devolucionId) =>
        repositorio.ObtenerLineasRevision(devolucionId);

    public CabeceraRevision ObtenerCabeceraRevision(int devolucionId) =>
        repositorio.ObtenerCabeceraRevision(devolucionId);

    // Solo se puede reabrir el último paso completado (el siguiente aún no ha empezado):
    // fase 3 → 2 deshace la recepción; fase 4 → 3 reabre la revisión;
    // fase 6 → 4 reabre el abono (abono y cierre se registran a la vez).
    public void Reabrir(int devolucionId, int faseActual, string motivo)
    {
        if (faseActual is not (Fase.MercanciaRecibida or Fase.RevisadaAlmacen or Fase.Cierre))
            throw new ReglaNegocioException("Este paso ya no se puede reabrir.");

        motivo = motivo.Trim();
        if (motivo.Length < 5)
            throw new ReglaNegocioException("Explica el motivo para reabrir (al menos 5 caracteres).");
        if (motivo.Length > 500)
            throw new ReglaNegocioException("El motivo no puede superar los 500 caracteres.");

        if (!repositorio.Reabrir(devolucionId, faseActual, motivo))
            throw new ReglaNegocioException(MensajeOtroUsuario);
    }

    // Hace falta el recibo de la agencia: el que se adjunta ahora o uno de una
    // recepción anterior que se deshizo
    public void MarcarRecibida(int devolucionId, IEnumerable<string> rutasRecibo)
    {
        var recibo = LeerArchivos(TipoAdjunto.Recibo, rutasRecibo);
        if (recibo.Count == 0 && repositorio.ObtenerAdjuntos(devolucionId, TipoAdjunto.Recibo).Count == 0)
            throw new ReglaNegocioException("Adjunta el recibo de la agencia de transporte.");

        if (!repositorio.MarcarRecibida(devolucionId, DateTime.Today, recibo))
            throw new ReglaNegocioException(MensajeOtroUsuario);
    }

    // Comprueba que una línea de la revisión esté completa. Devuelve el error o null si está bien.
    public static string? ErrorLinea(int? productoId, string? lote, int? cantidad, bool? apto)
    {
        if (productoId == null) return "Elige el código del producto.";
        if (lote?.Length > 30) return "El lote no puede superar los 30 caracteres.";
        if (cantidad == null) return "Indica la cantidad.";
        if (cantidad <= 0) return "La cantidad debe ser mayor que 0.";
        if (apto == null) return "Indica si está bien para servir.";
        return null;
    }

    public void FinalizarRevision(DatosRevision datos)
    {
        if (string.IsNullOrWhiteSpace(datos.RevisadaPor))
            throw new ReglaNegocioException("Indica quién ha revisado la mercancía.");
        if (datos.RevisadaPor.Length > 100 || datos.Agencia?.Length > 100)
            throw new ReglaNegocioException("La agencia y quién revisa no pueden superar los 100 caracteres.");

        if (datos.Lineas.Count == 0)
            throw new ReglaNegocioException("Apunta al menos un producto de la mercancía recibida.");
        if (datos.Lineas.Any(l => ErrorLinea(l.ProductoId, l.Lote, l.Cantidad, l.Apto) != null))
            throw new ReglaNegocioException("Hay líneas de producto incompletas o con errores.");

        if (!repositorio.FinalizarRevision(datos))
            throw new ReglaNegocioException(MensajeOtroUsuario);
    }

    // Abono y cierre

    // Pasados estos días desde la llegada de la mercancía sin abonar, se avisa al usuario
    public const int DiasAvisoAbono = 3;

    // Días desde que llegó la mercancía, o null si no está esperando abono
    // (aún no ha llegado o ya está cerrada)
    public static int? DiasSinAbono(DevolucionFila devolucion) =>
        devolucion.Fase is Fase.MercanciaRecibida or Fase.RevisadaAlmacen && devolucion.FechaLlegada is DateTime llegada
            ? (DateTime.Today - llegada.Date).Days
            : null;

    public static bool AbonoRetrasado(DevolucionFila devolucion) =>
        DiasSinAbono(devolucion) >= DiasAvisoAbono;

    public AbonoGuardado? ObtenerAbono(int devolucionId) => repositorio.ObtenerAbono(devolucionId);

    // Solo es un aviso: un mismo abono podría cubrir varias devoluciones
    public string? DevolucionConNumeroAbono(string numeroAbono, int devolucionId) =>
        repositorio.DevolucionConNumeroAbono(numeroAbono.Trim(), devolucionId);

    public void RegistrarAbono(DatosAbono datos, DateTime? fechaLlegada)
    {
        ValidarAbono(datos, fechaLlegada);

        if (datos.Resultado != ResultadoAbono.Ninguno
            && repositorio.ObtenerAdjuntos(datos.DevolucionId, TipoAdjunto.Abono).Count == 0)
            throw new ReglaNegocioException("Adjunta el documento del abono.");

        if (!repositorio.RegistrarAbono(datos, DateTime.Today))
            throw new ReglaNegocioException(MensajeOtroUsuario);
    }

    private static void ValidarAbono(DatosAbono datos, DateTime? fechaLlegada)
    {
        if (datos.Resultado is not (ResultadoAbono.Total or ResultadoAbono.Parcial or ResultadoAbono.Ninguno))
            throw new ReglaNegocioException("Indica si se abona todo, una parte o nada.");

        if (datos.Resultado != ResultadoAbono.Ninguno)
        {
            if (string.IsNullOrWhiteSpace(datos.Numero))
                throw new ReglaNegocioException("Indica el número del abono.");
            if (datos.Numero.Length > 30)
                throw new ReglaNegocioException("El número del abono no puede superar los 30 caracteres.");
            if (datos.Importe is not > 0)
                throw new ReglaNegocioException("El importe del abono debe ser mayor que 0.");
        }
        else if (datos.Numero != null || datos.Importe != null)
        {
            throw new ReglaNegocioException("Si no hay abono no se indica número ni importe.");
        }

        if (datos.Resultado != ResultadoAbono.Total && string.IsNullOrWhiteSpace(datos.Observaciones))
            throw new ReglaNegocioException(datos.Resultado == ResultadoAbono.Parcial
                ? "Explica en observaciones por qué el abono es parcial."
                : "Explica en observaciones por qué no se abona.");

        if (datos.Fecha.Date > DateTime.Today)
            throw new ReglaNegocioException("La fecha del abono no puede ser posterior a hoy.");
        if (fechaLlegada != null && datos.Fecha.Date < fechaLlegada.Value.Date)
            throw new ReglaNegocioException("La fecha del abono no puede ser anterior a la llegada de la mercancía.");
    }

    // Adjuntos

    public const int TamanoMaximoAdjunto = 10 * 1024 * 1024;   // 10 MB por archivo

    private static readonly Dictionary<string, string[]> ExtensionesPermitidas = new()
    {
        [TipoAdjunto.Foto] = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" },
        [TipoAdjunto.Abono] = new[] { ".pdf", ".jpg", ".jpeg", ".png" },
        [TipoAdjunto.Recibo] = new[] { ".pdf", ".jpg", ".jpeg", ".png" },
    };

    public static string[] Extensiones(string tipo) => ExtensionesPermitidas[tipo];

    public List<AdjuntoInfo> ObtenerAdjuntos(int devolucionId, string tipo) =>
        repositorio.ObtenerAdjuntos(devolucionId, tipo);

    public byte[] ObtenerContenidoAdjunto(int adjuntoId) => repositorio.ObtenerContenidoAdjunto(adjuntoId);

    public void EliminarAdjunto(int adjuntoId) => repositorio.EliminarAdjunto(adjuntoId);

    public void AnadirAdjuntos(int devolucionId, string tipo, IEnumerable<string> rutas)
    {
        var archivos = LeerArchivos(tipo, rutas);
        if (archivos.Count > 0)
            repositorio.InsertarAdjuntos(devolucionId, tipo, archivos);
    }

    // Comprueba el tipo y el tamaño de cada archivo y lo lee
    private static List<ArchivoNuevo> LeerArchivos(string tipo, IEnumerable<string> rutas)
    {
        var archivos = new List<ArchivoNuevo>();
        foreach (string ruta in rutas)
        {
            string nombre = Path.GetFileName(ruta);

            if (!ExtensionesPermitidas[tipo].Contains(Path.GetExtension(ruta).ToLowerInvariant()))
                throw new ReglaNegocioException($"\"{nombre}\" no es un tipo de archivo permitido.");

            if (new FileInfo(ruta).Length > TamanoMaximoAdjunto)
                throw new ReglaNegocioException($"\"{nombre}\" ocupa más de 10 MB.");

            archivos.Add(new ArchivoNuevo(nombre, File.ReadAllBytes(ruta)));
        }
        return archivos;
    }

    // Recogida

    public void GuardarRecogida(DatosRecogida datos, DateTime fechaSolicitud)
    {
        ValidarRecogida(datos, fechaSolicitud);
        if (!repositorio.GuardarRecogida(datos))
            throw new ReglaNegocioException("La mercancía ya ha llegado: la recogida ya no se puede modificar.");
        AvisoServicio.EnviarPendientesEnSegundoPlano();
    }

    private static void ValidarRecogida(DatosRecogida datos, DateTime fechaSolicitud)
    {
        if (string.IsNullOrWhiteSpace(datos.Direccion))
            throw new ReglaNegocioException("La dirección de recogida es obligatoria.");

        if (datos.Direccion.Length > 200)
            throw new ReglaNegocioException("La dirección no puede superar los 200 caracteres.");

        if (datos.FechaRecogida.Date < fechaSolicitud.Date)
            throw new ReglaNegocioException("La fecha de recogida prevista no puede ser anterior a la de solicitud.");

        if (datos.TelefonoContacto != null && !TelefonoValido(datos.TelefonoContacto))
            throw new ReglaNegocioException("El teléfono de contacto no es válido.");
    }

    // Admite dígitos, espacios, guiones y "+" inicial, con al menos 9 dígitos
    public static bool TelefonoValido(string telefono) =>
        telefono.Length <= 30
        && telefono.Count(char.IsDigit) >= 9
        && telefono.Select((c, i) => char.IsDigit(c) || c == ' ' || c == '-' || (c == '+' && i == 0)).All(ok => ok);

    private static void Validar(DatosNuevaDevolucion datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Motivo))
            throw new ReglaNegocioException("El motivo de la devolución es obligatorio.");

        if (datos.Motivo.Length > 50)
            throw new ReglaNegocioException("El motivo no puede superar los 50 caracteres.");

        if (datos.Lineas.Any(l => l.Lote?.Length > 30))
            throw new ReglaNegocioException("El lote no puede superar los 30 caracteres.");

        // Los productos son opcionales: muchas veces no se sabe qué va a devolver el cliente
        if (datos.Lineas.Any(l => l.Cantidad <= 0))
            throw new ReglaNegocioException("Todas las cantidades deben ser mayores que 0.");

        if (datos.FechaSolicitud.Date > DateTime.Today)
            throw new ReglaNegocioException("La fecha de solicitud no puede ser posterior a hoy.");
    }
}