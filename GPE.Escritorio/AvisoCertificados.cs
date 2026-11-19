using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Lista de los certificados de proveedores activos que ya han caducado o caducan en 30 días o menos.
// Se abre con "Ver" desde el aviso pequeño de la esquina o desde la pantalla Proveedores.
public class FormAvisoCertificados : Form
{
    private readonly ListView lista = new();
    private readonly Button btnAbrir = Tema.BotonPrincipal("Abrir proveedor", 180);

    // El proveedor que el usuario ha elegido abrir, o null si ha pulsado "Ahora no"
    public int? ProveedorElegido { get; private set; }

    public FormAvisoCertificados(List<CertificadoPorCaducar> certificados)
    {
        Text = "Certificados de proveedores";
        ClientSize = new Size(900, 440);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        var lblTitulo = new Label
        {
            Text = "⚠  " + Resumen(certificados),
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(20, 15, 20, 0),
            BackColor = Tema.AvisoFondo,
            ForeColor = Tema.Aviso,
            Font = Tema.FuenteAviso
        };
        var lblExplicacion = new Label
        {
            Text = $"Certificados de proveedores activos que caducan en {ProveedorServicio.DiasAvisoCertificado} días o menos. " +
                   "Dejan de salir cuando pones la nueva fecha en la ficha del proveedor.",
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
        lista.Columns.Add("Nº", 60);
        lista.Columns.Add("Proveedor", 300);
        lista.Columns.Add("Certificado", 140);
        lista.Columns.Add("Caduca el", 110);
        lista.Columns.Add("", 210);

        // Primero los ya caducados, y dentro de cada grupo el que caduca antes
        foreach (var c in certificados.OrderBy(c => c.Certificado.Caduca))
        {
            var item = new ListViewItem(new[]
            {
                c.NumeroProveedor?.ToString() ?? CamposProveedor.SinNota,
                c.NombreProveedor,
                c.Certificado.Nombre,
                c.Certificado.Caduca.ToString("dd/MM/yyyy", Componentes.Es),
                ProveedorServicio.TextoCaducidad(c.Certificado)
            }) { Tag = c, UseItemStyleForSubItems = false };
            bool caducado = ProveedorServicio.Estado(c.Certificado) == ProveedorServicio.EstadoCertificado.Caducado;
            item.SubItems[4].ForeColor = caducado ? Tema.Error : Tema.Aviso;
            item.SubItems[4].Font = Tema.FuenteNegrita;
            lista.Items.Add(item);
        }
        lista.SelectedIndexChanged += (s, e) => btnAbrir.Enabled = lista.SelectedItems.Count > 0;
        lista.DoubleClick += (s, e) => Abrir();

        var marcoLista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 0) };
        marcoLista.Controls.Add(lista);

        var btnMasTarde = Tema.BotonSecundario("Cerrar", 140);
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

    // "2 certificados caducados y 1 certificado caduca pronto"
    public static string Resumen(List<CertificadoPorCaducar> certificados)
    {
        int caducados = certificados.Count(c => ProveedorServicio.Estado(c.Certificado) == ProveedorServicio.EstadoCertificado.Caducado);
        int porCaducar = certificados.Count - caducados;
        var partes = new List<string>();
        if (caducados > 0) partes.Add(caducados == 1 ? "1 certificado caducado" : $"{caducados} certificados caducados");
        if (porCaducar > 0) partes.Add(porCaducar == 1 ? "1 certificado caduca pronto" : $"{porCaducar} certificados caducan pronto");
        return string.Join(" y ", partes);
    }

    // Abre la lista y, si se elige uno, devuelve su proveedor
    public static int? Elegir(IWin32Window padre, List<CertificadoPorCaducar> certificados)
    {
        using var ventana = new FormAvisoCertificados(certificados);
        ventana.ShowDialog(padre);
        return ventana.ProveedorElegido;
    }

    private void Abrir()
    {
        if (lista.SelectedItems.Count == 0) return;
        ProveedorElegido = ((CertificadoPorCaducar)lista.SelectedItems[0].Tag!).Certificado.ProveedorId;
        DialogResult = DialogResult.OK;
    }
}
