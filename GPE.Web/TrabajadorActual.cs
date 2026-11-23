using GPE.Datos;
using System.Security.Claims;

namespace GPE.Web;

// Quién ha entrado en la web (lo guarda la cookie al entrar, en /acciones/iniciar-sesion).
// Entran con su usuario del escritorio (tabla USUARIO) y sus roles deciden qué ven:
// Trabajador, sus acciones; Calidad, el control diario y el de agua; Administración, todo (también
// el DeCA). Los demás (Almacén, Transporte, sin rol) no tienen nada en la web y no pueden entrar.
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

    // "Mis acciones" es para los trabajadores
    public static bool VeAcciones(ClaimsPrincipal usuario) =>
        usuario.IsInRole(Rol.Trabajador) || usuario.IsInRole(Rol.Administracion);

    // Si no puede ver ninguna página, no se le deja entrar
    public static bool VeAlgo(ClaimsPrincipal usuario) => VeAcciones(usuario) || VeControl(usuario);

    // El DeCA se abre sin entrar; en el menú solo le sale a Administración
    public static bool VeDeca(ClaimsPrincipal usuario) => usuario.IsInRole(Rol.Administracion);

    // Su página de inicio: la primera que puede ver
    public static string Inicio(ClaimsPrincipal usuario) => VeAcciones(usuario) ? "/acciones" : "/control";
}
