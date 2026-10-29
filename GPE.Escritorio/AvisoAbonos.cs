using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Aviso de devoluciones que llegaron hace días y siguen sin abono.
// ModuloDevoluciones lo muestra al entrar y lo repite cada cierto tiempo mientras quede alguna.
public class FormAvisoAbonos : Form
{

    private readonly ListView lista = new();
    private readonly Button btnAbrir = Tema.BotonPrincipal("Abrir devolución", 180);

    // La que el usuario ha elegido abrir, o null si ha pulsado "Más tarde"
    public int? DevolucionElegida { get; private set; }

    public FormAvisoAbonos(List<DevolucionFila> retrasadas)
    {
        Text = "Abonos pendientes";
        ClientSize = new Size(760, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        int n = retrasadas.Count;
        var lblTitulo = new Label
        {
            Text = n == 1
                ? "⚠  1 devolución está esperando el abono"
                : $"⚠  {n} devoluciones están esperando el abono",
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(20, 15, 20, 0),
            BackColor = Tema.AvisoFondo,
            ForeColor = Tema.Aviso,
            Font = Tema.FuenteAviso
        };
        var lblExplicacion = new Label
        {
            Text = "La mercancía ya está en el almacén y el cliente está esperando el abono. " +
                   "Revisa la devolución y registra el abono. Este aviso se repetirá hasta que se registre.",
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
        lista.Columns.Add("Llegada", 100);
        lista.Columns.Add("Llegó hace", 90);
        lista.Columns.Add("Pendiente de", 180);

        foreach (var d in retrasadas.OrderByDescending(d => DevolucionServicio.DiasSinAbono(d)))
        {
            int dias = DevolucionServicio.DiasSinAbono(d) ?? 0;
            lista.Items.Add(new ListViewItem(new[]
            {
                d.Numero,
                d.Cliente,
                d.FechaLlegada?.ToString("dd/MM/yyyy", Componentes.Es) ?? "",
                DevolucionServicio.TextoDias(dias),
                d.Fase == Fase.MercanciaRecibida ? "Revisión del almacén" : "Registrar el abono"
            }) { Tag = d });
        }
        lista.SelectedIndexChanged += (s, e) => btnAbrir.Enabled = lista.SelectedItems.Count > 0;
        lista.DoubleClick += (s, e) => Abrir();

        var marcoLista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 0) };
        marcoLista.Controls.Add(lista);

        var btnMasTarde = Tema.BotonSecundario("Recordármelo más tarde", 220);
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
