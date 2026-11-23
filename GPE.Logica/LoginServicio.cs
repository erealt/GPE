using GPE.Datos;

namespace GPE.Logica;

// Login del escritorio con usuario y contraseña
public class LoginServicio
{
    private readonly UsuarioRepositorio repositorio = new();

    // El usuario si existe, está activo y la contraseña es la suya; si no, null
    // (sin decir cuál de las cosas falla)
    public Usuario? Entrar(string usuario, string clave)
    {
        usuario = usuario.Trim();
        if (usuario == "" || clave == "") return null;

        var encontrado = repositorio.ObtenerPorUsuario(usuario);
        if (encontrado == null || !encontrado.Activo || encontrado.Clave == null
            || !Clave.Comprobar(clave, encontrado.Clave))
            return null;
        return encontrado.Usuario;
    }
}
