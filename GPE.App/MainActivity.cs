using System.Text.RegularExpressions;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.Core.Content;
using Uri = Android.Net.Uri;

namespace GPE.App;

// La app enseña la web de GPE (http://192.168.2.249) por dentro: lo mismo que en el navegador
// (Mis acciones, control diario, agua, almacén...) según el usuario. Lo propio de la app son los
// avisos de acciones nuevas (ServicioAvisos) y que la sesión se queda abierta.
[Activity(Label = "@string/app_name", MainLauncher = true, LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.UiMode)]
public class MainActivity : Activity
{
    public const string ExtraRuta = "ruta";
    private const int PedirArchivos = 1;
    private const int PedirPermisoAvisos = 2;

    // Fotos y PDF de la web: se descargan con la sesión y se abren con la app del móvil
    private static readonly Regex Archivos = new(@"^/(almacen/archivo/\d+|acciones/foto/\d+|deca/\d+/(pdf|qr))$");

    private WebView web = null!;
    private IValueCallback? archivosElegidos;   // a quién devolver las fotos elegidas
    private Uri? fotoCamara;                     // dónde deja la cámara la foto

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.SetStatusBarColor(ServicioAvisos.ColorGpe);

        web = new WebView(this);
        web.Settings.JavaScriptEnabled = true;
        web.Settings.DomStorageEnabled = true;
        web.Settings.MediaPlaybackRequiresUserGesture = false;
        CookieManager.Instance!.SetAcceptCookie(true);
        web.SetWebViewClient(new Cliente(this));
        web.SetWebChromeClient(new Navegador(this));
        SetContentView(web);

        // Atrás: página anterior de la web; en la primera, sale de la app
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, new Atras(this));

        Cargar(Intent?.GetStringExtra(ExtraRuta) ?? "/acciones");
        PedirPermisos();
    }

    // Al tocar un aviso con la app ya abierta
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent?.GetStringExtra(ExtraRuta) is string ruta)
            Cargar(ruta);
    }

    protected override void OnPause()
    {
        base.OnPause();
        CookieManager.Instance!.Flush();   // que la sesión quede guardada aunque Android cierre la app
    }

    // Por si el móvil no tiene OnBackInvokedDispatcher (Android 12 o antes)
#pragma warning disable CS0672, CA1422
    public override void OnBackPressed() => IrAtras();
#pragma warning restore CS0672, CA1422

    private void IrAtras()
    {
        if (web.CanGoBack()) web.GoBack();
        else Finish();
    }

    private void Cargar(string ruta) => web.LoadUrl(Ajustes.Servidor(this) + ruta);

    // Permiso de notificaciones (Android 13 o más) y que el ahorro de batería no la pare
    private void PedirPermisos()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(new[] { Android.Manifest.Permission.PostNotifications }, PedirPermisoAvisos);
            return;
        }
        PedirSinAhorroBateria();
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == PedirPermisoAvisos) PedirSinAhorroBateria();
    }

    private void PedirSinAhorroBateria()
    {
        var energia = (PowerManager)GetSystemService(PowerService)!;
        if (energia.IsIgnoringBatteryOptimizations(PackageName)) return;
        try
        {
            var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations, Uri.Parse("package:" + PackageName));
            StartActivity(intent);
        }
        catch (ActivityNotFoundException) { }
    }

    // Cada vez que se carga una página con la sesión abierta, que el servicio esté en marcha
    // (si quien ha entrado no tiene acciones, el servicio se para solo)
    private void PaginaCargada()
    {
        CookieManager.Instance!.Flush();
        if (!string.IsNullOrEmpty(Ajustes.Cookie(this)))
            ServicioAvisos.Arrancar(this);
    }

    // Sin conexión con el servidor: una página propia con "Reintentar" y "Cambiar servidor"
    private void SinConexion(string? direccion)
    {
        string servidor = Ajustes.Servidor(this);
        string html = $$"""
            <html><head><meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
              body { font-family: sans-serif; padding: 32px 24px; color: #1d1d1f; }
              h2 { margin: 0 0 12px; } p { color: #555; line-height: 1.4; }
              a { display: block; text-align: center; padding: 14px; margin-top: 14px; border-radius: 8px;
                  text-decoration: none; font-weight: bold; }
              .principal { background: #5900F6; color: white; } .secundario { border: 1px solid #ccc; color: #1d1d1f; }
            </style></head><body>
            <h2>No se puede conectar con GPE</h2>
            <p>Comprueba que el móvil está conectado a la wifi de la empresa.</p>
            <p style="font-size: 13px">Servidor: {{WebUtility(servidor)}}</p>
            <a class="principal" href="gpe:reintentar">Reintentar</a>
            <a class="secundario" href="gpe:servidor">Cambiar servidor</a>
            </body></html>
            """;
        web.Tag = new Java.Lang.String(direccion ?? Ajustes.Servidor(this) + "/acciones");
        web.LoadDataWithBaseURL(null, html, "text/html", "utf-8", null);
    }

    private static string WebUtility(string texto) => System.Net.WebUtility.HtmlEncode(texto);

    private void CambiarServidor()
    {
        var caja = new EditText(this) { Text = Ajustes.Servidor(this) };
        caja.SetSingleLine(true);
        var marco = new FrameLayout(this);
        marco.SetPadding(48, 16, 48, 0);
        marco.AddView(caja);
        new AlertDialog.Builder(this)
            .SetTitle("Dirección del servidor")!
            .SetMessage("La de la web de GPE. Normalmente " + Ajustes.ServidorPorDefecto)!
            .SetView(marco)!
            .SetPositiveButton("Guardar", (s, e) =>
            {
                Ajustes.GuardarServidor(this, caja.Text ?? "");
                Cargar("/acciones");
            })!
            .SetNegativeButton("Cancelar", (s, e) => { })!
            .Show();
    }

    // Fotos y PDF: descargarlos con la sesión y abrirlos con el visor del móvil
    private async Task AbrirArchivo(string direccion)
    {
        Toast.MakeText(this, "Abriendo...", ToastLength.Short)!.Show();
        try
        {
            using var cliente = new HttpClient(new HttpClientHandler { UseCookies = false });
            using var peticion = new HttpRequestMessage(HttpMethod.Get, direccion);
            peticion.Headers.Add("Cookie", Ajustes.Cookie(this) ?? "");
            using var respuesta = await cliente.SendAsync(peticion);
            if (!respuesta.IsSuccessStatusCode)
            {
                Toast.MakeText(this, "No se ha podido abrir el archivo.", ToastLength.Long)!.Show();
                return;
            }
            string tipo = respuesta.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            string extension = tipo switch
            {
                "application/pdf" => ".pdf",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/heic" => ".heic",
                "image/bmp" => ".bmp",
                _ => ".jpg"
            };
            var carpeta = new Java.IO.File(CacheDir, "abiertos");
            carpeta.Mkdirs();
            foreach (var viejo in carpeta.ListFiles() ?? Array.Empty<Java.IO.File>()) viejo.Delete();
            var archivo = new Java.IO.File(carpeta, "archivo" + extension);
            await File.WriteAllBytesAsync(archivo.AbsolutePath, await respuesta.Content.ReadAsByteArrayAsync());

            var uri = FileProvider.GetUriForFile(this, "com.gpe.app.archivos", archivo);
            var ver = new Intent(Intent.ActionView);
            ver.SetDataAndType(uri, tipo);
            ver.AddFlags(ActivityFlags.GrantReadUriPermission);
            StartActivity(ver);
        }
        catch (ActivityNotFoundException)
        {
            Toast.MakeText(this, "No hay ninguna app en el móvil para abrir este archivo.", ToastLength.Long)!.Show();
        }
        catch (Exception)
        {
            Toast.MakeText(this, "No se ha podido abrir el archivo.", ToastLength.Long)!.Show();
        }
    }

    // "Hacer foto" (capture) abre la cámara; "Elegir", la galería o los archivos
    private bool ElegirArchivos(IValueCallback callback, WebChromeClient.FileChooserParams parametros)
    {
        archivosElegidos?.OnReceiveValue(null);
        archivosElegidos = callback;
        fotoCamara = null;
        try
        {
            Intent intent;
            if (parametros.IsCaptureEnabled)
            {
                var carpeta = new Java.IO.File(CacheDir, "fotos");
                carpeta.Mkdirs();
                var archivo = new Java.IO.File(carpeta, $"foto_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                fotoCamara = FileProvider.GetUriForFile(this, "com.gpe.app.archivos", archivo);
                intent = new Intent(MediaStore.ActionImageCapture);
                intent.PutExtra(MediaStore.ExtraOutput, fotoCamara);
                intent.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
            }
            else
            {
                intent = parametros.CreateIntent()!;
                if (parametros.Mode == ChromeFileChooserMode.OpenMultiple)
                    intent.PutExtra(Intent.ExtraAllowMultiple, true);
            }
            StartActivityForResult(intent, PedirArchivos);
            return true;
        }
        catch (ActivityNotFoundException)
        {
            archivosElegidos = null;
            Toast.MakeText(this, "No se ha podido abrir la cámara ni la galería.", ToastLength.Long)!.Show();
            return false;
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != PedirArchivos || archivosElegidos == null) return;

        Uri[]? elegidos = null;
        if (resultCode == Result.Ok)
        {
            if (fotoCamara != null)
                elegidos = new[] { fotoCamara };
            else if (data?.ClipData is ClipData varios)
                elegidos = Enumerable.Range(0, varios.ItemCount).Select(i => varios.GetItemAt(i)!.Uri!).ToArray();
            else if (data?.Data is Uri uno)
                elegidos = new[] { uno };
        }
        archivosElegidos.OnReceiveValue(elegidos);
        archivosElegidos = null;
        fotoCamara = null;
    }

    private sealed class Cliente(MainActivity app) : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
        {
            var url = request?.Url;
            if (url == null) return false;

            if (url.Scheme == "gpe")
            {
                if (url.SchemeSpecificPart == "servidor") app.CambiarServidor();
                else view?.LoadUrl(view.Tag?.ToString() ?? Ajustes.Servidor(app) + "/acciones");
                return true;
            }

            var servidor = Uri.Parse(Ajustes.Servidor(app))!;
            if (url.Host != servidor.Host)
            {
                // Otra web (no la de GPE): en el navegador del móvil
                try { app.StartActivity(new Intent(Intent.ActionView, url)); }
                catch (ActivityNotFoundException) { }
                return true;
            }
            if (Archivos.IsMatch(url.Path ?? ""))
            {
                _ = app.AbrirArchivo(url.ToString()!);
                return true;
            }
            return false;
        }

        public override void OnPageFinished(WebView? view, string? url)
        {
            base.OnPageFinished(view, url);
            if (url != null && url.StartsWith("http")) app.PaginaCargada();
        }

        public override void OnReceivedError(WebView? view, IWebResourceRequest? request, WebResourceError? error)
        {
            base.OnReceivedError(view, request, error);
            if (request?.IsForMainFrame == true) app.SinConexion(request.Url?.ToString());
        }
    }

    private sealed class Navegador(MainActivity app) : WebChromeClient
    {
        public override bool OnShowFileChooser(WebView? webView, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams) =>
            filePathCallback != null && fileChooserParams != null && app.ElegirArchivos(filePathCallback, fileChooserParams);
    }

    private sealed class Atras(MainActivity app) : Java.Lang.Object, Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => app.IrAtras();
    }
}
