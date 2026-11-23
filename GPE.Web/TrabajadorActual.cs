using GPE.Datos;
using System.Security.Claims;

namespace GPE.Web;

// Quién ha entrado en la web (lo guarda la cookie al entrar, en /acciones/iniciar-sesion).
// Entran con su usuario del escritorio (tabla USUARIO) y sus roles deciden qué ven:
// Calidad, el control diario; los demás, sus acciones; Administración, todo (también el DeCA).
public static class TrabajadorActual
{
    public static bool HaEntrado(ClaimsPrincipal usuario) => usuario.Identity?.IsAuthenticated == true;

    // Para guardar a su nombre: using (Sesion.Como(TrabajadorActual.Usuario(...))) { ... }
    public static Usuario? Usuario(ClaimsPrincipal usuario) =>
        HaEntrado(usuario) && int.TryParse(usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id)
            ? new Usuario(id, usuario.Identity!.Name ?? "")
            : null;

    // Su nombre, que es el que se pone de responsable en las acciones (lista PERSONA)
    public static string? Nombre(ClaimsPrincipal usuario) => HaEntrado(usuario) ? usuario.Identity!.Name : null;

    public static bool VeControl(ClaimsPrincipal usuario) =>
        usuario.IsInRole(Rol.Calidad) || usuario.IsInRole(Rol.Administracion);

    // Todos menos quien solo es de Calidad
    public static bool VeAcciones(ClaimsPrincipal usuario)
    {
        var roles = usuario.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
        return HaEntrado(usuario) && (roles.Count == 0 || roles.Any(r => r != Rol.Calidad));
    }

    // El DeCA se abre sin entrar; en el menú solo le sale a Administración
    public static bool VeDeca(ClaimsPrincipal usuario) => usuario.IsInRole(Rol.Administracion);

    // Su página de inicio: la primera que puede ver
    public static string Inicio(ClaimsPrincipal usuario) => VeAcciones(usuario) ? "/acciones" : "/control";
}
