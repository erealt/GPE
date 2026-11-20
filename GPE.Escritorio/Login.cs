using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Entrada al programa con usuario y contraseña: lo que se haga queda a su nombre en el historial
public class FormLogin : Form
{
    private readonly LoginServicio login = new();
    private readonly DevolucionServicio servicio = new();

    private readonly TextBox txtUsuario = new();
    private readonly TextBox txtClave = new();
    private readonly Label lblError = new();
    private readonly Button btnEntrar = Tema.BotonPrincipal("Entrar", 120);

    public FormLogin()
    {
        Text = "GPE - Entrar";
        ClientSize = new Size(420, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        var lblTitulo = new Label
        {
            Text = "Entrar en GPE",
            Font = Tema.FuenteTituloVentana,
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

        var lblUsuario = new Label { Text = "Usuario", ForeColor = Tema.Texto, Location = new Point(32, 100), AutoSize = true };
        txtUsuario.Location = new Point(32, 122);
        txtUsuario.Width = 356;

        var lblClave = new Label { Text = "Contraseña", ForeColor = Tema.Texto, Location = new Point(32, 160), AutoSize = true };
        txtClave.Location = new Point(32, 182);
        txtClave.Width = 356;
        txtClave.UseSystemPasswordChar = true;

        lblError.ForeColor = Tema.Error;
        lblError.Location = new Point(32, 218);
        lblError.AutoSize = true;

        btnEntrar.Location = new Point(268, 260);
        btnEntrar.Click += (s, e) => Entrar();

        AcceptButton = btnEntrar;
        Controls.AddRange(new Control[] { lblTitulo, lblSubtitulo, lblUsuario, txtUsuario, lblClave, txtClave, lblError, btnEntrar });
    }

    private void Entrar()
    {
        lblError.Text = "";

        Usuario? usuario;
        try
        {
            usuario = login.Entrar(txtUsuario.Text, txtClave.Text);
            if (usuario != null)
                Sesion.Roles = servicio.ObtenerRoles(usuario.Id).ToHashSet();
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo comprobar el usuario en la base de datos.", ex);
            return;
        }

        if (usuario == null)
        {
            lblError.Text = "Usuario o contraseña incorrectos.";
            txtClave.Clear();
            txtClave.Focus();
            return;
        }

        if (Sesion.Roles.Count == 0)
            Mensajes.Informacion(this, "Sin permisos",
                $"{usuario.Nombre} no tiene ningún rol asignado: podrá consultar las devoluciones, pero no hacer ningún paso.");

        Sesion.UsuarioActual = usuario;
        DialogResult = DialogResult.OK;
    }
}
