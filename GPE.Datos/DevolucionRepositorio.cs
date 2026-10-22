using Microsoft.Data.SqlClient;

namespace GPE.Datos;

public class DevolucionRepositorio
{

    // Lectura

    private const string SqlDevoluciones = @"
            SELECT d.id, d.numero, c.nombre, c.codigo, d.motivo,
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

        using var conexion = Conexion.Abrir();
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
                CodigoCliente: lector.GetString(3),
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

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            estados.Add(lector.GetString(0));

        return estados;
    }

    public List<Cliente> ObtenerClientes()
    {
        const string sql = "SELECT id, codigo, nombre FROM CLIENTE ORDER BY nombre;";

        var clientes = new List<Cliente>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            clientes.Add(new Cliente(lector.GetInt32(0), lector.GetString(1), lector.GetString(2)));

        return clientes;
    }

    public List<Producto> ObtenerProductos()
    {
        const string sql = "SELECT id, codigo, nombre, formato FROM PRODUCTO ORDER BY nombre;";

        var productos = new List<Producto>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            productos.Add(new Producto(lector.GetInt32(0), lector.GetString(1), lector.GetString(2),
                lector.IsDBNull(3) ? null : lector.GetString(3)));

        return productos;
    }

    public List<Usuario> ObtenerUsuarios()
    {
        const string sql = "SELECT id, nombre FROM USUARIO ORDER BY nombre;";

        var usuarios = new List<Usuario>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            usuarios.Add(new Usuario(lector.GetInt32(0), lector.GetString(1)));

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

        using var conexion = Conexion.Abrir();
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

        using var conexion = Conexion.Abrir();
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

        using var conexion = Conexion.Abrir();
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

        using var conexion = Conexion.Abrir();
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

    // Escritura

    public int InsertarDevolucion(DatosNuevaDevolucion datos)
    {
        const string sqlCabecera = @"
            INSERT INTO DEVOLUCION
                (fabrica_id, cliente_id, estado_id, motivo, observaciones, fecha_solicitud)
            VALUES
                (@fabrica, @cliente, (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @fase),
                 @motivo, @observaciones, @fecha);

            -- No se puede usar OUTPUT INSERTED.id porque DEVOLUCION tiene trigger.
            -- SCOPE_IDENTITY() ignora el id que genera el trigger en HISTORIAL_CAMBIOS.
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        const string sqlLinea = @"
            INSERT INTO DEVOLUCION_PRODUCTO (devolucion_id, producto_id, lote, cantidad_esperada)
            VALUES (@devolucion, @producto, @lote, @cantidad);";

        using var conexion = Conexion.Abrir();

        // Si ocurre una excepción antes del Commit, al salir del método la transacción
        // se deshace sola (Rollback automático al hacer Dispose).
        using var transaccion = conexion.BeginTransaction();

        int idDevolucion;
        using (var cabecera = new SqlCommand(sqlCabecera, conexion, transaccion))
        {
            cabecera.Parameters.AddWithValue("@fabrica", datos.FabricaId);
            cabecera.Parameters.AddWithValue("@cliente", datos.ClienteId);
            cabecera.Parameters.AddWithValue("@fase", Fase.SolicitudRecibida);
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

        // Aviso por correo a quien gestiona la recogida (el siguiente paso)
        AvisoRepositorio.Apuntar(conexion, transaccion, idDevolucion, datos.FabricaId,
            TipoAviso.NuevaDevolucion, Fase.RecogidaSolicitada);

        transaccion.Commit();
        return idDevolucion;
    }

    // Almacén

    // Pasa la devolución de "Recogida solicitada" (fase 2) a "Mercancía recibida" (fase 3)
    // y guarda el recibo de la agencia, todo en una transacción.
    // Devuelve false si ya no estaba en la fase 2 (otro usuario la cambió antes).
    public bool MarcarRecibida(int devolucionId, DateTime fechaLlegada, List<ArchivoNuevo> recibo)
    {
        const string sql = @"
            UPDATE d
            SET estado_id = (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @faseNueva),
                fecha_llegada = @fecha
            FROM DEVOLUCION d
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion AND e.fase = @faseActual;";

        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        using (var comando = new SqlCommand(sql, conexion, transaccion))
        {
            comando.Parameters.AddWithValue("@devolucion", devolucionId);
            comando.Parameters.AddWithValue("@fecha", fechaLlegada.Date);
            comando.Parameters.AddWithValue("@faseActual", Fase.RecogidaSolicitada);
            comando.Parameters.AddWithValue("@faseNueva", Fase.MercanciaRecibida);
            if (comando.ExecuteNonQuery() != 1)
                return false;   // al salir sin Commit, la transacción se deshace
        }

        InsertarAdjuntos(conexion, transaccion, devolucionId, TipoAdjunto.Recibo, recibo);

        transaccion.Commit();
        return true;
    }

    public List<LineaSolicitada> ObtenerProductosSolicitados(int devolucionId)
    {
        const string sql = @"
            SELECT dp.producto_id, p.codigo, p.nombre, p.formato, dp.lote, dp.cantidad_esperada
            FROM DEVOLUCION_PRODUCTO dp
            JOIN PRODUCTO p ON p.id = dp.producto_id
            WHERE dp.devolucion_id = @devolucion
            ORDER BY dp.id;";

        var lineas = new List<LineaSolicitada>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            lineas.Add(new LineaSolicitada(
                ProductoId: lector.GetInt32(0),
                Codigo: lector.GetString(1),
                Producto: lector.GetString(2),
                Formato: lector.IsDBNull(3) ? null : lector.GetString(3),
                Lote: lector.IsDBNull(4) ? null : lector.GetString(4),
                Cantidad: lector.GetInt32(5)));
        }

        return lineas;
    }

    public List<LineaRevision> ObtenerLineasRevision(int devolucionId)
    {
        const string sql = @"
            SELECT rl.producto_id, p.codigo, p.nombre, p.formato, rl.lote, rl.caducidad, rl.cantidad, rl.apto
            FROM REVISION_LINEA rl
            JOIN PRODUCTO p ON p.id = rl.producto_id
            WHERE rl.devolucion_id = @devolucion
            ORDER BY rl.id;";

        var lineas = new List<LineaRevision>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            lineas.Add(new LineaRevision(
                ProductoId: lector.GetInt32(0),
                Codigo: lector.GetString(1),
                Producto: lector.GetString(2),
                Formato: lector.IsDBNull(3) ? null : lector.GetString(3),
                Lote: lector.IsDBNull(4) ? null : lector.GetString(4),
                Caducidad: lector.IsDBNull(5) ? null : lector.GetDateTime(5),
                Cantidad: lector.GetInt32(6),
                Apto: lector.GetBoolean(7)));
        }

        return lineas;
    }

    public string? ObtenerObservacionesSolicitud(int devolucionId)
    {
        const string sql = "SELECT observaciones FROM DEVOLUCION WHERE id = @devolucion;";

        using var conexion = Conexion.Abrir();
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

        using var conexion = Conexion.Abrir();
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

    public CabeceraRevision ObtenerCabeceraRevision(int devolucionId)
    {
        const string sql = @"
            SELECT d.revisada_por, d.agencia, d.observaciones_revision, t.nombre
            FROM DEVOLUCION d
            LEFT JOIN RECOGIDA r ON r.devolucion_id = d.id
            LEFT JOIN TRANSPORTISTA t ON t.id = r.transportista_id
            WHERE d.id = @devolucion;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);

        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return new CabeceraRevision(null, null, null, null);

        return new CabeceraRevision(
            RevisadaPor: lector.IsDBNull(0) ? null : lector.GetString(0),
            Agencia: lector.IsDBNull(1) ? null : lector.GetString(1),
            Observaciones: lector.IsDBNull(2) ? null : lector.GetString(2),
            Transportista: lector.IsDBNull(3) ? null : lector.GetString(3));
    }

    // Guarda la revisión (cabecera y líneas) y pasa la devolución de la fase 3 a la 4,
    // todo en una transacción. Las líneas se sustituyen enteras por las nuevas.
    // Devuelve false si ya no estaba en la fase 3.
    public bool FinalizarRevision(DatosRevision datos)
    {
        const string sqlDevolucion = @"
            UPDATE d
            SET estado_id = (SELECT id FROM ESTADO_DEVOLUCION WHERE fase = @faseNueva),
                revisada_por = @revisadaPor,
                agencia = @agencia,
                observaciones_revision = @observaciones
            FROM DEVOLUCION d
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion AND e.fase = @faseActual;";

        const string sqlBorrar = "DELETE FROM REVISION_LINEA WHERE devolucion_id = @devolucion;";

        const string sqlLinea = @"
            INSERT INTO REVISION_LINEA (devolucion_id, producto_id, lote, caducidad, cantidad, apto)
            VALUES (@devolucion, @producto, @lote, @caducidad, @cantidad, @apto);";

        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        using (var devolucion = new SqlCommand(sqlDevolucion, conexion, transaccion))
        {
            devolucion.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            devolucion.Parameters.AddWithValue("@revisadaPor", datos.RevisadaPor);
            devolucion.Parameters.AddWithValue("@agencia", (object?)datos.Agencia ?? DBNull.Value);
            devolucion.Parameters.AddWithValue("@observaciones", (object?)datos.Observaciones ?? DBNull.Value);
            devolucion.Parameters.AddWithValue("@faseActual", Fase.MercanciaRecibida);
            devolucion.Parameters.AddWithValue("@faseNueva", Fase.RevisadaAlmacen);
            if (devolucion.ExecuteNonQuery() != 1)
                return false;   // al salir sin Commit, la transacción se deshace
        }

        using (var borrar = new SqlCommand(sqlBorrar, conexion, transaccion))
        {
            borrar.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            borrar.ExecuteNonQuery();
        }

        foreach (var linea in datos.Lineas)
        {
            using var comando = new SqlCommand(sqlLinea, conexion, transaccion);
            comando.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            comando.Parameters.AddWithValue("@producto", linea.ProductoId);
            comando.Parameters.AddWithValue("@lote", (object?)linea.Lote ?? DBNull.Value);
            comando.Parameters.AddWithValue("@caducidad", (object?)linea.Caducidad?.Date ?? DBNull.Value);
            comando.Parameters.AddWithValue("@cantidad", linea.Cantidad);
            comando.Parameters.AddWithValue("@apto", linea.Apto);
            comando.ExecuteNonQuery();
        }

        // Aviso a quien registra el abono (el siguiente paso)
        using (var fabrica = new SqlCommand("SELECT fabrica_id FROM DEVOLUCION WHERE id = @devolucion;", conexion, transaccion))
        {
            fabrica.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            AvisoRepositorio.Apuntar(conexion, transaccion, datos.DevolucionId, (int)fabrica.ExecuteScalar()!,
                TipoAviso.RevisionFinalizada, Fase.AbonoRealizado);
        }

        transaccion.Commit();
        return true;
    }

    // Abono y cierre

    // Null si todavía no se ha registrado ningún abono
    public AbonoGuardado? ObtenerAbono(int devolucionId)
    {
        const string sql = @"
            SELECT resultado_abono, numero_abono, fecha_abono, importe_abono,
                   observaciones_abono, fecha_cierre
            FROM DEVOLUCION
            WHERE id = @devolucion AND resultado_abono IS NOT NULL;";

        using var conexion = Conexion.Abrir();
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

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@abono", numeroAbono);
        comando.Parameters.AddWithValue("@devolucion", devolucionExcluida);
        return comando.ExecuteScalar() as string;
    }

    // Guarda el abono y cierra la devolución (fase 4 → 6) en un solo UPDATE, y avisa
    // al almacén, todo en una transacción. Devuelve false si ya no estaba en la fase 4.
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

        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        using (var comando = new SqlCommand(sql, conexion, transaccion))
        {
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
            if (comando.ExecuteNonQuery() != 1)
                return false;   // al salir sin Commit, la transacción se deshace
        }

        // Aviso al almacén (el responsable de la revisión) de cómo ha quedado el abono
        using (var fabrica = new SqlCommand("SELECT fabrica_id FROM DEVOLUCION WHERE id = @devolucion;", conexion, transaccion))
        {
            fabrica.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            AvisoRepositorio.Apuntar(conexion, transaccion, datos.DevolucionId, (int)fabrica.ExecuteScalar()!,
                TipoAviso.AbonoRegistrado, Fase.RevisadaAlmacen);
        }

        transaccion.Commit();
        return true;
    }

    // Adjuntos (fotos y documentos)

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

        using var conexion = Conexion.Abrir();
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

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@adjunto", adjuntoId);
        return (byte[])comando.ExecuteScalar()!;
    }

    // Todos los archivos elegidos a la vez se guardan juntos (una sola entrada en el historial)
    public void InsertarAdjuntos(int devolucionId, string tipo, List<ArchivoNuevo> archivos)
    {
        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        InsertarAdjuntos(conexion, transaccion, devolucionId, tipo, archivos);

        transaccion.Commit();
    }

    private static void InsertarAdjuntos(SqlConnection conexion, SqlTransaction transaccion,
        int devolucionId, string tipo, List<ArchivoNuevo> archivos)
    {
        const string sql = @"
            INSERT INTO ADJUNTO (devolucion_id, tipo, nombre_archivo, tamano_bytes, contenido, usuario_id)
            VALUES (@devolucion, @tipo, @nombre, @tamano, @contenido, @usuario);";

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
    }

    public void EliminarAdjunto(int adjuntoId)
    {
        const string sql = "DELETE FROM ADJUNTO WHERE id = @adjunto;";

        using var conexion = Conexion.Abrir();
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

        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        // UPDLOCK: bloquea la devolución hasta el final para que nadie cambie la fase entretanto
        int faseActual, fabricaId;
        using (var fase = new SqlCommand(@"
            SELECT e.fase, d.fabrica_id FROM DEVOLUCION d WITH (UPDLOCK)
            JOIN ESTADO_DEVOLUCION e ON e.id = d.estado_id
            WHERE d.id = @devolucion;", conexion, transaccion))
        {
            fase.Parameters.AddWithValue("@devolucion", datos.DevolucionId);
            using var lector = fase.ExecuteReader();
            if (!lector.Read()) return false;
            faseActual = lector.GetInt32(0);
            fabricaId = lector.GetInt32(1);
        }
        if (faseActual > Fase.RecogidaSolicitada)
            return false;

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

        // Solo la primera vez (al pasar a "Recogida solicitada"), no al modificar la recogida:
        // aviso a quien recibe la mercancía (el siguiente paso)
        if (faseActual == Fase.SolicitudRecibida)
            AvisoRepositorio.Apuntar(conexion, transaccion, datos.DevolucionId, fabricaId,
                TipoAviso.RecogidaSolicitada, Fase.MercanciaRecibida);

        transaccion.Commit();
        return true;
    }
}
