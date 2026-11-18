using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Evaluación de proveedores y fraude (PROVEEDOR y PROVEEDOR_PRODUCTO, Excel CP-RG05).
public class ProveedorRepositorio
{
    // Columnas de un producto, en el orden en que las lee LeerProducto
    private const string ColumnasProducto = @"
        id, proveedor_id, codigo, descripcion, tipo_producto, fecha_alta, baja, alergenos, posibles_alergenos,
        fecha_revision, ficha_tecnica, analisis_quimicos, analisis_micro, analisis_alergenos,
        eurochoc_fecha, eurochoc_alcance, eurochoc_resultado, impacto, control_recepcion, reclamaciones, parametro,
        p_historial, p_motivacion, p_facilidad, d_origen, d_cadena, d_medidas,
        medidas_preventivas, observaciones, version";

    // Todos los proveedores con sus productos, ordenados por Nº
    public List<ProveedorConProductos> ObtenerProveedores()
    {
        const string sqlProveedores = @"
            SELECT id, numero, nombre, fecha_alta, baja, tipo, fecha_revision, impacto, estatus, reclamaciones,
                   certificados, ensayo_trazabilidad, auditoria, rfa, relacion, historial, subcontratado, revisar, version
            FROM PROVEEDOR
            ORDER BY CASE WHEN numero IS NULL THEN 1 ELSE 0 END, numero, nombre;";

        const string sqlProductos = "SELECT " + ColumnasProducto + @"
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
                    Reclamaciones: null,   // la pone ProveedorServicio con sus NC; la columna era la nota a mano del Excel
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
            while (lector.Read())
            {
                var producto = LeerProducto(lector);
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
    public List<ProductoProveedor> ObtenerProductos(int proveedorId)
    {
        const string sql = "SELECT " + ColumnasProducto + @"
            FROM PROVEEDOR_PRODUCTO
            WHERE proveedor_id = @proveedor
            ORDER BY codigo, id;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@proveedor", proveedorId);
        using var lector = comando.ExecuteReader();
        var productos = new List<ProductoProveedor>();
        while (lector.Read())
            productos.Add(LeerProducto(lector));
        return productos;
    }

    // Por proveedor, sus NC detectadas después de "desde" que no afectan y que afectan a la seguridad alimentaria.
    // Solo NC (no incidencias ni mejoras); sin indicar cuenta como que no afecta.
    public Dictionary<int, (int NoAfectan, int Afectan)> ContarReclamaciones(DateTime desde)
    {
        const string sql = @"
            SELECT proveedor_id,
                   SUM(CASE WHEN seguridad_alimentaria = 1 THEN 0 ELSE 1 END),
                   SUM(CASE WHEN seguridad_alimentaria = 1 THEN 1 ELSE 0 END)
            FROM NO_CONFORMIDAD
            WHERE tipo_registro = 'NC' AND proveedor_id IS NOT NULL AND fecha_detectado > @desde
            GROUP BY proveedor_id;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.Add("@desde", System.Data.SqlDbType.Date).Value = desde.Date;
        using var lector = comando.ExecuteReader();
        var cuentas = new Dictionary<int, (int, int)>();
        while (lector.Read())
            cuentas[lector.GetInt32(0)] = (lector.GetInt32(1), lector.GetInt32(2));
        return cuentas;
    }

    // Todos los proveedores, solo id, nombre y si está de baja, por orden alfabético
    public List<ProveedorDeLista> ObtenerLista()
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand("SELECT id, nombre, baja FROM PROVEEDOR ORDER BY nombre;", conexion);
        using var lector = comando.ExecuteReader();
        var lista = new List<ProveedorDeLista>();
        while (lector.Read())
            lista.Add(new ProveedorDeLista(lector.GetInt32(0), lector.GetString(1), lector.GetBoolean(2)));
        return lista;
    }

    // Cuántos proveedores no están de baja (para el inicio)
    public int ContarActivos()
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand("SELECT COUNT(*) FROM PROVEEDOR WHERE baja = 0;", conexion);
        return (int)comando.ExecuteScalar();
    }

    // Una fila leída con las columnas de ColumnasProducto
    private static ProductoProveedor LeerProducto(SqlDataReader lector)
    {
        string? Texto(int i) => lector.IsDBNull(i) ? null : lector.GetString(i);
        DateTime? Fecha(int i) => lector.IsDBNull(i) ? null : lector.GetDateTime(i);
        byte? Nota(int i) => lector.IsDBNull(i) ? null : lector.GetByte(i);

        return new ProductoProveedor(
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
    }

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

    // Certificados (PROVEEDOR_CERTIFICADO)

    // Los de un proveedor, del que caduca antes al que caduca después
    public List<CertificadoProveedor> ObtenerCertificados(int proveedorId)
    {
        const string sql = @"
            SELECT id, proveedor_id, nombre, caduca, version
            FROM PROVEEDOR_CERTIFICADO
            WHERE proveedor_id = @proveedor
            ORDER BY caduca, nombre;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@proveedor", proveedorId);
        using var lector = comando.ExecuteReader();
        var certificados = new List<CertificadoProveedor>();
        while (lector.Read())
            certificados.Add(LeerCertificado(lector));
        return certificados;
    }

    // Los que caducan hasta esa fecha (también los ya caducados), de proveedores que no están de baja
    public List<CertificadoPorCaducar> ObtenerCertificadosHasta(DateTime hasta)
    {
        const string sql = @"
            SELECT c.id, c.proveedor_id, c.nombre, c.caduca, c.version, p.numero, p.nombre
            FROM PROVEEDOR_CERTIFICADO c
            JOIN PROVEEDOR p ON p.id = c.proveedor_id
            WHERE p.baja = 0 AND c.caduca <= @hasta
            ORDER BY c.caduca, p.nombre;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.Add("@hasta", System.Data.SqlDbType.Date).Value = hasta.Date;
        using var lector = comando.ExecuteReader();
        var certificados = new List<CertificadoPorCaducar>();
        while (lector.Read())
            certificados.Add(new CertificadoPorCaducar(
                LeerCertificado(lector),
                lector.IsDBNull(5) ? null : lector.GetInt32(5),
                lector.GetString(6)));
        return certificados;
    }

    private static CertificadoProveedor LeerCertificado(SqlDataReader lector) => new(
        Id: lector.GetInt32(0),
        ProveedorId: lector.GetInt32(1),
        Nombre: lector.GetString(2),
        Caduca: lector.GetDateTime(3),
        Version: (byte[])lector[4]);

    public int InsertarCertificado(CertificadoProveedor c, int usuarioId)
    {
        const string sql = @"
            INSERT INTO PROVEEDOR_CERTIFICADO (proveedor_id, nombre, caduca, creado_por)
            VALUES (@proveedor, @nombre, @caduca, @usuario);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@proveedor", c.ProveedorId);
        comando.Parameters.AddWithValue("@nombre", c.Nombre);
        comando.Parameters.Add("@caduca", System.Data.SqlDbType.Date).Value = c.Caduca.Date;
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        return (int)comando.ExecuteScalar();
    }

    // false si otro lo ha cambiado o borrado desde que se leyó (no se guarda nada)
    public bool ActualizarCertificado(CertificadoProveedor c)
    {
        const string sql = @"
            UPDATE PROVEEDOR_CERTIFICADO SET nombre = @nombre, caduca = @caduca
            WHERE id = @id AND version = @version;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@nombre", c.Nombre);
        comando.Parameters.Add("@caduca", System.Data.SqlDbType.Date).Value = c.Caduca.Date;
        comando.Parameters.AddWithValue("@id", c.Id);
        comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)c.Version ?? DBNull.Value;
        return comando.ExecuteNonQuery() > 0;
    }

    // false si otro lo ha cambiado o borrado desde que se leyó
    public bool BorrarCertificado(CertificadoProveedor c)
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(
            "DELETE FROM PROVEEDOR_CERTIFICADO WHERE id = @id AND version = @version;", conexion);
        comando.Parameters.AddWithValue("@id", c.Id);
        comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)c.Version ?? DBNull.Value;
        return comando.ExecuteNonQuery() > 0;
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
            INSERT INTO PROVEEDOR (numero, nombre, fecha_alta, baja, tipo, fecha_revision, impacto, estatus,
                certificados, ensayo_trazabilidad, auditoria, rfa, relacion, historial, subcontratado, creado_por)
            VALUES (@numero, @nombre, @alta, @baja, @tipo, @revision, @impacto, @estatus,
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
                fecha_revision = @revision, impacto = @impacto, estatus = @estatus,
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
        // reclamaciones no se guarda: la nota sale de sus NC (ProveedorServicio.NotaReclamaciones)
        Valor("@certificados", p.Certificados);
        Valor("@ensayo", p.EnsayoTrazabilidad);
        Valor("@auditoria", p.Auditoria);
        Valor("@rfa", p.Rfa);
        Valor("@relacion", p.Relacion);
        Valor("@historial", p.Historial);
        Valor("@subcontratado", p.Subcontratado);
    }
}
