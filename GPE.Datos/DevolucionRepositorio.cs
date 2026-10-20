using Microsoft.Data.SqlClient;

namespace GPE.Datos;

public class DevolucionRepositorio
{

    // La variable de entorno GPE_CONEXION permite usar otra base de datos (p. ej. una de pruebas)
    private static readonly string CadenaConexion =
        Environment.GetEnvironmentVariable("GPE_CONEXION")
        ?? "Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;";

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

    private const string SqlDevoluciones = @"
            SELECT d.id, d.numero, c.nombre, d.albaran, d.motivo,
                   d.fecha_solicitud, d.fecha_recogida, d.fecha_llegada,
                   e.nombre, e.fase
            FROM DEVOLUCION d
            JOIN CLIENTE c ON c.id = d.cliente_id
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id";

    public List<DevolucionFila> ObtenerDevoluciones() =>
        LeerDevoluciones(SqlDevoluciones + " ORDER BY d.fecha_solicitud DESC, d.id DESC;", null);

    // Una devolución con sus datos actuales, o null si ya no existe
    public DevolucionFila? ObtenerDevolucion(int devolucionId) =>
        LeerDevoluciones(SqlDevoluciones + " WHERE d.id = @devolucion;", devolucionId).FirstOrDefault();

    private List<DevolucionFila> LeerDevoluciones(string sql, int? devolucionId)
    {
        var lista = new List<DevolucionFila>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        if (devolucionId != null)
            comando.Parameters.AddWithValue("@devolucion", devolucionId);

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
                (@fabrica, @cliente, (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @fase),
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
            cabecera.Parameters.AddWithValue("@fase", Fase.SolicitudRecibida);
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

    // ---------- Almacén ----------

    // Pasa la devolución de "Recogida solicitada" (fase 2) a "Mercancía recibida" (fase 3).
    // Devuelve false si ya no estaba en la fase 2 (otro usuario la cambió antes).
    public bool MarcarRecibida(int devolucionId, DateTime fechaLlegada)
    {
        const string sql = @"
            UPDATE d
            SET estado_id = (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @faseNueva),
                fecha_llegada = @fecha
            FROM DEVOLUCION d
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion AND e.fase = @faseActual;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);
        comando.Parameters.AddWithValue("@fecha", fechaLlegada.Date);
        comando.Parameters.AddWithValue("@faseActual", Fase.RecogidaSolicitada);
        comando.Parameters.AddWithValue("@faseNueva", Fase.MercanciaRecibida);
        return comando.ExecuteNonQuery() == 1;
    }

    public List<LineaRevision> ObtenerLineasRevision(int devolucionId)
    {
        const string sql = @"
            SELECT dp.id, p.nombre, dp.lote, dp.cantidad_esperada,
                   dp.cantidad_recibida, dp.cantidad_apta, dp.observaciones
            FROM DEVOLUCION_PRODUCTO dp
            JOIN PRODUCTO p ON p.id = dp.producto_id
            WHERE dp.devolucion_id = @devolucion
            ORDER BY dp.id;";

        var lineas = new List<LineaRevision>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            lineas.Add(new LineaRevision(
                Id: lector.GetInt32(0),
                Producto: lector.GetString(1),
                Lote: lector.IsDBNull(2) ? null : lector.GetString(2),
                Solicitada: lector.GetInt32(3),
                Recibida: lector.IsDBNull(4) ? null : lector.GetInt32(4),
                Apta: lector.IsDBNull(5) ? null : lector.GetInt32(5),
                Observaciones: lector.IsDBNull(6) ? null : lector.GetString(6)));
        }

        return lineas;
    }

    public string? ObtenerObservacionesSolicitud(int devolucionId)
    {
        const string sql = "SELECT observaciones FROM DEVOLUCION WHERE id = @devolucion;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);
        return comando.ExecuteScalar() as string;
    }

    // Vuelve de la fase actual a la anterior guardando el motivo (sale en el historial).
    // Al volver de la 3 a la 2 se borra la fecha de llegada; lo rellenado en la
    // revisión se conserva. Desde el cierre (6) se vuelve a la 4, porque abono y cierre
    // se registran juntos: los datos del abono se conservan para corregirlos.
    // Devuelve false si la devolución ya no estaba en esa fase.
    public bool Reabrir(int devolucionId, int faseActual, string motivo)
    {
        const string sql = @"
            UPDATE d
            SET estado_id = (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @faseNueva),
                motivo_reapertura = @motivo,
                fecha_llegada = CASE WHEN @borrarLlegada = 1 THEN NULL ELSE d.fecha_llegada END,
                fecha_cierre = CASE WHEN @borrarCierre = 1 THEN NULL ELSE d.fecha_cierre END
            FROM DEVOLUCION d
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion AND e.fase = @fase;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);
        comando.Parameters.AddWithValue("@fase", faseActual);
        comando.Parameters.AddWithValue("@faseNueva",
            faseActual == Fase.Cierre ? Fase.RevisadaAlmacen : faseActual - 1);
        comando.Parameters.AddWithValue("@borrarLlegada", faseActual == Fase.MercanciaRecibida);
        comando.Parameters.AddWithValue("@borrarCierre", faseActual == Fase.Cierre);
        comando.Parameters.AddWithValue("@motivo", motivo);
        return comando.ExecuteNonQuery() == 1;
    }

    public string? ObtenerObservacionesRevision(int devolucionId)
    {
        const string sql = "SELECT observaciones_revision FROM DEVOLUCION WHERE id = @devolucion;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);
        return comando.ExecuteScalar() as string;
    }

    // Guarda las cantidades de cada línea y pasa la devolución de la fase 3 a la 4,
    // todo en una transacción. Devuelve false si ya no estaba en la fase 3.
    public bool FinalizarRevision(int devolucionId, List<LineaRevisada> lineas, string? observaciones)
    {
        const string sqlDevolucion = @"
            UPDATE d
            SET estado_id = (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @faseNueva),
                observaciones_revision = @observaciones
            FROM DEVOLUCION d
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion AND e.fase = @faseActual;";

        const string sqlLinea = @"
            UPDATE DEVOLUCION_PRODUCTO
            SET cantidad_recibida = @recibida, cantidad_apta = @apta, observaciones = @observaciones
            WHERE id = @linea AND devolucion_id = @devolucion;";

        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();

        using (var devolucion = new SqlCommand(sqlDevolucion, conexion, transaccion))
        {
            devolucion.Parameters.AddWithValue("@devolucion", devolucionId);
            devolucion.Parameters.AddWithValue("@observaciones", (object?)observaciones ?? DBNull.Value);
            devolucion.Parameters.AddWithValue("@faseActual", Fase.MercanciaRecibida);
            devolucion.Parameters.AddWithValue("@faseNueva", Fase.RevisadaAlmacen);
            if (devolucion.ExecuteNonQuery() != 1)
                return false;   // al salir sin Commit, la transacción se deshace
        }

        foreach (var linea in lineas)
        {
            using var comando = new SqlCommand(sqlLinea, conexion, transaccion);
            comando.Parameters.AddWithValue("@linea", linea.Id);
            comando.Parameters.AddWithValue("@devolucion", devolucionId);
            comando.Parameters.AddWithValue("@recibida", linea.Recibida);
            comando.Parameters.AddWithValue("@apta", linea.Apta);
            comando.Parameters.AddWithValue("@observaciones", (object?)linea.Observaciones ?? DBNull.Value);
            comando.ExecuteNonQuery();
        }

        transaccion.Commit();
        return true;
    }

    // ---------- Abono y cierre ----------

    // Null si todavía no se ha registrado ningún abono
    public AbonoGuardado? ObtenerAbono(int devolucionId)
    {
        const string sql = @"
            SELECT resultado_abono, numero_abono, fecha_abono, importe_abono,
                   observaciones_abono, fecha_cierre
            FROM DEVOLUCION
            WHERE id = @devolucion AND resultado_abono IS NOT NULL;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);

        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return null;

        return new AbonoGuardado(
            Resultado: lector.GetString(0),
            Numero: lector.IsDBNull(1) ? null : lector.GetString(1),
            Fecha: lector.GetDateTime(2),
            Importe: lector.IsDBNull(3) ? null : lector.GetDecimal(3),
            Observaciones: lector.IsDBNull(4) ? null : lector.GetString(4),
            FechaCierre: lector.IsDBNull(5) ? null : lector.GetDateTime(5));
    }

    // Número (DEV-...) de otra devolución que ya usa ese nº de abono, o null si no lo usa ninguna
    public string? DevolucionConNumeroAbono(string numeroAbono, int devolucionExcluida)
    {
        const string sql = @"
            SELECT TOP 1 numero FROM DEVOLUCION
            WHERE numero_abono = @abono AND id <> @devolucion
            ORDER BY id;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@abono", numeroAbono);
        comando.Parameters.AddWithValue("@devolucion", devolucionExcluida);
        return comando.ExecuteScalar() as string;
    }

    // Guarda el abono y cierra la devolución (fase 4 → 6) en un solo UPDATE.
    // Devuelve false si ya no estaba en la fase 4.
    public bool RegistrarAbono(DatosAbono datos, DateTime fechaCierre)
    {
        const string sql = @"
            UPDATE d
            SET estado_id = (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @faseNueva),
                resultado_abono = @resultado,
                numero_abono = @numero,
                fecha_abono = @fecha,
                importe_abono = @importe,
                observaciones_abono = @observaciones,
                fecha_cierre = @cierre
            FROM DEVOLUCION d
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion AND e.fase = @faseActual;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
        comando.Parameters.AddWithValue("@faseActual", Fase.RevisadaAlmacen);
        comando.Parameters.AddWithValue("@faseNueva", Fase.Cierre);
        comando.Parameters.AddWithValue("@resultado", datos.Resultado);
        comando.Parameters.AddWithValue("@numero", (object?)datos.Numero ?? DBNull.Value);
        comando.Parameters.AddWithValue("@fecha", datos.Fecha.Date);
        comando.Parameters.Add("@importe", System.Data.SqlDbType.Decimal).Value = (object?)datos.Importe ?? DBNull.Value;
        comando.Parameters["@importe"].Precision = 10;
        comando.Parameters["@importe"].Scale = 2;
        comando.Parameters.AddWithValue("@observaciones", (object?)datos.Observaciones ?? DBNull.Value);
        comando.Parameters.AddWithValue("@cierre", fechaCierre.Date);
        return comando.ExecuteNonQuery() == 1;
    }

    // ---------- Adjuntos (fotos y documentos) ----------

    public List<AdjuntoInfo> ObtenerAdjuntos(int devolucionId, string tipo)
    {
        // No se lee la columna contenido: solo hace falta al abrir un archivo
        const string sql = @"
            SELECT a.id, a.nombre_archivo, a.tamano_bytes, a.subido_en, u.nombre
            FROM ADJUNTO a
            JOIN USUARIO u ON u.id = a.usuario_id
            WHERE a.devolucion_id = @devolucion AND a.tipo = @tipo
            ORDER BY a.subido_en, a.id;";

        var adjuntos = new List<AdjuntoInfo>();

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);
        comando.Parameters.AddWithValue("@tipo", tipo);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            adjuntos.Add(new AdjuntoInfo(
                lector.GetInt32(0), lector.GetString(1), lector.GetInt32(2),
                lector.GetDateTime(3), lector.GetString(4)));
        }

        return adjuntos;
    }

    public byte[] ObtenerContenidoAdjunto(int adjuntoId)
    {
        const string sql = "SELECT contenido FROM ADJUNTO WHERE id = @adjunto;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@adjunto", adjuntoId);
        return (byte[])comando.ExecuteScalar()!;
    }

    // Todos los archivos elegidos a la vez se guardan juntos (una sola entrada en el historial)
    public void InsertarAdjuntos(int devolucionId, string tipo, List<ArchivoNuevo> archivos)
    {
        const string sql = @"
            INSERT INTO ADJUNTO (devolucion_id, tipo, nombre_archivo, tamano_bytes, contenido, usuario_id)
            VALUES (@devolucion, @tipo, @nombre, @tamano, @contenido, @usuario);";

        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();

        foreach (var archivo in archivos)
        {
            using var comando = new SqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("@devolucion", devolucionId);
            comando.Parameters.AddWithValue("@tipo", tipo);
            comando.Parameters.AddWithValue("@nombre", archivo.NombreArchivo);
            comando.Parameters.AddWithValue("@tamano", archivo.Contenido.Length);
            comando.Parameters.Add("@contenido", System.Data.SqlDbType.VarBinary, -1).Value = archivo.Contenido;
            comando.Parameters.AddWithValue("@usuario", Sesion.UsuarioActual!.Id);
            comando.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    public void EliminarAdjunto(int adjuntoId)
    {
        const string sql = "DELETE FROM ADJUNTO WHERE id = @adjunto;";

        using var conexion = AbrirConexion();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@adjunto", adjuntoId);
        comando.ExecuteNonQuery();
    }

    // Crea la recogida (o la actualiza si ya existe) y pasa la devolución a
    // "Recogida solicitada" con su fecha de recogida, todo en una transacción.
    // Devuelve false si la mercancía ya ha llegado: entonces la recogida no se puede cambiar
    public bool GuardarRecogida(DatosRecogida datos)
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

        // Solo avanza el estado si sigue en "Solicitud recibida"; si ya está más adelante no se toca
        const string sqlDevolucion = @"
            UPDATE d
            SET fecha_recogida = @fecha,
                estado_id = CASE WHEN e.fase = @faseActual
                                 THEN (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @faseNueva)
                                 ELSE d.estado_id END
            FROM DEVOLUCION d
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion;";

        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();

        // UPDLOCK: bloquea la devolución hasta el final para que nadie cambie la fase entretanto
        using (var fase = new SqlCommand(@"
            SELECT e.fase FROM DEVOLUCION d WITH (UPDLOCK)
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion;", conexion, transaccion))
        {
            fase.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            if (fase.ExecuteScalar() is not int faseActual || faseActual > Fase.RecogidaSolicitada)
                return false;
        }

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
            devolucion.Parameters.AddWithValue("@faseActual", Fase.SolicitudRecibida);
            devolucion.Parameters.AddWithValue("@faseNueva", Fase.RecogidaSolicitada);
            devolucion.ExecuteNonQuery();
        }

        transaccion.Commit();
        return true;
    }
}