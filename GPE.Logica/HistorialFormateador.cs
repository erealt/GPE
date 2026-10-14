using System.Text.Json;
using GPE.Datos;

namespace GPE.Logica;

public record EntradaHistorial(DateTime Fecha, string Usuario, string Accion, string Elemento, string Detalle);

// Convierte una fila de HISTORIAL_CAMBIOS (valores en JSON) en un texto legible.
public static class HistorialFormateador
{
    // Columnas que no aportan nada al leer el historial
    private static readonly HashSet<string> CamposOcultos =
        new() { "id", "numero", "devolucion_id", "creada_en" };

    private static readonly Dictionary<string, string> NombresCampos = new()
    {
        ["fabrica_id"] = "Fábrica",
        ["cliente_id"] = "Cliente",
        ["estado_id"] = "Estado",
        ["albaran"] = "Albarán",
        ["motivo"] = "Motivo",
        ["observaciones"] = "Observaciones",
        ["fecha_solicitud"] = "Fecha solicitud",
        ["fecha_recogida"] = "Fecha recogida",
        ["fecha_llegada"] = "Fecha llegada",
        ["producto_id"] = "Producto",
        ["lote"] = "Lote",
        ["cantidad_esperada"] = "Cantidad esperada",
        ["cantidad_recibida"] = "Cantidad recibida",
        ["transportista_id"] = "Transportista",
        ["direccion"] = "Dirección",
        ["telefono_contacto"] = "Teléfono contacto",
    };

    public static EntradaHistorial Formatear(CambioHistorial cambio)
    {
        string accion = cambio.Accion switch
        {
            "INSERT" => "Alta",
            "UPDATE" => "Modificación",
            "DELETE" => "Borrado",
            _ => cambio.Accion
        };

        string elemento = cambio.Tabla switch
        {
            "DEVOLUCION" => "Devolución",
            "DEVOLUCION_PRODUCTO" => $"Línea de producto #{cambio.RegistroId}",
            "RECOGIDA" => $"Recogida #{cambio.RegistroId}",
            _ => $"{cambio.Tabla} #{cambio.RegistroId}"
        };

        var anteriores = Leer(cambio.ValoresAnteriores);
        var nuevos = Leer(cambio.ValoresNuevos);

        string detalle = cambio.Accion switch
        {
            // En una modificación solo se muestran los campos que han cambiado
            "UPDATE" => string.Join("; ", nuevos
                .Where(c => !CamposOcultos.Contains(c.Key))
                .Where(c => anteriores.GetValueOrDefault(c.Key) != c.Value)
                .Select(c => $"{Nombre(c.Key)}: {anteriores.GetValueOrDefault(c.Key) ?? "—"} → {c.Value ?? "—"}")),

            "INSERT" => Resumen(nuevos),
            "DELETE" => Resumen(anteriores),
            _ => ""
        };

        return new EntradaHistorial(cambio.Fecha, cambio.Usuario, accion, elemento, detalle);
    }

    private static string Resumen(Dictionary<string, string?> valores) =>
        string.Join("; ", valores
            .Where(c => !CamposOcultos.Contains(c.Key) && c.Value != null)
            .Select(c => $"{Nombre(c.Key)}: {c.Value}"));

    private static string Nombre(string campo) =>
        NombresCampos.GetValueOrDefault(campo) ?? campo.Replace('_', ' ');

    private static Dictionary<string, string?> Leer(string? json)
    {
        var valores = new Dictionary<string, string?>();
        if (string.IsNullOrEmpty(json)) return valores;

        using var documento = JsonDocument.Parse(json);
        foreach (var propiedad in documento.RootElement.EnumerateObject())
        {
            valores[propiedad.Name] = propiedad.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => propiedad.Value.GetString(),
                _ => propiedad.Value.GetRawText()
            };
        }
        return valores;
    }
}
