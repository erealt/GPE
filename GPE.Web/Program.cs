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

// Web de los trabajadores (/acciones), control diario de calidad (/control) y almacén (/almacen): entran con su usuario y
// contraseña del escritorio y la sesión se queda abierta en el móvil 90 días desde la última vez que
// la usan. El DeCA no pide nada.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "GPE.Web";   // distinto del de TRABAJADOR_WEB: esas sesiones ya no valen
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
    string volver = Volver(formulario["volver"].ToString());
    string conVolver = volver == "/acciones" ? "" : "&volver=" + Uri.EscapeDataString(volver);

    Usuario? encontrado;
    List<string> roles = new();
    try
    {
        encontrado = new LoginServicio().Entrar(usuario, formulario["clave"].ToString());
        if (encontrado != null)
            roles = new DevolucionServicio().ObtenerRoles(encontrado.Id);
    }
    catch (DbException)
    {
        return Results.Redirect("/acciones/entrar?error=bd" + conVolver);
    }
    if (encontrado == null)
        return Results.Redirect("/acciones/entrar?error=clave&usuario=" + Uri.EscapeDataString(usuario.Trim()) + conVolver);

    var identidad = new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, encontrado.Id.ToString()), new Claim(ClaimTypes.Name, encontrado.Nombre),
         .. roles.Select(r => new Claim(ClaimTypes.Role, r))],
        CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identidad);

    // Transporte o sin rol: no hay ninguna página para ellos en la web
    if (!TrabajadorActual.VeAlgo(principal))
        return Results.Redirect("/acciones/entrar?error=sinweb&usuario=" + Uri.EscapeDataString(usuario.Trim()) + conVolver);

    await http.SignInAsync(principal, new AuthenticationProperties { IsPersistent = true });

    // Si iba a una página que no le toca, a la suya
    return Results.Redirect(TrabajadorActual.PuedeVer(principal, volver) ? volver : TrabajadorActual.Inicio(principal));
}).DisableAntiforgery();

// Recibo y fotos de una devolución, para verlos desde la web del almacén
app.MapGet("/almacen/archivo/{id:int}", (int id, HttpContext http) =>
{
    if (!TrabajadorActual.VeAlmacen(http.User)) return Results.Redirect("/acciones/entrar?volver=%2Falmacen");

    var archivo = new DevolucionServicio().ObtenerAdjunto(id);
    if (archivo == null) return Results.NotFound("Este archivo ya no existe.");

    var (nombre, contenido) = archivo.Value;
    string tipo = Path.GetExtension(nombre).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };
    // Sin nombre: uno con tildes no vale en la cabecera, y así se abre en el navegador
    return Results.File(contenido, tipo);
});

app.MapGet("/acciones/salir", async (HttpContext http, string? volver) =>
{
    await http.SignOutAsync();
    volver = Volver(volver);
    return Results.Redirect("/acciones/entrar" + (volver == "/acciones" ? "" : "?volver=" + Uri.EscapeDataString(volver)));
});

// Fotos de la ficha, para verlas desde "Mis acciones". Solo las de fichas en las que tiene alguna acción.
app.MapGet("/acciones/foto/{id:int}", (int id, HttpContext http) =>
{
    if (!TrabajadorActual.VeAcciones(http.User))
        return Results.Unauthorized();

    var foto = new TrabajadorWebServicio().ObtenerFoto(id, TrabajadorActual.Nombre(http.User)!);
    if (foto == null)
        return Results.NotFound("Esta foto no existe o no es de una de tus acciones.");

    string nombre = foto.Value.NombreArchivo;
    string tipo = Path.GetExtension(nombre).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };
    http.Response.Headers.CacheControl = "private, max-age=3600";
    return Results.File(foto.Value.Contenido, tipo);
});

app.Run();

// Página a la que se vuelve después de entrar: solo las de la web con login (nunca otra dirección)
static string Volver(string? volver) => volver is "/control" or "/agua" or "/almacen" ? volver : "/acciones";

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
