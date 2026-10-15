using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;

// Paso 1 de la barra: los datos con los que se creó la devolución (solo consulta)
public class FormSolicitud : PantallaFase
{
    public const int PasoSolicitud = 1;

    private static readonly CultureInfo Es = new("es-ES");

    private readonly DevolucionServicio servicio = new();
    private readonly DevolucionFila devolucion;

    private readonly DataGridView grid = new();
    private readonly Label lblObservaciones = new();

    public FormSolicitud(DevolucionFila devolucion)
    {
        this.devolucion = devolucion;

        Text = $"Solicitud · {devolucion.Numero}";
        Size = new Size(1100, 820);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        var (scroll, pila) = Componentes.Pagina();

        pila.Controls.Add(Componentes.Cabecera(
            $"DEVOLUCIONES / {devolucion.Numero} / SOLICITUD",
            "Solicitud de devolución",
            $"Datos con los que se registró la devolución {devolucion.Numero}."));
        pila.Controls.Add(Progreso(devolucion, PasoSolicitud));
        pila.Controls.Add(Componentes.Resumen(
            ("CLIENTE", devolucion.Cliente),
            ("ALBARÁN", devolucion.Albaran ?? "—"),
            ("MOTIVO", devolucion.Motivo),
            ("FECHA SOLICITUD", devolucion.FechaSolicitud.ToString("dd MMM yyyy", Es))));

        var tarjetaProductos = Componentes.Tarjeta("Productos esperados", "Lo que el cliente indicó que iba a devolver.");
        ConfigurarGrid();
        tarjetaProductos.Controls.Add(grid);
        pila.Controls.Add(tarjetaProductos);

        var tarjetaObservaciones = Componentes.Tarjeta("Observaciones", "");
        lblObservaciones.AutoSize = true;
        lblObservaciones.MaximumSize = new Size(900, 0);
        tarjetaObservaciones.Controls.Add(lblObservaciones);
        pila.Controls.Add(tarjetaObservaciones);

        var btnCerrar = Tema.BotonSecundario("Cerrar", 130);
        btnCerrar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCerrar;
        pila.Controls.Add(Componentes.Acciones(btnCerrar));

        Controls.Add(scroll);
        Cargar();
    }

    private void ConfigurarGrid()
    {
        grid.Dock = DockStyle.Top;
        grid.ReadOnly = true;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Tema.Linea;
        grid.ScrollBars = ScrollBars.None;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowTemplate.Height = 40;

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 40;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Tema.CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Tema.CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Suave;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

        grid.DefaultCellStyle.ForeColor = Tema.Texto;
        grid.DefaultCellStyle.SelectionBackColor = Color.White;
        grid.DefaultCellStyle.SelectionForeColor = Tema.Texto;
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

        grid.Columns.Add("PRODUCTO", "PRODUCTO");
        grid.Columns.Add("LOTE", "LOTE");
        grid.Columns.Add("CANTIDAD", "CANTIDAD");
        grid.Columns[0].FillWeight = 60;
        grid.Columns[1].FillWeight = 20;
        grid.Columns[2].FillWeight = 20;
        grid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
    }

    private void Cargar()
    {
        try
        {
            foreach (var linea in servicio.ObtenerLineasRevision(devolucion.Id))
                grid.Rows.Add(linea.Producto, linea.Lote ?? "—", linea.Solicitada);

            string? observaciones = servicio.ObtenerObservacionesSolicitud(devolucion.Id);
            lblObservaciones.Text = observaciones ?? "Sin observaciones.";
            lblObservaciones.ForeColor = observaciones == null ? Tema.Suave : Tema.Texto;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo cargar la solicitud.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        grid.Height = grid.ColumnHeadersHeight + grid.Rows.Count * grid.RowTemplate.Height + 2;
        grid.ClearSelection();
    }
}
