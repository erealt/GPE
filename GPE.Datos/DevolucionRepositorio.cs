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
                   h.valores_anteriores, h.valores_nuevos, h.id, h.transaccion_id
            FROM HISTORIAL_CAMBIOS h
            JOIN USUARIO u ON u.id = h.usuario_id
            WHERE h.devolucion_id = @devolucion
            ORDER BY h.fecha, h.id;";

        var cambios = new List<CambioHistorial>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            cambios.Add(new CambioHistorial(
                Id: lector.GetInt32(7),
                TransaccionId: lector.IsDBNull(8) ? null : lector.GetInt64(8),
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

    public List<Transportista> ObtenerTransportistas()
    {
        const string sql = "SELECT id, nombre, telefono FROM TRANSPORTISTA WHERE activo = 1 ORDER BY nombre;";

        var transportistas = new List<Transportista>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            transportistas.Add(new Transportista(
                lector.GetInt32(0),
                lector.GetString(1),
                lector.IsDBNull(2) ? null : lector.GetString(2)));
        }

        return transportistas;
    }

    // Recogida de una devolución, o null si todavía no se ha programado
    public RecogidaGuardada? ObtenerRecogida(int devolucionId)
    {
        const string sql = @"
            SELECT TOP 1 id, transportista_id, fecha_recogida, direccion,
                   telefono_contacto, observaciones, creada_en
            FROM RECOGIDA
            WHERE devolucion_id = @devolucion
            ORDER BY creada_en DESC;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);

        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return null;

        return new RecogidaGuardada(
            Id: lector.GetInt32(0),
            TransportistaId: lector.GetInt32(1),
            FechaRecogida: lector.GetDateTime(2),
            Direccion: lector.GetString(3),
            TelefonoContacto: lector.IsDBNull(4) ? null : lector.GetString(4),
            Observaciones: lector.IsDBNull(5) ? null : lector.GetString(5),
            CreadaEn: lector.GetDateTime(6));
    }

    public Dictionary<string, Dictionary<int, string>> ObtenerNombresPorCampo()
    {
        const string sql = @"
            SELECT 'estado_id', id, nombre FROM ESTADO_DEVOLUCION
            UNION ALL SELECT 'cliente_id', id, nombre FROM CLIENTE
            UNION ALL SELECT 'fabrica_id', id, nombre FROM FABRICA
            UNION ALL SELECT 'producto_id', id, nombre FROM PRODUCTO
            UNION ALL SELECT 'transportista_id', id, nombre FROM TRANSPORTISTA
            -- No es un nombre: la fase de cada estado, para colorear el cambio de fase
            UNION ALL SELECT 'estado_fase', id, CAST(fase AS NVARCHAR(10)) FROM ESTADO_DEVOLUCION;";

        var nombres = new Dictionary<string, Dictionary<int, string>>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            string campo = lector.GetString(0);
            if (!nombres.TryGetValue(campo, out var porId))
                nombres[campo] = porId = new Dictionary<int, string>();
            porId[lector.GetInt32(1)] = lector.GetString(2);
        }

        return nombres;
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

    // Crea la recogida (o la actualiza si ya existe) y pasa la devolución a
    // "Recogida solicitada" con su fecha de recogida, todo en una transacción.
    public void GuardarRecogida(DatosRecogida datos)
    {
        const string sqlRecogida = @"
            UPDATE RECOGIDA
            SET transportista_id = @transportista, fecha_recogida = @fecha,
                direccion = @direccion, telefono_contacto = @telefono, observaciones = @observaciones
            WHERE devolucion_id = @devolucion;

            IF @@ROWCOUNT = 0
                INSERT INTO RECOGIDA
                    (devolucion_id, transportista_id, fecha_recogida, direccion, telefono_contacto, observaciones)
                VALUES
                    (@devolucion, @transportista, @fecha, @direccion, @telefono, @observaciones);";

        // Solo avanza el estado si sigue en la fase 1; si ya está más adelante no se toca
        const string sqlDevolucion = @"
            UPDATE d
            SET fecha_recogida = @fecha,
                estado_id = CASE WHEN e.fase = 1
                                 THEN (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = 2)
                                 ELSE d.estado_id END
            FROM DEVOLUCION d
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion;";

        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();

        using (var recogida = new SqlCommand(sqlRecogida, conexion, transaccion))
        {
            recogida.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            recogida.Parameters.AddWithValue("@transportista", datos.TransportistaId);
            recogida.Parameters.AddWithValue("@fecha", datos.FechaRecogida.Date);
            recogida.Parameters.AddWithValue("@direccion", datos.Direccion);
            recogida.Parameters.AddWithValue("@telefono", (object?)datos.TelefonoContacto ?? DBNull.Value);
            recogida.Parameters.AddWithValue("@observaciones", (object?)datos.Observaciones ?? DBNull.Value);
            recogida.ExecuteNonQuery();
        }

        using (var devolucion = new SqlCommand(sqlDevolucion, conexion, transaccion))
        {
            devolucion.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            devolucion.Parameters.AddWithValue("@fecha", datos.FechaRecogida.Date);
            devolucion.ExecuteNonQuery();
        }

        transaccion.Commit();
    }
}