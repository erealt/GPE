using System.Net;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;

namespace GPE.App;

// Sigue preguntando al servidor por las acciones de quien ha entrado, aunque el móvil esté bloqueado
// o la app cerrada, y saca una notificación con sonido por cada acción nueva. Solo funciona dentro
// de la wifi de la empresa (el servidor no se ve desde fuera): fuera, espera a volver.
// Está declarado en AndroidManifest.xml.
[Register("com.gpe.app.ServicioAvisos")]
public class ServicioAvisos : Service
{
    private const string CanalServicio = "servicio";
    private const string CanalAcciones = "acciones";
    private const int IdFija = 1;
    private const int IdAcciones = 1000;   // la notificación de cada acción es IdAcciones + su id
    private static readonly TimeSpan CadaCuanto = TimeSpan.FromSeconds(30);
    private static readonly long[] Vibracion = { 0, 500, 250, 500 };
    public static readonly Android.Graphics.Color ColorGpe = Android.Graphics.Color.ParseColor("#5900F6");

    private static readonly HttpClient cliente = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private CancellationTokenSource? parar;
    private PowerManager.WakeLock? despierto;

    public static void Arrancar(Context contexto)
    {
        // Android puede no dejar arrancarlo en algún momento: se vuelve a intentar al abrir la app
        try { contexto.StartForegroundService(new Intent(contexto, typeof(ServicioAvisos))); }
        catch (Exception) { }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        CrearCanales();

        // Android obliga a que un servicio así lleve una notificación fija
        var fija = new NotificationCompat.Builder(this, CanalServicio)
            .SetSmallIcon(Resource.Drawable.ic_aviso)
            .SetColor(ColorGpe)
            .SetContentTitle("GPE")
            .SetContentText("Atento a tus acciones nuevas")
            .SetOngoing(true)
            .SetContentIntent(AbrirApp(null, 0))
            .Build()!;
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(IdFija, fija, ForegroundService.TypeSpecialUse);
        else
            StartForeground(IdFija, fija);

        if (parar == null)
        {
            // Que el móvil no se duerma del todo y deje de preguntar
            var energia = (PowerManager)GetSystemService(PowerService)!;
            despierto = energia.NewWakeLock(WakeLockFlags.Partial, "gpe:avisos");
            despierto?.Acquire();

            parar = new CancellationTokenSource();
            _ = Vigilar(parar.Token);
        }
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        parar?.Cancel();
        parar = null;
        if (despierto?.IsHeld == true) despierto.Release();
        base.OnDestroy();
    }

    private async Task Vigilar(CancellationToken fin)
    {
        while (!fin.IsCancellationRequested)
        {
            bool seguir = true;
            try { seguir = await Mirar(); }
            catch (Exception) { }   // sin wifi, servidor apagado...: se vuelve a probar
            if (!seguir)
            {
                // Quien ha entrado no tiene "Mis acciones" (solo Calidad, solo Almacén): nada que avisar
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return;
            }
            try { await Task.Delay(CadaCuanto, fin); }
            catch (TaskCanceledException) { }
        }
    }

    // false: dejar de preguntar
    private async Task<bool> Mirar()
    {
        string? cookie = Ajustes.Cookie(this);
        if (string.IsNullOrEmpty(cookie)) return true;   // aún no ha entrado: espera

        using var peticion = new HttpRequestMessage(HttpMethod.Get, Ajustes.Servidor(this) + "/acciones/avisos");
        peticion.Headers.Add("Cookie", cookie);
        using var respuesta = await cliente.SendAsync(peticion);
        if (respuesta.StatusCode == HttpStatusCode.Forbidden) return false;
        if (!respuesta.IsSuccessStatusCode) return true;   // sin sesión (ha salido) o fallo del servidor: espera

        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        string usuario = json.RootElement.GetProperty("usuario").GetString() ?? "";
        var acciones = json.RootElement.GetProperty("acciones").EnumerateArray()
            .Select(a => (Id: a.GetProperty("id").GetInt32(),
                          Titulo: a.GetProperty("titulo").GetString() ?? "",
                          Texto: a.GetProperty("texto").GetString() ?? ""))
            .ToList();

        var (usuarioAntes, avisadas) = Ajustes.Avisadas(this);
        if (usuarioAntes != usuario)
        {
            // Primera vez de esta persona en este móvil: las que ya tenía no se avisan, solo las nuevas
            Ajustes.GuardarAvisadas(this, usuario, acciones.Select(a => a.Id));
            return true;
        }

        var gestor = NotificationManagerCompat.From(this);
        foreach (var accion in acciones.Where(a => !avisadas.Contains(a.Id)))
            Notificar(gestor, accion.Id, accion.Titulo, accion.Texto);

        // Las que ya no están pendientes (hechas, borradas, pasadas a otro): fuera su notificación
        var pendientes = acciones.Select(a => a.Id).ToHashSet();
        foreach (int id in avisadas.Where(id => !pendientes.Contains(id)))
            gestor.Cancel(IdAcciones + id);

        Ajustes.GuardarAvisadas(this, usuario, pendientes);
        return true;
    }

    private void Notificar(NotificationManagerCompat gestor, int id, string titulo, string texto)
    {
        var notificacion = new NotificationCompat.Builder(this, CanalAcciones)
            .SetSmallIcon(Resource.Drawable.ic_aviso)
            .SetColor(ColorGpe)
            .SetContentTitle("Nueva acción · " + titulo)
            .SetContentText(texto)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(texto))
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetCategory(NotificationCompat.CategoryReminder)
            .SetVisibility(NotificationCompat.VisibilityPublic)   // se lee con el móvil bloqueado
            .SetVibrate(Vibracion)
            .SetAutoCancel(true)
            .SetContentIntent(AbrirApp("/acciones?accion=" + id, id))
            .Build()!;

        try { gestor.Notify(IdAcciones + id, notificacion); }
        catch (Java.Lang.SecurityException) { }   // sin permiso de notificaciones
    }

    // Al tocar la notificación se abre la app en esa página
    private PendingIntent AbrirApp(string? ruta, int codigo)
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        if (ruta != null) intent.PutExtra(MainActivity.ExtraRuta, ruta);
        return PendingIntent.GetActivity(this, codigo, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private void CrearCanales()
    {
        var gestor = (NotificationManager)GetSystemService(NotificationService)!;

        var servicio = new NotificationChannel(CanalServicio, "Aplicación en marcha", NotificationImportance.Min)
        {
            Description = "Notificación fija mientras la app está atenta a las acciones nuevas"
        };
        servicio.SetShowBadge(false);
        gestor.CreateNotificationChannel(servicio);

        var acciones = new NotificationChannel(CanalAcciones, "Acciones nuevas", NotificationImportance.High)
        {
            Description = "Cuando calidad te asigna una acción",
            LockscreenVisibility = NotificationVisibility.Public
        };
        acciones.EnableVibration(true);
        acciones.SetVibrationPattern(Vibracion);
        gestor.CreateNotificationChannel(acciones);
    }
}

// Al encender el móvil vuelve a ponerse atento, sin tener que abrir la app
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class AlEncender : BroadcastReceiver
{
    public override void OnReceive(Context? contexto, Intent? intent)
    {
        if (contexto != null && intent?.Action == Intent.ActionBootCompleted && !string.IsNullOrEmpty(Ajustes.Cookie(contexto)))
            ServicioAvisos.Arrancar(contexto);
    }
}
