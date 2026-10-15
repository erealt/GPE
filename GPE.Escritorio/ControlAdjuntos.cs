using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;

namespace GPE.Escritorio;

// Lista de archivos de una devolución con botones Añadir / Ver / Quitar.
// Se usa para las fotos del almacén (FOTO) y para el documento del abono (ABONO).
// Los cambios se guardan en la base de datos al momento, sin esperar a otro botón.
public class ControlAdjuntos : UserControl
{
    private static readonly CultureInfo Es = new("es-ES");

    private readonly DevolucionServicio servicio = new();
    private readonly int devolucionId;
    private readonly string tipo;

    private readonly ListView lista = new();
    private readonly Button btnAnadir;
    private readonly Button btnVer = Tema.BotonSecundario("Ver", 90);
    private readonly Button btnQuitar = Tema.BotonSecundario("Quitar", 90);

    public int Cantidad => lista.Items.Count;

    // En solo lectura solo se pueden ver los archivos, no añadir ni quitar
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool SoloLectura
    {
        get => !btnAnadir.Visible;
        set => btnAnadir.Visible = btnQuitar.Visible = !value;
    }

    public ControlAdjuntos(int devolucionId, string tipo, string textoAnadir)
    {
        this.devolucionId = devolucionId;
        this.tipo = tipo;

        btnAnadir = Tema.BotonSecundario("+  " + textoAnadir, 160);

        Height = 230;
        BackColor = Color.White;

        lista.Dock = DockStyle.Fill;
        lista.View = View.Details;
        lista.FullRowSelect = true;
        lista.MultiSelect = false;
        lista.HideSelection = false;
        lista.BorderStyle = BorderStyle.FixedSingle;
        lista.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lista.Columns.Add("Archivo", 320);
        lista.Columns.Add("Tamaño", 90);
        lista.Columns.Add("Añadido por", 160);
        lista.Columns.Add("Fecha", 150);
        lista.SelectedIndexChanged += (s, e) => ActualizarBotones();
        lista.DoubleClick += (s, e) => Ver();

        var botones = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(0, 10, 0, 0)
        };
        foreach (var boton in new[] { btnAnadir, btnVer, btnQuitar })
        {
            boton.Height = 36;
            boton.Margin = new Padding(0, 0, 8, 0);
            botones.Controls.Add(boton);
        }

        btnAnadir.Click += (s, e) => Anadir();
        btnVer.Click += (s, e) => Ver();
        btnQuitar.Click += (s, e) => Quitar();

        Controls.Add(lista);
        Controls.Add(botones);

        Load += (s, e) => Cargar();
    }

    private void Cargar()
    {
        lista.Items.Clear();
        try
        {
            foreach (var adjunto in servicio.ObtenerAdjuntos(devolucionId, tipo))
            {
                var item = new ListViewItem(new[]
                {
                    adjunto.NombreArchivo,
                    FormatoTamano(adjunto.TamanoBytes),
                    adjunto.Usuario,
                    adjunto.SubidoEn.ToString("dd/MM/yyyy HH:mm", Es)
                }) { Tag = adjunto };
                lista.Items.Add(item);
            }
        }
        catch (DbException ex)
        {
            Error("No se pudieron cargar los archivos.", ex);
        }
        ActualizarBotones();
    }

    private void Anadir()
    {
        var extensiones = DevolucionServicio.Extensiones(tipo);
        using var dialogo = new OpenFileDialog
        {
            Title = btnAnadir.Text.TrimStart('+', ' '),
            Multiselect = true,
            Filter = $"Archivos permitidos|{string.Join(";", extensiones.Select(e => "*" + e))}"
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            servicio.AnadirAdjuntos(devolucionId, tipo, dialogo.FileNames);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "Archivo no válido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
        {
            Error("No se pudieron guardar los archivos.", ex);
            return;
        }
        Cargar();
    }

    // Se copia el archivo a la carpeta temporal y se abre con el programa de Windows
    private static readonly string[] ExtensionesImagen = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

    private static bool EsImagen(AdjuntoInfo adjunto) =>
        ExtensionesImagen.Contains(Path.GetExtension(adjunto.NombreArchivo).ToLowerInvariant());

    // Las imágenes se ven en el visor de la app; el resto (PDF...) con el programa de Windows
    private void Ver()
    {
        if (Seleccionado() is not AdjuntoInfo adjunto) return;

        if (EsImagen(adjunto))
        {
            var imagenes = lista.Items.Cast<ListViewItem>()
                .Select(i => (AdjuntoInfo)i.Tag!)
                .Where(EsImagen)
                .ToList();

            using var visor = new FormVisorImagen(imagenes, imagenes.IndexOf(adjunto), servicio.ObtenerContenidoAdjunto);
            visor.ShowDialog(this);
            return;
        }

        try
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "GPE");
            Directory.CreateDirectory(carpeta);
            string ruta = Path.Combine(carpeta, $"{adjunto.Id}_{adjunto.NombreArchivo}");
            File.WriteAllBytes(ruta, servicio.ObtenerContenidoAdjunto(adjunto.Id));
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is DbException or IOException or System.ComponentModel.Win32Exception)
        {
            Error("No se pudo abrir el archivo.", ex);
        }
    }

    private void Quitar()
    {
        if (Seleccionado() is not AdjuntoInfo adjunto) return;

        if (MessageBox.Show(this, $"¿Quitar \"{adjunto.NombreArchivo}\"?", "Quitar archivo",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            servicio.EliminarAdjunto(adjunto.Id);
        }
        catch (DbException ex)
        {
            Error("No se pudo quitar el archivo.", ex);
            return;
        }
        Cargar();
    }

    private AdjuntoInfo? Seleccionado() =>
        lista.SelectedItems.Count > 0 ? lista.SelectedItems[0].Tag as AdjuntoInfo : null;

    private void ActualizarBotones()
    {
        btnVer.Enabled = btnQuitar.Enabled = Seleccionado() != null;
    }

    private void Error(string mensaje, Exception ex) =>
        MessageBox.Show(this, mensaje + "\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static string FormatoTamano(int bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024):0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}
