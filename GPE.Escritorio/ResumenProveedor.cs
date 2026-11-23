using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Tres recuadros de arriba de la ficha del proveedor, con lo más importante para verlo sin leer las tarjetas:
// evaluación, productos activos y reclamaciones.
// Se crean una vez; Actualizar solo cambia sus textos y colores.
public class ResumenProveedor : TableLayoutPanel
{
    private readonly Recuadro evaluacion = new("Evaluación", ultimo: false);
    private readonly Recuadro activos = new("Productos activos", ultimo: false);
    private readonly Recuadro reclamaciones = new("Reclamaciones", ultimo: true);

    public ResumenProveedor()
    {
        Dock = DockStyle.Fill;
        AutoSize = true;
        ColumnCount = 3;
        RowCount = 1;
        Margin = new Padding(0, 0, 0, 20);
        for (int i = 0; i < 3; i++)
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(evaluacion.Panel, 0, 0);
        Controls.Add(activos.Panel, 1, 0);
        Controls.Add(reclamaciones.Panel, 2, 0);
    }

    // p: lo que hay escrito en la ficha, aunque no esté guardado
    public void Actualizar(Proveedor p, List<ProductoProveedor> productos)
    {
        string? nota = ProveedorServicio.Evaluacion(p);
        evaluacion.Poner(nota ?? CamposProveedor.SinNota,
            nota == null ? "Faltan datos para calcularla"
                         : $"Impacto + estatus + reclamaciones = {(p.Impacto ?? 0) + (p.Estatus ?? 0) + (p.Reclamaciones ?? 0)}",
            Tema.ColorNotaProveedor(nota ?? ""));

        int numeroActivos = productos.Count(x => !x.Baja);
        int deBaja = productos.Count - numeroActivos;
        activos.Poner(numeroActivos.ToString(), deBaja > 0 ? $"y {deBaja} de baja" : "Ninguno de baja", null);

        reclamaciones.Poner(p.Reclamaciones?.ToString() ?? CamposProveedor.SinNota, "Nota de 0 a 3, sale de sus NC", null);
    }

    // Etiqueta, cifra grande y una nota. Con color (A/B/C, BAJO/MEDIO/ALTO) el fondo es el de la nota; sin él, blanco.
    private sealed class Recuadro
    {
        public FlowLayoutPanel Panel { get; }
        private readonly Label etiqueta;
        private readonly Label valor = new() { AutoSize = true, Font = EstiloProveedores.FuenteCifra, Margin = new Padding(0, 6, 0, 2) };
        private readonly Label nota = new() { AutoSize = true, Font = Tema.FuenteNota };

        public Recuadro(string texto, bool ultimo)
        {
            etiqueta = new Label { Text = texto.ToUpper(Componentes.Es), AutoSize = true, Font = Tema.FuenteEtiqueta };
            Panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(20, 15, 20, 15),
                Margin = new Padding(0, 0, ultimo ? 0 : 15, 0)
            };
            Panel.Controls.Add(etiqueta);
            Panel.Controls.Add(valor);
            Panel.Controls.Add(nota);
        }

        public void Poner(string cifra, string explicacion, (Color Fondo, Color Texto)? colores)
        {
            // Solo se toca lo que cambia, para no redibujar el recuadro con cada tecla
            if (valor.Text != cifra) valor.Text = cifra;
            if (nota.Text != explicacion) nota.Text = explicacion;
            var fondo = colores?.Fondo ?? Color.White;
            if (Panel.BackColor != fondo) Panel.BackColor = fondo;
            valor.ForeColor = colores?.Texto ?? Tema.Texto;
            etiqueta.ForeColor = nota.ForeColor = colores?.Texto ?? Tema.Suave;
        }
    }
}
