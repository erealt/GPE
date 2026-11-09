using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace GPE.Escritorio;

// Tarjeta blanca con las esquinas redondeadas sobre el fondo gris; con Raya, una franja de color arriba
public class TarjetaRedondeada : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radio { get; set; } = 12;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? Raya { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Relleno { get; set; } = Color.White;

    public TarjetaRedondeada()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Fondo;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var caja = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var ruta = Componentes.Redondeado(caja, Radio))
        {
            using (var relleno = new SolidBrush(Relleno))
                e.Graphics.FillPath(relleno, ruta);
            if (Raya is Color raya)
            {
                // La franja se recorta con la forma de la tarjeta para que siga las esquinas
                var antes = e.Graphics.Clip;
                e.Graphics.SetClip(ruta);
                using var pincel = new SolidBrush(raya);
                e.Graphics.FillRectangle(pincel, 0, 0, Width, 4);
                e.Graphics.Clip = antes;
            }
            using var borde = new Pen(Tema.Linea);
            e.Graphics.DrawPath(borde, ruta);
        }
    }
}

// Etiqueta de color redondeada ("EN MARCHA", "PARADA 12 min"...), pegada a la izquierda de su sitio
public class Pastilla : Control
{
    private Color fondo = Tema.Linea, colorTexto = Tema.Texto;

    public Pastilla()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Font = Tema.FuenteEtiqueta;
    }

    public void Poner(string texto, Color fondo, Color colorTexto)
    {
        if (Text == texto && this.fondo == fondo && this.colorTexto == colorTexto) return;
        Text = texto;
        this.fondo = fondo;
        this.colorTexto = colorTexto;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        if (string.IsNullOrEmpty(Text)) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int alto = Math.Min(24, Height);
        var medida = TextRenderer.MeasureText(Text, Font);
        var caja = new Rectangle(0, (Height - alto) / 2, Math.Min(medida.Width + 18, Width - 1), alto);
        using (var ruta = Componentes.Redondeado(caja, alto / 2))
        using (var pincel = new SolidBrush(fondo))
            e.Graphics.FillPath(pincel, ruta);
        TextRenderer.DrawText(e.Graphics, Text, Font, caja, colorTexto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
