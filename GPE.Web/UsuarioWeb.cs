using GPE.Datos;
using GPE.Logica;

namespace GPE.Web;

// La web no pide usuario: lo que se guarda desde ella queda a nombre del usuario de
// appsettings.json ("UsuarioWeb"), porque cada DeCA tiene que decir quién lo creó y lo cambió.
public class UsuarioWeb(IConfiguration configuracion)
{
    private Usuario? usuario;

    // Se lee la primera vez que hace falta; si la base de datos falla, se reintenta la siguiente
    public Usuario Actual
    {
        get
        {
            if (usuario != null) return usuario;

            int id = configuracion.GetValue("UsuarioWeb", 1);
            usuario = new DevolucionServicio().ObtenerUsuarios().FirstOrDefault(u => u.Id == id)
                ?? throw new ReglaNegocioException(
                    $"El usuario {id} de \"UsuarioWeb\" (appsettings.json) no existe en la tabla USUARIO.");
            return usuario;
        }
    }

    // using (usuarioWeb.Operacion()) { servicio.Guardar(...) }: lo de dentro queda a su nombre
    public IDisposable Operacion() => Sesion.Como(Actual);
}
