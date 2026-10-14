using Microsoft.Data.SqlClient;

namespace GPE.Escritorio;

/// <summary>Una fila de la lista de devoluciones (ya con el nombre del cliente y del estado).</summary>
public record DevolucionFila(
    int Id,
    string Numero,
    string Cliente,
    string? Albaran,
    string Motivo,
    DateTime FechaSolicitud,
    DateTime? FechaRecogida,
    DateTime? FechaLlegada,
    string Estado,
    int Fase);

/// <summary>Cliente para el desplegable. ToString lo que muestra el ComboBox.</summary>
public record Cliente(int Id, string Nombre)
{
    public override string ToString() => Nombre;
}

/// <summary>Producto para el desplegable.</summary>
public record Producto(int Id, string Codigo, string Nombre)
{
    public override string ToString() => Nombre;
}

/// <summary>Una línea de producto de una devolución nueva.</summary>
public record LineaNuevaDevolucion(int ProductoId, int Cantidad, string? Lote);

/// <summary>Todos los datos necesarios para crear una devolución.</summary>
public record DatosNuevaDevolucion(
    int FabricaId,
    int ClienteId,
    string Motivo,
    string? Albaran,
    string? Observaciones,
    DateTime FechaSolicitud,
    List<LineaNuevaDevolucion> Lineas);

/// <summary>Único sitio donde se habla con la base de datos de devoluciones.</summary>
public class DevolucionRepositorio
{
    // Windows Authentication contra la instancia local. Si usas una instancia con nombre,
    // cambia "localhost" por "localhost\\SQLEXPRESS" (o el nombre que tenga).
    private const string CadenaConexion =
        "Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;";

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

        using var conexion = new SqlConnection(CadenaConexion);
        using var comando = new SqlCommand(sql, conexion);
        conexion.Open();

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

    /// <summary>Todos los estados posibles, en el orden del proceso (para el filtro).</summary>
    public List<string> ObtenerEstados()
    {
        const string sql = "SELECT nombre FROM ESTADO_DEVOLUCION ORDER BY fase;";

        var estados = new List<string>();

        using var conexion = new SqlConnection(CadenaConexion);
        using var comando = new SqlCommand(sql, conexion);
        conexion.Open();

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            estados.Add(lector.GetString(0));

        return estados;
    }

    public List<Cliente> ObtenerClientes()
    {
        const string sql = "SELECT id, nombre FROM CLIENTE ORDER BY nombre;";

        var clientes = new List<Cliente>();

        using var conexion = new SqlConnection(CadenaConexion);
        using var comando = new SqlCommand(sql, conexion);
        conexion.Open();

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            clientes.Add(new Cliente(lector.GetInt32(0), lector.GetString(1)));

        return clientes;
    }

    public List<Producto> ObtenerProductos()
    {
        const string sql = "SELECT id, codigo, nombre FROM PRODUCTO ORDER BY nombre;";

        var productos = new List<Producto>();

        using var conexion = new SqlConnection(CadenaConexion);
        using var comando = new SqlCommand(sql, conexion);
        conexion.Open();

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            productos.Add(new Producto(lector.GetInt32(0), lector.GetString(1), lector.GetString(2)));

        return productos;
    }

    // ---------- Escritura ----------

    /// <summary>
    /// Guarda la devolución y sus líneas en una sola transacción: o se guarda todo o no se guarda nada.
    /// Devuelve el id de la devolución creada.
    /// </summary>
    public int InsertarDevolucion(DatosNuevaDevolucion datos)
    {
        // El estado inicial es el de la fase 1 ("Solicitud recibida"); el número DEV-AAAA-NNN se calcula solo.
        const string sqlCabecera = @"
            INSERT INTO DEVOLUCION
                (fabrica_id, cliente_id, estado_id, albaran, motivo, observaciones, fecha_solicitud)
            OUTPUT INSERTED.id
            VALUES
                (@fabrica, @cliente, (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = 1),
                 @albaran, @motivo, @observaciones, @fecha);";

        const string sqlLinea = @"
            INSERT INTO DEVOLUCION_PRODUCTO (devolucion_id, producto_id, lote, cantidad_esperada)
            VALUES (@devolucion, @producto, @lote, @cantidad);";

        using var conexion = new SqlConnection(CadenaConexion);
        conexion.Open();

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