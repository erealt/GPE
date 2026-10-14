using Microsoft.Data.SqlClient;

namespace GPE.Datos;

public class DevolucionRepositorio
{

    private const string CadenaConexion =
        "Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;";

    // Abre la conexión e indica a SQL Server qué usuario de la aplicación la usa;
    // los triggers de historial lo leen con SESSION_CONTEXT(N'usuario_id').
    private static SqlConnection AbrirConexion()
    {
        var conexion = new SqlConnection(CadenaConexion);
        conexion.Open();

        if (Sesion.UsuarioActual != null)
        {
            using var comando = new SqlCommand(
                "EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;", conexion);
            comando.Parameters.AddWithValue("@usuario", Sesion.UsuarioActual.Id);
            comando.ExecuteNonQuery();
        }

        return conexion;
    }

    // ---------- Lectura ----------

    public List<DevolucionFila> ObtenerDevoluciones()
    {
        const string sql = @"
            SELECT d.id, d.numero, c.nombre, d.albaran, d.motivo,
                   d.fecha_solicitud, d.fecha_recogida, d.fecha_llegada,
                   e.nombre, e.fase
            FROM DEVOLUCION d
            JOIN CLIENTE c ON c.id = d.cliente_id
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            ORDER BY d.fecha_solicitud DESC, d.id DESC;";

        var lista = new List<DevolucionFila>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            lista.Add(new DevolucionFila(
                Id: lector.GetInt32(0),
                Numero: lector.GetString(1),
                Cliente: lector.GetString(2),
                Albaran: lector.IsDBNull(3) ? null : lector.GetString(3),
                Motivo: lector.GetString(4),
                FechaSolicitud: lector.GetDateTime(5),
                FechaRecogida: lector.IsDBNull(6) ? null : (DateTime?)lector.GetDateTime(6),
                FechaLlegada: lector.IsDBNull(7) ? null : (DateTime?)lector.GetDateTime(7),
                Estado: lector.GetString(8),
                Fase: lector.GetInt32(9)));
        }

        return lista;
    }

    public List<string> ObtenerEstados()
    {
        const string sql = "SELECT nombre FROM ESTADO_DEVOLUCION ORDER BY fase;";

        var estados = new List<string>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            estados.Add(lector.GetString(0));

        return estados;
    }

    public List<Cliente> ObtenerClientes()
    {
        const string sql = "SELECT id, nombre FROM CLIENTE ORDER BY nombre;";

        var clientes = new List<Cliente>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            clientes.Add(new Cliente(lector.GetInt32(0), lector.GetString(1)));

        return clientes;
    }

    public List<Producto> ObtenerProductos()
    {
        const string sql = "SELECT id, codigo, nombre FROM PRODUCTO ORDER BY nombre;";

        var productos = new List<Producto>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            productos.Add(new Producto(lector.GetInt32(0), lector.GetString(1), lector.GetString(2)));

        return productos;
    }

    public List<Usuario> ObtenerUsuarios()
    {
        const string sql = "SELECT id, nombre, email FROM USUARIO ORDER BY nombre;";

        var usuarios = new List<Usuario>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            usuarios.Add(new Usuario(lector.GetInt32(0), lector.GetString(1), lector.GetString(2)));

        return usuarios;
    }

    public List<CambioHistorial> ObtenerHistorial(int devolucionId)
    {
        const string sql = @"
            SELECT h.fecha, u.nombre, h.tabla_afectada, h.registro_id, h.accion,
                   h.valores_anteriores, h.valores_nuevos
            FROM HISTORIAL_CAMBIOS h
            JOIN USUARIO u ON u.id = h.usuario_id
            WHERE h.devolucion_id = @devolucion
            ORDER BY h.fecha DESC, h.id DESC;";

        var cambios = new List<CambioHistorial>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            cambios.Add(new CambioHistorial(
                Fecha: lector.GetDateTime(0),
                Usuario: lector.GetString(1),
                Tabla: lector.GetString(2),
                RegistroId: lector.GetInt32(3),
                Accion: lector.GetString(4),
                ValoresAnteriores: lector.IsDBNull(5) ? null : lector.GetString(5),
                ValoresNuevos: lector.IsDBNull(6) ? null : lector.GetString(6)));
        }

        return cambios;
    }

    // ---------- Escritura ----------

    public int InsertarDevolucion(DatosNuevaDevolucion datos)
    {
        const string sqlCabecera = @"
            INSERT INTO DEVOLUCION
                (fabrica_id, cliente_id, estado_id, albaran, motivo, observaciones, fecha_solicitud)
            VALUES
                (@fabrica, @cliente, (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = 1),
                 @albaran, @motivo, @observaciones, @fecha);

            -- No se puede usar OUTPUT INSERTED.id porque DEVOLUCION tiene trigger.
            -- SCOPE_IDENTITY() ignora el id que genera el trigger en HISTORIAL_CAMBIOS.
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        const string sqlLinea = @"
            INSERT INTO DEVOLUCION_PRODUCTO (devolucion_id, producto_id, lote, cantidad_esperada)
            VALUES (@devolucion, @producto, @lote, @cantidad);";

        using var conexion = AbrirConexion();

        // Si ocurre una excepción antes del Commit, al salir del método la transacción
        // se deshace sola (Rollback automático al hacer Dispose).
        using var transaccion = conexion.BeginTransaction();

        int idDevolucion;
        using (var cabecera = new SqlCommand(sqlCabecera, conexion, transaccion))
        {
            cabecera.Parameters.AddWithValue("@fabrica", datos.FabricaId);
            cabecera.Parameters.AddWithValue("@cliente", datos.ClienteId);
            cabecera.Parameters.AddWithValue("@albaran", (object?)datos.Albaran ?? DBNull.Value);
            cabecera.Parameters.AddWithValue("@motivo", datos.Motivo);
            cabecera.Parameters.AddWithValue("@observaciones", (object?)datos.Observaciones ?? DBNull.Value);
            cabecera.Parameters.AddWithValue("@fecha", datos.FechaSolicitud.Date);

            idDevolucion = (int)cabecera.ExecuteScalar()!;
        }

        foreach (var linea in datos.Lineas)
        {
            using var comandoLinea = new SqlCommand(sqlLinea, conexion, transaccion);
            comandoLinea.Parameters.AddWithValue("@devolucion", idDevolucion);
            comandoLinea.Parameters.AddWithValue("@producto", linea.ProductoId);
            comandoLinea.Parameters.AddWithValue("@lote", (object?)linea.Lote ?? DBNull.Value);
            comandoLinea.Parameters.AddWithValue("@cantidad", linea.Cantidad);
            comandoLinea.ExecuteNonQuery();
        }

        transaccion.Commit();
        return idDevolucion;
    }
}