using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Avisos a calidad de las acciones que los trabajadores han marcado hechas desde la web.
// Cada usuaria los ve por su cuenta: se dan por vistos al pulsar "Ver" o la ✕ del aviso.
public class AccionHechaWebRepositorio
{
    // Los que ese usuario no ha visto todavía, del más antiguo al más nuevo.
    // Solo los del último mes: así a un usuario nuevo de calidad no le salen todos los antiguos.
    public List<AccionHechaWeb> ObtenerSinVer(int usuarioId, int dias)
    {
        const string sql = @"
            SELECT w.id, w.persona, w.comentario, w.hecha_en,
                   a.clase, a.accion, n.id, n.numero, n.tipo_registro
            FROM NC_ACCION_HECHA_WEB w
            JOIN NC_ACCION a ON a.id = w.accion_id
            JOIN NO_CONFORMIDAD n ON n.id = a.no_conformidad_id
            WHERE w.hecha_en >= DATEADD(DAY, -@dias, CAST(GETDATE() AS DATE))
              AND NOT EXISTS (SELECT 1 FROM NC_ACCION_HECHA_VISTO v
                              WHERE v.aviso_id = w.id AND v.usuario_id = @usuario)
            ORDER BY w.hecha_en, w.id;";

        var avisos = new List<AccionHechaWeb>();
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@usuario", usuarioId);
        comando.Parameters.AddWithValue("@dias", dias);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            avisos.Add(new AccionHechaWeb(
                Id: lector.GetInt32(0),
                Persona: lector.GetString(1),
                Comentario: lector.IsDBNull(2) ? null : lector.GetString(2),
                HechaEn: lector.GetDateTime(3),
                Clase: lector.GetString(4),
                Accion: lector.GetString(5),
                NoConformidadId: lector.GetInt32(6),
                NumeroNc: lector.IsDBNull(7) ? null : lector.GetString(7),
                TipoRegistro: lector.GetString(8)));
        return avisos;
    }

    public void MarcarVistos(int usuarioId, IEnumerable<int> avisos)
    {
        const string sql = @"
            INSERT INTO NC_ACCION_HECHA_VISTO (aviso_id, usuario_id)
            SELECT @aviso, @usuario
            WHERE EXISTS (SELECT 1 FROM NC_ACCION_HECHA_WEB WHERE id = @aviso)
              AND NOT EXISTS (SELECT 1 FROM NC_ACCION_HECHA_VISTO WHERE aviso_id = @aviso AND usuario_id = @usuario);";

        using var conexion = Conexion.Abrir();
        foreach (int aviso in avisos)
        {
            using var comando = new SqlCommand(sql, conexion);
            comando.Parameters.AddWithValue("@aviso", aviso);
            comando.Parameters.AddWithValue("@usuario", usuarioId);
            comando.ExecuteNonQuery();
        }
    }
}

public record AccionHechaWeb(
    int Id,
    string Persona,
    string? Comentario,
    DateTime HechaEn,
    string Clase,
    string Accion,
    int NoConformidadId,
    string? NumeroNc,
    string TipoRegistro)
{
    // "No conformidad 558", "Incidencia 840"
    public string Registro => $"{GPE.Datos.TipoRegistro.Nombre(TipoRegistro)} {NumeroNc ?? "(sin número)"}";

    public string NombreClase => Clase == "INMEDIATA" ? "acción inmediata" : "acción correctora";
}
