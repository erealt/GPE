using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Ventanita de un análisis (una fila de la hoja MICRO & FYQ): nuevo o corregir uno apuntado (y borrarlo).
public class FormAnalisis : Form
{
    private const int Ancho = 520;

    private readonly AnalisisServicio servicio = new();
    private readonly Analisis analisis;   // Id 0 si es nuevo
    private readonly CamposFormulario campos = new();

    private readonly ComboBox cmbTipo = new();
    private readonly TextBox txtDescripcion = new();
    private readonly TextBox txtLote = new();
    private readonly DateTimePicker dtpCaducidad = new();
    private readonly TextBox txtTotales = new();
    private readonly ComboBox cmbParametro = new();
    private readonly ComboBox cmbDetalle = new();
    private readonly DateTimePicker dtpFecha = new();
    private readonly TextBox txtResultado = new();

    // El análisis tal como se ha guardado (null si se ha borrado o cancelado)
    public Analisis? Guardado { get; private set; }

    // descripciones: las ya apuntadas, para que se autocompleten al escribir
    public FormAnalisis(Analisis? analisis, IEnumerable<string> descripciones)
    {
        bool nuevo = analisis == null || analisis.Id == 0;
        this.analisis = analisis ?? new Analisis(0, "", "", null, null, null, null, null, DateTime.Today, null);

        Text = nuevo ? "Nuevo análisis" : "Corregir análisis";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(30, 20, 30, 10) };

        // Tipo y fecha del análisis en una fila
        cmbTipo.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbTipo.Width = 160;
        cmbTipo.Items.AddRange(ListasAnalisis.Tipos);
        cmbTipo.SelectedItem = ListasAnalisis.Tipos.FirstOrDefault(t => t == this.analisis.Tipo);
        dtpFecha.Format = DateTimePickerFormat.Short;
        dtpFecha.Width = 160;
        dtpFecha.MaxDate = DateTime.Today;
        dtpFecha.ShowCheckBox = true;   // sin marcar = todavía sin fecha
        dtpFecha.Value = this.analisis.FechaAnalisis is DateTime f && f <= DateTime.Today ? f : DateTime.Today;
        dtpFecha.Checked = this.analisis.FechaAnalisis != null;
        layout.Controls.Add(Fila(campos.Campo("Tipo", cmbTipo), campos.Campo("Fecha análisis", dtpFecha)));

        txtDescripcion.Width = Ancho;
        txtDescripcion.MaxLength = AnalisisServicio.LargoDescripcion;
        txtDescripcion.Text = this.analisis.Descripcion;
        txtDescripcion.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        txtDescripcion.AutoCompleteSource = AutoCompleteSource.CustomSource;
        txtDescripcion.AutoCompleteCustomSource.AddRange(descripciones.Distinct().ToArray());
        layout.Controls.Add(campos.Campo("Descripción", txtDescripcion));

        // Lote y caducidad (si solo se sabe el mes, el día 1)
        txtLote.Width = 330;
        txtLote.MaxLength = AnalisisServicio.LargoLote;
        txtLote.Text = this.analisis.Lote ?? "";
        dtpCaducidad.Format = DateTimePickerFormat.Short;
        dtpCaducidad.Width = 160;
        dtpCaducidad.ShowCheckBox = true;
        dtpCaducidad.Value = this.analisis.Caducidad ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        dtpCaducidad.Checked = this.analisis.Caducidad != null;
        layout.Controls.Add(Fila(campos.Campo("Lote", txtLote), campos.Campo("Caducidad", dtpCaducidad)));

        txtTotales.Width = Ancho;
        txtTotales.MaxLength = AnalisisServicio.LargoTotales;
        txtTotales.Text = this.analisis.AnalisisTotales ?? "";
        layout.Controls.Add(campos.Campo("Análisis totales", txtTotales));

        // Parámetro de la lista; el detalle se elige de la lista o se escribe
        cmbParametro.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbParametro.Width = 200;
        cmbParametro.Items.Add("");
        cmbParametro.Items.AddRange(ListasAnalisis.Parametros);
        if (this.analisis.Parametro is string p && !ListasAnalisis.Parametros.Contains(p))
            cmbParametro.Items.Add(p);   // uno que ya no está en la lista
        cmbParametro.SelectedItem = this.analisis.Parametro ?? "";
        cmbDetalle.DropDownStyle = ComboBoxStyle.DropDown;
        cmbDetalle.Width = 290;
        cmbDetalle.MaxLength = AnalisisServicio.LargoDetalle;
        cmbDetalle.MaxDropDownItems = 20;
        cmbDetalle.Items.AddRange(ListasAnalisis.Detalles);
        cmbDetalle.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        cmbDetalle.AutoCompleteSource = AutoCompleteSource.ListItems;
        cmbDetalle.Text = this.analisis.ParametroDetalle ?? "";
        layout.Controls.Add(Fila(campos.Campo("Parámetro", cmbParametro), campos.Campo("Parámetro - detalle", cmbDetalle)));

        txtResultado.Multiline = true;
        txtResultado.Height = 70;
        txtResultado.Width = Ancho;
        txtResultado.MaxLength = AnalisisServicio.LargoResultado;
        txtResultado.ScrollBars = ScrollBars.Vertical;
        txtResultado.Text = this.analisis.Resultado ?? "";
        layout.Controls.Add(campos.Campo("Resultado", txtResultado));

        var btnGuardar = Tema.BotonPrincipal("Guardar", 130);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;

        var botones = new List<Button> { btnGuardar, btnCancelar };
        if (!nuevo)
        {
            var btnBorrar = Tema.BotonSecundario("Borrar", 110);
            btnBorrar.ForeColor = Tema.Error;
            btnBorrar.Click += (s, e) => Borrar();
            botones.Add(btnBorrar);
        }
        layout.Controls.Add(Componentes.Acciones(botones.ToArray()));

        Controls.Add(layout);
        Shown += (s, e) => (cmbTipo.SelectedItem == null ? cmbTipo : (Control)txtResultado).Focus();
    }

    private static FlowLayoutPanel Fila(params Control[] controles)
    {
        var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        fila.Controls.AddRange(controles);
        return fila;
    }

    private void Guardar()
    {
        campos.LimpiarErrores();

        if (cmbTipo.SelectedItem is not string tipo)
        {
            campos.MarcarError(cmbTipo, "Elige el tipo.");
            cmbTipo.Focus();
            return;
        }
        if (txtDescripcion.Text.Trim() == "")
        {
            campos.MarcarError(txtDescripcion, "Escribe la descripción.");
            txtDescripcion.Focus();
            return;
        }

        try
        {
            Guardado = servicio.Guardar(analisis with
            {
                Tipo = tipo,
                Descripcion = txtDescripcion.Text,
                Lote = txtLote.Text,
                Caducidad = dtpCaducidad.Checked ? dtpCaducidad.Value : null,
                AnalisisTotales = txtTotales.Text,
                Parametro = cmbParametro.SelectedItem as string,
                ParametroDetalle = cmbDetalle.Text,
                FechaAnalisis = dtpFecha.Checked ? dtpFecha.Value : null,
                Resultado = txtResultado.Text
            });
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Informacion(this, "No se ha guardado", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar el análisis.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }

    private void Borrar()
    {
        if (!Mensajes.Confirmar(this, "Borrar análisis", "¿Borrar este análisis? No se puede deshacer.", aviso: true))
            return;
        try
        {
            servicio.Borrar(analisis);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Informacion(this, "No se ha borrado", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo borrar el análisis.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
