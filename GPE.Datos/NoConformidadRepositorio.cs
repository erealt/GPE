using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Incidencias, no conformidades y acciones de mejora (tablas NO_CONFORMIDAD, NC_ACCION y CALIDAD_LISTA).
// NC_ACCION guarda las acciones inmediatas, las correctoras y los seguimientos.
public class NoConformidadRepositorio
{
    // Lectura

    // En el mismo orden que el record NoConformidad
    private const string SqlRegistros = @"
            SELECT id, tipo_registro, numero, fecha_detectado, origen, seccion, tipo,
                   problema, descripcion_causa, responsable,
                   fecha_prevista, fecha_fin, cliente, proveedor,
                   mc_pcc_bpm, parametro, operario,
                   capitulo_norma, analisis_causa_raiz,
                   coste_economico, horas_mano_obra, version
            FROM NO_CONFORMIDAD";

    public List<NoConformidad> ObtenerRegistros() =>
        LeerRegistros(SqlRegistros + " ORDER BY fecha_detectado DESC, id DESC;", null);

    // Un registro con sus datos actuales, o null si ya no existe
    public NoConformidad? ObtenerRegistro(int id) =>
        LeerRegistros(SqlRegistros + " WHERE id = @id;", id).FirstOrDefault();

    private static List<NoConformidad> LeerRegistros(string sql, int? id)
    {
        var lista = new List<NoConformidad>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        if (id != null)
            comando.Parameters.AddWithValue("@id", id);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            string? Texto(int i) => lector.IsDBNull(i) ? null : lector.GetString(i);
            DateTime? Fecha(int i) => lector.IsDBNull(i) ? null : lector.GetDateTime(i);
            decimal? Numero(int i) => lector.IsDBNull(i) ? null : lector.GetDecimal(i);

            lista.Add(new NoConformidad(
                Id: lector.GetInt32(0),
                TipoRegistro: lector.GetString(1),
                Numero: Texto(2),
                FechaDetectado: lector.GetDateTime(3),
                Origen: Texto(4),
                Seccion: Texto(5),
                Tipo: Texto(6),
                Problema: lector.GetString(7),
                DescripcionCausa: Texto(8),
                Responsable: Texto(9),
                FechaPrevista: Fecha(10),
                FechaFin: Fecha(11),
                Cliente: Texto(12),
                Proveedor: Texto(13),
                McPccBpm: Texto(14),
                Parametro: Texto(15),
                Operario: Texto(16),
                CapituloNorma: Texto(17),
                AnalisisCausaRaiz: Texto(18),
                CosteEconomico: Numero(19),
                HorasManoObra: Numero(20),
                Version: (byte[])lector[21]));
        }

        return lista;
    }

    // Fecha prevista de cada registro abierto (sin fecha fin), para contar los que
    // están fuera de plazo sin leer todos los registros
    public List<DateTime?> ObtenerPlazosAbiertos()
    {
        const string sql = "SELECT fecha_prevista FROM NO_CONFORMIDAD WHERE fecha_fin IS NULL;";

        var lista = new List<DateTime?>();
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            lista.Add(lector.IsDBNull(0) ? null : lector.GetDateTime(0));
        return lista;
    }

    // Acciones inmediatas, correctoras y seguimientos de un registro, en su orden
    public List<AccionNc> ObtenerAcciones(int noConformidadId)
    {
        const string sql = @"
            SELECT id, clase, accion, fecha, responsable, observaciones, hecha
            FROM NC_ACCION
            WHERE no_conformidad_id = @id
            ORDER BY clase, orden, id;";

        var acciones = new List<AccionNc>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@id", noConformidadId);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            acciones.Add(new AccionNc(
                Id: lector.GetInt32(0),
                Clase: lector.GetString(1),
                Accion: lector.GetString(2),
                Fecha: lector.IsDBNull(3) ? null : lector.GetDateTime(3),
                Responsable: lector.IsDBNull(4) ? null : lector.GetString(4),
                Observaciones: lector.IsDBNull(5) ? null : lector.GetString(5),
                Hecha: lector.GetBoolean(6)));
        }

        return acciones;
    }

    // Opciones activas de todos los desplegables, agrupadas por lista y en su orden
    public Dictionary<string, List<OpcionLista>> ObtenerListas()
    {
        const string sql = @"
            SELECT lista, valor, descripcion FROM CALIDAD_LISTA
            WHERE activo = 1
            ORDER BY lista, orden, valor;";

        var listas = new Dictionary<string, List<OpcionLista>>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            string lista = lector.GetString(0);
            if (!listas.TryGetValue(lista, out var opciones))
                listas[lista] = opciones = new List<OpcionLista>();
            opciones.Add(new OpcionLista(lector.GetString(1), lector.IsDBNull(2) ? null : lector.GetString(2)));
        }

        return listas;
    }

    // Nombres ya usados, para sugerirlos al escribir. Los clientes incluyen los de devoluciones.
    public List<string> ObtenerClientes() => LeerNombres(@"
            SELECT nombre FROM CLIENTE
            UNION SELECT cliente FROM NO_CONFORMIDAD WHERE cliente IS NOT NULL;");

    public List<string> ObtenerProveedores() => LeerNombres(
        "SELECT DISTINCT proveedor FROM NO_CONFORMIDAD WHERE proveedor IS NOT NULL;");

    private static List<string> LeerNombres(string sql)
    {
        var nombres = new List<string>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            nombres.Add(lector.GetString(0));

        return nombres;
    }

    // Número que tocaría ahora en una serie. Solo para enseñarlo en la ficha: el
    // definitivo se calcula al guardar, por si otro usuario guarda antes.
    public string SiguienteNumero(SerieNumero serie)
    {
        using var conexion = Conexion.Abrir();
        return SiguienteNumero(serie, conexion, null);
    }

    // Prefijo + (el mayor número de la serie + 1): 558, AC-2026-3, AI-20/04/26-14...
    // Los números de otro formato (LC25-06...) no cuentan. Dentro de una transacción,
    // UPDLOCK y HOLDLOCK hacen esperar a quien quiera numerar a la vez en la misma serie.
    private static string SiguienteNumero(SerieNumero serie, SqlConnection conexion, SqlTransaction? transaccion)
    {
        const string sql = @"
            SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(numero, LEN(@prefijo) + 1, 30) AS INT)), 0) + 1
            FROM NO_CONFORMIDAD WITH (UPDLOCK, HOLDLOCK)
            WHERE tipo_registro = @tipo AND numero LIKE @prefijo + '%';";

        using var comando = new SqlCommand(sql, conexion, transaccion);
        comando.Parameters.AddWithValue("@tipo", serie.TipoRegistro);
        comando.Parameters.AddWithValue("@prefijo", serie.Prefijo);
        return serie.Prefijo + (int)comando.ExecuteScalar();
    }

    // Escritura

    // Crea el registro numerándolo en su serie (sin serie, sin número), con sus acciones. Devuelve el id.
    public int Insertar(NoConformidad nc, IReadOnlyList<AccionNc> acciones, SerieNumero? serie, int usuarioId)
    {
        const string sql = @"
            INSERT INTO NO_CONFORMIDAD
                (tipo_registro, numero, fecha_detectado, origen, seccion, tipo,
                 problema, descripcion_causa, responsable,
                 fecha_prevista, fecha_fin, cliente, proveedor,
                 mc_pcc_bpm, parametro, operario,
                 capitulo_norma, analisis_causa_raiz,
                 coste_economico, horas_mano_obra, creado_por)
            VALUES
                (@tipo_registro, @numero, @fecha_detectado, @origen, @seccion, @tipo,
                 @problema, @descripcion_causa, @responsable,
                 @fecha_prevista, @fecha_fin, @cliente, @proveedor,
                 @mc_pcc_bpm, @parametro, @operario,
                 @capitulo_norma, @analisis_causa_raiz,
                 @coste_economico, @horas_mano_obra, @usuario);

            -- Como en DEVOLUCION: con trigger no se puede usar OUTPUT INSERTED.id
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        if (serie != null)
            nc = nc with { Numero = SiguienteNumero(serie, conexion, transaccion) };

        int id;
        using (var comando = new SqlCommand(sql, conexion, transaccion))
        {
            AnadirParametros(comando, nc);
            comando.Parameters.AddWithValue("@usuario", usuarioId);
            id = (int)comando.ExecuteScalar();
        }
        GuardarAcciones(id, acciones, conexion, transaccion);

        transaccion.Commit();
        return id;
    }

    // Guarda los cambios y las acciones. Con serie, se le da un número nuevo de esa serie; sin ella,
    // se guarda el número que trae. false si el registro ya no existe (otro usuario lo ha borrado)
    // o si su versión ya no es la que trae nc (otro usuario lo ha guardado mientras tanto).
    // Las acciones se guardan siempre junto con la cabecera, así que su versión también las cubre.
    public bool Actualizar(NoConformidad nc, IReadOnlyList<AccionNc> acciones, SerieNumero? numerarEn)
    {
        const string sql = @"
            UPDATE NO_CONFORMIDAD SET
                tipo_registro = @tipo_registro, numero = @numero, fecha_detectado = @fecha_detectado,
                origen = @origen, seccion = @seccion, tipo = @tipo,
                problema = @problema, descripcion_causa = @descripcion_causa,
                responsable = @responsable,
                fecha_prevista = @fecha_prevista, fecha_fin = @fecha_fin,
                cliente = @cliente, proveedor = @proveedor,
                mc_pcc_bpm = @mc_pcc_bpm, parametro = @parametro, operario = @operario,
                capitulo_norma = @capitulo_norma, analisis_causa_raiz = @analisis_causa_raiz,
                coste_economico = @coste_economico, horas_mano_obra = @horas_mano_obra
            WHERE id = @id AND version = @version;";

        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        if (numerarEn != null)
            nc = nc with { Numero = SiguienteNumero(numerarEn, conexion, transaccion) };

        int filas;
        using (var comando = new SqlCommand(sql, conexion, transaccion))
        {
            AnadirParametros(comando, nc);
            comando.Parameters.AddWithValue("@id", nc.Id);
            comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value =
                (object?)nc.Version ?? DBNull.Value;
            filas = comando.ExecuteNonQuery();
        }
        if (filas == 0) return false;
        GuardarAcciones(nc.Id, acciones, conexion, transaccion);

        transaccion.Commit();
        return true;
    }

    // Deja en NC_ACCION justo estas acciones: borra las que se han quitado, actualiza
    // las que ya estaban (Id > 0) y añade las nuevas. El orden es el de la lista.
    private static void GuardarAcciones(int noConformidadId, IReadOnlyList<AccionNc> acciones,
        SqlConnection conexion, SqlTransaction transaccion)
    {
        var guardadas = new List<int>();
        using (var consulta = new SqlCommand(
            "SELECT id FROM NC_ACCION WHERE no_conformidad_id = @nc;", conexion, transaccion))
        {
            consulta.Parameters.AddWithValue("@nc", noConformidadId);
            using var lector = consulta.ExecuteReader();
            while (lector.Read())
                guardadas.Add(lector.GetInt32(0));
        }

        foreach (int id in guardadas.Except(acciones.Select(a => a.Id)))
        {
            using var borrar = new SqlCommand("DELETE FROM NC_ACCION WHERE id = @id;", conexion, transaccion);
            borrar.Parameters.AddWithValue("@id", id);
            borrar.ExecuteNonQuery();
        }

        const string sqlInsertar = @"
            INSERT INTO NC_ACCION (no_conformidad_id, clase, orden, accion, fecha, responsable, observaciones, hecha)
            VALUES (@nc, @clase, @orden, @accion, @fecha, @responsable, @observaciones, @hecha);";
        const string sqlActualizar = @"
            UPDATE NC_ACCION SET
                clase = @clase, orden = @orden, accion = @accion, fecha = @fecha,
                responsable = @responsable, observaciones = @observaciones, hecha = @hecha
            WHERE id = @id AND no_conformidad_id = @nc;";

        // El orden cuenta dentro de cada tabla (inmediatas 1, 2...; correctoras 1, 2...; seguimientos 1, 2...)
        foreach (var grupo in acciones.GroupBy(a => a.Clase))
        {
            int orden = 0;
            foreach (var accion in grupo)
            {
                bool nueva = accion.Id == 0 || !guardadas.Contains(accion.Id);
                using var comando = new SqlCommand(nueva ? sqlInsertar : sqlActualizar, conexion, transaccion);
                void Valor(string nombre, object? valor) =>
                    comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);

                Valor("@nc", noConformidadId);
                Valor("@clase", accion.Clase);
                Valor("@orden", ++orden);
                Valor("@accion", accion.Accion);
                Valor("@fecha", accion.Fecha?.Date);
                Valor("@responsable", accion.Responsable);
                Valor("@observaciones", accion.Observaciones);
                Valor("@hecha", accion.Hecha);
                if (!nueva) Valor("@id", accion.Id);
                comando.ExecuteNonQuery();
            }
        }
    }

    private static void AnadirParametros(SqlCommand comando, NoConformidad nc)
    {
        void Valor(string nombre, object? valor) =>
            comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);

        Valor("@tipo_registro", nc.TipoRegistro);
        Valor("@numero", nc.Numero);
        Valor("@fecha_detectado", nc.FechaDetectado.Date);
        Valor("@origen", nc.Origen);
        Valor("@seccion", nc.Seccion);
        Valor("@tipo", nc.Tipo);
        Valor("@problema", nc.Problema);
        Valor("@descripcion_causa", nc.DescripcionCausa);
        Valor("@responsable", nc.Responsable);
        Valor("@fecha_prevista", nc.FechaPrevista?.Date);
        Valor("@fecha_fin", nc.FechaFin?.Date);
        Valor("@cliente", nc.Cliente);
        Valor("@proveedor", nc.Proveedor);
        Valor("@mc_pcc_bpm", nc.McPccBpm);
        Valor("@parametro", nc.Parametro);
        Valor("@operario", nc.Operario);
        Valor("@capitulo_norma", nc.CapituloNorma);
        Valor("@analisis_causa_raiz", nc.AnalisisCausaRaiz);
        Valor("@coste_economico", nc.CosteEconomico);
        Valor("@horas_mano_obra", nc.HorasManoObra);
    }

    // Bloqueos (quién tiene abierta cada ficha)

    // Apunta que este GPE (sesion) tiene abierta la ficha, o renueva su ultimo_aviso si ya
    // la tenía. Se la queda también si el bloqueo de otro lleva más de caducidadSegundos
    // sin renovarse. Devuelve null si la ficha es de este GPE, o quién la tiene si no.
    // HOLDLOCK: si dos la abren a la vez, el segundo espera y ve el bloqueo del primero.
    public BloqueoNc? Bloquear(int noConformidadId, int usuarioId, Guid sesion, string equipo, int caducidadSegundos)
    {
        const string sql = @"
            MERGE NC_BLOQUEO WITH (HOLDLOCK) AS b
            USING (SELECT @nc AS no_conformidad_id) AS s ON b.no_conformidad_id = s.no_conformidad_id
            WHEN MATCHED AND (b.sesion = @sesion
                              OR b.ultimo_aviso < DATEADD(SECOND, -@caducidad, SYSDATETIME())) THEN
                UPDATE SET usuario_id = @usuario, sesion = @sesion, equipo = @equipo,
                           abierta_desde = CASE WHEN b.sesion = @sesion THEN b.abierta_desde ELSE SYSDATETIME() END,
                           ultimo_aviso = SYSDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (no_conformidad_id, usuario_id, sesion, equipo, abierta_desde, ultimo_aviso)
                VALUES (@nc, @usuario, @sesion, @equipo, SYSDATETIME(), SYSDATETIME());

            SELECT b.sesion, u.nombre, b.equipo, b.abierta_desde, b.usuario_id
            FROM NC_BLOQUEO b
            JOIN USUARIO u ON u.id = b.usuario_id
            WHERE b.no_conformidad_id = @nc;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@nc", noConformidadId);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        comando.Parameters.AddWithValue("@sesion", sesion);
        comando.Parameters.AddWithValue("@equipo", equipo);
        comando.Parameters.AddWithValue("@caducidad", caducidadSegundos);

        using var lector = comando.ExecuteReader();
        if (!lector.Read() || lector.GetGuid(0) == sesion)
            return null;

        return new BloqueoNc(
            Usuario: lector.GetString(1),
            Equipo: lector.GetString(2),
            Desde: lector.GetDateTime(3),
            MismoUsuario: lector.GetInt32(4) == usuarioId);
    }

    // Quita el bloqueo al cerrar la ficha, si sigue siendo de este GPE
    public void Desbloquear(int noConformidadId, Guid sesion)
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(
            "DELETE FROM NC_BLOQUEO WHERE no_conformidad_id = @nc AND sesion = @sesion;", conexion);
        comando.Parameters.AddWithValue("@nc", noConformidadId);
        comando.Parameters.AddWithValue("@sesion", sesion);
        comando.ExecuteNonQuery();
    }
}
