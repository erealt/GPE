using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Conexión a SQL Server común a todos los repositorios
internal static class Conexion
{
    // La variable de entorno GPE_CONEXION permite usar otra base de datos (p. ej. una de pruebas)
    private static readonly string CadenaConexion =
        Environment.GetEnvironmentVariable("GPE_CONEXION")
        ?? "Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;";

    // Abre la conexión e indica a SQL Server qué usuario de la aplicación la usa;
    // los triggers de historial lo leen con SESSION_CONTEXT(N'usuario_id').
    public static SqlConnection Abrir()
    {
        var conexion = new SqlConnection(CadenaConexion);
        conexion.Open();

        if (Sesion.UsuarioActual != null)
        {
            using var comando = new SqlCommand(
                "EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;", conexion);
            comando.Parameters.AddWithValue("@usuario", Sesion.UsuarioActual.Id);
            comando.ExecuteNonQuery();
        }

        return conexion;
    }
}
