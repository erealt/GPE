namespace GPE.Escritorio;

// Campo con la etiqueta encima y el error en rojo debajo, que se quita al corregirlo.
public class CamposFormulario
{
    private readonly Dictionary<Control, Label> errores = new();

    // "ayuda" es un texto opcional que va entre el control y el error
    public Control Campo(string etiqueta, Control control, Control? ayuda = null)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 15, 15)
        };
        panel.Controls.Add(new Label
        {
            Text = etiqueta,
            AutoSize = true,
            Font = Tema.FuentePequenaNegrita,
            Margin = new Padding(0, 0, 0, 4)
        });

        control.Dock = DockStyle.Top;
        panel.Controls.Add(control);
        if (ayuda != null) panel.Controls.Add(ayuda);

        var error = EtiquetaError();
        errores[control] = error;
        panel.Controls.Add(error);

        // Al corregir el campo desaparece su error
        switch (control)
        {
            case ComboBox combo: combo.SelectedIndexChanged += (s, e) => error.Visible = false; break;
            case NumericUpDown numero: numero.ValueChanged += (s, e) => error.Visible = false; break;
            case DateTimePicker fecha: fecha.ValueChanged += (s, e) => error.Visible = false; break;
            default: control.TextChanged += (s, e) => error.Visible = false; break;
        }

        return panel;
    }

    public void MarcarError(Control campo, string mensaje)
    {
        errores[campo].Text = mensaje;
        errores[campo].Visible = true;
    }

    public void OcultarError(Control campo) => errores[campo].Visible = false;

    public void LimpiarErrores()
    {
        foreach (var etiqueta in errores.Values)
            etiqueta.Visible = false;
    }

    // Etiqueta de error suelta, para errores que no son de un solo campo
    public static Label EtiquetaError() => new()
    {
        AutoSize = true,
        ForeColor = Tema.Error,
        Font = Tema.FuentePequena,
        Margin = new Padding(0, 4, 0, 0),
        Visible = false
    };
}
