namespace GPE.Escritorio;

// Ventanas de mensaje de todas las pantallas, para que todas avisen y pregunten igual.
// El título dice qué ha pasado ("No se pudo guardar"); el mensaje, el porqué.
public static class Mensajes
{
    // Algo que el usuario tiene que corregir o tener en cuenta (p. ej. una ReglaNegocioException)
    public static void Aviso(IWin32Window dueno, string titulo, string mensaje) =>
        MessageBox.Show(dueno, mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Informacion(IWin32Window dueno, string titulo, string mensaje) =>
        MessageBox.Show(dueno, mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);

    // Fallo al leer o guardar en SQL Server: qué se intentaba y el motivo que da SQL Server
    public static void ErrorBaseDatos(IWin32Window dueno, string mensaje, Exception ex) =>
        MessageBox.Show(dueno, mensaje + "\n\n" + ex.Message,
            "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);

    // Otros fallos (archivos, disco, programas externos): qué se intentaba y el motivo
    public static void Error(IWin32Window dueno, string mensaje, Exception ex) =>
        MessageBox.Show(dueno, mensaje + "\n\n" + ex.Message,
            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    // Pregunta Sí/No antes de un paso importante. Con aviso, sale el icono de advertencia.
    public static bool Confirmar(IWin32Window dueno, string titulo, string pregunta, bool aviso = false) =>
        MessageBox.Show(dueno, pregunta, titulo, MessageBoxButtons.YesNo,
            aviso ? MessageBoxIcon.Warning : MessageBoxIcon.Question) == DialogResult.Yes;

    // Salir perdiendo lo apuntado. "No" es el botón por defecto: un Enter sin querer no borra nada.
    public static bool DescartarCambios(IWin32Window dueno, string pregunta) =>
        MessageBox.Show(dueno, pregunta, "Cambios sin guardar", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
}
