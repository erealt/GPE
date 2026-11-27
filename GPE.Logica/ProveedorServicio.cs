using GPE.Datos;

namespace GPE.Logica;

// Evaluación de proveedores y fraude (Excel CP-RG05). Las notas se calculan con las tablas de la hoja LISTA,
// igual que las fórmulas del Excel: si la suma no llega al primer valor de la tabla, el Excel da #N/A y aquí null.
public class ProveedorServicio
{
    private readonly ProveedorRepositorio repositorio = new();

    // Con la nota de reclamaciones sacada de sus NC
    public List<ProveedorConProductos> ObtenerProveedores()
    {
        var proveedores = repositorio.ObtenerProveedores();
        var cuentas = repositorio.ContarReclamaciones(DateTime.Today.AddYears(-1));
        return proveedores
            .Select(p =>
            {
                var (noAfectan, afectan) = cuentas.GetValueOrDefault(p.Proveedor.Id);
                return p with { Proveedor = p.Proveedor with { Reclamaciones = NotaReclamaciones(noAfectan, afectan) } };
            })
            .ToList();
    }

    // Nº, nombre y fecha de alta son los de TenDB, donde se dan de alta y se cambian los proveedores.
    // Al abrir la lista se copian a GPE los que allí sean distintos; así las NC, los PDF y la web también los ven.
    // Si TenDB no responde, devuelve el error y se sigue con lo que hay en GPE.
    public string? ActualizarDesdeTenDB()
    {
        try
        {
            repositorio.ActualizarDesdeTenDB(new TenDBRepositorio().Proveedores());
            return null;
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or InvalidOperationException)
        {
            return ex.Message;
        }
    }

    // Proveedores de TenDB que aún no están en GPE, para elegir uno al crear un proveedor
    public List<ProveedorTenDB> ProveedoresTenDBSinDarDeAlta()
    {
        var enGpe = repositorio.Numeros();
        return new TenDBRepositorio().Proveedores().Where(p => !enGpe.Contains(p.Codigo)).ToList();
    }

    // Evaluación (A/B/C) de un proveedor con sus NC de ahora; null si no existe o no tiene notas
    public string? EvaluacionActual(int proveedorId) =>
        ObtenerProveedores().Select(p => p.Proveedor).Where(p => p.Id == proveedorId).Select(Evaluacion).FirstOrDefault();

    // Si la evaluación pasa a una que pide más controles (A → B, B → C...)
    public static bool Empeora(string? antes, string? despues) =>
        despues != null && string.CompareOrdinal(despues, antes ?? "") > 0;

    // Nota de reclamaciones (tabla de calidad), con las NC de los últimos 12 meses:
    // 0 NC → 0 · 1 que no afecta a la seguridad alimentaria → 1 · 2 o 3 que no afectan → 2 ·
    // más de 3 que no afectan, o alguna que afecta → 3. Las incidencias y las mejoras no cuentan.
    public static byte NotaReclamaciones(int noAfectan, int afectan) =>
        afectan > 0 || noAfectan > 3 ? (byte)3
        : noAfectan >= 2 ? (byte)2
        : noAfectan == 1 ? (byte)1
        : (byte)0;

    // Las NC de los últimos 12 meses que afectan y que no afectan a la seguridad alimentaria (sin indicar = no afecta)
    public static (int NoAfectan, int Afectan) ContarReclamaciones(IEnumerable<NoConformidad> registros)
    {
        var delAno = registros
            .Where(nc => nc.TipoRegistro == TipoRegistro.NoConformidad && nc.FechaDetectado.Date > DateTime.Today.AddYears(-1))
            .ToList();
        int afectan = delAno.Count(nc => nc.SeguridadAlimentaria == true);
        return (delAno.Count - afectan, afectan);
    }

    // "73 proveedores activos", para la tarjeta del inicio
    public string Resumen()
    {
        int activos = repositorio.ContarActivos();
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
            // Con "No aplica" no hay fecha; sin él, no hay motivo
            EnsayoFecha = p.EnsayoNoAplica ? null : p.EnsayoFecha,
            EnsayoMotivo = p.EnsayoNoAplica ? Limpio(p.EnsayoMotivo) : null,
            AuditoriaFecha = p.AuditoriaNoAplica ? null : p.AuditoriaFecha,
            AuditoriaMotivo = p.AuditoriaNoAplica ? Limpio(p.AuditoriaMotivo) : null
        };
    }

    // Errores de cada campo (por el nombre de la propiedad), para marcarlos en la pantalla
    public List<(string Campo, string Mensaje)> Errores(Proveedor p)
    {
        var errores = new List<(string, string)>();
        if (p.Nombre == "")
            errores.Add((nameof(Proveedor.Nombre), "Falta el nombre del proveedor."));
        else if (p.Nombre.Length > 200)
            errores.Add((nameof(Proveedor.Nombre), "El nombre no puede pasar de 200 letras."));

        if (p.Numero is int numero && repositorio.NumeroOcupado(numero, p.Id))
            errores.Add((nameof(Proveedor.Numero), $"Ya hay otro proveedor con el Nº {numero}."));

        foreach (var (campo, texto) in new[]
        {
            (nameof(Proveedor.EnsayoMotivo), p.EnsayoMotivo), (nameof(Proveedor.AuditoriaMotivo), p.AuditoriaMotivo)
        })
            if (texto?.Length > 500)
                errores.Add((campo, "No puede pasar de 500 letras."));

        if (p.RfaAnio is < 1990 or > 2100)
            errores.Add((nameof(Proveedor.RfaAnio), "Escribe un año de 4 cifras, por ejemplo 2024."));
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

    // Certificados

    // Se avisa cuando faltan estos días o menos para que caduque (lo pidió calidad: un mes antes)
    public const int DiasAvisoCertificado = 30;

    // Nombres que salen en el Excel, para elegirlos en la ventana del certificado (se puede escribir otro)
    public static readonly string[] NombresCertificado =
        { "BRC", "BRC PACK", "IFS", "IFS BROKER", "IFSPAC", "FSSC 22000", "ISO 9001", "ISO 14001", "ISO 22000", "FSC" };

    public enum EstadoCertificado { Vigente, PorCaducar, Caducado }

    public List<CertificadoProveedor> ObtenerCertificados(int proveedorId) => repositorio.ObtenerCertificados(proveedorId);

    // Los de proveedores activos que ya han caducado o caducan en los próximos 30 días
    public List<CertificadoPorCaducar> ObtenerCertificadosPorCaducar() =>
        repositorio.ObtenerCertificadosHasta(DateTime.Today.AddDays(DiasAvisoCertificado));

    // Días que faltan para que caduque: 0 = caduca hoy, negativo = ya caducado
    public static int DiasParaCaducar(CertificadoProveedor c) => (c.Caduca.Date - DateTime.Today).Days;

    // Caduca el mismo día que pone: hasta ese día incluido sigue valiendo
    public static EstadoCertificado Estado(CertificadoProveedor c) => DiasParaCaducar(c) switch
    {
        < 0 => EstadoCertificado.Caducado,
        <= DiasAvisoCertificado => EstadoCertificado.PorCaducar,
        _ => EstadoCertificado.Vigente
    };

    // "Caduca en 12 días", "Caduca hoy", "Caducó hace 3 días"...
    public static string TextoCaducidad(CertificadoProveedor c)
    {
        int dias = DiasParaCaducar(c);
        return dias switch
        {
            0 => "Caduca hoy",
            1 => "Caduca mañana",
            > 1 when dias <= DiasAvisoCertificado => $"Caduca en {dias} días",
            > 1 => "Vigente",
            -1 => "Caducó ayer",
            _ => $"Caducó hace {-dias} días"
        };
    }

    // Nuevo (Id 0) o con cambios. Devuelve su id.
    public int GuardarCertificado(CertificadoProveedor c)
    {
        if (Sesion.UsuarioActual is not Usuario usuario)
            throw new ReglaNegocioException("No hay ningún usuario elegido.");
        if (c.ProveedorId == 0)
            throw new ReglaNegocioException("Guarda primero el proveedor.");

        c = c with { Nombre = c.Nombre?.Trim() ?? "" };
        if (ErrorCertificado(c) is string error)
            throw new ReglaNegocioException(error);

        if (c.Id == 0)
            return repositorio.InsertarCertificado(c, usuario.Id);

        if (!repositorio.ActualizarCertificado(c))
            throw new ReglaNegocioException(MensajeCertificadoModificado);
        return c.Id;
    }

    public void BorrarCertificado(CertificadoProveedor c)
    {
        if (!repositorio.BorrarCertificado(c))
            throw new ReglaNegocioException(MensajeCertificadoModificado);
    }

    // Error del nombre (la fecha siempre está puesta), o null si está bien
    public static string? ErrorCertificado(CertificadoProveedor c) =>
        string.IsNullOrWhiteSpace(c.Nombre) ? "Escribe qué certificado es (BRC, IFS, ISO 9001...)."
        : c.Nombre.Trim().Length > 100 ? "El nombre no puede pasar de 100 letras."
        : null;

    private const string MensajeCertificadoModificado =
        "Otra persona ha cambiado o quitado este certificado mientras lo tenías abierto. " +
        "Para no borrar sus cambios, los tuyos no se han guardado.\n\n" +
        "Cierra la ficha del proveedor y vuelve a abrirla para ver lo último.";

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

    // FRECUENCIA MUESTREO (columna AH): IMPACTO DEL PRODUCTO + estatus + CONTROL RECEPCIÓN + RECLAMACIONES del producto.
    // 1-3 A, 4-6 B, 7-9 C, 10-12 D (LISTA, tabla 2º).
    public static string? FrecuenciaMuestreo(Proveedor p, ProductoProveedor producto) =>
        Buscar(N(producto.Impacto) + N(p.Estatus) + N(producto.ControlRecepcion) + N(producto.Reclamaciones),
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
    // (los tres son del producto, como en cada fila del Excel)
    public static int Puntuacion(ProductoProveedor x) =>
        OcurrenciaPorDeteccion(x) * Math.Max(N(x.Relacion), Math.Max(N(x.Historial), N(x.Subcontratado)));

    // RIESGO FRAUDE (columna AW): 1-6 BAJO, 7-12 MEDIO, 13-27 ALTO (LISTA, tabla 4º)
    public static string? RiesgoFraude(ProductoProveedor x) =>
        Buscar(Puntuacion(x), (1, "BAJO"), (7, "MEDIO"), (13, "ALTO"));

    // ACTUACIÓN CONTROL (columna AX)
    public static string? Actuacion(string? riesgoFraude) => riesgoFraude switch
    {
        "ALTO" => "Dos lotes aleatorios al año",
        "MEDIO" => "Un lote al año",
        "BAJO" => "Sin análisis",
        _ => null
    };

    public static readonly string[] Riesgos = { "BAJO", "MEDIO", "ALTO" };
}
