using GPE.Logica;
using GPE.Web;
using GPE.Web.Components;
using System.Globalization;

// Fechas y números como en España (1.234,50), igual que en el escritorio
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("es-ES");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("es-ES");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<UsuarioWeb>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// PDF y QR ya generados (los genera el botón del formulario). Se abren en el navegador para
// verlos o imprimirlos; al guardarlos conservan su nombre.
app.MapGet("/deca/{id:int}/pdf", (int id, HttpContext http) => Archivo(id, http, DecaServicio.NombreArchivoPdf, "application/pdf"));
app.MapGet("/deca/{id:int}/qr", (int id, HttpContext http) => Archivo(id, http, DecaServicio.NombreArchivoQr, "image/png"));

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
