using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Fotos de la ficha de calidad. A diferencia de las devoluciones, no se guardan al momento:
// se guardan con "Guardar" junto con el resto de la ficha (y así también vale para un registro nuevo).
// Las fotos del móvil llegan al ordenador por OneDrive: "Añadir fotos" abre su carpeta de la cámara.
public class ControlFotosNc : UserControl
{
    private readonly NoConformidadServicio servicio = new();

    // Id > 0: guardada en NC_FOTO. Ruta: añadida ahora, pendiente de guardar.
    private record Foto(int Id, string Nombre, int Tamano, string Usuario, string Fecha, string? Ruta);

    private readonly List<int> quitadas = new();
    private readonly ListView lista = new();
    private readonly Label lblAyuda = new();
    private readonly Button btnAnadir = Tema.BotonSecundario("+  Añadir fotos", 160);
    private readonly Button btnVer = Tema.BotonSecundario("Ver", 90);
    private readonly Button btnQuitar = Tema.BotonSecundario("Quitar", 90);
    private bool soloLectura;

    public event EventHandler? Cambiado;

    // Para guardar con la ficha
    public IEnumerable<string> Nuevas => Fotos().Where(f => f.Ruta != null).Select(f => f.Ruta!);
    public IEnumerable<int> Quitadas => quitadas;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool SoloLectura
    {
        get => soloLectura;
        set
        {
            soloLectura = value;
            btnAnadir.Visible = btnQuitar.Visible = !value;
            lista.AllowDrop = !value;
            lblAyuda.Visible = !value;
        }
    }

    public ControlFotosNc()
    {
        Height = 250;
        Dock = DockStyle.Top;
        BackColor = Color.White;

        lista.Dock = DockStyle.Fill;
        lista.View = View.Details;
        lista.FullRowSelect = true;
        lista.MultiSelect = false;
        lista.HideSelection = false;
        lista.BorderStyle = BorderStyle.FixedSingle;
        lista.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lista.Columns.Add("Foto", 340);
        lista.Columns.Add("Tamaño", 90);
        lista.Columns.Add("Añadida por", 170);
        lista.Columns.Add("Fecha", 160);
        lista.SelectedIndexChanged += (s, e) => ActualizarBotones();
        lista.DoubleClick += (s, e) => Ver();
        lista.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete && !soloLectura) Quitar();
        };

        // Arrastrar fotos desde el Explorador (por ejemplo, desde la carpeta de OneDrive)
        lista.AllowDrop = true;
        lista.DragEnter += (s, e) =>
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        lista.DragDrop += (s, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] rutas) Anadir(rutas);
        };

        lblAyuda.Dock = DockStyle.Top;
        lblAyuda.Height = 26;
        lblAyuda.ForeColor = Tema.Suave;
        lblAyuda.Font = Tema.FuentePequena;
        lblAyuda.Text = "Las fotos del móvil llegan solas a OneDrive. Pulsa \"Añadir fotos\" o arrástralas aquí. " +
                        "Se guardan al guardar la ficha.";

        var botones = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(0, 10, 0, 0) };
        foreach (var boton in new[] { btnAnadir, btnVer, btnQuitar })
        {
            boton.Height = 36;
            boton.Margin = new Padding(0, 0, 8, 0);
            botones.Controls.Add(boton);
        }
        btnAnadir.Click += (s, e) => Elegir();
        btnVer.Click += (s, e) => Ver();
        btnQuitar.Click += (s, e) => Quitar();

        Controls.Add(lista);
        Controls.Add(lblAyuda);
        Controls.Add(botones);
        ActualizarBotones();
    }

    // Las fotos ya guardadas del registro (nada si es nuevo)
    public void Cargar(int noConformidadId)
    {
        lista.Items.Clear();
        quitadas.Clear();
        if (noConformidadId == 0) return;
        try
        {
            foreach (var foto in servicio.ObtenerFotos(noConformidadId))
                Poner(new Foto(foto.Id, foto.NombreArchivo, foto.TamanoBytes, foto.Usuario,
                    foto.SubidoEn.ToString("dd/MM/yyyy HH:mm", Componentes.Es), null));
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar las fotos.", ex);
        }
        ActualizarBotones();
    }

    private void Poner(Foto foto)
    {
        var item = new ListViewItem(new[] { foto.Nombre, FormatoTamano(foto.Tamano), foto.Usuario, foto.Fecha }) { Tag = foto };
        if (foto.Ruta != null) item.ForeColor = Tema.Aviso;   // pendiente de guardar
        lista.Items.Add(item);
    }

    private IEnumerable<Foto> Fotos() => lista.Items.Cast<ListViewItem>().Select(i => (Foto)i.Tag!);

    // Añadir

    private void Elegir()
    {
        using var dialogo = new OpenFileDialog
        {
            Title = "Añadir fotos",
            Multiselect = true,
            Filter = $"Fotos|{string.Join(";", NoConformidadServicio.ExtensionesFoto.Select(e => "*" + e))}|Todos los archivos|*.*"
        };
        if (CarpetaOneDrive() is string carpeta) dialogo.InitialDirectory = carpeta;
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        Anadir(dialogo.FileNames);
    }

    // La carpeta de la cámara de OneDrive (la app del móvil sube ahí las fotos); si no está, OneDrive
    private static string? CarpetaOneDrive()
    {
        var raices = new[] { "OneDriveCommercial", "OneDrive", "OneDriveConsumer" }
            .Select(Environment.GetEnvironmentVariable)
            .OfType<string>()
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string raiz in raices)
            foreach (string imagenes in new[] { "Imágenes", "Pictures", "Fotos", "Photos" })
                foreach (string camara in new[] { "Álbum de cámara", "Camera Roll", "Carrete" })
                {
                    string ruta = Path.Combine(raiz, imagenes, camara);
                    if (Directory.Exists(ruta)) return ruta;
                }
        return raices.FirstOrDefault();
    }

    private void Anadir(IEnumerable<string> rutas)
    {
        var errores = new List<string>();
        int anadidas = 0;
        foreach (string ruta in rutas.Where(File.Exists))
        {
            if (Fotos().Any(f => string.Equals(f.Ruta, ruta, StringComparison.OrdinalIgnoreCase))) continue;   // ya está
            if (NoConformidadServicio.ErrorFoto(ruta) is string error)
            {
                errores.Add(error);
                continue;
            }
            Poner(new Foto(0, Path.GetFileName(ruta), (int)new FileInfo(ruta).Length, "Pendiente de guardar", "", ruta));
            anadidas++;
        }

        if (errores.Count > 0)
            Mensajes.Aviso(this, "Fotos no añadidas", string.Join("\n\n", errores));
        if (anadidas == 0) return;
        ActualizarBotones();
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    // Ver y quitar

    private void Ver()
    {
        if (Seleccionada() is not Foto elegida) return;

        // El visor pide cada foto por su id: las pendientes van con id negativo y se leen del disco
        var fotos = Fotos().ToList();
        var infos = fotos.Select((f, i) => new AdjuntoInfo(f.Ruta == null ? f.Id : -(i + 1), f.Nombre, f.Tamano,
            DateTime.Now, f.Usuario)).ToList();
        byte[] Contenido(int id) => id > 0
            ? servicio.ObtenerContenidoFoto(id)
            : File.ReadAllBytes(fotos[-id - 1].Ruta!);

        try
        {
            using var visor = new FormVisorImagen(infos, fotos.IndexOf(elegida), Contenido);
            visor.ShowDialog(this);
        }
        catch (Exception ex) when (ex is DbException or IOException)
        {
            Mensajes.Error(this, "No se pudo abrir la foto.", ex);
        }
    }

    private void Quitar()
    {
        if (Seleccionada() is not Foto foto) return;
        if (!Mensajes.Confirmar(this, "Quitar foto", $"¿Quitar \"{foto.Nombre}\"? Se quitará al guardar la ficha."))
            return;

        if (foto.Id > 0) quitadas.Add(foto.Id);
        lista.Items.Remove(lista.SelectedItems[0]);
        ActualizarBotones();
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    private Foto? Seleccionada() => lista.SelectedItems.Count > 0 ? lista.SelectedItems[0].Tag as Foto : null;

    private void ActualizarBotones() => btnVer.Enabled = btnQuitar.Enabled = Seleccionada() != null;

    private static string FormatoTamano(int bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024):0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}
