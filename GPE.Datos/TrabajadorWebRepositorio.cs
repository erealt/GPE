using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Web de los trabajadores: sus acciones de NC/incidencias pendientes y marcarlas hechas.
// Las acciones son de una persona de la lista PERSONA (NC_ACCION.responsable); en la web se busca
// por el nombre del usuario que ha entrado (USUARIO.nombre).
public class TrabajadorWebRepositorio
{
    // Las mismas que reciben el correo: inmediatas y correctoras sin hacer, de registros sin cerrar
    private const string Pendientes = @"
        FROM NC_ACCION a
        JOIN NO_CONFORMIDAD n ON n.id = a.no_conformidad_id
        WHERE a.responsable = @persona
          AND a.hecha = 0
          AND a.clase IN (N'INMEDIATA', N'CORRECTORA')
          AND n.fecha_fin IS NULL";

    public List<AccionTrabajador> ObtenerPendientes(string persona)
    {
        string sql = $@"
            SELECT a.id, n.numero, n.tipo_registro, n.problema, a.clase, a.accion, a.fecha, a.prioridad,
                   n.id, n.fecha_detectado, n.seccion, n.descripcion_causa
            {Pendientes}
            ORDER BY CASE a.prioridad WHEN N'Alta' THEN 1 WHEN N'Media' THEN 2 WHEN N'Baja' THEN 3 ELSE 4 END,
                     CASE WHEN a.fecha IS NULL THEN 1 ELSE 0 END, a.fecha, a.id;";

        var acciones = new List<AccionTrabajador>();
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@persona", persona);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            acciones.Add(new AccionTrabajador(
                Id: lector.GetInt32(0),
                NumeroNc: lector.IsDBNull(1) ? null : lector.GetString(1),
                TipoRegistro: lector.GetString(2),
                Problema: lector.GetString(3),
                Clase: lector.GetString(4),
                Accion: lector.GetString(5),
                Fecha: lector.IsDBNull(6) ? null : lector.GetDateTime(6),
                Prioridad: lector.IsDBNull(7) ? null : lector.GetString(7),
                NoConformidadId: lector.GetInt32(8),
                FechaDetectado: lector.GetDateTime(9),
                Seccion: lector.IsDBNull(10) ? null : lector.GetString(10),
                Causa: lector.IsDBNull(11) ? null : lector.GetString(11)));
        lector.Close();

        // Las fotos de esas fichas (solo el id: cada foto se abre con /acciones/foto/{id})
        string sqlFotos = $@"
            SELECT f.id, f.no_conformidad_id FROM NC_FOTO f
            WHERE f.no_conformidad_id IN (SELECT a.no_conformidad_id {Pendientes})
            ORDER BY f.subido_en, f.id;";
        using var comandoFotos = new SqlCommand(sqlFotos, conexion);
        comandoFotos.Parameters.AddWithValue("@persona", persona);
        using var lectorFotos = comandoFotos.ExecuteReader();
        while (lectorFotos.Read())
        {
            int nc = lectorFotos.GetInt32(1);
            foreach (var a in acciones.Where(a => a.NoConformidadId == nc))
                a.Fotos.Add(lectorFotos.GetInt32(0));
        }
        return acciones;
    }

    // Las que ha hecho en los últimos días (marcadas en la web o por calidad en el escritorio).
    // La fecha en que se marcó hecha sale del historial: las que ya venían hechas del Excel no salen.
    public List<AccionHecha> ObtenerHechas(string persona, int dias)
    {
        const string sql = @"
            SELECT a.id, n.numero, n.tipo_registro, n.problema, a.clase, a.accion, h.cuando
            FROM NC_ACCION a
            JOIN NO_CONFORMIDAD n ON n.id = a.no_conformidad_id
            CROSS APPLY (
                SELECT MAX(hc.fecha) AS cuando
                FROM HISTORIAL_CAMBIOS hc
                WHERE hc.tabla_afectada = 'NC_ACCION'
                  AND hc.registro_id = a.id
                  AND JSON_VALUE(hc.valores_nuevos, '$.hecha') = 'true'
                  AND ISNULL(JSON_VALUE(hc.valores_anteriores, '$.hecha'), 'false') = 'false') h
            WHERE a.responsable = @persona
              AND a.hecha = 1
              AND a.clase IN (N'INMEDIATA', N'CORRECTORA')
              AND h.cuando >= DATEADD(DAY, -@dias, CAST(GETDATE() AS DATE))
            ORDER BY h.cuando DESC, a.id DESC;";

        var hechas = new List<AccionHecha>();
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@persona", persona);
        comando.Parameters.AddWithValue("@dias", dias);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            hechas.Add(new AccionHecha(
                Id: lector.GetInt32(0),
                NumeroNc: lector.IsDBNull(1) ? null : lector.GetString(1),
                TipoRegistro: lector.GetString(2),
                Problema: lector.GetString(3),
                Clase: lector.GetString(4),
                Accion: lector.GetString(5),
                HechaEl: lector.GetDateTime(6)));
        return hechas;
    }

    // Una foto, solo si es de una ficha en la que esa persona tiene alguna acción (hecha o no).
    // Si no, null: así no se pueden ver las de otras fichas cambiando el número en la dirección.
    public (string NombreArchivo, byte[] Contenido)? ObtenerFoto(int fotoId, string persona)
    {
        const string sql = @"
            SELECT f.nombre_archivo, f.contenido
            FROM NC_FOTO f
            WHERE f.id = @id
              AND EXISTS (SELECT 1 FROM NC_ACCION a
                          WHERE a.no_conformidad_id = f.no_conformidad_id AND a.responsable = @persona);";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@id", fotoId);
        comando.Parameters.AddWithValue("@persona", persona);
        using var lector = comando.ExecuteReader();
        return lector.Read() ? (lector.GetString(0), (byte[])lector[1]) : null;
    }

    // Marca la acción como hecha y añade la nota al final de sus observaciones, que es lo que
    // calidad ve en la ficha. Solo si sigue pendiente y es de esa persona; si no, devuelve false.
    // También cambia la versión del registro: si calidad tiene la ficha abierta en el escritorio,
    // al guardar se le avisará de que ha cambiado, en vez de volver a dejar la acción sin hacer.
    // Y deja el aviso para calidad (NC_ACCION_HECHA_WEB), que le sale en el programa de escritorio.
    public bool MarcarHecha(int accionId, string persona, string nota, string? comentario)
    {
        // UPDLOCK: si se pulsa dos veces, la segunda espera y ya no la encuentra pendiente.
        // problema = problema no cambia ningún dato (el historial no apunta nada), solo la versión.
        const string sql = @"
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            -- Las mismas condiciones que Pendientes
            DECLARE @nc INT = (
                SELECT a.no_conformidad_id
                FROM NC_ACCION a WITH (UPDLOCK)
                JOIN NO_CONFORMIDAD n ON n.id = a.no_conformidad_id
                WHERE a.id = @id
                  AND a.responsable = @persona
                  AND a.hecha = 0
                  AND a.clase IN (N'INMEDIATA', N'CORRECTORA')
                  AND n.fecha_fin IS NULL);

            IF @nc IS NOT NULL
            BEGIN
                UPDATE NC_ACCION SET
                    hecha = 1,
                    observaciones = CASE WHEN NULLIF(LTRIM(RTRIM(observaciones)), N'') IS NULL THEN @nota
                                         ELSE observaciones + NCHAR(13) + NCHAR(10) + @nota END
                WHERE id = @id;

                UPDATE NO_CONFORMIDAD SET problema = problema WHERE id = @nc;

                INSERT INTO NC_ACCION_HECHA_WEB (accion_id, persona, comentario)
                VALUES (@id, @persona, NULLIF(@comentario, N''));
            END;

            COMMIT;
            SELECT @nc;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@id", accionId);
        comando.Parameters.AddWithValue("@persona", persona);
        comando.Parameters.AddWithValue("@nota", nota);
        comando.Parameters.AddWithValue("@comentario", (object?)comentario ?? DBNull.Value);
        return comando.ExecuteScalar() is int;
    }
}

public record AccionTrabajador(
    int Id,
    string? NumeroNc,
    string TipoRegistro,
    string Problema,
    string Clase,
    string Accion,
    DateTime? Fecha,
    string? Prioridad,
    int NoConformidadId,
    DateTime FechaDetectado,
    string? Seccion,
    string? Causa)
{
    public List<int> Fotos { get; } = new();
}

public record AccionHecha(
    int Id,
    string? NumeroNc,
    string TipoRegistro,
    string Problema,
    string Clase,
    string Accion,
    DateTime HechaEl);
