using GPE.Datos;

namespace GPE.Escritorio;

// Pantalla de un paso de la devolución (solicitud, recogida, almacén...).
// Al pulsar otro paso en la barra de progreso se cierra indicando a cuál ir,
// y ModuloDevoluciones abre la pantalla de ese paso.
public class PantallaFase : Form
{
    public int? PasoSolicitado { get; private set; }

    // Barra de progreso en la que se pueden pulsar los pasos completados y el actual
    protected Control Progreso(DevolucionFila devolucion, int pasoVisto)
    {
        var barra = (BarraProgresoDevolucion)Componentes.Progreso(devolucion.Fase);
        barra.PasoVisto = pasoVisto;
        barra.PasoMaximoPulsable = Fase.PasoEnCurso(devolucion.Fase);
        barra.PasoPulsado += IrAPaso;
        new ToolTip().SetToolTip(barra, "Pulsa un paso para ver lo que se hizo en él");
        return barra;
    }

    protected void IrAPaso(int paso)
    {
        PasoSolicitado = paso;
        Close();   // en una ventana modal el cierre se completa después, al volver al bucle de mensajes
    }

    // Si alguien cancela el cierre (p. ej. "cambios sin guardar" → No), se olvida el paso pedido
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);   // aquí se ejecutan los manejadores de FormClosing de la pantalla
        if (e.Cancel) PasoSolicitado = null;
    }

    // Subtítulo de las tarjetas cuando el usuario no tiene el rol que hace ese paso
    protected static string SoloConsulta(string rol) => $"Solo consulta: este paso lo hace el rol {rol}.";

    // Botón "Cerrar" de las pantallas de consulta; también responde a Esc
    protected Button BotonCerrar()
    {
        var boton = Tema.BotonSecundario("Cerrar", 130);
        boton.DialogResult = DialogResult.Cancel;
        CancelButton = boton;
        return boton;
    }

    // Botón en rojo para volver a un paso anterior (deshacer recepción, reabrir...)
    protected static Button BotonReabrir(string texto, int ancho, Action accion)
    {
        var boton = Tema.BotonSecundario(texto, ancho);
        boton.ForeColor = Tema.Error;
        boton.Click += (s, e) => accion();
        return boton;
    }

    // Pide el motivo para volver a un paso anterior. Devuelve null si se cancela.
    protected string? PedirMotivo(string titulo, string explicacion)
    {
        using var dialogo = new Form
        {
            Text = titulo,
            ClientSize = new Size(480, 260),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Tema.Fondo,
            Font = Tema.Fuente
        };

        var lblExplicacion = new Label
        {
            Text = explicacion,
            Location = new Point(20, 15),
            Size = new Size(440, 45),
            ForeColor = Tema.Texto
        };
        var txtMotivo = new TextBox
        {
            Multiline = true,
            MaxLength = 500,
            Location = new Point(20, 65),
            Size = new Size(440, 110),
            PlaceholderText = "Motivo (obligatorio)"
        };

        var btnAceptar = Tema.BotonPrincipal("Reabrir", 120);
        btnAceptar.Location = new Point(340, 200);
        btnAceptar.DialogResult = DialogResult.OK;

        var btnCancelar = Tema.BotonSecundario("Cancelar", 120);
        btnCancelar.Location = new Point(210, 200);
        btnCancelar.DialogResult = DialogResult.Cancel;

        btnAceptar.Enabled = false;
        txtMotivo.TextChanged += (s, e) => btnAceptar.Enabled = txtMotivo.Text.Trim().Length >= 5;

        dialogo.Controls.AddRange(new Control[] { lblExplicacion, txtMotivo, btnAceptar, btnCancelar });
        dialogo.AcceptButton = null;   // Enter hace salto de línea en el motivo
        dialogo.CancelButton = btnCancelar;

        return dialogo.ShowDialog(this) == DialogResult.OK ? txtMotivo.Text.Trim() : null;
    }
}
