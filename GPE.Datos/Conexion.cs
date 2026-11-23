using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Conexión a SQL Server común a todos los repositorios
internal static class Conexion
{
    // Por orden: la variable de entorno GPE_CONEXION (servidor y pruebas), el archivo conexion.txt junto
    // al programa (lo lleva la aplicación instalada con ClickOnce) y, si no hay ninguno, la base local
    private static readonly string CadenaConexion =
        Environment.GetEnvironmentVariable("GPE_CONEXION")
        ?? LeerArchivo()
        ?? "Server=localhost;Database=miGPE;Integrated Security=True;TrustServerCertificate=True;";

    private static string? LeerArchivo()
    {
        string ruta = Path.Combine(AppContext.BaseDirectory, "conexion.txt");
        if (!File.Exists(ruta)) return null;
        string texto = File.ReadAllText(ruta).Trim();
        return texto == "" ? null : texto;
    }

    // Los triggers de historial leen el usuario de SESSION_CONTEXT(N'usuario_id').
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
