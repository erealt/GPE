using System.Data.Common;
using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Ventana pequeña para añadir, cambiar o quitar un certificado del proveedor. Guarda al momento.
public class FormCertificadoProveedor : Form
{
    private readonly ProveedorServicio servicio = new();
    private readonly CamposFormulario campos = new();
    private readonly CertificadoProveedor original;
    private readonly ComboBox cmbNombre = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly DateTimePicker dtpCaduca = new() { Format = DateTimePickerFormat.Short };

    // certificado == null: certificado nuevo de este proveedor
    public FormCertificadoProveedor(Proveedor proveedor, CertificadoProveedor? certificado)
    {
        bool nuevo = certificado == null;
        original = certificado ?? new CertificadoProveedor(0, proveedor.Id, "", DateTime.Today.AddYears(1), null);

        Text = nuevo ? $"Nuevo certificado - {proveedor.Nombre}" : $"Certificado {original.Nombre} - {proveedor.Nombre}";
        ClientSize = new Size(660, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        cmbNombre.Items.AddRange(ProveedorServicio.NombresCertificado);
        cmbNombre.MaxLength = 100;
        cmbNombre.Text = original.Nombre;
        dtpCaduca.Value = original.Caduca;

        var tarjeta = Componentes.Tarjeta(nuevo ? "Nuevo certificado" : "Certificado",
            $"Se avisará al abrir GPE {ProveedorServicio.DiasAvisoCertificado} días antes de que caduque.");
        var rejilla = Componentes.Rejilla(2);
        rejilla.Controls.Add(campos.Campo("Certificado", cmbNombre));
        rejilla.Controls.Add(campos.Campo("Caduca el", dtpCaduca));
        tarjeta.Controls.Add(rejilla);
        tarjeta.Margin = Padding.Empty;

        var btnGuardar = Tema.BotonPrincipal(nuevo ? "Guardar certificado" : "Guardar cambios", 190);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 120);
        btnCancelar.DialogResult = DialogResult.Cancel;

        var botones = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(20, 15, 20, 0)
        };
        var lista = new List<Button> { btnGuardar, btnCancelar };
        if (!nuevo)
        {
            var btnQuitar = Tema.BotonSecundario("Quitar certificado", 170);
            btnQuitar.ForeColor = Tema.Error;
            btnQuitar.Click += (s, e) => Quitar();
            lista.Add(btnQuitar);
        }
        foreach (var boton in lista)
        {
            boton.Height = 40;
            boton.Margin = new Padding(10, 0, 0, 0);
            botones.Controls.Add(boton);
        }

        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 20, 0) };
        marco.Controls.Add(tarjeta);
        Controls.Add(marco);
        Controls.Add(botones);

        AcceptButton = btnGuardar;
        CancelButton = btnCancelar;
        Shown += (s, e) => cmbNombre.Focus();
    }

    private void Guardar()
    {
        campos.LimpiarErrores();
        var leido = original with { Nombre = cmbNombre.Text.Trim(), Caduca = dtpCaduca.Value.Date };
        if (ProveedorServicio.ErrorCertificado(leido) is string error)
        {
            campos.MarcarError(cmbNombre, error);
            cmbNombre.Focus();
            return;
        }

        try
        {
            servicio.GuardarCertificado(leido);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede guardar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar el certificado.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }

    private void Quitar()
    {
        if (!Mensajes.Confirmar(this, "Quitar certificado",
                $"¿Quitar el certificado {original.Nombre} de este proveedor?", aviso: true))
            return;

        try
        {
            servicio.BorrarCertificado(original);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede quitar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo quitar el certificado.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
