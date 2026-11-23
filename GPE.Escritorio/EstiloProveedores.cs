namespace GPE.Escritorio;

// Estilo más marcado de las pantallas de proveedores (lista y ficha). De momento solo se usa aquí:
// si gusta, se puede pasar a Componentes para el resto de pantallas.
public static class EstiloProveedores
{
    public static readonly Color CabeceraTabla = Tema.Acento;
    public static readonly Color FilaAlterna = Color.FromArgb(246, 247, 245);
    public static readonly Color FilaRaton = Color.FromArgb(240, 236, 255);
    public static readonly Color FilaElegida = Color.FromArgb(226, 216, 255);

    // Raya izquierda de cada tarjeta de la ficha
    public static readonly Color RayaDatos = Color.FromArgb(30, 80, 170);     // azul
    public static readonly Color RayaEvaluacion = Tema.Acento;                // morado
    public static readonly Color RayaRiesgo = Color.FromArgb(220, 120, 0);    // naranja
    public static readonly Color RayaProductos = Color.FromArgb(0, 140, 145); // turquesa

    private static readonly Font FuenteTituloTarjeta = new("Segoe UI", 13, FontStyle.Bold);
    public static readonly Font FuenteCifra = new("Segoe UI", 20, FontStyle.Bold);

    // Cabecera en color, filas alternas y la fila bajo el ratón resaltada (va después de Componentes.EstiloTabla)
    public static void TablaMarcada(DataGridView grid)
    {
        grid.ColumnHeadersDefaultCellStyle.BackColor = CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = FilaAlterna;
        grid.DefaultCellStyle.SelectionBackColor = FilaElegida;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = FilaElegida;

        int filaRaton = -1;
        void Marcar(int fila)
        {
            if (fila == filaRaton) return;
            int anterior = filaRaton;
            filaRaton = fila;
            if (anterior >= 0 && anterior < grid.RowCount) grid.InvalidateRow(anterior);
            if (fila >= 0 && fila < grid.RowCount) grid.InvalidateRow(fila);
        }
        grid.CellMouseEnter += (s, e) => Marcar(e.RowIndex);
        grid.MouseLeave += (s, e) => Marcar(-1);
        grid.CellFormatting += (s, e) =>
        {
            if (e.RowIndex == filaRaton && e.RowIndex >= 0 && e.CellStyle != null)
                e.CellStyle.BackColor = FilaRaton;
        };
    }

    // Raya de color a la izquierda de la tarjeta y su título en negrita
    public static void MarcarTarjeta(Control tarjeta, Color color)
    {
        tarjeta.Paint += (s, e) =>
        {
            using var pincel = new SolidBrush(color);
            e.Graphics.FillRectangle(pincel, 0, 0, 6, tarjeta.Height);
        };
        if (BuscarTitulo(tarjeta) is Label titulo)
            titulo.Font = FuenteTituloTarjeta;
    }

    private static Label? BuscarTitulo(Control control)
    {
        foreach (Control hijo in control.Controls)
        {
            if (hijo is Label l && l.Font == Tema.FuenteApartado) return l;
            if (BuscarTitulo(hijo) is Label encontrado) return encontrado;
        }
        return null;
    }
}
