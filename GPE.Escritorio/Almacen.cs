using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;


public class FormAlmacen : PantallaFase
{
    public const int PasoLlegada = 3, PasoRevision = 4;

    private static readonly CultureInfo Es = new("es-ES");

    // Columnas de la tabla de revisión
    private const int ColProducto = 0, ColLote = 1, ColSolicitada = 2,
                      ColRecibida = 3, ColApta = 4, ColObservaciones = 5;

    private static readonly Color FondoSoloLectura = Color.FromArgb(247, 248, 246);
    private static readonly Color FondoDiferencia = Color.FromArgb(255, 241, 214);
    private static readonly Color FondoError = Color.FromArgb(253, 228, 230);

    private readonly DevolucionServicio servicio = new();
    private DevolucionFila devolucion;
    private int paso;

    // Se crean de nuevo cada vez que se reconstruye la pantalla
    private DataGridView grid = new();
    private Label lblErrorLineas = new();
    private TextBox txtObservaciones = new();

    private bool hayCambios;   // para avisar al cerrar sin finalizar

    public FormAlmacen(DevolucionFila devolucion, int paso)
    {
        this.devolucion = devolucion;
        this.paso = paso;

        Text = $"Almacén · {devolucion.Numero}";
        Size = new Size(1150, 880);
        MinimumSize = new Size(950, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        FormClosing += AvisarCambiosSinGuardar;

        Construir();
    }

    // Se vuelve a llamar al marcar como recibida o al reabrir un paso
    private void Construir()
    {
        SuspendLayout();
        Controls.Clear();
        CancelButton = null;
        grid = new DataGridView();
        lblErrorLineas = new Label();
        txtObservaciones = new TextBox();
        hayCambios = false;

        var (scroll, pila) = Componentes.Pagina();

        pila.Controls.Add(Componentes.Cabecera(
            $"DEVOLUCIONES / {devolucion.Numero} / ALMACÉN",
            paso == PasoLlegada ? "Llegada de la mercancía" : "Revisión de la mercancía",
            $"Devolución {devolucion.Numero} de {devolucion.Cliente}."));
        pila.Controls.Add(Progreso(devolucion, paso));
        pila.Controls.Add(Componentes.Resumen(
            ("CLIENTE", devolucion.Cliente),
            ("ALBARÁN", devolucion.Albaran ?? "—"),
            ("MOTIVO", devolucion.Motivo),
            ("FECHA RECOGIDA", Fecha(devolucion.FechaRecogida)),
            ("FECHA LLEGADA", devolucion.FechaLlegada == null ? "Pendiente" : Fecha(devolucion.FechaLlegada))));

        if (paso == PasoLlegada)
        {
            if (devolucion.Fase < 3)
            {
                pila.Controls.Add(CrearTarjetaPendiente());
                pila.Controls.Add(Componentes.Acciones(BotonCerrar()));
            }
            else
            {
                pila.Controls.Add(Componentes.Tarjeta("Mercancía recibida",
                    $"La mercancía llegó al almacén el {Fecha(devolucion.FechaLlegada)}."));

                // Solo mientras no se haya finalizado la revisión
                var botones = new List<Button> { BotonCerrar() };
                if (devolucion.Fase == 3)
                    botones.Insert(0, BotonReabrir("Deshacer recepción", 190, () => Reabrir(
                        "Deshacer recepción",
                        "La devolución volverá a \"Recogida solicitada\" y se borrará la fecha de llegada. " +
                        "Lo que ya se haya anotado en la revisión se conserva.",
                        PasoLlegada)));
                pila.Controls.Add(Componentes.Acciones(botones.ToArray()));
            }
        }
        else
        {
            bool editable = devolucion.Fase == 3;

            pila.Controls.Add(CrearTarjetaProductos(editable));
            pila.Controls.Add(CrearTarjetaObservaciones(editable));
            pila.Controls.Add(CrearTarjetaFotos(editable));
            pila.Controls.Add(editable ? CrearAccionesRevision() : CrearAccionesConsulta());
            CargarRevision(editable);
        }

        Controls.Add(scroll);
        ResumeLayout();
    }

    private Button BotonCerrar()
    {
        var boton = Tema.BotonSecundario("Cerrar", 130);
        boton.DialogResult = DialogResult.Cancel;
        CancelButton = boton;
        return boton;
    }

    private static Button BotonReabrir(string texto, int ancho, Action accion)
    {
        var boton = Tema.BotonSecundario(texto, ancho);
        boton.ForeColor = Tema.Error;
        boton.Click += (s, e) => accion();
        return boton;
    }

    // ---------- Paso 3: llegada ----------

    private Control CrearTarjetaPendiente()
    {
        var tarjeta = Componentes.Tarjeta("La mercancía todavía no ha llegado",
            "Cuando llegue al almacén, márcala como recibida para empezar la revisión.");

        var btnRecibida = Tema.BotonPrincipal("Marcar como recibida", 220);
        btnRecibida.Height = 45;
        btnRecibida.Click += (s, e) => MarcarRecibida();
        tarjeta.Controls.Add(btnRecibida);

        return tarjeta;
    }

    private void MarcarRecibida()
    {
        if (MessageBox.Show(this,
                $"¿Confirmas que la mercancía de {devolucion.Numero} ha llegado hoy al almacén?",
                "Marcar como recibida", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            servicio.MarcarRecibida(devolucion.Id);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo marcar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo marcar como recibida.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Se continúa directamente con la revisión en la misma pantalla
        Recargar(PasoRevision);
    }

    // ---------- Reabrir ----------

    // Vuelve a la fase anterior (con motivo) y muestra el paso indicado
    private void Reabrir(string titulo, string explicacion, int pasoDestino)
    {
        string? motivo = PedirMotivo(titulo, explicacion);
        if (motivo == null) return;

        try
        {
            servicio.Reabrir(devolucion.Id, devolucion.Fase, motivo);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo reabrir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo reabrir.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Recargar(pasoDestino);
    }

    private void Recargar(int nuevoPaso)
    {
        try
        {
            devolucion = servicio.ObtenerDevolucion(devolucion.Id) ?? devolucion;
        }
        catch (DbException)
        {
        }
        paso = nuevoPaso;
        Construir();
    }

    // ---------- Paso 4: revisión ----------

    private Control CrearTarjetaProductos(bool editable)
    {
        var tarjeta = Componentes.Tarjeta("Productos recibidos", editable
            ? "Indica cuánto ha llegado de cada producto y cuánto está en buen estado para volver a venderse."
            : "Revisión finalizada: solo consulta.");

        ConfigurarGrid(editable);
        tarjeta.Controls.Add(grid);

        lblErrorLineas.AutoSize = true;
        lblErrorLineas.ForeColor = Tema.Error;
        lblErrorLineas.Margin = new Padding(0, 10, 0, 0);
        lblErrorLineas.Visible = false;
        tarjeta.Controls.Add(lblErrorLineas);

        return tarjeta;
    }

    private void ConfigurarGrid(bool editable)
    {
        grid.Dock = DockStyle.Top;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Tema.Linea;
        grid.ScrollBars = ScrollBars.None;   // la página entera ya tiene scroll

        grid.ReadOnly = !editable;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.EditMode = DataGridViewEditMode.EditOnEnter;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowTemplate.Height = 40;
        grid.ShowCellErrors = true;

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 40;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Tema.CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Tema.CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Suave;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

        grid.DefaultCellStyle.ForeColor = Tema.Texto;
        grid.DefaultCellStyle.SelectionBackColor = editable ? Tema.AcentoSuave : Color.White;
        grid.DefaultCellStyle.SelectionForeColor = Tema.Texto;
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

        AnadirColumna("PRODUCTO", 28, soloLectura: true);
        AnadirColumna("LOTE", 12, soloLectura: true);
        AnadirColumna("SOLICITADA", 14, soloLectura: true);
        AnadirColumna("RECIBIDA", 11, soloLectura: false);
        AnadirColumna("APTA", 11, soloLectura: false);
        AnadirColumna("OBSERVACIONES", 32, soloLectura: false);

        foreach (int col in new[] { ColSolicitada, ColRecibida, ColApta })
            grid.Columns[col].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        ((DataGridViewTextBoxColumn)grid.Columns[ColObservaciones]).MaxInputLength = 500;
        ((DataGridViewTextBoxColumn)grid.Columns[ColRecibida]).MaxInputLength = 6;
        ((DataGridViewTextBoxColumn)grid.Columns[ColApta]).MaxInputLength = 6;

        grid.CellValidating += ValidarNumero;
        grid.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            hayCambios = true;
            QuitarError(grid.Rows[e.RowIndex]);
        };
        grid.CellFormatting += ResaltarDiferencia;
    }

    private void AnadirColumna(string titulo, float peso, bool soloLectura)
    {
        var columna = new DataGridViewTextBoxColumn
        {
            HeaderText = titulo,
            FillWeight = peso,
            ReadOnly = soloLectura,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (soloLectura)
        {
            columna.DefaultCellStyle.BackColor = FondoSoloLectura;
            columna.DefaultCellStyle.ForeColor = Tema.Suave;
        }
        grid.Columns.Add(columna);
    }

    private Control CrearTarjetaObservaciones(bool editable)
    {
        var tarjeta = Componentes.Tarjeta("Observaciones generales",
            "Estado general de la mercancía, embalaje, incidencias en la llegada...");

        txtObservaciones.Multiline = true;
        txtObservaciones.Height = 90;
        txtObservaciones.Dock = DockStyle.Top;
        txtObservaciones.ScrollBars = ScrollBars.Vertical;
        txtObservaciones.ReadOnly = !editable;
        txtObservaciones.BackColor = Color.White;
        txtObservaciones.TextChanged += (s, e) => hayCambios = true;
        tarjeta.Controls.Add(txtObservaciones);

        return tarjeta;
    }

    private Control CrearTarjetaFotos(bool editable)
    {
        var tarjeta = Componentes.Tarjeta("Fotos de la mercancía", editable
            ? "Las fotos se guardan al momento. Haz doble clic en una para verla."
            : "Haz doble clic en una foto para verla.");

        var fotos = new ControlAdjuntos(devolucion.Id, TipoAdjunto.Foto, "Añadir foto")
        {
            Dock = DockStyle.Top,
            SoloLectura = !editable
        };
        tarjeta.Controls.Add(fotos);

        return tarjeta;
    }

    private Control CrearAccionesRevision()
    {
        var btnFinalizar = Tema.BotonPrincipal("Finalizar revisión", 190);
        btnFinalizar.Click += (s, e) => FinalizarRevision();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;

        return Componentes.Acciones(btnFinalizar, btnCancelar);
    }

    // Revisión ya finalizada: se puede reabrir mientras no se haya registrado el abono
    private Control CrearAccionesConsulta()
    {
        var botones = new List<Button> { BotonCerrar() };
        if (devolucion.Fase == 4)
            botones.Insert(0, BotonReabrir("Reabrir revisión", 180, () => Reabrir(
                "Reabrir revisión",
                "La devolución volverá a \"Mercancía recibida\" para poder corregir la revisión. " +
                "Las cantidades, observaciones y fotos se conservan.",
                PasoRevision)));
        return Componentes.Acciones(botones.ToArray());
    }

    private void CargarRevision(bool editable)
    {
        try
        {
            foreach (var linea in servicio.ObtenerLineasRevision(devolucion.Id))
            {
                int i = grid.Rows.Add(
                    linea.Producto,
                    linea.Lote ?? "—",
                    linea.Solicitada,
                    linea.Recibida?.ToString() ?? "",
                    linea.Apta?.ToString() ?? "",
                    linea.Observaciones ?? "");
                grid.Rows[i].Tag = linea;
            }
            txtObservaciones.Text = servicio.ObtenerObservacionesRevision(devolucion.Id) ?? "";
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudieron cargar los productos de la devolución.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // Sin scroll propio: la tabla mide lo que sus filas
        grid.Height = grid.ColumnHeadersHeight + grid.Rows.Count * grid.RowTemplate.Height + 2;

        // Al editar, el cursor empieza en la primera cantidad a rellenar
        if (editable && grid.Rows.Count > 0)
            grid.CurrentCell = grid.Rows[0].Cells[ColRecibida];
        else
            grid.ClearSelection();

        hayCambios = false;
    }

    // ---------- Validación de la tabla ----------

    private void ValidarNumero(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (e.ColumnIndex != ColRecibida && e.ColumnIndex != ColApta) return;

        string texto = e.FormattedValue?.ToString()?.Trim() ?? "";
        bool valido = texto == "" || (int.TryParse(texto, out int n) && n >= 0);

        grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = valido ? "" : "Escribe un número entero (0 o más).";
        e.Cancel = !valido;
    }

    // Si lo recibido no coincide con lo solicitado, la celda se marca en ámbar
    private void ResaltarDiferencia(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != ColRecibida || e.CellStyle == null) return;
        if (grid.Rows[e.RowIndex].Tag is not LineaRevision linea) return;
        if (grid.Rows[e.RowIndex].Cells[ColRecibida].ErrorText != "") return;

        int? recibida = Numero(e.Value);
        e.CellStyle.BackColor = recibida != null && recibida != linea.Solicitada ? FondoDiferencia : Color.White;
    }

    private static int? Numero(object? valor) =>
        int.TryParse(valor?.ToString()?.Trim(), out int n) ? n : null;

    private static string? Texto(object? valor) =>
        string.IsNullOrWhiteSpace(valor?.ToString()) ? null : valor.ToString()!.Trim();

    // Marca la celda que hay que corregir en cada línea. Devuelve cuántas líneas tienen error.
    private int MarcarErrores()
    {
        int conError = 0;
        foreach (DataGridViewRow fila in grid.Rows)
        {
            QuitarError(fila);

            int? recibida = Numero(fila.Cells[ColRecibida].Value);
            int? apta = Numero(fila.Cells[ColApta].Value);
            string? observaciones = Texto(fila.Cells[ColObservaciones].Value);

            string? error = DevolucionServicio.ErrorLinea(recibida, apta, observaciones);
            if (error == null) continue;

            int columna = recibida is null or < 0 ? ColRecibida
                : apta is null or < 0 || apta > recibida ? ColApta
                : ColObservaciones;

            var celda = fila.Cells[columna];
            celda.ErrorText = error;
            celda.Style.BackColor = FondoError;
            conError++;
        }
        return conError;
    }

    private static void QuitarError(DataGridViewRow fila)
    {
        foreach (int col in new[] { ColRecibida, ColApta, ColObservaciones })
        {
            fila.Cells[col].ErrorText = "";
            fila.Cells[col].Style.BackColor = Color.Empty;
        }
    }

    // ---------- Finalizar ----------

    private void FinalizarRevision()
    {
        if (!grid.EndEdit()) return;   // la celda en edición tiene un valor no válido

        int conError = MarcarErrores();
        lblErrorLineas.Visible = conError > 0;
        if (conError > 0)
        {
            lblErrorLineas.Text = conError == 1
                ? "Hay 1 línea incompleta. Pasa el ratón por el icono rojo para ver qué falta."
                : $"Hay {conError} líneas incompletas. Pasa el ratón por los iconos rojos para ver qué falta.";
            return;
        }

        if (MessageBox.Show(this,
                "Al finalizar, la revisión pasa a solo consulta. Podrás reabrirla mientras no se registre el abono.\n\n" +
                "¿Finalizar la revisión?",
                "Finalizar revisión", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var lineas = grid.Rows.Cast<DataGridViewRow>()
            .Select(f => new LineaRevisada(
                ((LineaRevision)f.Tag!).Id,
                Numero(f.Cells[ColRecibida].Value)!.Value,
                Numero(f.Cells[ColApta].Value)!.Value,
                Texto(f.Cells[ColObservaciones].Value)))
            .ToList();

        try
        {
            servicio.FinalizarRevision(devolucion.Id, lineas, Texto(txtObservaciones.Text));
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo finalizar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo guardar la revisión.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    // Las cantidades solo se guardan al finalizar: si se cierra antes, se avisa
    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (MessageBox.Show(this,
                "Las cantidades y observaciones solo se guardan al finalizar la revisión.\n" +
                "Si sales ahora se perderán (las fotos sí se han guardado).\n\n¿Salir de todos modos?",
                "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            e.Cancel = true;
            DialogResult = DialogResult.None;
        }
    }

    private static string Fecha(DateTime? fecha) => fecha?.ToString("dd MMM yyyy", Es) ?? "—";
}
