using System.Security.Claims;

namespace GPE.Web;

// Quién ha entrado en la web de los trabajadores (lo guarda la cookie al entrar, en /acciones/iniciar-sesion)
public static class TrabajadorActual
{
    // Su nombre en la lista PERSONA: el responsable de sus acciones
    public const string Persona = "gpe:persona";

    public static string? PersonaDe(ClaimsPrincipal usuario) =>
        usuario.Identity?.IsAuthenticated == true ? usuario.FindFirst(Persona)?.Value : null;
}
