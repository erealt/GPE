using System.Globalization;
using System.Text.Json;
using GPE.Datos;

namespace GPE.Logica;


public record CambioFase(string? Anterior, int? FaseAnterior, string Nueva, int FaseNueva);

public record EntradaHistorial(
    DateTime Fecha,
    string Usuario,
    string Titulo,
    List<string> Detalles,
    CambioFase? Fase);

public static class HistorialFormateador
{
    private static readonly HashSet<string> CamposOcultos =
        new() { "id", "numero", "devolucion_id", "creada_en", "fabrica_id", "estado_id", "motivo_reapertura" };

    private static readonly Dictionary<string, string> NombresCampos = new()
    {
        ["cliente_id"] = "Cliente",
        ["albaran"] = "Albarán",
        ["motivo"] = "Motivo",
        ["observaciones"] = "Observaciones",
        ["fecha_solicitud"] = "Fecha de solicitud",
        ["fecha_recogida"] = "Fecha de recogida prevista",
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
        ["revisada_por"] = "Revisada por",
        ["agencia"] = "Agencia",
        ["resultado_abono"] = "Resultado del abono",
        ["numero_abono"] = "Nº de abono",
        ["fecha_abono"] = "Fecha del abono",
        ["importe_abono"] = "Importe del abono",
        ["observaciones_abono"] = "Observaciones del abono",
        ["fecha_cierre"] = "Fecha de cierre",
    };

    private static readonly Dictionary<string, string> NombresResultadoAbono = new()
    {
        [ResultadoAbono.Total] = "Abono total",
        [ResultadoAbono.Parcial] = "Abono parcial",
        [ResultadoAbono.Ninguno] = "Sin abono",
    };

    private static readonly Dictionary<int, string> TitulosPorFase = new()
    {
        [Fase.RecogidaSolicitada] = "Solicitó la recogida",
        [Fase.MercanciaRecibida] = "Registró la llegada de la mercancía",
        [Fase.RevisadaAlmacen] = "Finalizó la revisión",
        [Fase.AbonoRealizado] = "Registró el abono",
        [Fase.Cierre] = "Registró el abono y cerró la devolución",
    };


    private static readonly Dictionary<int, string> TitulosReapertura = new()
    {
        [Fase.MercanciaRecibida] = "Deshizo la recepción de la mercancía",
        [Fase.RevisadaAlmacen] = "Reabrió la revisión",
        [Fase.Cierre] = "Reabrió el abono",
    };


    private static readonly TimeSpan MargenSinTransaccion = TimeSpan.FromSeconds(2);

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
        // Al finalizar la revisión sus líneas se borran y se vuelven a escribir enteras
        bool escribeRevision = grupo.Any(c => c.Tabla == "REVISION_LINEA" && c.Accion == "INSERT");

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
                        fase = CrearCambioFase(null, estadoInicial, nombres);
                    break;

                case ("DEVOLUCION", "DELETE"):
                    titulos.Add((0, "Borró la devolución"));
                    break;

                case ("DEVOLUCION", "UPDATE"):
                    if (idsAntes.TryGetValue("estado_id", out int estadoAntes)
                        && idsDespues.TryGetValue("estado_id", out int estadoDespues)
                        && estadoAntes != estadoDespues)
                    {
                        fase = CrearCambioFase(estadoAntes, estadoDespues, nombres);
                    }

                    // Vuelta a un paso anterior: solo interesa el motivo
                    if (fase is { FaseAnterior: int desde } && fase.FaseNueva < desde)
                    {
                        detalles.Add($"Motivo: {despues.GetValueOrDefault("motivo_reapertura")}");
                        break;
                    }

                
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
                    detalles.Add($"{despues.GetValueOrDefault("producto_id")} · " +
                                 string.Join("; ", Diferencias(antes, despues).Select(d => d.Texto)));
                    break;

                case ("REVISION_LINEA", "INSERT"):
                    titulos.Add((2, "Apuntó la revisión"));
                    detalles.Add(LineaRevision(despues));
                    break;

                case ("REVISION_LINEA", "DELETE"):
                    if (borraDevolucion || escribeRevision) break;
                    titulos.Add((2, "Quitó líneas de la revisión"));
                    detalles.Add("Quitado: " + LineaRevision(antes));
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
                    titulos.Add((2, TituloAdjunto(despues, "Añadió fotos", "Adjuntó el recibo de la agencia", "Adjuntó el documento del abono")));
                    detalles.Add(despues.GetValueOrDefault("nombre_archivo") ?? "");
                    break;

                case ("ADJUNTO", "DELETE"):
                    if (borraDevolucion) break;
                    titulos.Add((2, TituloAdjunto(antes, "Quitó fotos", "Quitó el recibo de la agencia", "Quitó el documento del abono")));
                    detalles.Add(antes.GetValueOrDefault("nombre_archivo") ?? "");
                    break;
            }
        }

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

    private static CambioFase CrearCambioFase(int? estadoAntes, int estadoDespues,
        Dictionary<string, Dictionary<int, string>> nombres)
    {
        string? Nombre(int? id) => id is int i ? nombres.GetValueOrDefault("estado_id")?.GetValueOrDefault(i) ?? $"Estado {i}" : null;
        int? Numero(int? id) =>
            id is int i && int.TryParse(nombres.GetValueOrDefault("estado_fase")?.GetValueOrDefault(i), out int f) ? f : null;

        return new CambioFase(Nombre(estadoAntes), Numero(estadoAntes), Nombre(estadoDespues)!, Numero(estadoDespues) ?? Fase.SolicitudRecibida);
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

   
    // "Cacao en polvo 500 g × 3 · lote 564 · cad. 01/03/2027 · apto"
    private static string LineaRevision(Dictionary<string, string?> linea)
    {
        var partes = new List<string>
        {
            $"{linea.GetValueOrDefault("producto_id")} × {linea.GetValueOrDefault("cantidad")}"
        };
        if (linea.GetValueOrDefault("lote") is string lote) partes.Add($"lote {lote}");
        if (linea.GetValueOrDefault("caducidad") is string caducidad) partes.Add($"cad. {caducidad}");
        partes.Add(linea.GetValueOrDefault("apto") == "true" ? "apto" : "no apto");
        return string.Join(" · ", partes);
    }

    private static string TituloAdjunto(Dictionary<string, string?> adjunto, string foto, string recibo, string abono) =>
        adjunto.GetValueOrDefault("tipo") switch
        {
            TipoAdjunto.Foto => foto,
            TipoAdjunto.Recibo => recibo,
            _ => abono
        };

    // "Cacao en polvo 500 g × 3 · lote 564"
    private static string LineaProducto(Dictionary<string, string?> linea)
    {
        string texto = $"{linea.GetValueOrDefault("producto_id")} × {linea.GetValueOrDefault("cantidad_esperada")}";
        string? lote = linea.GetValueOrDefault("lote");
        return lote == null ? texto : $"{texto} · lote {lote}";
    }

    private static string Nombre(string campo) =>
        NombresCampos.GetValueOrDefault(campo) ?? campo.Replace('_', ' ');


    private static string FormatoTexto(string texto) =>
        texto.Length == 10 && DateTime.TryParseExact(texto, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha.ToString("dd/MM/yyyy")
            : texto;


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

            if (propiedad.Name == "importe_abono" && valor.ValueKind == JsonValueKind.Number)
            {
                valores[propiedad.Name] = valor.GetDecimal().ToString("C", new CultureInfo("es-ES"));
                continue;
            }
            if (propiedad.Name == "resultado_abono" && valor.ValueKind == JsonValueKind.String)
            {
                valores[propiedad.Name] = NombresResultadoAbono.GetValueOrDefault(valor.GetString()!) ?? valor.GetString();
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
