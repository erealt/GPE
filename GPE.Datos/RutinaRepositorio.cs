using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Rutina diaria del supervisor de calidad (tablas RUTINA_SECCION, RUTINA_PUNTO,
// RUTINA_APLICA, RUTINA y RUTINA_RESULTADO)
public class RutinaRepositorio
{
    // ---------- Plantilla ----------

    public PlantillaRutina ObtenerPlantilla()
    {
        var secciones = new List<RutinaSeccion>();
        var puntos = new List<RutinaPunto>();
        var aplica = new HashSet<(int, int)>();

        const string sql = @"
            SELECT id, nombre, planta, seccion_calidad FROM RUTINA_SECCION
            WHERE activo = 1 ORDER BY orden, id;
            SELECT id, nombre, numerico, mc_pcc_bpm FROM RUTINA_PUNTO
            WHERE activo = 1 ORDER BY orden, id;
            SELECT punto_id, seccion_id FROM RUTINA_APLICA;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        using var lector = comando.ExecuteReader();

        while (lector.Read())
            secciones.Add(new RutinaSeccion(lector.GetInt32(0), lector.GetString(1), lector.GetString(2),
                lector.IsDBNull(3) ? null : lector.GetString(3)));
        lector.NextResult();
        while (lector.Read())
            puntos.Add(new RutinaPunto(lector.GetInt32(0), lector.GetString(1), lector.GetBoolean(2),
                lector.IsDBNull(3) ? null : lector.GetString(3)));
        lector.NextResult();
        while (lector.Read())
            aplica.Add((lector.GetInt32(0), lector.GetInt32(1)));

        return new PlantillaRutina(secciones, puntos, aplica);
    }

    // Deja en RUTINA_APLICA justo estas casillas
    public void GuardarPlantilla(IReadOnlyCollection<(int PuntoId, int SeccionId)> aplica)
    {
        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        var guardadas = new HashSet<(int, int)>();
        using (var consulta = new SqlCommand("SELECT punto_id, seccion_id FROM RUTINA_APLICA;", conexion, transaccion))
        using (var lector = consulta.ExecuteReader())
            while (lector.Read())
                guardadas.Add((lector.GetInt32(0), lector.GetInt32(1)));

        foreach (var (punto, seccion) in guardadas.Except(aplica))
            Ejecutar("DELETE FROM RUTINA_APLICA WHERE punto_id = @punto AND seccion_id = @seccion;", punto, seccion);
        foreach (var (punto, seccion) in aplica.Except(guardadas))
            Ejecutar("INSERT INTO RUTINA_APLICA (punto_id, seccion_id) VALUES (@punto, @seccion);", punto, seccion);

        transaccion.Commit();

        void Ejecutar(string sql, int punto, int seccion)
        {
            using var comando = new SqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("@punto", punto);
            comando.Parameters.AddWithValue("@seccion", seccion);
            comando.ExecuteNonQuery();
        }
    }

    // ---------- Rondas ----------

    // La ronda de ese día, o null si aún no se ha guardado ninguna
    public Rutina? ObtenerRutina(DateTime fecha)
    {
        const string sql = @"
            SELECT r.id, r.fecha, r.caja_operario, r.caja_estado, r.depositos, u.nombre, r.version
            FROM RUTINA r
            JOIN USUARIO u ON u.id = r.creado_por
            WHERE r.fecha = @fecha;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@fecha", fecha.Date);

        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return null;
        string? Texto(int i) => lector.IsDBNull(i) ? null : lector.GetString(i);
        return new Rutina(
            Id: lector.GetInt32(0),
            Fecha: lector.GetDateTime(1),
            CajaOperario: Texto(2),
            CajaEstado: Texto(3),
            Depositos: Texto(4),
            CreadoPor: lector.GetString(5),
            Version: (byte[])lector[6]);
    }

    public List<ObservacionRutina> ObtenerObservaciones(int rutinaId)
    {
        const string sql = @"
            SELECT seccion_id, texto FROM RUTINA_OBSERVACION
            WHERE rutina_id = @rutina ORDER BY orden, id;";

        var observaciones = new List<ObservacionRutina>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@rutina", rutinaId);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            observaciones.Add(new ObservacionRutina(lector.GetInt32(0), lector.GetString(1)));
        return observaciones;
    }

    public List<ResultadoRutina> ObtenerResultados(int rutinaId)
    {
        const string sql = @"
            SELECT r.punto_id, r.seccion_id, r.estado, r.valor, r.comentario, r.no_conformidad_id, nc.numero
            FROM RUTINA_RESULTADO r
            LEFT JOIN NO_CONFORMIDAD nc ON nc.id = r.no_conformidad_id
            WHERE r.rutina_id = @rutina;";

        var resultados = new List<ResultadoRutina>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@rutina", rutinaId);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultados.Add(new ResultadoRutina(
                PuntoId: lector.GetInt32(0),
                SeccionId: lector.GetInt32(1),
                Estado: lector.IsDBNull(2) ? null : lector.GetString(2),
                Valor: lector.IsDBNull(3) ? null : lector.GetDecimal(3),
                Comentario: lector.IsDBNull(4) ? null : lector.GetString(4),
                NoConformidadId: lector.IsDBNull(5) ? null : lector.GetInt32(5),
                NumeroIncidencia: lector.IsDBNull(6) ? null : lector.GetString(6)));
        }
        return resultados;
    }

    // Crea la ronda (Id = 0) o guarda sus cambios, y deja en RUTINA_RESULTADO y RUTINA_OBSERVACION
    // justo estas casillas y observaciones. Devuelve el id, o null si otro la ha guardado
    // mientras tanto (su versión ya no es la que trae rutina, o ya creó la de ese día).
    // La incidencia de cada casilla no se toca aquí: la apunta Vincular.
    public int? Guardar(Rutina rutina, IReadOnlyList<ResultadoRutina> resultados,
        IReadOnlyList<ObservacionRutina> observaciones, int usuarioId)
    {
        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        void Fijas(SqlCommand comando)
        {
            comando.Parameters.AddWithValue("@caja_operario", (object?)rutina.CajaOperario ?? DBNull.Value);
            comando.Parameters.AddWithValue("@caja_estado", (object?)rutina.CajaEstado ?? DBNull.Value);
            comando.Parameters.AddWithValue("@depositos", (object?)rutina.Depositos ?? DBNull.Value);
        }

        int id;
        if (rutina.Id == 0)
        {
            const string sqlInsertar = @"
                INSERT INTO RUTINA (fecha, caja_operario, caja_estado, depositos, creado_por)
                VALUES (@fecha, @caja_operario, @caja_estado, @depositos, @usuario);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            using var comando = new SqlCommand(sqlInsertar, conexion, transaccion);
            comando.Parameters.AddWithValue("@fecha", rutina.Fecha.Date);
            Fijas(comando);
            comando.Parameters.AddWithValue("@usuario", usuarioId);
            try
            {
                id = (int)comando.ExecuteScalar();
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601)
            {
                return null;   // otro guardó primero la ronda de ese día
            }
        }
        else
        {
            // Se actualiza aunque las observaciones no cambien: así la versión cambia y
            // quien tenga la ronda abierta no pisa estas casillas
            const string sqlActualizar = @"
                UPDATE RUTINA SET caja_operario = @caja_operario, caja_estado = @caja_estado, depositos = @depositos
                WHERE id = @id AND version = @version;";
            using var comando = new SqlCommand(sqlActualizar, conexion, transaccion);
            Fijas(comando);
            comando.Parameters.AddWithValue("@id", rutina.Id);
            comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value =
                (object?)rutina.Version ?? DBNull.Value;
            if (comando.ExecuteNonQuery() == 0) return null;
            id = rutina.Id;
        }

        GuardarResultados(id, resultados, conexion, transaccion);
        GuardarObservaciones(id, observaciones, conexion, transaccion);
        transaccion.Commit();
        return id;
    }

    // Solo escribe las casillas que cambian, para no llenar el historial con cientos de filas
    private static void GuardarResultados(int rutinaId, IReadOnlyList<ResultadoRutina> resultados,
        SqlConnection conexion, SqlTransaction transaccion)
    {
        var guardados = new Dictionary<(int, int), (int Id, ResultadoRutina Resultado)>();
        using (var consulta = new SqlCommand(@"
            SELECT id, punto_id, seccion_id, estado, valor, comentario
            FROM RUTINA_RESULTADO WHERE rutina_id = @rutina;", conexion, transaccion))
        {
            consulta.Parameters.AddWithValue("@rutina", rutinaId);
            using var lector = consulta.ExecuteReader();
            while (lector.Read())
            {
                var resultado = new ResultadoRutina(lector.GetInt32(1), lector.GetInt32(2),
                    lector.IsDBNull(3) ? null : lector.GetString(3),
                    lector.IsDBNull(4) ? null : lector.GetDecimal(4),
                    lector.IsDBNull(5) ? null : lector.GetString(5));
                guardados[(resultado.PuntoId, resultado.SeccionId)] = (lector.GetInt32(0), resultado);
            }
        }

        var nuevos = resultados.ToDictionary(r => (r.PuntoId, r.SeccionId));

        foreach (var (clave, (id, _)) in guardados)
        {
            if (nuevos.ContainsKey(clave)) continue;
            using var borrar = new SqlCommand("DELETE FROM RUTINA_RESULTADO WHERE id = @id;", conexion, transaccion);
            borrar.Parameters.AddWithValue("@id", id);
            borrar.ExecuteNonQuery();
        }

        const string sqlInsertar = @"
            INSERT INTO RUTINA_RESULTADO (rutina_id, punto_id, seccion_id, estado, valor, comentario)
            VALUES (@rutina, @punto, @seccion, @estado, @valor, @comentario);";
        const string sqlActualizar = @"
            UPDATE RUTINA_RESULTADO SET estado = @estado, valor = @valor, comentario = @comentario
            WHERE id = @id;";

        foreach (var (clave, resultado) in nuevos)
        {
            bool existe = guardados.TryGetValue(clave, out var guardado);
            if (existe && guardado.Resultado.Estado == resultado.Estado && guardado.Resultado.Valor == resultado.Valor
                && guardado.Resultado.Comentario == resultado.Comentario)
                continue;

            using var comando = new SqlCommand(existe ? sqlActualizar : sqlInsertar, conexion, transaccion);
            void Valor(string nombre, object? valor) =>
                comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);

            if (existe)
                Valor("@id", guardado.Id);
            else
            {
                Valor("@rutina", rutinaId);
                Valor("@punto", resultado.PuntoId);
                Valor("@seccion", resultado.SeccionId);
            }
            Valor("@estado", resultado.Estado);
            Valor("@valor", resultado.Valor);
            Valor("@comentario", resultado.Comentario);
            comando.ExecuteNonQuery();
        }
    }

    // Son pocas al día: si algo cambia, se borran y se vuelven a escribir en su orden
    private static void GuardarObservaciones(int rutinaId, IReadOnlyList<ObservacionRutina> observaciones,
        SqlConnection conexion, SqlTransaction transaccion)
    {
        var guardadas = new List<ObservacionRutina>();
        using (var consulta = new SqlCommand(
            "SELECT seccion_id, texto FROM RUTINA_OBSERVACION WHERE rutina_id = @rutina ORDER BY orden, id;",
            conexion, transaccion))
        {
            consulta.Parameters.AddWithValue("@rutina", rutinaId);
            using var lector = consulta.ExecuteReader();
            while (lector.Read())
                guardadas.Add(new ObservacionRutina(lector.GetInt32(0), lector.GetString(1)));
        }
        if (guardadas.SequenceEqual(observaciones)) return;

        using (var borrar = new SqlCommand("DELETE FROM RUTINA_OBSERVACION WHERE rutina_id = @rutina;", conexion, transaccion))
        {
            borrar.Parameters.AddWithValue("@rutina", rutinaId);
            borrar.ExecuteNonQuery();
        }

        int orden = 0;
        foreach (var observacion in observaciones)
        {
            using var comando = new SqlCommand(@"
                INSERT INTO RUTINA_OBSERVACION (rutina_id, seccion_id, texto, orden)
                VALUES (@rutina, @seccion, @texto, @orden);", conexion, transaccion);
            comando.Parameters.AddWithValue("@rutina", rutinaId);
            comando.Parameters.AddWithValue("@seccion", observacion.SeccionId);
            comando.Parameters.AddWithValue("@texto", observacion.Texto);
            comando.Parameters.AddWithValue("@orden", ++orden);
            comando.ExecuteNonQuery();
        }
    }

    // Apunta en la casilla la incidencia que se ha abierto desde ella. false si la casilla ya no existe.
    public bool Vincular(int rutinaId, int puntoId, int seccionId, int noConformidadId)
    {
        const string sql = @"
            UPDATE RUTINA_RESULTADO SET no_conformidad_id = @nc
            WHERE rutina_id = @rutina AND punto_id = @punto AND seccion_id = @seccion;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@nc", noConformidadId);
        comando.Parameters.AddWithValue("@rutina", rutinaId);
        comando.Parameters.AddWithValue("@punto", puntoId);
        comando.Parameters.AddWithValue("@seccion", seccionId);
        return comando.ExecuteNonQuery() > 0;
    }
}
