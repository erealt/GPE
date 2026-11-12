using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Correos a los responsables de las acciones de calidad (NC_ACCION_AVISO y AVISO_RESUMEN).
// Solo inmediatas y correctoras sin hacer, de registros sin cerrar. El correo sale de CALIDAD_LISTA (PERSONA).
public class AvisoAccionRepositorio
{
    private const string AccionesAbiertas = @"
        FROM NC_ACCION a
        JOIN NO_CONFORMIDAD n ON n.id = a.no_conformidad_id
        OUTER APPLY (
            SELECT TOP 1 NULLIF(LTRIM(RTRIM(p.email)), N'') AS email
            FROM CALIDAD_LISTA p
            WHERE p.lista = N'PERSONA' AND p.valor = a.responsable AND NULLIF(LTRIM(RTRIM(p.email)), N'') IS NOT NULL
            ORDER BY p.activo DESC, p.id
        ) persona
        WHERE a.responsable IS NOT NULL
          AND a.hecha = 0
          AND a.clase IN (N'INMEDIATA', N'CORRECTORA')
          AND n.fecha_fin IS NULL";

    private const string Columnas = @"
        a.id, persona.email, a.responsable, n.numero, n.tipo_registro, n.problema, a.clase, a.accion, a.fecha, a.prioridad,
        a.no_conformidad_id, n.mc_pcc_bpm, n.parametro, n.operario";

    // Apunta como avisadas las acciones con un responsable nuevo (o cambiado) y devuelve las que hay
    // que enviar. Las de personas sin correo se apuntan igual y no se devuelven: no se les envía nada.
    // HOLDLOCK: si dos equipos lo hacen a la vez, cada acción solo la reserva uno.
    public List<AccionPendienteAviso> ReservarAsignaciones()
    {
        string sqlReservar = $@"
            MERGE NC_ACCION_AVISO WITH (HOLDLOCK) AS av
            USING (SELECT a.id, a.responsable, persona.email {AccionesAbiertas}) AS nueva
            ON av.accion_id = nueva.id
            WHEN MATCHED AND av.responsable <> nueva.responsable THEN
                UPDATE SET responsable = nueva.responsable, email = nueva.email, avisado_en = SYSDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (accion_id, responsable, email) VALUES (nueva.id, nueva.responsable, nueva.email)
            OUTPUT inserted.accion_id, inserted.email;";

        using var conexion = Conexion.Abrir();

        var ids = new List<int>();
        using (var reservar = new SqlCommand(sqlReservar, conexion))
        using (var lector = reservar.ExecuteReader())
            while (lector.Read())
                if (!lector.IsDBNull(1)) ids.Add(lector.GetInt32(0));
        if (ids.Count == 0) return new();

        string sqlDatos = $@"
            SELECT {Columnas} {AccionesAbiertas}
              AND a.id IN (SELECT value FROM STRING_SPLIT(@ids, ','));";
        using var datos = new SqlCommand(sqlDatos, conexion);
        datos.Parameters.AddWithValue("@ids", string.Join(",", ids));
        return Leer(datos).Where(a => a.Destinatario != "").ToList();
    }

    // Si el correo no ha salido, se quita la marca para que se vuelva a intentar la próxima vez
    public void QuitarAviso(int accionId)
    {
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand("DELETE FROM NC_ACCION_AVISO WHERE accion_id = @id;", conexion);
        comando.Parameters.AddWithValue("@id", accionId);
        comando.ExecuteNonQuery();
    }

    // Todas las acciones abiertas de las personas con correo, salvo de quien ya recibió hoy el resumen
    public List<AccionPendienteAviso> ObtenerPendientesResumen(string tipoResumen)
    {
        string sql = $@"
            SELECT {Columnas} {AccionesAbiertas}
              AND persona.email IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM AVISO_RESUMEN ar
                  WHERE ar.fecha = CAST(SYSDATETIME() AS DATE) AND ar.tipo = @tipo AND ar.destinatario = persona.email);";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@tipo", tipoResumen);
        return Leer(comando);
    }

    // Las fotos de esos registros, para adjuntarlas al correo, en el orden en que se subieron
    public List<ArchivoNuevo> ObtenerFotos(IEnumerable<int> noConformidadIds)
    {
        const string sql = @"
            SELECT nombre_archivo, contenido FROM NC_FOTO
            WHERE no_conformidad_id IN (SELECT value FROM STRING_SPLIT(@ids, ','))
            ORDER BY no_conformidad_id, subido_en, id;";

        var fotos = new List<ArchivoNuevo>();
        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@ids", string.Join(",", noConformidadIds.Distinct()));
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            fotos.Add(new ArchivoNuevo(lector.GetString(0), (byte[])lector[1]));
        return fotos;
    }

    private static List<AccionPendienteAviso> Leer(SqlCommand comando)
    {
        var acciones = new List<AccionPendienteAviso>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            acciones.Add(new AccionPendienteAviso(
                AccionId: lector.GetInt32(0),
                NoConformidadId: lector.GetInt32(10),
                Destinatario: lector.IsDBNull(1) ? "" : lector.GetString(1),
                Responsable: lector.GetString(2),
                NumeroNc: lector.IsDBNull(3) ? null : lector.GetString(3),
                TipoRegistro: lector.GetString(4),
                Problema: lector.GetString(5),
                Clase: lector.GetString(6),
                Accion: lector.GetString(7),
                Fecha: lector.IsDBNull(8) ? null : lector.GetDateTime(8),
                Prioridad: lector.IsDBNull(9) ? null : lector.GetString(9),
                McPccBpm: lector.IsDBNull(11) ? null : lector.GetString(11),
                Parametro: lector.IsDBNull(12) ? null : lector.GetString(12),
                Operario: lector.IsDBNull(13) ? null : lector.GetString(13)));
        return acciones;
    }
}
