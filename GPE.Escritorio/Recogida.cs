using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

public class Recogida : PantallaFase
{
    private readonly DevolucionServicio servicio = new();
    private readonly DevolucionFila devolucion;
    private RecogidaGuardada? existente;
    private bool cargaCorrecta;

    public const int PasoRecogida = Fase.RecogidaSolicitada;

    private bool SinPermiso => !Sesion.TieneRol(Rol.Transporte);
    private bool SoloLectura => devolucion.Fase >= Fase.MercanciaRecibida || SinPermiso;

    private readonly ComboBox cmbTransportista = new();
    private readonly Label lblTelefonoTransportista = new();
    private readonly TextBox txtFechaRegistro = new();   // solo lectura: cuándo se registró el transportista
    private readonly DateTimePicker dtpRecogida = new();
    private readonly TextBox txtDireccion = new();
    private readonly TextBox txtTelefono = new();
    private readonly TextBox txtObservaciones = new();

    // Mensajes de error que aparecen debajo de cada campo
    private readonly CamposFormulario campos = new();

    public Recogida(DevolucionFila devolucion)
    {
        this.devolucion = devolucion;

        Text = $"Gestión de recogida · {devolucion.Numero}";
        Size = new Size(1100, 820);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        PrepararControles();
        if (SoloLectura) BloquearCampos();
        Controls.Add(CrearContenido());
    }

    private void PrepararControles()
    {
        cmbTransportista.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbTransportista.SelectedIndexChanged += (s, e) =>
        {
            lblTelefonoTransportista.Text = cmbTransportista.SelectedItem is Transportista { Telefono: not null } t
                ? $"Teléfono de {t.Nombre}: {t.Telefono}"
                : "";
        };

        lblTelefonoTransportista.AutoSize = true;
        lblTelefonoTransportista.ForeColor = Tema.Suave;
        lblTelefonoTransportista.Font = Tema.FuentePequena;
        lblTelefonoTransportista.Margin = new Padding(0, 4, 0, 0);

        txtFechaRegistro.ReadOnly = true;
        txtFechaRegistro.TabStop = false;
        txtFechaRegistro.BackColor = Tema.FondoSoloLectura;

        dtpRecogida.Format = DateTimePickerFormat.Short;
        dtpRecogida.MinDate = devolucion.FechaSolicitud.Date;   // no puede ser antes de la solicitud

        txtDireccion.MaxLength = 200;
        txtDireccion.PlaceholderText = "Calle, número, código postal y población";

        txtTelefono.MaxLength = 30;
        txtTelefono.PlaceholderText = "Ej. 600 123 456";

        txtObservaciones.Multiline = true;
        txtObservaciones.Height = 80;
        txtObservaciones.ScrollBars = ScrollBars.Vertical;
        txtObservaciones.PlaceholderText = "Horario de recogida, muelle de carga, persona de contacto...";

        try
        {
            cmbTransportista.Items.AddRange(servicio.ObtenerTransportistas().Cast<object>().ToArray());
            existente = servicio.ObtenerRecogida(devolucion.Id);
            cargaCorrecta = true;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar los datos de la recogida.", ex);
        }

        if (existente is { } guardada)
        {
            // Ya había una recogida: se cargan sus datos para modificarlos
            cmbTransportista.SelectedItem = cmbTransportista.Items.Cast<Transportista>()
                .FirstOrDefault(t => t.Id == guardada.TransportistaId);
            dtpRecogida.Value = guardada.FechaRecogida < dtpRecogida.MinDate
                ? dtpRecogida.MinDate : guardada.FechaRecogida;
            txtDireccion.Text = guardada.Direccion;
            txtTelefono.Text = guardada.TelefonoContacto ?? "";
            txtObservaciones.Text = guardada.Observaciones ?? "";
            txtFechaRegistro.Text = guardada.CreadaEn.ToString("dd/MM/yyyy HH:mm", Componentes.Es);
        }
        else
        {
            txtFechaRegistro.PlaceholderText = "Se guardará al solicitar la recogida";
            dtpRecogida.Value = DateTime.Today < dtpRecogida.MinDate ? dtpRecogida.MinDate : DateTime.Today;
        }
    }

    private void BloquearCampos()
    {
        cmbTransportista.Enabled = false;
        dtpRecogida.Enabled = false;
        foreach (var texto in new[] { txtDireccion, txtTelefono, txtObservaciones })
        {
            texto.ReadOnly = true;
            texto.BackColor = Color.White;
        }
    }

    private Panel CrearContenido()
    {
        var (scroll, pila) = Componentes.Pagina();

        pila.Controls.Add(Componentes.Cabecera(
            $"DEVOLUCIONES / {devolucion.Numero} / RECOGIDA",
            "Gestión de recogida",
            SoloLectura
                ? $"Recogida de la devolución {devolucion.Numero} de {devolucion.Cliente}."
                : $"Programa la recogida de la devolución {devolucion.Numero} de {devolucion.Cliente}."));
        pila.Controls.Add(Progreso(devolucion, PasoRecogida));
        pila.Controls.Add(Componentes.Resumen(
            ("CLIENTE", devolucion.Cliente),
            ("CÓD. CLIENTE", devolucion.CodigoCliente),
            ("MOTIVO", devolucion.Motivo),
            ("FECHA SOLICITUD", Componentes.Fecha(devolucion.FechaSolicitud))));
        pila.Controls.Add(CrearTarjetaRecogida());
        pila.Controls.Add(CrearAcciones());

        return scroll;
    }

    private Control CrearTarjetaRecogida()
    {
        var tarjeta = Componentes.Tarjeta("Datos de la recogida", SinPermiso
            ? SoloConsulta(Rol.Transporte)
            : SoloLectura
            ? "La mercancía ya ha llegado: la recogida solo se puede consultar."
            : existente == null
                ? "Indica quién recoge la mercancía, dónde y cuándo está previsto."
                : "Esta devolución ya tiene una recogida programada. Puedes modificarla hasta que llegue la mercancía.");

        var rejilla = Componentes.Rejilla(2);

        rejilla.Controls.Add(campos.Campo("Transportista", cmbTransportista, lblTelefonoTransportista));
        rejilla.Controls.Add(campos.Campo("Fecha de registro del transportista", txtFechaRegistro));

        rejilla.Controls.Add(campos.Campo("Fecha de recogida prevista", dtpRecogida));
        rejilla.Controls.Add(campos.Campo("Teléfono de contacto", txtTelefono));

        var campoDireccion = campos.Campo("Dirección de recogida", txtDireccion);
        rejilla.Controls.Add(campoDireccion);
        rejilla.SetColumnSpan(campoDireccion, 2);

        var campoObservaciones = campos.Campo("Observaciones", txtObservaciones);
        rejilla.Controls.Add(campoObservaciones);
        rejilla.SetColumnSpan(campoObservaciones, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control CrearAcciones()
    {
        if (SoloLectura)
            return Componentes.Acciones(BotonCerrar());

        var btnGuardar = Tema.BotonPrincipal(existente == null ? "Solicitar recogida" : "Guardar cambios", 190);
        btnGuardar.Enabled = cargaCorrecta;
        btnGuardar.Click += (s, e) => Guardar();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;   // Esc cierra sin guardar

        return Componentes.Acciones(btnGuardar, btnCancelar);
    }

    private void Guardar()
    {
        // Se marcan todos los campos con error a la vez
        campos.LimpiarErrores();
        Control? primerError = null;

        if (cmbTransportista.SelectedItem is not Transportista)
        {
            campos.MarcarError(cmbTransportista, "Selecciona un transportista.");
            primerError ??= cmbTransportista;
        }

        if (string.IsNullOrWhiteSpace(txtDireccion.Text))
        {
            campos.MarcarError(txtDireccion, "Indica la dirección de recogida.");
            primerError ??= txtDireccion;
        }

        string telefono = txtTelefono.Text.Trim();
        if (telefono != "" && !DevolucionServicio.TelefonoValido(telefono))
        {
            campos.MarcarError(txtTelefono, "Teléfono no válido: usa al menos 9 dígitos (se admiten espacios, guiones y +).");
            primerError ??= txtTelefono;
        }

        if (primerError != null)
        {
            primerError.Focus();
            return;
        }

        var datos = new DatosRecogida(
            devolucion.Id,
            ((Transportista)cmbTransportista.SelectedItem!).Id,
            dtpRecogida.Value,
            txtDireccion.Text.Trim(),
            telefono == "" ? null : telefono,
            string.IsNullOrWhiteSpace(txtObservaciones.Text) ? null : txtObservaciones.Text.Trim());

        try
        {
            servicio.GuardarRecogida(datos, devolucion.FechaSolicitud);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "Revisa los datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar la recogida.", ex);
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
