using System.Globalization;
using System.Text.Json;
using GPE.Datos;

namespace GPE.Logica;

// Paso de una fase a otra dentro de una entrada (Anterior es null al crear la devolución)
public record CambioFase(string? Anterior, int? FaseAnterior, string Nueva, int FaseNueva);

// Una acción del usuario: todo lo que se guardó con un mismo "Guardar"
public record EntradaHistorial(
    DateTime Fecha,
    string Usuario,
    string Titulo,
    List<string> Detalles,
    CambioFase? Fase);

// Convierte las filas de HISTORIAL_CAMBIOS (una por tabla y registro) en entradas legibles.
public static class HistorialFormateador
{
    // Columnas que no aportan nada al leer el historial
    private static readonly HashSet<string> CamposOcultos =
        new() { "id", "numero", "devolucion_id", "creada_en", "fabrica_id", "estado_id", "motivo_reapertura" };

    private static readonly Dictionary<string, string> NombresCampos = new()
    {
        ["cliente_id"] = "Cliente",
        ["albaran"] = "Albarán",
        ["motivo"] = "Motivo",
        ["observaciones"] = "Observaciones",
        ["fecha_solicitud"] = "Fecha de solicitud",
        ["fecha_recogida"] = "Fecha de recogida",
        ["fecha_llegada"] = "Fecha de llegada",
        ["producto_id"] = "Producto",
        ["lote"] = "Lote",
        ["cantidad_esperada"] = "Cantidad esperada",
        ["cantidad_recibida"] = "Cantidad recibida",
        ["transportista_id"] = "Transportista",
        ["direccion"] = "Dirección",
        ["telefono_contacto"] = "Teléfono de contacto",
        ["cantidad_apta"] = "Cantidad apta",
        ["observaciones_revision"] = "Observaciones de la revisión",
    };

    private static readonly Dictionary<int, string> TitulosPorFase = new()
    {
        [2] = "Solicitó la recogida",
        [3] = "Registró la llegada de la mercancía",
        [4] = "Finalizó la revisión",
        [5] = "Registró el abono",
        [6] = "Cerró la devolución",
    };

    // Al volver atrás, según la fase de la que se sale
    private static readonly Dictionary<int, string> TitulosReapertura = new()
    {
        [3] = "Deshizo la recepción de la mercancía",
        [4] = "Reabrió la revisión",
    };

    // Filas sin transaccion_id (anteriores a esa columna): se agrupan si son
    // del mismo usuario y están separadas como mucho por este tiempo.
    private static readonly TimeSpan MargenSinTransaccion = TimeSpan.FromSeconds(2);

    // cambios: en orden cronológico. Devuelve las entradas de la más reciente a la más antigua.
    public static List<EntradaHistorial> Agrupar(
        List<CambioHistorial> cambios, Dictionary<string, Dictionary<int, string>> nombres)
    {
        var grupos = new List<List<CambioHistorial>>();

        foreach (var cambio in cambios)
        {
            var ultimo = grupos.Count > 0 ? grupos[^1][^1] : null;

            bool mismoGuardado = ultimo != null
                && ultimo.Usuario == cambio.Usuario
                && (cambio.TransaccionId != null && ultimo.TransaccionId != null
                    ? cambio.TransaccionId == ultimo.TransaccionId
                    : (cambio.Fecha - ultimo.Fecha).Duration() <= MargenSinTransaccion);

            if (mismoGuardado)
                grupos[^1].Add(cambio);
            else
                grupos.Add(new List<CambioHistorial> { cambio });
        }

        return grupos
            .Select(g => Describir(g, nombres))
            .OrderByDescending(e => e.Fecha)
            .ToList();
    }

    private static EntradaHistorial Describir(
        List<CambioHistorial> grupo, Dictionary<string, Dictionary<int, string>> nombres)
    {
        bool creaDevolucion = grupo.Any(c => c.Tabla == "DEVOLUCION" && c.Accion == "INSERT");
        bool borraDevolucion = grupo.Any(c => c.Tabla == "DEVOLUCION" && c.Accion == "DELETE");
        bool tocaRecogida = grupo.Any(c => c.Tabla == "RECOGIDA");

        // Varias filas pueden proponer un título; gana el de menor prioridad
        var titulos = new List<(int Prioridad, string Texto)>();
        var detalles = new List<string>();
        CambioFase? fase = null;

        foreach (var cambio in grupo)
        {
            var antes = Leer(cambio.ValoresAnteriores, nombres);
            var despues = Leer(cambio.ValoresNuevos, nombres);
            var idsAntes = LeerIds(cambio.ValoresAnteriores);
            var idsDespues = LeerIds(cambio.ValoresNuevos);

            switch (cambio.Tabla, cambio.Accion)
            {
                case ("DEVOLUCION", "INSERT"):
                    titulos.Add((0, "Creó la devolución"));
                    detalles.AddRange(Resumen(despues));
                    if (idsDespues.TryGetValue("estado_id", out int estadoInicial))
                        fase = Fase(null, estadoInicial, nombres);
                    break;

                case ("DEVOLUCION", "DELETE"):
                    titulos.Add((0, "Borró la devolución"));
                    break;

                case ("DEVOLUCION", "UPDATE"):
                    if (idsAntes.TryGetValue("estado_id", out int estadoAntes)
                        && idsDespues.TryGetValue("estado_id", out int estadoDespues)
                        && estadoAntes != estadoDespues)
                    {
                        fase = Fase(estadoAntes, estadoDespues, nombres);
                    }

                    // Vuelta a un paso anterior: solo interesa el motivo
                    if (fase is { FaseAnterior: int desde } && fase.FaseNueva < desde)
                    {
                        detalles.Add($"Motivo: {despues.GetValueOrDefault("motivo_reapertura")}");
                        break;
                    }

                    // La fecha de recogida de la devolución es copia de la de RECOGIDA: no se repite
                    var cambiosDevolucion = Diferencias(antes, despues)
                        .Where(d => !(tocaRecogida && d.Campo == "fecha_recogida"))
                        .ToList();
                    detalles.AddRange(cambiosDevolucion.Select(d => d.Texto));

                    titulos.Add(cambiosDevolucion.Count > 0
                        ? (3, "Modificó la devolución")
                        : (4, "Cambió la fase"));
                    break;

                case ("DEVOLUCION_PRODUCTO", "INSERT"):
                    if (!creaDevolucion) titulos.Add((2, "Añadió productos"));
                    detalles.Add(LineaProducto(despues));
                    break;

                case ("DEVOLUCION_PRODUCTO", "UPDATE"):
                    titulos.Add((2, "Modificó los productos"));
                    detalles.Add(antes.GetValueOrDefault("cantidad_recibida") == null
                        ? LineaRevisada(despues)   // primera revisión: un resumen por producto
                        : $"{despues.GetValueOrDefault("producto_id")} · " +
                          string.Join("; ", Diferencias(antes, despues).Select(d => d.Texto)));
                    break;

                case ("DEVOLUCION_PRODUCTO", "DELETE"):
                    if (borraDevolucion) break;
                    titulos.Add((2, "Quitó productos"));
                    detalles.Add("Quitado: " + LineaProducto(antes));
                    break;

                case ("RECOGIDA", "INSERT"):
                    titulos.Add((1, "Solicitó la recogida"));
                    detalles.AddRange(Resumen(despues));
                    break;

                case ("RECOGIDA", "UPDATE"):
                    titulos.Add((1, "Modificó la recogida"));
                    detalles.AddRange(Diferencias(antes, despues).Select(d => d.Texto));
                    break;

                case ("RECOGIDA", "DELETE"):
                    if (!borraDevolucion) titulos.Add((1, "Anuló la recogida"));
                    break;

                case ("ADJUNTO", "INSERT"):
                    titulos.Add((2, EsFoto(despues) ? "Añadió fotos" : "Adjuntó el documento del abono"));
                    detalles.Add(despues.GetValueOrDefault("nombre_archivo") ?? "");
                    break;

                case ("ADJUNTO", "DELETE"):
                    if (borraDevolucion) break;
                    titulos.Add((2, EsFoto(antes) ? "Quitó fotos" : "Quitó el documento del abono"));
                    detalles.Add(antes.GetValueOrDefault("nombre_archivo") ?? "");
                    break;
            }
        }

        // Si la entrada pasa a otra fase, el título dice qué paso se completó (o se reabrió)
        if (fase is { FaseAnterior: int faseDesde })
        {
            bool haciaAtras = fase.FaseNueva < faseDesde;
            var titulosFase = haciaAtras ? TitulosReapertura : TitulosPorFase;
            if (titulosFase.TryGetValue(haciaAtras ? faseDesde : fase.FaseNueva, out var tituloFase))
                titulos.Add((-1, tituloFase));
        }

        string titulo = titulos.Count > 0 ? titulos.MinBy(t => t.Prioridad).Texto : "Modificó la devolución";
        return new EntradaHistorial(grupo[0].Fecha, grupo[0].Usuario, titulo, detalles, fase);
    }

    // ---------- Textos ----------

    private static CambioFase Fase(int? estadoAntes, int estadoDespues,
        Dictionary<string, Dictionary<int, string>> nombres)
    {
        string? Nombre(int? id) => id is int i ? nombres.GetValueOrDefault("estado_id")?.GetValueOrDefault(i) ?? $"Estado {i}" : null;
        int? Numero(int? id) =>
            id is int i && int.TryParse(nombres.GetValueOrDefault("estado_fase")?.GetValueOrDefault(i), out int f) ? f : null;

        return new CambioFase(Nombre(estadoAntes), Numero(estadoAntes), Nombre(estadoDespues)!, Numero(estadoDespues) ?? 1);
    }

    private static IEnumerable<string> Resumen(Dictionary<string, string?> valores) =>
        valores
            .Where(c => !CamposOcultos.Contains(c.Key) && c.Value != null)
            .Select(c => $"{Nombre(c.Key)}: {c.Value}");

    private static IEnumerable<(string Campo, string Texto)> Diferencias(
        Dictionary<string, string?> antes, Dictionary<string, string?> despues) =>
        despues
            .Where(c => !CamposOcultos.Contains(c.Key))
            .Where(c => antes.GetValueOrDefault(c.Key) != c.Value)
            .Select(c => (c.Key, antes.GetValueOrDefault(c.Key) is string anterior
                ? $"{Nombre(c.Key)}: {anterior} → {c.Value ?? "—"}"
                : $"{Nombre(c.Key)}: {c.Value}"));   // antes estaba vacío: solo el valor nuevo

    // "Crema de cacao 400 g: 5 recibidas, 4 aptas · 1 bote abollado"
    private static string LineaRevisada(Dictionary<string, string?> linea)
    {
        string texto = $"{linea.GetValueOrDefault("producto_id")}: " +
                       $"{linea.GetValueOrDefault("cantidad_recibida")} recibidas, " +
                       $"{linea.GetValueOrDefault("cantidad_apta")} aptas";
        string? observaciones = linea.GetValueOrDefault("observaciones");
        return observaciones == null ? texto : $"{texto} · {observaciones}";
    }

    private static bool EsFoto(Dictionary<string, string?> adjunto) =>
        adjunto.GetValueOrDefault("tipo") == TipoAdjunto.Foto;

    // "Cacao en polvo 500 g × 3 · lote 564"
    private static string LineaProducto(Dictionary<string, string?> linea)
    {
        string texto = $"{linea.GetValueOrDefault("producto_id")} × {linea.GetValueOrDefault("cantidad_esperada")}";
        string? lote = linea.GetValueOrDefault("lote");
        return lote == null ? texto : $"{texto} · lote {lote}";
    }

    private static string Nombre(string campo) =>
        NombresCampos.GetValueOrDefault(campo) ?? campo.Replace('_', ' ');

    // ---------- Lectura del JSON ----------

    // Las fechas llegan del JSON como "2026-10-14"; se muestran como 14/10/2026
    private static string FormatoTexto(string texto) =>
        texto.Length == 10 && DateTime.TryParseExact(texto, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha.ToString("dd/MM/yyyy")
            : texto;

    // Valores listos para mostrar: los ids con nombre conocido se sustituyen por su nombre
    private static Dictionary<string, string?> Leer(
        string? json, Dictionary<string, Dictionary<int, string>> nombres)
    {
        var valores = new Dictionary<string, string?>();
        if (string.IsNullOrEmpty(json)) return valores;

        using var documento = JsonDocument.Parse(json);
        foreach (var propiedad in documento.RootElement.EnumerateObject())
        {
            var valor = propiedad.Value;

            if (valor.ValueKind == JsonValueKind.Number
                && nombres.TryGetValue(propiedad.Name, out var porId)
                && porId.TryGetValue(valor.GetInt32(), out var nombre))
            {
                valores[propiedad.Name] = nombre;
                continue;
            }

            valores[propiedad.Name] = valor.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => FormatoTexto(valor.GetString()!),
                _ => valor.GetRawText()
            };
        }
        return valores;
    }

    // Columnas *_id con su valor numérico original (para detectar el cambio de fase)
    private static Dictionary<string, int> LeerIds(string? json)
    {
        var ids = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(json)) return ids;

        using var documento = JsonDocument.Parse(json);
        foreach (var propiedad in documento.RootElement.EnumerateObject())
        {
            if (propiedad.Name.EndsWith("_id") && propiedad.Value.ValueKind == JsonValueKind.Number)
                ids[propiedad.Name] = propiedad.Value.GetInt32();
        }
        return ids;
    }
}
