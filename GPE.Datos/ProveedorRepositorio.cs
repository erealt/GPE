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

    // Los productos de un proveedor (para volver a leerlos después de guardar uno)
    public List<ProductoProveedor> ObtenerProductos(int proveedorId) =>
        ObtenerProveedores().FirstOrDefault(p => p.Proveedor.Id == proveedorId)?.Productos ?? new();

    // Un producto nuevo; devuelve su id
    public int InsertarProducto(ProductoProveedor x, int usuarioId)
    {
        const string sql = @"
            INSERT INTO PROVEEDOR_PRODUCTO (proveedor_id, codigo, descripcion, tipo_producto, fecha_alta, baja, alergenos,
                posibles_alergenos, fecha_revision, ficha_tecnica, analisis_quimicos, analisis_micro, analisis_alergenos,
                eurochoc_fecha, eurochoc_alcance, eurochoc_resultado, impacto, control_recepcion, reclamaciones, parametro,
                p_historial, p_motivacion, p_facilidad, d_origen, d_cadena, d_medidas, medidas_preventivas, observaciones, creado_por)
            VALUES (@proveedor, @codigo, @descripcion, @tipo, @alta, @baja, @alergenos,
                @posibles, @revision, @ficha, @quimicos, @micro, @analisisAlergenos,
                @eurochocFecha, @eurochocAlcance, @eurochocResultado, @impacto, @control, @reclamaciones, @parametro,
                @pHistorial, @pMotivacion, @pFacilidad, @dOrigen, @dCadena, @dMedidas, @medidas, @observaciones, @usuario);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        ParametrosProducto(comando, x);
        comando.Parameters.AddWithValue("@proveedor", x.ProveedorId);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        return (int)comando.ExecuteScalar();
    }

    // false si otro lo ha cambiado o borrado desde que se leyó (no se guarda nada)
    public bool ActualizarProducto(ProductoProveedor x)
    {
        const string sql = @"
            UPDATE PROVEEDOR_PRODUCTO SET codigo = @codigo, descripcion = @descripcion, tipo_producto = @tipo,
                fecha_alta = @alta, baja = @baja, alergenos = @alergenos, posibles_alergenos = @posibles,
                fecha_revision = @revision, ficha_tecnica = @ficha, analisis_quimicos = @quimicos, analisis_micro = @micro,
                analisis_alergenos = @analisisAlergenos, eurochoc_fecha = @eurochocFecha, eurochoc_alcance = @eurochocAlcance,
                eurochoc_resultado = @eurochocResultado, impacto = @impacto, control_recepcion = @control,
                reclamaciones = @reclamaciones, parametro = @parametro, p_historial = @pHistorial, p_motivacion = @pMotivacion,
                p_facilidad = @pFacilidad, d_origen = @dOrigen, d_cadena = @dCadena, d_medidas = @dMedidas,
                medidas_preventivas = @medidas, observaciones = @observaciones
            WHERE id = @id AND version = @version;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        ParametrosProducto(comando, x);
        comando.Parameters.AddWithValue("@id", x.Id);
        comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)x.Version ?? DBNull.Value;
        return comando.ExecuteNonQuery() > 0;
    }

    private static void ParametrosProducto(SqlCommand comando, ProductoProveedor x)
    {
        void Valor(string nombre, object? valor) => comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        Valor("@codigo", x.Codigo);
        Valor("@descripcion", x.Descripcion);
        Valor("@tipo", x.TipoProducto);
        Valor("@alta", x.FechaAlta);
        Valor("@baja", x.Baja);
        Valor("@alergenos", x.Alergenos);
        Valor("@posibles", x.PosiblesAlergenos);
        Valor("@revision", x.FechaRevision);
        Valor("@ficha", x.FichaTecnica);
        Valor("@quimicos", x.AnalisisQuimicos);
        Valor("@micro", x.AnalisisMicro);
        Valor("@analisisAlergenos", x.AnalisisAlergenos);
        Valor("@eurochocFecha", x.EurochocFecha);
        Valor("@eurochocAlcance", x.EurochocAlcance);
        Valor("@eurochocResultado", x.EurochocResultado);
        Valor("@impacto", x.Impacto);
        Valor("@control", x.ControlRecepcion);
        Valor("@reclamaciones", x.Reclamaciones);
        Valor("@parametro", x.Parametro);
        Valor("@pHistorial", x.PHistorial);
        Valor("@pMotivacion", x.PMotivacion);
        Valor("@pFacilidad", x.PFacilidad);
        Valor("@dOrigen", x.DOrigen);
        Valor("@dCadena", x.DCadena);
        Valor("@dMedidas", x.DMedidas);
        Valor("@medidas", x.MedidasPreventivas);
        Valor("@observaciones", x.Observaciones);
    }

    // El Nº más alto más uno (1 si no hay ninguno)
    public int SiguienteNumero()
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand("SELECT ISNULL(MAX(numero), 0) + 1 FROM PROVEEDOR;", conexion);
        return (int)comando.ExecuteScalar();
    }

    // Si otro proveedor (no el de idPropio) ya tiene ese Nº
    public bool NumeroOcupado(int numero, int idPropio)
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand("SELECT COUNT(*) FROM PROVEEDOR WHERE numero = @numero AND id <> @id;", conexion);
        comando.Parameters.AddWithValue("@numero", numero);
        comando.Parameters.AddWithValue("@id", idPropio);
        return (int)comando.ExecuteScalar() > 0;
    }

    // Un proveedor nuevo; devuelve su id
    public int Insertar(Proveedor p, int usuarioId)
    {
        const string sql = @"
            INSERT INTO PROVEEDOR (numero, nombre, fecha_alta, baja, tipo, fecha_revision, impacto, estatus, reclamaciones,
                certificados, ensayo_trazabilidad, auditoria, rfa, relacion, historial, subcontratado, creado_por)
            VALUES (@numero, @nombre, @alta, @baja, @tipo, @revision, @impacto, @estatus, @reclamaciones,
                @certificados, @ensayo, @auditoria, @rfa, @relacion, @historial, @subcontratado, @usuario);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        Parametros(comando, p);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        return (int)comando.ExecuteScalar();
    }

    // Al guardar se quita el aviso de datos distintos del Excel: alguien ya lo ha revisado.
    // false si otro lo ha cambiado o borrado desde que se leyó (no se guarda nada).
    public bool Actualizar(Proveedor p)
    {
        const string sql = @"
            UPDATE PROVEEDOR SET numero = @numero, nombre = @nombre, fecha_alta = @alta, baja = @baja, tipo = @tipo,
                fecha_revision = @revision, impacto = @impacto, estatus = @estatus, reclamaciones = @reclamaciones,
                certificados = @certificados, ensayo_trazabilidad = @ensayo, auditoria = @auditoria, rfa = @rfa,
                relacion = @relacion, historial = @historial, subcontratado = @subcontratado, revisar = NULL
            WHERE id = @id AND version = @version;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        Parametros(comando, p);
        comando.Parameters.AddWithValue("@id", p.Id);
        comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)p.Version ?? DBNull.Value;
        return comando.ExecuteNonQuery() > 0;
    }

    private static void Parametros(SqlCommand comando, Proveedor p)
    {
        void Valor(string nombre, object? valor) => comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        Valor("@numero", p.Numero);
        Valor("@nombre", p.Nombre);
        Valor("@alta", p.FechaAlta);
        Valor("@baja", p.Baja);
        Valor("@tipo", p.Tipo);
        Valor("@revision", p.FechaRevision);
        Valor("@impacto", p.Impacto);
        Valor("@estatus", p.Estatus);
        Valor("@reclamaciones", p.Reclamaciones);
        Valor("@certificados", p.Certificados);
        Valor("@ensayo", p.EnsayoTrazabilidad);
        Valor("@auditoria", p.Auditoria);
        Valor("@rfa", p.Rfa);
        Valor("@relacion", p.Relacion);
        Valor("@historial", p.Historial);
        Valor("@subcontratado", p.Subcontratado);
    }
}
