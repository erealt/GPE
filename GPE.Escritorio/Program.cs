namespace GPE.Escritorio;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Un error no previsto se enseña en un mensaje en vez de cerrar la aplicación
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => MostrarErrorInesperado(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex) MostrarErrorInesperado(ex);
        };

        // Con "Salir" se vuelve a la pantalla de entrar; al cerrar la ventana se acaba
        bool primeraVez = true;
        while (true)
        {
            // Sin usuario no se puede registrar el historial, así que no se entra
            using (var entrar = new FormLogin())
            {
                // Al abrir el programa, si se marcó "Recordarme", entra directamente
                bool dentro = primeraVez && entrar.EntrarRecordado();
                primeraVez = false;
                if (!dentro && entrar.ShowDialog() != DialogResult.OK) return;
            }

            // Avisos por correo que quedaron sin enviar (sin conexión, fallo del servidor...)
            GPE.Logica.AvisoServicio.EnviarPendientesEnSegundoPlano();
            GPE.Logica.AvisoServicio.EnviarAsignacionesEnSegundoPlano();

            using var principal = new FormPrincipal();
            Application.Run(principal);
            if (!principal.CambiarUsuario) return;

            // Quien pulsa "Salir" deja el ordenador libre para otro: se olvida el usuario recordado
            Recordar.Olvidar();

            GPE.Datos.Sesion.UsuarioActual = null;
            GPE.Datos.Sesion.Roles = new();
        }
    }

    private static void MostrarErrorInesperado(Exception ex) =>
        MessageBox.Show(
            "Ha ocurrido un error inesperado. Si se repite, avisa con este mensaje:\n\n" + ex.Message,
            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
}