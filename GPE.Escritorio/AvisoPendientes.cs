using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Aviso al abrir GPE con las devoluciones que esperan algo del usuario (lo lanza ModuloDevoluciones).
public class FormAvisoPendientes : Form
{

    private readonly ListView lista = new();
    private readonly Button btnAbrir = Tema.BotonPrincipal("Abrir devolución", 180);

    // La que el usuario ha elegido abrir, o null si ha pulsado "Ahora no"
    public int? DevolucionElegida { get; private set; }

    public FormAvisoPendientes(List<DevolucionFila> pendientes)
    {
        Text = "Devoluciones pendientes";
        ClientSize = new Size(760, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        int n = pendientes.Count;
        var lblTitulo = new Label
        {
            Text = n == 1
                ? "⚠  Tienes 1 devolución pendiente"
                : $"⚠  Tienes {n} devoluciones pendientes",
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(20, 15, 20, 0),
            BackColor = Tema.AvisoFondo,
            ForeColor = Tema.Aviso,
            Font = Tema.FuenteAviso
        };
        var lblExplicacion = new Label
        {
            Text = "Estas devoluciones están esperando algo de ti. " +
                   "Volverán a salir aquí cada vez que abras GPE, hasta que las hagas.",
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(20, 10, 20, 0),
            ForeColor = Tema.Texto
        };

        lista.Dock = DockStyle.Fill;
        lista.View = View.Details;
        lista.FullRowSelect = true;
        lista.MultiSelect = false;
        lista.HideSelection = false;
        lista.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lista.Columns.Add("Nº devolución", 130);
        lista.Columns.Add("Cliente", 190);
        lista.Columns.Add("Desde", 100);
        lista.Columns.Add("Esperando", 90);
        lista.Columns.Add("Qué falta", 180);

        foreach (var d in pendientes.OrderByDescending(DevolucionServicio.DiasEsperando))
        {
            lista.Items.Add(new ListViewItem(new[]
            {
                d.Numero,
                d.Cliente,
                DevolucionServicio.EsperaDesde(d).ToString("dd/MM/yyyy", Componentes.Es),
                DevolucionServicio.TextoDias(DevolucionServicio.DiasEsperando(d)),
                DevolucionServicio.TextoPendiente(d.Fase)
            }) { Tag = d });
        }
        lista.SelectedIndexChanged += (s, e) => btnAbrir.Enabled = lista.SelectedItems.Count > 0;
        lista.DoubleClick += (s, e) => Abrir();

        var marcoLista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 0) };
        marcoLista.Controls.Add(lista);

        var btnMasTarde = Tema.BotonSecundario("Ahora no", 140);
        btnMasTarde.DialogResult = DialogResult.Cancel;
        btnAbrir.Click += (s, e) => Abrir();

        var botones = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(20, 15, 20, 0)
        };
        foreach (var boton in new[] { btnAbrir, btnMasTarde })
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
        CancelButton = btnMasTarde;

        if (lista.Items.Count > 0) lista.Items[0].Selected = true;
        btnAbrir.Enabled = lista.SelectedItems.Count > 0;
    }

    private void Abrir()
    {
        if (lista.SelectedItems.Count == 0) return;
        DevolucionElegida = ((DevolucionFila)lista.SelectedItems[0].Tag!).Id;
        DialogResult = DialogResult.OK;
    }
}
