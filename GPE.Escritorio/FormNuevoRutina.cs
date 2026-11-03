using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Ventanita de la plantilla para añadir una máquina (columna) o un punto de control (fila).
public class FormNuevoRutina : Form
{
    private readonly RutinaServicio servicio = new();
    private readonly bool esMaquina;
    private readonly CamposFormulario campos = new();
    private readonly TextBox txtNombre = new();
    private readonly RadioButton rbBaja = new() { Text = Planta.Nombre(Planta.Baja), AutoSize = true, Checked = true };
    private readonly RadioButton rbSuperior = new() { Text = Planta.Nombre(Planta.Superior), AutoSize = true };
    private readonly CheckBox chkNumerico = new() { Text = "Dato numérico", AutoSize = true };

    public FormNuevoRutina(bool esMaquina)
    {
        this.esMaquina = esMaquina;
        Text = esMaquina ? "Añadir máquina" : "Añadir punto de control";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(30, 20, 30, 20)
        };

        txtNombre.Width = 420;
        txtNombre.MaxLength = esMaquina ? RutinaServicio.LargoNombreSeccion : RutinaServicio.LargoNombrePunto;
        layout.Controls.Add(campos.Campo(esMaquina ? "Nombre de la máquina" : "Nombre del punto de control", txtNombre));

        if (esMaquina)
        {
            var plantas = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 15) };
            plantas.Controls.Add(rbBaja);
            plantas.Controls.Add(rbSuperior);
            layout.Controls.Add(new Label { Text = "Planta", AutoSize = true, Font = Tema.FuentePequenaNegrita });
            layout.Controls.Add(plantas);
        }
        else
        {
            chkNumerico.Margin = new Padding(0, 0, 0, 15);
            layout.Controls.Add(chkNumerico);
        }

        layout.Controls.Add(new Label
        {
            Text = esMaquina
                ? "Se añade al final de su planta. Después marca qué puntos se revisan en ella."
                : "Se añade al final. Después marca en qué máquinas se revisa.",
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena,
            Margin = new Padding(0, 0, 0, 10)
        });

        var btnAnadir = Tema.BotonPrincipal("Añadir", 130);
        btnAnadir.Click += (s, e) => Anadir();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        AcceptButton = btnAnadir;
        CancelButton = btnCancelar;
        layout.Controls.Add(Componentes.Acciones(btnAnadir, btnCancelar));

        Controls.Add(layout);
    }

    private void Anadir()
    {
        try
        {
            if (esMaquina)
                servicio.AnadirSeccion(txtNombre.Text, rbSuperior.Checked ? Planta.Superior : Planta.Baja);
            else
                servicio.AnadirPunto(txtNombre.Text, chkNumerico.Checked);
        }
        catch (ReglaNegocioException ex)
        {
            campos.MarcarError(txtNombre, ex.Message);
            txtNombre.Focus();
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, esMaquina ? "No se pudo añadir la máquina." : "No se pudo añadir el punto.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
