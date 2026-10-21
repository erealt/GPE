using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Incidencias, no conformidades y acciones de mejora (tablas NO_CONFORMIDAD y CALIDAD_LISTA)
public class NoConformidadRepositorio
{
    // ---------- Lectura ----------

    // En el mismo orden que el record NoConformidad
    private const string SqlRegistros = @"
            SELECT id, tipo_registro, numero, fecha_detectado, origen, seccion, tipo,
                   problema, descripcion_causa, accion_inmediata, responsable,
                   fecha_prevista, fecha_fin, cliente, proveedor,
                   mc_pcc_bpm, parametro, operario,
                   capitulo_norma, analisis_causa_raiz, accion_correctora, responsable_accion_correctora,
                   coste_economico, horas_mano_obra, seguimiento
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
                AccionInmediata: Texto(9),
                Responsable: Texto(10),
                FechaPrevista: Fecha(11),
                FechaFin: Fecha(12),
                Cliente: Texto(13),
                Proveedor: Texto(14),
                McPccBpm: Texto(15),
                Parametro: Texto(16),
                Operario: Texto(17),
                CapituloNorma: Texto(18),
                AnalisisCausaRaiz: Texto(19),
                AccionCorrectora: Texto(20),
                ResponsableAccionCorrectora: Texto(21),
                CosteEconomico: Numero(22),
                HorasManoObra: Numero(23),
                Seguimiento: Texto(24)));
        }

        return lista;
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

    // ---------- Escritura ----------

    // Crea el registro numerándolo en su serie (sin serie, sin número). Devuelve el id.
    public int Insertar(NoConformidad nc, SerieNumero? serie, int usuarioId)
    {
        const string sql = @"
            INSERT INTO NO_CONFORMIDAD
                (tipo_registro, numero, fecha_detectado, origen, seccion, tipo,
                 problema, descripcion_causa, accion_inmediata, responsable,
                 fecha_prevista, fecha_fin, cliente, proveedor,
                 mc_pcc_bpm, parametro, operario,
                 capitulo_norma, analisis_causa_raiz, accion_correctora, responsable_accion_correctora,
                 coste_economico, horas_mano_obra, seguimiento, creado_por)
            VALUES
                (@tipo_registro, @numero, @fecha_detectado, @origen, @seccion, @tipo,
                 @problema, @descripcion_causa, @accion_inmediata, @responsable,
                 @fecha_prevista, @fecha_fin, @cliente, @proveedor,
                 @mc_pcc_bpm, @parametro, @operario,
                 @capitulo_norma, @analisis_causa_raiz, @accion_correctora, @responsable_accion_correctora,
                 @coste_economico, @horas_mano_obra, @seguimiento, @usuario);

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

        transaccion.Commit();
        return id;
    }

    // Guarda los cambios. Con serie, se le da un número nuevo de esa serie; sin ella,
    // se guarda el número que trae. false si el registro ya no existe (otro usuario lo ha borrado).
    public bool Actualizar(NoConformidad nc, SerieNumero? numerarEn)
    {
        const string sql = @"
            UPDATE NO_CONFORMIDAD SET
                tipo_registro = @tipo_registro, numero = @numero, fecha_detectado = @fecha_detectado,
                origen = @origen, seccion = @seccion, tipo = @tipo,
                problema = @problema, descripcion_causa = @descripcion_causa,
                accion_inmediata = @accion_inmediata, responsable = @responsable,
                fecha_prevista = @fecha_prevista, fecha_fin = @fecha_fin,
                cliente = @cliente, proveedor = @proveedor,
                mc_pcc_bpm = @mc_pcc_bpm, parametro = @parametro, operario = @operario,
                capitulo_norma = @capitulo_norma, analisis_causa_raiz = @analisis_causa_raiz,
                accion_correctora = @accion_correctora,
                responsable_accion_correctora = @responsable_accion_correctora,
                coste_economico = @coste_economico, horas_mano_obra = @horas_mano_obra,
                seguimiento = @seguimiento
            WHERE id = @id;";

        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        if (numerarEn != null)
            nc = nc with { Numero = SiguienteNumero(numerarEn, conexion, transaccion) };

        int filas;
        using (var comando = new SqlCommand(sql, conexion, transaccion))
        {
            AnadirParametros(comando, nc);
            comando.Parameters.AddWithValue("@id", nc.Id);
            filas = comando.ExecuteNonQuery();
        }

        transaccion.Commit();
        return filas > 0;
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
        Valor("@accion_inmediata", nc.AccionInmediata);
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
        Valor("@accion_correctora", nc.AccionCorrectora);
        Valor("@responsable_accion_correctora", nc.ResponsableAccionCorrectora);
        Valor("@coste_economico", nc.CosteEconomico);
        Valor("@horas_mano_obra", nc.HorasManoObra);
        Valor("@seguimiento", nc.Seguimiento);
    }
}
