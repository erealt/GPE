using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Pantalla provisional hasta que haya login: el usuario elige quién es
// para que el historial de cambios sepa a quién atribuir cada modificación.
public class FormSeleccionUsuario : Form
{

    private readonly DevolucionServicio servicio = new();

    private readonly ComboBox cmbUsuario = new();
    private readonly Button btnEntrar = Tema.BotonPrincipal("Entrar", 120);

    public FormSeleccionUsuario()
    {
        Text = "GPE - Identificación";
        ClientSize = new Size(420, 230);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        var lblTitulo = new Label
        {
            Text = "¿Quién eres?",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Tema.Texto,
            Location = new Point(30, 20),
            AutoSize = true
        };

        var lblSubtitulo = new Label
        {
            Text = "Tus cambios quedarán registrados en el historial.",
            ForeColor = Tema.Suave,
            Location = new Point(32, 65),
            AutoSize = true
        };

        cmbUsuario.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbUsuario.Location = new Point(32, 105);
        cmbUsuario.Width = 356;

        btnEntrar.Location = new Point(268, 160);
        btnEntrar.Click += (s, e) => Entrar();

        AcceptButton = btnEntrar;
        Controls.AddRange(new Control[] { lblTitulo, lblSubtitulo, cmbUsuario, btnEntrar });

        Load += (s, e) => CargarUsuarios();
    }

    private void CargarUsuarios()
    {
        try
        {
            foreach (var usuario in servicio.ObtenerUsuarios())
                cmbUsuario.Items.Add(usuario);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar los usuarios de la base de datos.", ex);
            Close();
            return;
        }

        if (cmbUsuario.Items.Count == 0)
        {
            MessageBox.Show(this, "No hay usuarios dados de alta en la tabla USUARIO.",
                "Sin usuarios", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
            return;
        }

        cmbUsuario.SelectedIndex = 0;
    }

    private void Entrar()
    {
        if (cmbUsuario.SelectedItem is not Usuario usuario) return;

        try
        {
            Sesion.Roles = servicio.ObtenerRoles(usuario.Id).ToHashSet();
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar los permisos del usuario.", ex);
            return;
        }

        if (Sesion.Roles.Count == 0)
            MessageBox.Show(this,
                $"{usuario.Nombre} no tiene ningún rol asignado: podrá consultar las devoluciones, pero no hacer ningún paso.",
                "Sin permisos", MessageBoxButtons.OK, MessageBoxIcon.Information);

        Sesion.UsuarioActual = usuario;
        DialogResult = DialogResult.OK;
    }
}
