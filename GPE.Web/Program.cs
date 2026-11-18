using GPE.Logica;
using GPE.Web;
using GPE.Web.Components;
using GPE.Datos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Data.Common;
using System.Globalization;
using System.Security.Claims;

// Fechas y números como en España (1.234,50), igual que en el escritorio
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("es-ES");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("es-ES");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<UsuarioWeb>();

// Web de los trabajadores (/acciones): entran con usuario y contraseña y la sesión se queda
// abierta en el móvil 90 días desde la última vez que la usan. El DeCA no pide nada.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "GPE.Trabajador";
        opciones.Cookie.SameSite = SameSiteMode.Strict;
        opciones.LoginPath = "/acciones/entrar";
        opciones.ExpireTimeSpan = TimeSpan.FromDays(90);
        opciones.SlidingExpiration = true;
    });
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// PDF y QR ya generados (los genera el botón del formulario). Se abren en el navegador para
// verlos o imprimirlos; al guardarlos conservan su nombre.
app.MapGet("/deca/{id:int}/pdf", (int id, HttpContext http) => Archivo(id, http, DecaServicio.NombreArchivoPdf, "application/pdf"));
app.MapGet("/deca/{id:int}/qr", (int id, HttpContext http) => Archivo(id, http, DecaServicio.NombreArchivoQr, "image/png"));

// Entrar y salir no pueden ser de Blazor: la cookie de la sesión solo se puede poner en una
// petición normal. Sin antiforgery: la cookie es SameSite=Strict y aquí no se cambia ningún dato.
app.MapPost("/acciones/iniciar-sesion", async (HttpContext http) =>
{
    var formulario = await http.Request.ReadFormAsync();
    string usuario = formulario["usuario"].ToString();

    TrabajadorWeb? trabajador;
    try
    {
        trabajador = new TrabajadorWebServicio().Entrar(usuario, formulario["clave"].ToString());
    }
    catch (DbException)
    {
        return Results.Redirect("/acciones/entrar?error=bd");
    }
    if (trabajador == null)
        return Results.Redirect("/acciones/entrar?error=clave&usuario=" + Uri.EscapeDataString(usuario.Trim()));

    var identidad = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, trabajador.Usuario), new Claim(TrabajadorActual.Persona, trabajador.Persona)],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(new ClaimsPrincipal(identidad), new AuthenticationProperties { IsPersistent = true });
    return Results.Redirect("/acciones");
}).DisableAntiforgery();

app.MapGet("/acciones/salir", async (HttpContext http) =>
{
    await http.SignOutAsync();
    return Results.Redirect("/acciones/entrar");
});

app.Run();

static IResult Archivo(int id, HttpContext http, Func<GPE.Datos.DecaGuardado, string?> nombre, string tipo)
{
    var deca = new DecaServicio().ObtenerDeca(id);
    if (deca == null) return Results.NotFound("Este DeCA no existe.");

    string? archivo = nombre(deca);
    string ruta = Path.Combine(DecaServicio.CarpetaPdf, archivo ?? "");
    if (archivo == null || !File.Exists(ruta))
        return Results.NotFound("Todavía no se ha generado. Pulsa \"Generar PDF y QR\" en el DeCA.");

    http.Response.Headers.ContentDisposition = $"inline; filename=\"{Path.GetFileName(ruta)}\"";
    return Results.File(ruta, tipo);
}
