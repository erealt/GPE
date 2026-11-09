using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;

namespace GPE.Movil;

// Sigue mirando los avisos aunque la pantalla esté bloqueada o la aplicación cerrada,
// y saca una notificación con sonido y vibración cuando una máquina pide mecánico.
// Está declarado en AndroidManifest.xml.
[Register("com.gpe.mecanicos.ServicioAvisos")]
public class ServicioAvisos : Service
{
    private const string CanalServicio = "servicio";
    private const string CanalAvisos = "avisos";
    private const int IdFija = 1;
    private const int IdAvisos = 1000;    // la notificación de cada aviso es IdAvisos + su número
    private static readonly TimeSpan CadaCuanto = TimeSpan.FromSeconds(10);

    private CancellationTokenSource? parar;
    private PowerManager.WakeLock? despierto;
    private readonly HashSet<int> notificados = new();

    public static void Arrancar(Context contexto) =>
        contexto.StartForegroundService(new Intent(contexto, typeof(ServicioAvisos)));

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        CrearCanales();

        // Android obliga a que un servicio así lleve una notificación fija
        var fija = new NotificationCompat.Builder(this, CanalServicio)
            .SetSmallIcon(Resource.Drawable.ic_aviso)
            .SetColor(Android.Graphics.Color.ParseColor("#5900F6"))
            .SetContentTitle("GPE Mecánicos")
            .SetContentText("Atento a los avisos de las máquinas")
            .SetOngoing(true)
            .SetContentIntent(AbrirApp(0))
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
            try { await Mirar(); } catch (Exception) { }
            try { await Task.Delay(CadaCuanto, fin); } catch (TaskCanceledException) { }
        }
    }

    private async Task Mirar()
    {
        var lista = await Api.Avisos();
        if (lista == null) return;
        var gestor = NotificationManagerCompat.From(this);

        foreach (var aviso in lista)
        {
            bool esperando = aviso.Llegada == null && aviso.Resuelto == null;
            if (!esperando)
            {
                // Ya va alguien: se quita la notificación de todos los móviles
                if (notificados.Remove(aviso.Id)) gestor.Cancel(IdAvisos + aviso.Id);
                continue;
            }
            if (notificados.Add(aviso.Id)) Notificar(gestor, aviso);
        }
    }

    private void Notificar(NotificationManagerCompat gestor, Aviso aviso)
    {
        var resumen = string.Join(" · ", new[] { aviso.Motivo, aviso.Maquinista == null ? null : $"Avisa {aviso.Maquinista}" }
            .Where(t => !string.IsNullOrWhiteSpace(t)));
        if (resumen == "") resumen = "Pulsa para verlo";
        var largo = string.IsNullOrWhiteSpace(aviso.Comentario) ? resumen : $"{resumen}\n“{aviso.Comentario}”";

        var notificacion = new NotificationCompat.Builder(this, CanalAvisos)
            .SetSmallIcon(Resource.Drawable.ic_aviso)
            .SetColor(Android.Graphics.Color.ParseColor("#5900F6"))
            .SetContentTitle($"Necesitan mecánico: {aviso.Maquina}")
            .SetContentText(resumen)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(largo))
            .SetWhen(new DateTimeOffset(aviso.Avisado).ToUnixTimeMilliseconds())
            .SetShowWhen(true)
            .SetPriority(NotificationCompat.PriorityMax)
            .SetCategory(NotificationCompat.CategoryAlarm)
            .SetVisibility(NotificationCompat.VisibilityPublic)   // se lee con el móvil bloqueado
            .SetVibrate(Vibracion)
            .SetAutoCancel(true)
            .SetContentIntent(AbrirApp(aviso.Id))
            .Build()!;

        try { gestor.Notify(IdAvisos + aviso.Id, notificacion); }
        catch (Java.Lang.SecurityException) { }   // sin permiso de notificaciones
    }

    private static readonly long[] Vibracion = { 0, 800, 300, 800, 300, 800 };

    private void CrearCanales()
    {
        var gestor = (NotificationManager)GetSystemService(NotificationService)!;

        var servicio = new NotificationChannel(CanalServicio, "Aplicación en marcha", NotificationImportance.Min)
        {
            Description = "Notificación fija mientras la aplicación está atenta a los avisos"
        };
        servicio.SetShowBadge(false);
        gestor.CreateNotificationChannel(servicio);

        var avisos = new NotificationChannel(CanalAvisos, "Avisos de máquinas", NotificationImportance.High)
        {
            Description = "Cuando una máquina necesita mecánico",
            LockscreenVisibility = NotificationVisibility.Public
        };
        avisos.EnableVibration(true);
        avisos.SetVibrationPattern(Vibracion);
        avisos.EnableLights(true);
        avisos.LightColor = Android.Graphics.Color.ParseColor("#5900F6");
        avisos.SetSound(Android.Media.RingtoneManager.GetDefaultUri(Android.Media.RingtoneType.Notification),
            new Android.Media.AudioAttributes.Builder()
                .SetUsage(Android.Media.AudioUsageKind.Notification)!
                .SetContentType(Android.Media.AudioContentType.Sonification)!
                .Build());
        gestor.CreateNotificationChannel(avisos);
    }

    private PendingIntent AbrirApp(int codigo)
    {
        var intent = new Intent(this, typeof(MainActivity)).AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        return PendingIntent.GetActivity(this, codigo, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }
}

// Al encender el móvil vuelve a arrancar el servicio, sin tener que abrir la aplicación
[BroadcastReceiver(Name = "com.gpe.mecanicos.AlEncender", Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class AlEncender : BroadcastReceiver
{
    public override void OnReceive(Context? contexto, Intent? intent)
    {
        if (contexto != null && intent?.Action == Intent.ActionBootCompleted && !string.IsNullOrEmpty(Api.Mecanico))
            ServicioAvisos.Arrancar(contexto);
    }
}

// Lo que se pide la primera vez: permiso de notificaciones y que Android no lo duerma para ahorrar batería
public static class Avisador
{
    public static async Task Preparar()
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                await Permissions.RequestAsync<Permissions.PostNotifications>();

            var contexto = Platform.AppContext;
            ServicioAvisos.Arrancar(contexto);

            var energia = (PowerManager)contexto.GetSystemService(Context.PowerService)!;
            if (!energia.IsIgnoringBatteryOptimizations(contexto.PackageName) && !Preferences.Default.Get("bateria_preguntada", false))
            {
                Preferences.Default.Set("bateria_preguntada", true);
                var intent = new Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations,
                    Android.Net.Uri.Parse("package:" + contexto.PackageName));
                intent.AddFlags(ActivityFlags.NewTask);
                contexto.StartActivity(intent);
            }
        }
        catch (Exception) { }
    }
}
