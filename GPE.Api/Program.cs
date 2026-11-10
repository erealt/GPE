// API de los avisos al mecánico: la pantalla Máquina da el aviso y la aplicación del móvil lo recoge.
// PRUEBA: los avisos se guardan en memoria (se pierden al cerrar la API), como el resto de las pantallas de máquinas.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<Avisos>();

var app = builder.Build();

// Escucha en toda la red para que entren los móviles por wifi (no solo este ordenador)
app.Urls.Add(Environment.GetEnvironmentVariable("GPE_API_URL") ?? "http://0.0.0.0:5080");

// La página de los mecánicos (wwwroot/index.html), para abrirla en el navegador de cualquier móvil
// Sin caché: si no, el móvil sigue enseñando la versión anterior de la página
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache, no-store"
});

// Los de hoy, el más antiguo primero
app.MapGet("/avisos", (Avisos avisos) => avisos.DeHoy());

app.MapPost("/avisos", (NuevoAviso nuevo, Avisos avisos) => avisos.Crear(nuevo));

// El motivo y el comentario se pueden poner en la máquina después de avisar
app.MapPost("/avisos/{id:int}/datos", (int id, DatosAviso datos, Avisos avisos) =>
    avisos.Cambiar(id, a => { a.Motivo = datos.Motivo; a.Comentario = datos.Comentario; }));

// Desde el móvil llega con el nombre del mecánico; desde la pantalla Máquina, sin él.
// Un mecánico no puede coger otro aviso hasta resolver el que tiene.
app.MapPost("/avisos/{id:int}/llegada", (int id, Llegada llegada, Avisos avisos) =>
    avisos.Llegar(id, Limpio(llegada.Mecanico)));

// Resuelto desde el móvil (con lo que ha hecho) o porque la máquina ha vuelto a arrancar (sin informe todavía)
app.MapPost("/avisos/{id:int}/resuelto", (int id, Informe informe, Avisos avisos) =>
    avisos.Cambiar(id, a =>
    {
        a.Resuelto ??= DateTime.Now;
        if (Limpio(informe.QueHaHecho) is string hecho)
        {
            a.QueHaHecho = hecho;
            a.ResueltoPor = Limpio(informe.Mecanico) ?? a.ResueltoPor;
        }
    }));

app.Run();

static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

public record NuevoAviso(string Maquina, string? Motivo, string? Comentario, string? Maquinista);
public record DatosAviso(string? Motivo, string? Comentario);
public record Llegada(string? Mecanico);
public record Informe(string? QueHaHecho, string? Mecanico);

public class Aviso
{
    public int Id { get; set; }
    public string Maquina { get; set; } = "";
    public string? Motivo { get; set; }
    public string? Comentario { get; set; }
    public string? Maquinista { get; set; }
    public DateTime Avisado { get; set; }
    public DateTime? Llegada { get; set; }
    public string? Mecanico { get; set; }       // el que pulsó "He llegado"
    public DateTime? Resuelto { get; set; }
    public string? QueHaHecho { get; set; }
    public string? ResueltoPor { get; set; }    // el que escribió el informe
}

public class Avisos
{
    private readonly List<Aviso> lista = new();
    private readonly Lock cerrojo = new();

    public List<Aviso> DeHoy()
    {
        lock (cerrojo) return lista.Where(a => a.Avisado.Date == DateTime.Today).OrderBy(a => a.Avisado).ToList();
    }

    // Una máquina solo tiene un aviso abierto: si llega otro (se ha enviado dos veces), se devuelve el que ya hay
    public Aviso Crear(NuevoAviso nuevo)
    {
        lock (cerrojo)
        {
            if (lista.FirstOrDefault(a => a.Maquina == nuevo.Maquina && a.Resuelto == null) is Aviso abierto)
                return abierto;

            var aviso = new Aviso
            {
                Id = lista.Count + 1,
                Maquina = nuevo.Maquina,
                Motivo = nuevo.Motivo,
                Comentario = nuevo.Comentario,
                Maquinista = nuevo.Maquinista,
                Avisado = DateTime.Now
            };
            lista.Add(aviso);
            return aviso;
        }
    }

    public IResult Llegar(int id, string? mecanico)
    {
        lock (cerrojo)
        {
            if (lista.FirstOrDefault(a => a.Id == id) is not Aviso aviso) return Results.NotFound();
            if (aviso.Llegada != null) return Results.Ok(aviso);

            if (mecanico != null && lista.FirstOrDefault(a => a.Id != id && a.Llegada != null && a.Resuelto == null &&
                    string.Equals(a.Mecanico, mecanico, StringComparison.OrdinalIgnoreCase)) is Aviso otro)
                return Results.Conflict($"Ya estás en la {otro.Maquina}. Márcala como resuelta antes de coger otro aviso.");

            aviso.Llegada = DateTime.Now;
            aviso.Mecanico = mecanico;
            return Results.Ok(aviso);
        }
    }

    public IResult Cambiar(int id, Action<Aviso> cambio)
    {
        lock (cerrojo)
        {
            if (lista.FirstOrDefault(a => a.Id == id) is not Aviso aviso) return Results.NotFound();
            cambio(aviso);
            return Results.Ok(aviso);
        }
    }
}
