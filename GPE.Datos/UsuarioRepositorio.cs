using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Lo que hace falta para comprobar el login del escritorio
public record UsuarioLogin(Usuario Usuario, string? Clave, bool Activo);

public class UsuarioRepositorio
{
    // El usuario con ese nombre de usuario (sin distinguir mayúsculas), o null si no existe
    public UsuarioLogin? ObtenerPorUsuario(string usuario)
    {
        const string sql = "SELECT id, nombre, clave, activo FROM USUARIO WHERE usuario = @usuario;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@usuario", usuario);

        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return null;

        return new UsuarioLogin(
            new Usuario(lector.GetInt32(0), lector.GetString(1)),
            lector.IsDBNull(2) ? null : lector.GetString(2),
            lector.GetBoolean(3));
    }
}
