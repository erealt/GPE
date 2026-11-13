using GPE.Datos;

namespace GPE.Logica;

// Evaluación de proveedores y fraude (Excel CP-RG05). Las notas se calculan con las tablas de la hoja LISTA,
// igual que las fórmulas del Excel: si la suma no llega al primer valor de la tabla, el Excel da #N/A y aquí null.
public class ProveedorServicio
{
    private readonly ProveedorRepositorio repositorio = new();

    public List<ProveedorConProductos> ObtenerProveedores() => repositorio.ObtenerProveedores();

    // "73 proveedores activos", para la tarjeta del inicio
    public string Resumen()
    {
        int activos = repositorio.ObtenerProveedores().Count(p => !p.Proveedor.Baja);
        return $"{activos} {(activos == 1 ? "proveedor activo" : "proveedores activos")}";
    }

    public int SiguienteNumero() => repositorio.SiguienteNumero();

    // Guardado

    private const string MensajeModificado =
        "Otra persona ha cambiado este proveedor mientras lo tenías abierto. " +
        "Para no borrar sus cambios, los tuyos no se han guardado.\n\n" +
        "Apunta lo que hayas escrito y vuelve a abrirlo para ver lo último.";

    // Textos sin espacios de más; vacío = null
    public static Proveedor Normalizar(Proveedor p)
    {
        static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        return p with
        {
            Nombre = p.Nombre?.Trim() ?? "",
            Tipo = Limpio(p.Tipo),
            Certificados = Limpio(p.Certificados),
            EnsayoTrazabilidad = Limpio(p.EnsayoTrazabilidad),
            Auditoria = Limpio(p.Auditoria),
            Rfa = Limpio(p.Rfa)
        };
    }

    // Errores de cada campo (por el nombre de la propiedad), para marcarlos en la pantalla
    public List<(string Campo, string Mensaje)> Errores(Proveedor p)
    {
        var errores = new List<(string, string)>();
        if (p.Nombre == "")
            errores.Add((nameof(Proveedor.Nombre), "Escribe el nombre del proveedor."));
        else if (p.Nombre.Length > 200)
            errores.Add((nameof(Proveedor.Nombre), "El nombre no puede pasar de 200 letras."));

        if (p.Numero is int numero && repositorio.NumeroOcupado(numero, p.Id))
            errores.Add((nameof(Proveedor.Numero), $"Ya hay otro proveedor con el Nº {numero}."));

        foreach (var (campo, texto) in new[]
        {
            (nameof(Proveedor.Certificados), p.Certificados), (nameof(Proveedor.EnsayoTrazabilidad), p.EnsayoTrazabilidad),
            (nameof(Proveedor.Auditoria), p.Auditoria), (nameof(Proveedor.Rfa), p.Rfa)
        })
            if (texto?.Length > 500)
                errores.Add((campo, "No puede pasar de 500 letras."));
        return errores;
    }

    // Nuevo (Id 0) o con cambios. Devuelve su id.
    public int Guardar(Proveedor p)
    {
        if (Sesion.UsuarioActual is not Usuario usuario)
            throw new ReglaNegocioException("No hay ningún usuario elegido.");

        p = Normalizar(p);
        if (Errores(p) is { Count: > 0 } errores)
            throw new ReglaNegocioException(errores[0].Mensaje);

        if (p.Id == 0)
            return repositorio.Insertar(p, usuario.Id);

        if (!repositorio.Actualizar(p))
            throw new ReglaNegocioException(MensajeModificado);
        return p.Id;
    }

    // Productos

    public List<ProductoProveedor> ObtenerProductos(int proveedorId) => repositorio.ObtenerProductos(proveedorId);

    private const string MensajeProductoModificado =
        "Otra persona ha cambiado este producto mientras lo tenías abierto. " +
        "Para no borrar sus cambios, los tuyos no se han guardado.\n\n" +
        "Apunta lo que hayas escrito y vuelve a abrirlo para ver lo último.";

    // Largo máximo de cada texto del producto (columnas de PROVEEDOR_PRODUCTO)
    public static readonly (string Campo, int Largo)[] LargosProducto =
    {
        (nameof(ProductoProveedor.Codigo), 30), (nameof(ProductoProveedor.Descripcion), 300),
        (nameof(ProductoProveedor.Alergenos), 300), (nameof(ProductoProveedor.PosiblesAlergenos), 300),
        (nameof(ProductoProveedor.FichaTecnica), 50), (nameof(ProductoProveedor.AnalisisQuimicos), 500),
        (nameof(ProductoProveedor.AnalisisMicro), 500), (nameof(ProductoProveedor.AnalisisAlergenos), 500),
        (nameof(ProductoProveedor.EurochocAlcance), 500), (nameof(ProductoProveedor.EurochocResultado), 1000),
        (nameof(ProductoProveedor.Parametro), 1000), (nameof(ProductoProveedor.MedidasPreventivas), 1000)
    };

    public static ProductoProveedor Normalizar(ProductoProveedor x)
    {
        static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        return x with
        {
            Codigo = Limpio(x.Codigo),
            Descripcion = Limpio(x.Descripcion),
            TipoProducto = Limpio(x.TipoProducto),
            Alergenos = Limpio(x.Alergenos),
            PosiblesAlergenos = Limpio(x.PosiblesAlergenos),
            FichaTecnica = Limpio(x.FichaTecnica),
            AnalisisQuimicos = Limpio(x.AnalisisQuimicos),
            AnalisisMicro = Limpio(x.AnalisisMicro),
            AnalisisAlergenos = Limpio(x.AnalisisAlergenos),
            EurochocAlcance = Limpio(x.EurochocAlcance),
            EurochocResultado = Limpio(x.EurochocResultado),
            Parametro = Limpio(x.Parametro),
            MedidasPreventivas = Limpio(x.MedidasPreventivas),
            Observaciones = Limpio(x.Observaciones)
        };
    }

    public static List<(string Campo, string Mensaje)> ErroresProducto(ProductoProveedor x)
    {
        var errores = new List<(string, string)>();
        if (x.Descripcion == null)
            errores.Add((nameof(ProductoProveedor.Descripcion), "Escribe la descripción del producto."));

        var propiedades = typeof(ProductoProveedor);
        foreach (var (campo, largo) in LargosProducto)
            if (propiedades.GetProperty(campo)!.GetValue(x) is string texto && texto.Length > largo)
                errores.Add((campo, $"No puede pasar de {largo} letras."));
        return errores;
    }

    // Nuevo (Id 0) o con cambios. Devuelve su id.
    public int GuardarProducto(ProductoProveedor x)
    {
        if (Sesion.UsuarioActual is not Usuario usuario)
            throw new ReglaNegocioException("No hay ningún usuario elegido.");
        if (x.ProveedorId == 0)
            throw new ReglaNegocioException("Guarda primero el proveedor.");

        x = Normalizar(x);
        if (ErroresProducto(x) is { Count: > 0 } errores)
            throw new ReglaNegocioException(errores[0].Mensaje);

        if (x.Id == 0)
            return repositorio.InsertarProducto(x, usuario.Id);

        if (!repositorio.ActualizarProducto(x))
            throw new ReglaNegocioException(MensajeProductoModificado);
        return x.Id;
    }

    // Tabla de la hoja LISTA: el valor de la fila más alta cuyo "valor inferior" no pasa de la suma
    private static string? Buscar(int suma, params (int Desde, string Valor)[] tabla)
    {
        string? resultado = null;
        foreach (var (desde, valor) in tabla)
            if (suma >= desde) resultado = valor;
        return resultado;
    }

    // Una celda vacía cuenta como 0, como en las sumas y los MAX del Excel
    private static int N(byte? nota) => nota ?? 0;

    // EVALUACIÓN (columna J): IMPACTO + ESTATUS + RECLAMACIONES. 1-3 A, 4-6 B, 7-9 C (LISTA, tabla 1º).
    public static string? Evaluacion(Proveedor p) =>
        Buscar(N(p.Impacto) + N(p.Estatus) + N(p.Reclamaciones), (1, "A"), (4, "B"), (7, "C"));

    // Lo que pide cada evaluación (comentario de la columna J)
    public static string? QueSupone(string? evaluacion) => evaluacion switch
    {
        "A" => "Revisión anual documental",
        "B" => "Revisión anual documental con un ensayo de trazabilidad",
        "C" => "Revisión anual documental con ensayo de trazabilidad y auditoría en planta/documental según el caso",
        "D" => "Baja temporal hasta superación de auditoría",
        _ => null
    };

    // IMPACTO DEL PRODUCTO (columna AD): el del proveedor, salvo que el producto tenga el suyo
    public static byte? ImpactoProducto(Proveedor p, ProductoProveedor producto) => producto.Impacto ?? p.Impacto;

    // RELACIÓN (columna AS): el estatus, salvo que el proveedor tenga otra
    public static byte? Relacion(Proveedor p) => p.Relacion ?? p.Estatus;

    // FRECUENCIA MUESTREO (columna AH): IMPACTO DEL PRODUCTO + estatus + CONTROL RECEPCIÓN + RECLAMACIONES del producto.
    // 1-3 A, 4-6 B, 7-9 C, 10-12 D (LISTA, tabla 2º).
    public static string? FrecuenciaMuestreo(Proveedor p, ProductoProveedor producto) =>
        Buscar(N(ImpactoProducto(p, producto)) + N(p.Estatus) + N(producto.ControlRecepcion) + N(producto.Reclamaciones),
            (1, "A"), (4, "B"), (7, "C"), (10, "D"));

    // ALCANCE MUESTREO (columna AI)
    public static string? AlcanceMuestreo(string? frecuencia) => frecuencia switch
    {
        "A" => "Parámetro anual. 1 muestreo anual en la recepción",
        "B" => "Anual 33% parámetros. Trianual 100%. 2 muestreos anuales en la recepción",
        "C" => "Anual 50% parámetros. Bianual 100%. Muestreo en todas las recepciones",
        "D" => "Semestral 50% parámetros. Anual 100%. Muestreo en todas las recepciones",
        _ => null
    };

    // O * D (columna AQ): la ocurrencia más alta por la detección más alta
    public static int OcurrenciaPorDeteccion(ProductoProveedor x) =>
        Math.Max(N(x.PHistorial), Math.Max(N(x.PMotivacion), N(x.PFacilidad)))
        * Math.Max(N(x.DOrigen), Math.Max(N(x.DCadena), N(x.DMedidas)));

    // RIESGO (columna AR): 1-2 BAJO, 3-5 MEDIO, 6-9 ALTO (LISTA, tabla 3º)
    public static string? RiesgoVulnerabilidad(ProductoProveedor x) =>
        Buscar(OcurrenciaPorDeteccion(x), (1, "BAJO"), (3, "MEDIO"), (6, "ALTO"));

    // PUNTUACIÓN (columna AV): O * D por el valor más alto de RELACIÓN, HISTORIAL y P. SUBCONTRATADO
    public static int Puntuacion(Proveedor p, ProductoProveedor x) =>
        OcurrenciaPorDeteccion(x) * Math.Max(N(Relacion(p)), Math.Max(N(p.Historial), N(p.Subcontratado)));

    // RIESGO FRAUDE (columna AW): 1-6 BAJO, 7-12 MEDIO, 13-27 ALTO (LISTA, tabla 4º)
    public static string? RiesgoFraude(Proveedor p, ProductoProveedor x) =>
        Buscar(Puntuacion(p, x), (1, "BAJO"), (7, "MEDIO"), (13, "ALTO"));

    // ACTUACIÓN CONTROL (columna AX)
    public static string? Actuacion(string? riesgoFraude) => riesgoFraude switch
    {
        "ALTO" => "Dos lotes aleatorios al año",
        "MEDIO" => "Un lote al año",
        "BAJO" => "Sin análisis",
        _ => null
    };

    public static readonly string[] Riesgos = { "BAJO", "MEDIO", "ALTO" };

    // El riesgo de fraude más alto de los productos que no están de baja (null si ninguno lo tiene)
    public static string? RiesgoFraudeMasAlto(ProveedorConProductos p) =>
        p.Productos.Where(x => !x.Baja)
            .Select(x => RiesgoFraude(p.Proveedor, x))
            .OfType<string>()
            .MaxBy(r => Array.IndexOf(Riesgos, r));
}
