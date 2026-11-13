using GPE.Datos;

namespace GPE.Logica;

// Evaluación de proveedores y fraude (Excel CP-RG05). Las notas se calculan con las tablas de la hoja LISTA,
// igual que las fórmulas del Excel: si la suma no llega al primer valor de la tabla, el Excel da #N/A y aquí null.
public class ProveedorServicio
{
    private readonly ProveedorRepositorio repositorio = new();

    public List<ProveedorConProductos> ObtenerProveedores() => repositorio.ObtenerProveedores();

    // "103 proveedores · 22 por revisar", para la tarjeta del inicio
    public string Resumen()
    {
        var lista = repositorio.ObtenerProveedores();
        int activos = lista.Count(p => !p.Proveedor.Baja);
        int revisar = lista.Count(p => !p.Proveedor.Baja && p.Proveedor.Revisar != null);
        return $"{activos} {(activos == 1 ? "proveedor" : "proveedores")}"
            + (revisar > 0 ? $"  ·  ⚠ {revisar} por revisar" : "");
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
