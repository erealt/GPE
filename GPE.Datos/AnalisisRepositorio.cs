using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Plan de análisis de calidad (tabla ANALISIS)
public class AnalisisRepositorio
{
    // Todos, los últimos analizados primero (los que no tienen fecha, arriba)
    public List<Analisis> ObtenerTodos()
    {
        const string sql = @"
            SELECT a.id, a.tipo, a.descripcion, a.lote, a.caducidad, a.analisis_totales, a.parametro,
                   a.parametro_detalle, a.fecha_analisis, a.resultado, u.nombre, a.version
            FROM ANALISIS a
            JOIN USUARIO u ON u.id = a.creado_por
            ORDER BY CASE WHEN a.fecha_analisis IS NULL THEN 0 ELSE 1 END, a.fecha_analisis DESC, a.id DESC;";

        var lista = new List<Analisis>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        using var lector = comando.ExecuteReader();
        string? Texto(int i) => lector.IsDBNull(i) ? null : lector.GetString(i);
        DateTime? Fecha(int i) => lector.IsDBNull(i) ? null : lector.GetDateTime(i);
        while (lector.Read())
            lista.Add(new Analisis(
                Id: lector.GetInt32(0),
                Tipo: lector.GetString(1),
                Descripcion: lector.GetString(2),
                Lote: Texto(3),
                Caducidad: Fecha(4),
                AnalisisTotales: Texto(5),
                Parametro: Texto(6),
                ParametroDetalle: Texto(7),
                FechaAnalisis: Fecha(8),
                Resultado: Texto(9),
                CreadoPor: lector.GetString(10),
                Version: (byte[])lector[11]));
        return lista;
    }

    public int Contar()
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand("SELECT COUNT(*) FROM ANALISIS;", conexion);
        return (int)comando.ExecuteScalar();
    }

    // Uno nuevo; devuelve su id
    public int Insertar(Analisis analisis, int usuarioId)
    {
        const string sql = @"
            INSERT INTO ANALISIS (tipo, descripcion, lote, caducidad, analisis_totales, parametro, parametro_detalle,
                fecha_analisis, resultado, creado_por)
            VALUES (@tipo, @descripcion, @lote, @caducidad, @totales, @parametro, @detalle, @fecha, @resultado, @usuario);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        Parametros(comando, analisis);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        return (int)comando.ExecuteScalar();
    }

    // false si otro lo ha cambiado o borrado desde que se leyó (no se guarda nada)
    public bool Actualizar(Analisis analisis)
    {
        const string sql = @"
            UPDATE ANALISIS SET tipo = @tipo, descripcion = @descripcion, lote = @lote, caducidad = @caducidad,
                analisis_totales = @totales, parametro = @parametro, parametro_detalle = @detalle,
                fecha_analisis = @fecha, resultado = @resultado
            WHERE id = @id AND version = @version;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        Parametros(comando, analisis);
        comando.Parameters.AddWithValue("@id", analisis.Id);
        comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)analisis.Version ?? DBNull.Value;
        return comando.ExecuteNonQuery() > 0;
    }

    // false si otro lo ha cambiado o borrado desde que se leyó (no se borra nada)
    public bool Borrar(Analisis analisis)
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand("DELETE FROM ANALISIS WHERE id = @id AND version = @version;", conexion);
        comando.Parameters.AddWithValue("@id", analisis.Id);
        comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)analisis.Version ?? DBNull.Value;
        return comando.ExecuteNonQuery() > 0;
    }

    private static void Parametros(SqlCommand comando, Analisis a)
    {
        comando.Parameters.AddWithValue("@tipo", a.Tipo);
        comando.Parameters.AddWithValue("@descripcion", a.Descripcion);
        comando.Parameters.AddWithValue("@lote", (object?)a.Lote ?? DBNull.Value);
        comando.Parameters.Add("@caducidad", System.Data.SqlDbType.Date).Value = (object?)a.Caducidad ?? DBNull.Value;
        comando.Parameters.AddWithValue("@totales", (object?)a.AnalisisTotales ?? DBNull.Value);
        comando.Parameters.AddWithValue("@parametro", (object?)a.Parametro ?? DBNull.Value);
        comando.Parameters.AddWithValue("@detalle", (object?)a.ParametroDetalle ?? DBNull.Value);
        comando.Parameters.Add("@fecha", System.Data.SqlDbType.Date).Value = (object?)a.FechaAnalisis ?? DBNull.Value;
        comando.Parameters.AddWithValue("@resultado", (object?)a.Resultado ?? DBNull.Value);
    }
}
