using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Web de los trabajadores (TRABAJADOR_WEB): sus acciones de NC/incidencias pendientes y marcarlas hechas.
// Las acciones son de una persona de la lista PERSONA (NC_ACCION.responsable), no de un usuario de GPE.
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

    public TrabajadorWeb? ObtenerPorUsuario(string usuario)
    {
        const string sql = "SELECT id, usuario, clave, persona, activo FROM TRABAJADOR_WEB WHERE usuario = @usuario;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@usuario", usuario);
        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return null;
        return new TrabajadorWeb(lector.GetInt32(0), lector.GetString(1), lector.GetString(2),
            lector.GetString(3), lector.GetBoolean(4));
    }

    public List<AccionTrabajador> ObtenerPendientes(string persona)
    {
        string sql = $@"
            SELECT a.id, n.numero, n.tipo_registro, n.problema, a.clase, a.accion, a.fecha, a.prioridad
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
                Prioridad: lector.IsDBNull(7) ? null : lector.GetString(7)));
        return acciones;
    }

    // Marca la acción como hecha y añade la nota al final de sus observaciones, que es lo que
    // calidad ve en la ficha. Solo si sigue pendiente y es de esa persona; si no, devuelve false.
    // También cambia la versión del registro: si calidad tiene la ficha abierta en el escritorio,
    // al guardar se le avisará de que ha cambiado, en vez de volver a dejar la acción sin hacer.
    public bool MarcarHecha(int accionId, string persona, string nota)
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
            END;

            COMMIT;
            SELECT @nc;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@id", accionId);
        comando.Parameters.AddWithValue("@persona", persona);
        comando.Parameters.AddWithValue("@nota", nota);
        return comando.ExecuteScalar() is int;
    }
}

public record TrabajadorWeb(int Id, string Usuario, string Clave, string Persona, bool Activo);

public record AccionTrabajador(
    int Id,
    string? NumeroNc,
    string TipoRegistro,
    string Problema,
    string Clase,
    string Accion,
    DateTime? Fecha,
    string? Prioridad);
