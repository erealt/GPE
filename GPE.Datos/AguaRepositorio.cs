using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Control de agua de calidad (tablas AGUA_TOMA y AGUA_CONTROL)
public class AguaRepositorio
{
    public List<AguaToma> ObtenerTomas()
    {
        const string sql = @"
            SELECT id, nombre, cloro_min, cloro_max
            FROM AGUA_TOMA WHERE activo = 1 ORDER BY orden, id;";

        var tomas = new List<AguaToma>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            tomas.Add(new AguaToma(
                Id: lector.GetInt32(0),
                Nombre: lector.GetString(1),
                CloroMin: lector.IsDBNull(2) ? null : lector.GetDecimal(2),
                CloroMax: lector.IsDBNull(3) ? null : lector.GetDecimal(3)));
        return tomas;
    }

    // Cuántas tomas hay apuntadas ese día
    public int ContarControles(DateTime fecha)
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand("SELECT COUNT(*) FROM AGUA_CONTROL WHERE fecha = @fecha;", conexion);
        comando.Parameters.AddWithValue("@fecha", fecha.Date);
        return (int)comando.ExecuteScalar();
    }

    // Los controles entre dos días (ambos incluidos), por fecha, hora y en el orden en que se apuntaron.
    // Trae el punto aunque esté dado de baja, para que no desaparezcan controles antiguos.
    public List<ControlAguaConToma> ObtenerControles(DateTime desde, DateTime hasta)
    {
        const string sql = @"
            SELECT c.id, c.fecha, c.toma_id, c.cloro, c.sabor_ok, c.olor_ok, c.color_ok, c.turbidez_ok,
                   c.comentario, u.nombre, c.version, c.hora, c.repite_id, t.nombre, t.cloro_min, t.cloro_max
            FROM AGUA_CONTROL c
            JOIN USUARIO u ON u.id = c.creado_por
            JOIN AGUA_TOMA t ON t.id = c.toma_id
            WHERE c.fecha BETWEEN @desde AND @hasta
            ORDER BY c.fecha, c.hora, c.id;";

        var controles = new List<ControlAguaConToma>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@desde", desde.Date);
        comando.Parameters.AddWithValue("@hasta", hasta.Date);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            var control = new ControlAgua(
                Id: lector.GetInt32(0),
                Fecha: lector.GetDateTime(1),
                TomaId: lector.GetInt32(2),
                Cloro: lector.IsDBNull(3) ? null : lector.GetDecimal(3),
                SaborOk: lector.GetBoolean(4),
                OlorOk: lector.GetBoolean(5),
                ColorOk: lector.GetBoolean(6),
                TurbidezOk: lector.GetBoolean(7),
                Comentario: lector.IsDBNull(8) ? null : lector.GetString(8),
                CreadoPor: lector.GetString(9),
                Version: (byte[])lector[10],
                Hora: lector.IsDBNull(11) ? null : lector.GetTimeSpan(11),
                RepiteId: lector.IsDBNull(12) ? null : lector.GetInt32(12));
            controles.Add(new ControlAguaConToma(control, new AguaToma(
                Id: control.TomaId,
                Nombre: lector.GetString(13),
                CloroMin: lector.IsDBNull(14) ? null : lector.GetDecimal(14),
                CloroMax: lector.IsDBNull(15) ? null : lector.GetDecimal(15))));
        }
        return controles;
    }

    // Una toma nueva; devuelve su id
    public int Insertar(ControlAgua control, int usuarioId)
    {
        const string sql = @"
            INSERT INTO AGUA_CONTROL (fecha, hora, toma_id, cloro, sabor_ok, olor_ok, color_ok, turbidez_ok, comentario, repite_id, creado_por)
            VALUES (@fecha, @hora, @toma, @cloro, @sabor, @olor, @color, @turbidez, @comentario, @repite, @usuario);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        Parametros(comando, control);
        comando.Parameters.AddWithValue("@repite", (object?)control.RepiteId ?? DBNull.Value);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        return (int)comando.ExecuteScalar();
    }

    // false si otro la ha cambiado o borrado desde que se leyó (no se guarda nada)
    public bool Actualizar(ControlAgua control)
    {
        const string sql = @"
            UPDATE AGUA_CONTROL SET fecha = @fecha, hora = @hora, toma_id = @toma, cloro = @cloro, sabor_ok = @sabor,
                olor_ok = @olor, color_ok = @color, turbidez_ok = @turbidez, comentario = @comentario
            WHERE id = @id AND version = @version;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        Parametros(comando, control);
        comando.Parameters.AddWithValue("@id", control.Id);
        comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)control.Version ?? DBNull.Value;
        return comando.ExecuteNonQuery() > 0;
    }

    // Si alguna toma repetía esta, pasa a repetir la que repetía esta (o ninguna).
    // false si otro la ha cambiado o borrado desde que se leyó (no se borra nada).
    public bool Borrar(ControlAgua control)
    {
        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        using (var enlazar = new SqlCommand(
            "UPDATE AGUA_CONTROL SET repite_id = @repite WHERE repite_id = @id;", conexion, transaccion))
        {
            enlazar.Parameters.AddWithValue("@id", control.Id);
            enlazar.Parameters.AddWithValue("@repite", (object?)control.RepiteId ?? DBNull.Value);
            enlazar.ExecuteNonQuery();
        }

        using var borrar = new SqlCommand(
            "DELETE FROM AGUA_CONTROL WHERE id = @id AND version = @version;", conexion, transaccion);
        borrar.Parameters.AddWithValue("@id", control.Id);
        borrar.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)control.Version ?? DBNull.Value;
        if (borrar.ExecuteNonQuery() == 0) return false;   // se deshace también el UPDATE

        transaccion.Commit();
        return true;
    }

    private static void Parametros(SqlCommand comando, ControlAgua control)
    {
        comando.Parameters.AddWithValue("@fecha", control.Fecha.Date);
        comando.Parameters.Add("@hora", System.Data.SqlDbType.Time).Value = (object?)control.Hora ?? DBNull.Value;
        comando.Parameters.AddWithValue("@toma", control.TomaId);
        var cloro = comando.Parameters.Add("@cloro", System.Data.SqlDbType.Decimal);
        cloro.Precision = 4;
        cloro.Scale = 2;
        cloro.Value = (object?)control.Cloro ?? DBNull.Value;
        comando.Parameters.AddWithValue("@sabor", control.SaborOk);
        comando.Parameters.AddWithValue("@olor", control.OlorOk);
        comando.Parameters.AddWithValue("@color", control.ColorOk);
        comando.Parameters.AddWithValue("@turbidez", control.TurbidezOk);
        comando.Parameters.AddWithValue("@comentario", (object?)control.Comentario ?? DBNull.Value);
    }
}
