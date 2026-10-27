namespace GPE.Escritorio;

public static class Mensajes
{
    // Fallo al leer o guardar en SQL Server: qué se intentaba y el motivo que da SQL Server
    public static void ErrorBaseDatos(IWin32Window dueno, string mensaje, Exception ex) =>
        MessageBox.Show(dueno, mensaje + "\n\n" + ex.Message,
            "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
