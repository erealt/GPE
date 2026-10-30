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

    // Los controles de un día, en el orden en que se apuntaron
    public List<ControlAgua> ObtenerControles(DateTime fecha)
    {
        const string sql = @"
            SELECT c.id, c.fecha, c.toma_id, c.cloro, c.sabor_ok, c.olor_ok, c.color_ok, c.turbidez_ok,
                   c.comentario, u.nombre, c.version
            FROM AGUA_CONTROL c
            JOIN USUARIO u ON u.id = c.creado_por
            WHERE c.fecha = @fecha
            ORDER BY c.id;";

        var controles = new List<ControlAgua>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@fecha", fecha.Date);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            controles.Add(new ControlAgua(
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
                Version: (byte[])lector[10]));
        return controles;
    }

    // Guarda el día en una transacción. false si otro lo ha cambiado mientras tanto (no se guarda nada).
    public bool GuardarDia(IReadOnlyList<ControlAgua> cambiados, IReadOnlyList<ControlAgua> borrados, int usuarioId)
    {
        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        foreach (var control in borrados)
        {
            using var borrar = new SqlCommand(
                "DELETE FROM AGUA_CONTROL WHERE id = @id AND version = @version;", conexion, transaccion);
            borrar.Parameters.AddWithValue("@id", control.Id);
            borrar.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)control.Version ?? DBNull.Value;
            if (borrar.ExecuteNonQuery() == 0) return false;
        }

        foreach (var control in cambiados)
        {
            const string sqlInsertar = @"
                INSERT INTO AGUA_CONTROL (fecha, toma_id, cloro, sabor_ok, olor_ok, color_ok, turbidez_ok, comentario, creado_por)
                VALUES (@fecha, @toma, @cloro, @sabor, @olor, @color, @turbidez, @comentario, @usuario);";
            const string sqlActualizar = @"
                UPDATE AGUA_CONTROL SET toma_id = @toma, cloro = @cloro, sabor_ok = @sabor, olor_ok = @olor,
                    color_ok = @color, turbidez_ok = @turbidez, comentario = @comentario
                WHERE id = @id AND version = @version;";

            using var comando = new SqlCommand(control.Id == 0 ? sqlInsertar : sqlActualizar, conexion, transaccion);
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
            if (control.Id == 0)
            {
                comando.Parameters.AddWithValue("@fecha", control.Fecha.Date);
                comando.Parameters.AddWithValue("@usuario", usuarioId);
            }
            else
            {
                comando.Parameters.AddWithValue("@id", control.Id);
                comando.Parameters.Add("@version", System.Data.SqlDbType.Timestamp).Value = (object?)control.Version ?? DBNull.Value;
            }
            if (comando.ExecuteNonQuery() == 0) return false;
        }

        transaccion.Commit();
        return true;
    }
}
