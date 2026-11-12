using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Ventanita de una toma de agua: nueva, corregir una apuntada (y borrarla) o repetir una que no convenció.
public class FormTomaAgua : Form
{
    private readonly AguaServicio servicio = new();
    private readonly ControlAgua control;   // Id 0 si es nueva
    private readonly CamposFormulario campos = new();

    private readonly DateTimePicker dtpFecha = new();
    private readonly TextBox txtHora = new();
    private readonly ComboBox cmbPunto = new();
    private readonly TextBox txtCloro = new();
    private readonly CheckBox chkSabor = new() { Text = "Sabor correcto", AutoSize = true };
    private readonly CheckBox chkOlor = new() { Text = "Olor correcto", AutoSize = true };
    private readonly CheckBox chkColor = new() { Text = "Color correcto", AutoSize = true };
    private readonly CheckBox chkTurbidez = new() { Text = "Turbidez correcta", AutoSize = true };
    private readonly TextBox txtComentario = new();

    // La toma tal como se ha guardado (null si se ha borrado o cancelado)
    public ControlAgua? Guardada { get; private set; }

    // tomas: los puntos activos. punto: el de la toma, por si ya está dado de baja.
    // repetida: la toma que se repite (solo al repetir), para enseñar de cuál es.
    public FormTomaAgua(ControlAgua? control, List<AguaToma> tomas, AguaToma? punto = null, ControlAgua? repetida = null)
    {
        bool nueva = control == null || control.Id == 0;
        this.control = control ?? new ControlAgua(0, DateTime.Today, 0, null, true, true, true, true, null,
            Hora: Hora(DateTime.Now.TimeOfDay));

        Text = repetida != null ? "Repetir toma de agua" : nueva ? "Nueva toma de agua" : "Corregir toma de agua";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(30, 20, 30, 10) };

        if (repetida != null)
            layout.Controls.Add(new Label
            {
                Text = $"Repite la toma de las {(repetida.Hora is TimeSpan h ? AguaServicio.Hora(h) : "—")} " +
                       $"({(repetida.Cloro is decimal c ? AguaServicio.Ppm(c) : "sin cloro")}), que no convenció. " +
                       "Esa se queda guardada como repetida.",
                AutoSize = true,
                MaximumSize = new Size(460, 0),
                ForeColor = Tema.Acento,
                Margin = new Padding(0, 0, 0, 15)
            });

        // Fecha y hora en una fila
        dtpFecha.Format = DateTimePickerFormat.Short;
        dtpFecha.MaxDate = DateTime.Today;
        dtpFecha.Value = this.control.Fecha.Date <= DateTime.Today ? this.control.Fecha.Date : DateTime.Today;
        dtpFecha.Width = 150;
        txtHora.Width = 100;
        txtHora.MaxLength = 5;
        txtHora.Text = this.control.Hora is TimeSpan hora ? AguaServicio.Hora(hora) : "";
        var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        fila.Controls.Add(campos.Campo("Día", dtpFecha));
        fila.Controls.Add(campos.Campo("Hora", txtHora));
        layout.Controls.Add(fila);

        // Los puntos activos y, si la toma es de uno dado de baja, también ese
        cmbPunto.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPunto.Width = 460;
        cmbPunto.MaxDropDownItems = 15;
        var puntos = tomas.ToList();
        if (punto != null && !puntos.Any(t => t.Id == punto.Id)) puntos.Add(punto);
        cmbPunto.DisplayMember = nameof(AguaToma.Nombre);
        cmbPunto.Items.AddRange(puntos.ToArray());
        cmbPunto.SelectedItem = puntos.FirstOrDefault(t => t.Id == this.control.TomaId);
        layout.Controls.Add(campos.Campo("Punto de agua", cmbPunto));

        // Al repetir es el mismo punto y el mismo día
        if (repetida != null)
        {
            dtpFecha.Enabled = false;
            cmbPunto.Enabled = false;
        }

        txtCloro.Width = 120;
        txtCloro.MaxLength = 4;
        txtCloro.Text = this.control.Cloro?.ToString("0.00", Componentes.Es) ?? "";
        var filaCloro = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        filaCloro.Controls.Add(campos.Campo("Cloro (ppm)", txtCloro));
        layout.Controls.Add(filaCloro);

        chkSabor.Checked = this.control.SaborOk;
        chkOlor.Checked = this.control.OlorOk;
        chkColor.Checked = this.control.ColorOk;
        chkTurbidez.Checked = this.control.TurbidezOk;
        var organolepticos = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 15) };
        foreach (var casilla in new[] { chkSabor, chkOlor, chkColor, chkTurbidez })
        {
            casilla.Margin = new Padding(0, 0, 18, 0);
            organolepticos.Controls.Add(casilla);
        }
        layout.Controls.Add(new Label { Text = "Organolépticos", AutoSize = true, Font = Tema.FuentePequenaNegrita, Margin = new Padding(0, 0, 0, 4) });
        layout.Controls.Add(organolepticos);

        txtComentario.Multiline = true;
        txtComentario.Height = 70;
        txtComentario.Width = 460;
        txtComentario.MaxLength = AguaServicio.LargoComentario;
        txtComentario.ScrollBars = ScrollBars.Vertical;
        txtComentario.Text = this.control.Comentario ?? "";
        layout.Controls.Add(campos.Campo("Comentario", txtComentario));

        var btnGuardar = Tema.BotonPrincipal("Guardar", 130);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        AcceptButton = btnGuardar;
        CancelButton = btnCancelar;

        var botones = new List<Button> { btnGuardar, btnCancelar };
        if (!nueva)
        {
            var btnBorrar = Tema.BotonSecundario("Borrar", 110);
            btnBorrar.ForeColor = Tema.Error;
            btnBorrar.Click += (s, e) => Borrar();
            botones.Add(btnBorrar);
        }
        layout.Controls.Add(Componentes.Acciones(botones.ToArray()));

        Controls.Add(layout);
        // Lo primero que hay que rellenar: el punto en una nueva, el cloro en las demás
        Shown += (s, e) => (cmbPunto.SelectedItem == null ? cmbPunto : (Control)txtCloro).Focus();
    }

    private static TimeSpan Hora(TimeSpan hora) => new(hora.Hours, hora.Minutes, 0);

    private void Guardar()
    {
        campos.LimpiarErrores();

        if (!AguaServicio.LeerHora(txtHora.Text, out var hora))
        {
            campos.MarcarError(txtHora, "Escríbela así: 9:30.");
            txtHora.Focus();
            return;
        }
        if (cmbPunto.SelectedItem is not AguaToma punto)
        {
            campos.MarcarError(cmbPunto, "Elige el punto de agua.");
            cmbPunto.Focus();
            return;
        }
        decimal? cloro = null;
        string texto = txtCloro.Text.Trim().Replace('.', ',');
        if (texto != "")
        {
            if (!decimal.TryParse(texto, System.Globalization.NumberStyles.Number, Componentes.Es, out var valor))
            {
                campos.MarcarError(txtCloro, "Tiene que ser un número, por ejemplo 0,55.");
                txtCloro.Focus();
                return;
            }
            cloro = valor;
        }

        try
        {
            Guardada = servicio.Guardar(control with
            {
                Fecha = dtpFecha.Value.Date,
                Hora = hora,
                TomaId = punto.Id,
                Cloro = cloro,
                SaborOk = chkSabor.Checked,
                OlorOk = chkOlor.Checked,
                ColorOk = chkColor.Checked,
                TurbidezOk = chkTurbidez.Checked,
                Comentario = txtComentario.Text
            });
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Informacion(this, "No se ha guardado", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar la toma de agua.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }

    private void Borrar()
    {
        if (!Mensajes.Confirmar(this, "Borrar toma", "¿Borrar esta toma de agua? No se puede deshacer.", aviso: true))
            return;
        try
        {
            servicio.Borrar(control);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Informacion(this, "No se ha borrado", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo borrar la toma de agua.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
