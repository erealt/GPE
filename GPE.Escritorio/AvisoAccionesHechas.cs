using GPE.Datos;

namespace GPE.Escritorio;

// Lista de las acciones que los trabajadores han marcado hechas desde la web.
// Se abre con "Ver" desde el aviso de la esquina cuando hay más de una.
public class FormAvisoAccionesHechas : Form
{
    private readonly ListView lista = new();
    private readonly Button btnAbrir = Tema.BotonPrincipal("Abrir ficha", 160);

    // La ficha que el usuario ha elegido abrir, o null si ha pulsado "Cerrar"
    public int? NoConformidadElegida { get; private set; }

    public FormAvisoAccionesHechas(List<AccionHechaWeb> avisos)
    {
        Text = "Acciones hechas desde la web";
        ClientSize = new Size(1000, 440);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        var lblTitulo = new Label
        {
            Text = $"✔  {avisos.Count} acciones hechas desde la web",
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(20, 15, 20, 0),
            BackColor = Tema.AvisoFondo,
            ForeColor = Tema.Aviso,
            Font = Tema.FuenteAviso
        };
        var lblExplicacion = new Label
        {
            Text = "Ya salen como hechas en su ficha. Elige una para abrirla.",
            Dock = DockStyle.Top,
            Height = 45,
            Padding = new Padding(20, 10, 20, 0),
            ForeColor = Tema.Texto
        };

        lista.Dock = DockStyle.Fill;
        lista.View = View.Details;
        lista.FullRowSelect = true;
        lista.MultiSelect = false;
        lista.HideSelection = false;
        lista.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lista.Columns.Add("Hecha el", 130);
        lista.Columns.Add("Quién", 140);
        lista.Columns.Add("Registro", 170);
        lista.Columns.Add("Acción", 330);
        lista.Columns.Add("Comentario", 180);

        foreach (var a in avisos)
            lista.Items.Add(new ListViewItem(new[]
            {
                a.HechaEn.ToString("dd/MM/yyyy HH:mm", Componentes.Es),
                a.Persona,
                a.Registro,
                a.Accion,
                a.Comentario ?? ""
            }) { Tag = a, ToolTipText = a.Accion });
        lista.ShowItemToolTips = true;
        lista.SelectedIndexChanged += (s, e) => btnAbrir.Enabled = lista.SelectedItems.Count > 0;
        lista.DoubleClick += (s, e) => Abrir();

        var marcoLista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 0) };
        marcoLista.Controls.Add(lista);

        var btnCerrar = Tema.BotonSecundario("Cerrar", 140);
        btnCerrar.DialogResult = DialogResult.Cancel;
        btnAbrir.Click += (s, e) => Abrir();

        var botones = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(20, 15, 20, 0)
        };
        foreach (var boton in new[] { btnAbrir, btnCerrar })
        {
            boton.Height = 40;
            boton.Margin = new Padding(10, 0, 0, 0);
            botones.Controls.Add(boton);
        }

        Controls.Add(marcoLista);
        Controls.Add(botones);
        Controls.Add(lblExplicacion);
        Controls.Add(lblTitulo);

        AcceptButton = btnAbrir;
        CancelButton = btnCerrar;

        if (lista.Items.Count > 0) lista.Items[0].Selected = true;
        btnAbrir.Enabled = lista.SelectedItems.Count > 0;
    }

    // Abre la lista y, si se elige una, devuelve su ficha
    public static int? Elegir(IWin32Window padre, List<AccionHechaWeb> avisos)
    {
        using var ventana = new FormAvisoAccionesHechas(avisos);
        ventana.ShowDialog(padre);
        return ventana.NoConformidadElegida;
    }

    private void Abrir()
    {
        if (lista.SelectedItems.Count == 0) return;
        NoConformidadElegida = ((AccionHechaWeb)lista.SelectedItems[0].Tag!).NoConformidadId;
        DialogResult = DialogResult.OK;
    }
}
