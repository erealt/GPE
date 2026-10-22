using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Cola de avisos por correo (tabla AVISO_CORREO)
public class AvisoRepositorio
{
    // Tras este número de fallos un aviso se deja de reintentar
    public const int MaximoIntentos = 5;

    // Apunta un aviso para cada usuario con el rol responsable de la fase indicada
    // en la fábrica de la devolución. Va dentro de la transacción del cambio que lo provoca.
    public static void Apuntar(SqlConnection conexion, SqlTransaction transaccion,
        int devolucionId, int fabricaId, string tipo, int faseResponsable)
    {
        const string sql = @"
            INSERT INTO AVISO_CORREO (devolucion_id, tipo, destinatario)
            SELECT DISTINCT @devolucion, @tipo, u.email
            FROM ESTADO_DEVOLUCION e
            JOIN USUARIO_ROL ur ON ur.rol_id = e.rol_responsable_id AND ur.fabrica_id = @fabrica
            JOIN USUARIO u ON u.id = ur.usuario_id
            WHERE e.fase = @fase;";

        using var comando = new SqlCommand(sql, conexion, transaccion);
        comando.Parameters.AddWithValue("@devolucion", devolucionId);
        comando.Parameters.AddWithValue("@tipo", tipo);
        comando.Parameters.AddWithValue("@fabrica", fabricaId);
        comando.Parameters.AddWithValue("@fase", faseResponsable);
        comando.ExecuteNonQuery();
    }

    // Reserva los avisos pendientes durante unos minutos para que otro ordenador
    // que esté enviando a la vez no los mande también, y los devuelve.
    public List<AvisoPendiente> ReservarPendientes()
    {
        const string sql = @"
            UPDATE AVISO_CORREO
            SET bloqueado_hasta = DATEADD(MINUTE, 5, SYSDATETIME())
            OUTPUT inserted.id
            WHERE enviado_en IS NULL
              AND intentos < @maximo
              AND (bloqueado_hasta IS NULL OR bloqueado_hasta < SYSDATETIME());";

        const string sqlDatos = @"
            SELECT a.id, a.tipo, a.destinatario, d.numero, c.nombre, c.codigo,
                   d.motivo, d.fecha_solicitud, d.observaciones,
                   t.nombre, r.fecha_recogida, r.direccion
            FROM AVISO_CORREO a
            JOIN DEVOLUCION d ON d.id = a.devolucion_id
            JOIN CLIENTE c ON c.id = d.cliente_id
            LEFT JOIN RECOGIDA r ON r.devolucion_id = d.id
            LEFT JOIN TRANSPORTISTA t ON t.id = r.transportista_id
            WHERE a.id IN (SELECT value FROM STRING_SPLIT(@ids, ','))
            ORDER BY a.id;";

        using var conexion = Conexion.Abrir();

        var ids = new List<int>();
        using (var reserva = new SqlCommand(sql, conexion))
        {
            reserva.Parameters.AddWithValue("@maximo", MaximoIntentos);
            using var lector = reserva.ExecuteReader();
            while (lector.Read())
                ids.Add(lector.GetInt32(0));
        }
        if (ids.Count == 0) return new List<AvisoPendiente>();

        var avisos = new List<AvisoPendiente>();
        using var comando = new SqlCommand(sqlDatos, conexion);
        comando.Parameters.AddWithValue("@ids", string.Join(",", ids));

        using var datos = comando.ExecuteReader();
        while (datos.Read())
        {
            avisos.Add(new AvisoPendiente(
                Id: datos.GetInt32(0),
                Tipo: datos.GetString(1),
                Destinatario: datos.GetString(2),
                NumeroDevolucion: datos.GetString(3),
                Cliente: datos.GetString(4),
                CodigoCliente: datos.GetString(5),
                Motivo: datos.GetString(6),
                FechaSolicitud: datos.GetDateTime(7),
                Observaciones: datos.IsDBNull(8) ? null : datos.GetString(8),
                Transportista: datos.IsDBNull(9) ? null : datos.GetString(9),
                FechaRecogidaPrevista: datos.IsDBNull(10) ? null : datos.GetDateTime(10),
                DireccionRecogida: datos.IsDBNull(11) ? null : datos.GetString(11)));
        }
        return avisos;
    }

    public void MarcarEnviado(int avisoId)
    {
        const string sql = @"
            UPDATE AVISO_CORREO
            SET enviado_en = SYSDATETIME(), intentos = intentos + 1,
                ultimo_error = NULL, bloqueado_hasta = NULL
            WHERE id = @aviso;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@aviso", avisoId);
        comando.ExecuteNonQuery();
    }

    // Se libera para que se reintente la próxima vez
    public void MarcarFallo(int avisoId, string error)
    {
        const string sql = @"
            UPDATE AVISO_CORREO
            SET intentos = intentos + 1, ultimo_error = @error, bloqueado_hasta = NULL
            WHERE id = @aviso;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@aviso", avisoId);
        comando.Parameters.AddWithValue("@error", error.Length > 1000 ? error[..1000] : error);
        comando.ExecuteNonQuery();
    }
}
