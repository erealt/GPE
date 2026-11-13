using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Evaluación de proveedores y fraude (PROVEEDOR y PROVEEDOR_PRODUCTO, Excel CP-RG05).
public class ProveedorRepositorio
{
    // Todos los proveedores con sus productos, ordenados por Nº
    public List<ProveedorConProductos> ObtenerProveedores()
    {
        const string sqlProveedores = @"
            SELECT id, numero, nombre, fecha_alta, baja, tipo, fecha_revision, impacto, estatus, reclamaciones,
                   certificados, ensayo_trazabilidad, auditoria, rfa, relacion, historial, subcontratado, revisar, version
            FROM PROVEEDOR
            ORDER BY CASE WHEN numero IS NULL THEN 1 ELSE 0 END, numero, nombre;";

        const string sqlProductos = @"
            SELECT id, proveedor_id, codigo, descripcion, tipo_producto, fecha_alta, baja, alergenos, posibles_alergenos,
                   fecha_revision, ficha_tecnica, analisis_quimicos, analisis_micro, analisis_alergenos,
                   eurochoc_fecha, eurochoc_alcance, eurochoc_resultado, impacto, control_recepcion, reclamaciones, parametro,
                   p_historial, p_motivacion, p_facilidad, d_origen, d_cadena, d_medidas,
                   medidas_preventivas, observaciones, version
            FROM PROVEEDOR_PRODUCTO
            ORDER BY proveedor_id, codigo, id;";

        var proveedores = new List<Proveedor>();
        var productos = new Dictionary<int, List<ProductoProveedor>>();

        using var conexion = Conexion.Abrir();

        using (var comando = new SqlCommand(sqlProveedores, conexion))
        using (var lector = comando.ExecuteReader())
        {
            string? Texto(int i) => lector.IsDBNull(i) ? null : lector.GetString(i);
            DateTime? Fecha(int i) => lector.IsDBNull(i) ? null : lector.GetDateTime(i);
            byte? Nota(int i) => lector.IsDBNull(i) ? null : lector.GetByte(i);

            while (lector.Read())
                proveedores.Add(new Proveedor(
                    Id: lector.GetInt32(0),
                    Numero: lector.IsDBNull(1) ? null : lector.GetInt32(1),
                    Nombre: lector.GetString(2),
                    FechaAlta: Fecha(3),
                    Baja: lector.GetBoolean(4),
                    Tipo: Texto(5),
                    FechaRevision: Fecha(6),
                    Impacto: Nota(7),
                    Estatus: Nota(8),
                    Reclamaciones: Nota(9),
                    Certificados: Texto(10),
                    EnsayoTrazabilidad: Texto(11),
                    Auditoria: Texto(12),
                    Rfa: Texto(13),
                    Relacion: Nota(14),
                    Historial: Nota(15),
                    Subcontratado: Nota(16),
                    Revisar: Texto(17),
                    Version: (byte[])lector[18]));
        }

        using (var comando = new SqlCommand(sqlProductos, conexion))
        using (var lector = comando.ExecuteReader())
        {
            string? Texto(int i) => lector.IsDBNull(i) ? null : lector.GetString(i);
            DateTime? Fecha(int i) => lector.IsDBNull(i) ? null : lector.GetDateTime(i);
            byte? Nota(int i) => lector.IsDBNull(i) ? null : lector.GetByte(i);

            while (lector.Read())
            {
                var producto = new ProductoProveedor(
                    Id: lector.GetInt32(0),
                    ProveedorId: lector.GetInt32(1),
                    Codigo: Texto(2),
                    Descripcion: Texto(3),
                    TipoProducto: Texto(4),
                    FechaAlta: Fecha(5),
                    Baja: lector.GetBoolean(6),
                    Alergenos: Texto(7),
                    PosiblesAlergenos: Texto(8),
                    FechaRevision: Fecha(9),
                    FichaTecnica: Texto(10),
                    AnalisisQuimicos: Texto(11),
                    AnalisisMicro: Texto(12),
                    AnalisisAlergenos: Texto(13),
                    EurochocFecha: Fecha(14),
                    EurochocAlcance: Texto(15),
                    EurochocResultado: Texto(16),
                    Impacto: Nota(17),
                    ControlRecepcion: Nota(18),
                    Reclamaciones: Nota(19),
                    Parametro: Texto(20),
                    PHistorial: Nota(21),
                    PMotivacion: Nota(22),
                    PFacilidad: Nota(23),
                    DOrigen: Nota(24),
                    DCadena: Nota(25),
                    DMedidas: Nota(26),
                    MedidasPreventivas: Texto(27),
                    Observaciones: Texto(28),
                    Version: (byte[])lector[29]);

                if (!productos.TryGetValue(producto.ProveedorId, out var lista))
                    productos[producto.ProveedorId] = lista = new List<ProductoProveedor>();
                lista.Add(producto);
            }
        }

        return proveedores
            .Select(p => new ProveedorConProductos(p, productos.GetValueOrDefault(p.Id) ?? new()))
            .ToList();
    }
}
