using GPE.Datos;
using System.Data.Common;
using System.Diagnostics;

namespace GPE.Escritorio;

// Muestra las fotos de una devolución dentro de la app, con anterior / siguiente.
// Teclas: ← y → para cambiar de foto, Esc para cerrar.
public class FormVisorImagen : Form
{
    private readonly List<AdjuntoInfo> fotos;
    private readonly Func<int, byte[]> cargarContenido;   // id del adjunto → bytes
    private readonly Dictionary<int, byte[]> cache = new();
    private int indice;

    private readonly PictureBox imagen = new();
    private readonly Label lblTitulo = new();
    private readonly Button btnAnterior = Tema.BotonSecundario("◀  Anterior", 130);
    private readonly Button btnSiguiente = Tema.BotonSecundario("Siguiente  ▶", 130);

    public FormVisorImagen(List<AdjuntoInfo> fotos, int indice, Func<int, byte[]> cargarContenido)
    {
        this.fotos = fotos;
        this.indice = indice;
        this.cargarContenido = cargarContenido;

        Size = new Size(1100, 800);
        MinimumSize = new Size(600, 450);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(30, 30, 30);
        Font = Tema.Fuente;
        KeyPreview = true;

        imagen.Dock = DockStyle.Fill;
        imagen.SizeMode = PictureBoxSizeMode.Zoom;   // se ajusta a la ventana sin deformarse
        imagen.BackColor = BackColor;

        lblTitulo.Dock = DockStyle.Fill;
        lblTitulo.ForeColor = Color.White;
        lblTitulo.TextAlign = ContentAlignment.MiddleCenter;

        var btnAbrirFuera = Tema.BotonSecundario("Abrir fuera", 120);
        btnAbrirFuera.Click += (s, e) => AbrirFuera();

        var btnCerrar = Tema.BotonSecundario("Cerrar", 110);
        btnCerrar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCerrar;

        btnAnterior.Click += (s, e) => Mover(-1);
        btnSiguiente.Click += (s, e) => Mover(+1);

        // Barra inferior: [Anterior] [nombre y posición] [Siguiente]   [Abrir fuera] [Cerrar]
        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            ColumnCount = 5,
            BackColor = Color.FromArgb(45, 45, 45),
            Padding = new Padding(12, 10, 12, 10)
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var boton in new[] { btnAnterior, btnSiguiente, btnAbrirFuera, btnCerrar })
        {
            boton.Height = 38;
            boton.Margin = new Padding(6, 0, 0, 0);
        }
        barra.Controls.Add(btnAnterior, 0, 0);
        barra.Controls.Add(lblTitulo, 1, 0);
        barra.Controls.Add(btnSiguiente, 2, 0);
        barra.Controls.Add(btnAbrirFuera, 3, 0);
        barra.Controls.Add(btnCerrar, 4, 0);

        Controls.Add(imagen);
        Controls.Add(barra);

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Left) { Mover(-1); e.Handled = true; }
            if (e.KeyCode == Keys.Right) { Mover(+1); e.Handled = true; }
        };

        Load += (s, e) => Mostrar();
        FormClosed += (s, e) => imagen.Image?.Dispose();
    }

    private void Mover(int salto)
    {
        int nuevo = indice + salto;
        if (nuevo < 0 || nuevo >= fotos.Count) return;
        indice = nuevo;
        Mostrar();
    }

    private void Mostrar()
    {
        var foto = fotos[indice];
        Text = $"{foto.NombreArchivo} · {indice + 1} de {fotos.Count}";
        lblTitulo.Text = $"{foto.NombreArchivo}   ·   {indice + 1} de {fotos.Count}   ·   {foto.Usuario}, {foto.SubidoEn:dd/MM/yyyy HH:mm}";
        btnAnterior.Enabled = indice > 0;
        btnSiguiente.Enabled = indice < fotos.Count - 1;

        var anterior = imagen.Image;
        try
        {
            imagen.Image = CrearImagen(Contenido(foto));
        }
        catch (Exception ex) when (ex is DbException or ArgumentException or OutOfMemoryException)
        {
            // ArgumentException / OutOfMemoryException: el archivo no es una imagen que Windows sepa leer
            imagen.Image = null;
            lblTitulo.Text = $"No se pudo mostrar {foto.NombreArchivo}: {ex.Message}";
        }
        anterior?.Dispose();
    }

    private byte[] Contenido(AdjuntoInfo foto)
    {
        if (!cache.TryGetValue(foto.Id, out var bytes))
            cache[foto.Id] = bytes = cargarContenido(foto.Id);
        return bytes;
    }

    // Las fotos del móvil guardan su orientación aparte (EXIF): se gira para que no salga tumbada
    private static Image CrearImagen(byte[] bytes)
    {
        using var flujo = new MemoryStream(bytes);
        using var original = Image.FromStream(flujo);
        var copia = new Bitmap(original);   // copia independiente del flujo, que se cierra aquí

        const int Orientacion = 0x0112;
        if (original.PropertyIdList.Contains(Orientacion))
        {
            var giro = original.GetPropertyItem(Orientacion)?.Value?[0] switch
            {
                3 => RotateFlipType.Rotate180FlipNone,
                6 => RotateFlipType.Rotate90FlipNone,
                8 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            };
            copia.RotateFlip(giro);
        }
        return copia;
    }

    // Por si se quiere ampliar o editar con el visor de Windows
    private void AbrirFuera()
    {
        var foto = fotos[indice];
        try
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "GPE");
            Directory.CreateDirectory(carpeta);
            string ruta = Path.Combine(carpeta, $"{foto.Id}_{foto.NombreArchivo}");
            File.WriteAllBytes(ruta, Contenido(foto));
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is DbException or IOException or System.ComponentModel.Win32Exception)
        {
            Mensajes.Error(this, "No se pudo abrir el archivo.", ex);
        }
    }
}
