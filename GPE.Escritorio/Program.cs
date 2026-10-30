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

        // Con "Salir" se vuelve a elegir usuario; al cerrar la ventana se acaba
        while (true)
        {
            // Sin usuario no se puede registrar el historial, así que no se entra
            using (var seleccion = new FormSeleccionUsuario())
            {
                if (seleccion.ShowDialog() != DialogResult.OK) return;
            }

            // Avisos por correo que quedaron sin enviar (sin conexión, fallo del servidor...)
            GPE.Logica.AvisoServicio.EnviarPendientesEnSegundoPlano();

            using var principal = new FormPrincipal();
            Application.Run(principal);
            if (!principal.CambiarUsuario) return;

            GPE.Datos.Sesion.UsuarioActual = null;
            GPE.Datos.Sesion.Roles = new();
        }
    }

    private static void MostrarErrorInesperado(Exception ex) =>
        MessageBox.Show(
            "Ha ocurrido un error inesperado. Si se repite, avisa con este mensaje:\n\n" + ex.Message,
            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
}