using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;


public class FormAlmacen : PantallaFase
{
    public const int PasoLlegada = Fase.MercanciaRecibida, PasoRevision = Fase.RevisadaAlmacen;


    // Columnas de la tabla de revisión
    private const int ColCodigo = 0, ColDescripcion = 1, ColFormato = 2, ColLote = 3,
                      ColCaducidad = 4, ColCantidad = 5, ColApto = 6, ColQuitar = 7;

    private const string Si = "Sí", No = "No";


    private readonly DevolucionServicio servicio = new();
    private DevolucionFila devolucion;
    private int paso;
    private List<Producto> productos = new();

    // Se crean de nuevo cada vez que se reconstruye la pantalla
    private DataGridView grid = new();
    private Label lblErrorLineas = new();
    private TextBox txtAgencia = new();
    private TextBox txtRevisadaPor = new();
    private TextBox txtObservaciones = new();
    private CamposFormulario campos = new();

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
        txtAgencia = new TextBox();
        txtRevisadaPor = new TextBox();
        txtObservaciones = new TextBox();
        campos = new CamposFormulario();
        hayCambios = false;

        var (scroll, pila) = Componentes.Pagina();

        pila.Controls.Add(Componentes.Cabecera(
            $"DEVOLUCIONES / {devolucion.Numero} / ALMACÉN",
            paso == PasoLlegada ? "Llegada de la mercancía" : "Revisión de la mercancía",
            $"Devolución {devolucion.Numero} de {devolucion.Cliente}."));
        pila.Controls.Add(Progreso(devolucion, paso));
        pila.Controls.Add(Componentes.Resumen(
            ("CLIENTE", devolucion.Cliente),
            ("CÓD. CLIENTE", devolucion.CodigoCliente),
            ("MOTIVO", devolucion.Motivo),
            ("RECOGIDA PREVISTA", Componentes.Fecha(devolucion.FechaRecogida)),
            ("FECHA LLEGADA", devolucion.FechaLlegada == null ? "Pendiente" : Componentes.Fecha(devolucion.FechaLlegada))));

        if (paso == PasoLlegada)
        {
            if (devolucion.Fase < Fase.MercanciaRecibida)
            {
                pila.Controls.Add(CrearTarjetaPendiente());
                pila.Controls.Add(Componentes.Acciones(BotonCerrar()));
            }
            else
            {
                pila.Controls.Add(Componentes.Tarjeta("Mercancía recibida",
                    $"La mercancía llegó al almacén el {Componentes.Fecha(devolucion.FechaLlegada)}."));
                pila.Controls.Add(CrearTarjetaRecibo(editable: devolucion.Fase == Fase.MercanciaRecibida));

                // Solo mientras no se haya finalizado la revisión
                var botones = new List<Button> { BotonCerrar() };
                if (devolucion.Fase == Fase.MercanciaRecibida)
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
            bool editable = devolucion.Fase == Fase.MercanciaRecibida;

            CargarProductos();
            pila.Controls.Add(CrearTarjetaDatosRevision(editable));
            pila.Controls.Add(CrearTarjetaProductos(editable));
            pila.Controls.Add(CrearTarjetaObservaciones(editable));
            pila.Controls.Add(CrearTarjetaFotos(editable));
            pila.Controls.Add(editable ? CrearAccionesRevision() : CrearAccionesConsulta());
            CargarRevision(editable);
        }

        Controls.Add(scroll);
        ResumeLayout();
    }

    // Paso 3: llegada

    private Control CrearTarjetaPendiente()
    {
        var tarjeta = Componentes.Tarjeta("La mercancía todavía no ha llegado",
            "Cuando llegue al almacén, márcala como recibida y adjunta el recibo de la agencia para empezar la revisión.");

        var btnRecibida = Tema.BotonPrincipal("Marcar como recibida", 220);
        btnRecibida.Height = 45;
        btnRecibida.Click += (s, e) => MarcarRecibida();
        tarjeta.Controls.Add(btnRecibida);

        return tarjeta;
    }

    private void MarcarRecibida()
    {
        // Si se deshizo una recepción anterior, su recibo se conserva y no se vuelve a pedir
        bool yaHayRecibo;
        try
        {
            yaHayRecibo = servicio.ObtenerAdjuntos(devolucion.Id, TipoAdjunto.Recibo).Count > 0;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo comprobar el recibo de la agencia.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string[] rutasRecibo = Array.Empty<string>();
        if (!yaHayRecibo)
        {
            var extensiones = DevolucionServicio.Extensiones(TipoAdjunto.Recibo);
            using var dialogo = new OpenFileDialog
            {
                Title = "Adjunta el recibo de la agencia",
                Multiselect = true,
                Filter = $"Archivos permitidos|{string.Join(";", extensiones.Select(e => "*" + e))}"
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK) return;
            rutasRecibo = dialogo.FileNames;
        }

        string recibo = yaHayRecibo
            ? "Se usará el recibo de la agencia que ya estaba adjunto."
            : "Recibo de la agencia: " + string.Join(", ", rutasRecibo.Select(Path.GetFileName));
        if (MessageBox.Show(this,
                $"¿Confirmas que la mercancía de {devolucion.Numero} ha llegado hoy al almacén?\n\n{recibo}",
                "Marcar como recibida", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            servicio.MarcarRecibida(devolucion.Id, rutasRecibo);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo marcar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "No se pudo marcar como recibida.\n\n" + ex.Message,
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Se continúa directamente con la revisión en la misma pantalla
        Recargar(PasoRevision);
    }

    // Se puede cambiar el archivo mientras no se haya finalizado la revisión
    private Control CrearTarjetaRecibo(bool editable)
    {
        var tarjeta = Componentes.Tarjeta("Recibo de la agencia", editable
            ? "Los cambios se guardan al momento. Haz doble clic en un archivo para verlo."
            : "Haz doble clic en un archivo para verlo.");

        tarjeta.Controls.Add(new ControlAdjuntos(devolucion.Id, TipoAdjunto.Recibo, "Añadir recibo")
        {
            Dock = DockStyle.Top,
            SoloLectura = !editable
        });

        return tarjeta;
    }

    // Reabrir

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

    // Paso 4: revisión

    private void CargarProductos()
    {
        try
        {
            productos = servicio.ObtenerProductos();
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo cargar la lista de productos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Agencia y quién revisa vienen ya rellenados, pero se pueden cambiar
    private Control CrearTarjetaDatosRevision(bool editable)
    {
        var tarjeta = Componentes.Tarjeta("Datos de la revisión", editable
            ? "Agencia que ha traído la mercancía y persona del almacén que la revisa."
            : "Revisión finalizada: solo consulta.");

        foreach (var texto in new[] { txtAgencia, txtRevisadaPor })
        {
            texto.MaxLength = 100;   // DEVOLUCION.agencia y revisada_por son NVARCHAR(100)
            texto.ReadOnly = !editable;
            texto.BackColor = Color.White;
            texto.TextChanged += (s, e) => hayCambios = true;
        }

        var rejilla = Componentes.Rejilla(2);
        rejilla.Controls.Add(campos.Campo("Agencia", txtAgencia));
        rejilla.Controls.Add(campos.Campo("Mercancía revisada por", txtRevisadaPor));
        tarjeta.Controls.Add(rejilla);

        return tarjeta;
    }

    private Control CrearTarjetaProductos(bool editable)
    {
        Button? btnAnadir = null;
        if (editable)
        {
            btnAnadir = Tema.BotonSecundario("+  Añadir línea", 170);
            btnAnadir.Click += (s, e) => AnadirLinea();
        }

        var tarjeta = Componentes.Tarjeta("Productos recibidos", editable
            ? "Una línea por producto y lote. Si unas unidades están bien y otras no, apúntalas en dos líneas."
            : "", btnAnadir);

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
        Componentes.EstiloTabla(grid);
        grid.Dock = DockStyle.Top;
        grid.ReadOnly = !editable;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.EditMode = DataGridViewEditMode.EditOnEnter;
        grid.ShowCellErrors = true;
        if (editable) grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;

        // Al elegir el código se rellenan la descripción y el formato
        grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "CÓDIGO",
            FillWeight = 13,
            DataSource = productos,
            ValueMember = nameof(Producto.Id),
            DisplayMember = nameof(Producto.Codigo),
            DisplayStyle = editable ? DataGridViewComboBoxDisplayStyle.DropDownButton : DataGridViewComboBoxDisplayStyle.Nothing,
            FlatStyle = FlatStyle.Flat,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        AnadirColumna("DESCRIPCIÓN", 25, soloLectura: true);
        AnadirColumna("FORMATO", 14, soloLectura: true);
        AnadirColumna("LOTE", 11, soloLectura: false);
        AnadirColumna("CADUCIDAD", 11, soloLectura: false);
        AnadirColumna("CANTIDAD", 9, soloLectura: false);

        var colApto = new DataGridViewComboBoxColumn
        {
            HeaderText = "APTO PARA SERVIR",
            FillWeight = 12,
            DisplayStyle = editable ? DataGridViewComboBoxDisplayStyle.DropDownButton : DataGridViewComboBoxDisplayStyle.Nothing,
            FlatStyle = FlatStyle.Flat,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        colApto.Items.AddRange(Si, No);
        grid.Columns.Add(colApto);

        if (editable)
        {
            var colQuitar = new DataGridViewButtonColumn
            {
                HeaderText = "",
                Text = "✕",
                UseColumnTextForButtonValue = true,
                FillWeight = 5,
                FlatStyle = FlatStyle.Flat
            };
            colQuitar.DefaultCellStyle.ForeColor = Color.Firebrick;
            grid.Columns.Add(colQuitar);
        }

        grid.Columns[ColCantidad].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        ((DataGridViewTextBoxColumn)grid.Columns[ColLote]).MaxInputLength = 30;   // REVISION_LINEA.lote
        ((DataGridViewTextBoxColumn)grid.Columns[ColCaducidad]).MaxInputLength = 10;
        ((DataGridViewTextBoxColumn)grid.Columns[ColCantidad]).MaxInputLength = 6;

        // Los desplegables guardan el valor en cuanto se elige, sin esperar a salir de la celda
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.CurrentCell is DataGridViewComboBoxCell && grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            hayCambios = true;
            QuitarError(grid.Rows[e.RowIndex]);
            if (e.ColumnIndex == ColCodigo) RellenarProducto(grid.Rows[e.RowIndex]);
        };
        grid.CellContentClick += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColQuitar) return;
            grid.Rows.RemoveAt(e.RowIndex);
            Componentes.AjustarAltoTabla(grid);
            hayCambios = true;
        };
        grid.CellValidating += ValidarCelda;
        // Un código que ya no está en la lista de productos no debe romper la pantalla
        grid.DataError += (s, e) => e.ThrowException = false;
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
            columna.DefaultCellStyle.BackColor = Tema.FondoSoloLectura;
            columna.DefaultCellStyle.ForeColor = Tema.Suave;
        }
        grid.Columns.Add(columna);
    }

    private void AnadirLinea()
    {
        int i = grid.Rows.Add();
        Componentes.AjustarAltoTabla(grid);
        grid.CurrentCell = grid.Rows[i].Cells[ColCodigo];
        lblErrorLineas.Visible = false;
        hayCambios = true;
    }

    private void RellenarProducto(DataGridViewRow fila)
    {
        var producto = productos.FirstOrDefault(p => p.Id == ProductoId(fila));
        fila.Cells[ColDescripcion].Value = producto?.Nombre ?? "";
        fila.Cells[ColFormato].Value = producto?.Formato ?? "—";
    }

    private Control CrearTarjetaObservaciones(bool editable)
    {
        var tarjeta = Componentes.Tarjeta("Observaciones",
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
        if (devolucion.Fase == Fase.RevisadaAlmacen)
            botones.Insert(0, BotonReabrir("Reabrir revisión", 180, () => Reabrir(
                "Reabrir revisión",
                "La devolución volverá a \"Mercancía recibida\" para poder corregir la revisión. " +
                "Las líneas, observaciones y fotos se conservan.",
                PasoRevision)));
        return Componentes.Acciones(botones.ToArray());
    }

    // Si la revisión aún no se ha guardado nunca, la tabla empieza con los productos
    // que se indicaron al crear la devolución (si se indicaron)
    private void CargarRevision(bool editable)
    {
        try
        {
            var cabecera = servicio.ObtenerCabeceraRevision(devolucion.Id);
            txtAgencia.Text = cabecera.Agencia ?? cabecera.Transportista ?? "";
            txtRevisadaPor.Text = cabecera.RevisadaPor ?? Sesion.UsuarioActual?.Nombre ?? "";
            txtObservaciones.Text = cabecera.Observaciones ?? "";

            var lineas = servicio.ObtenerLineasRevision(devolucion.Id);
            if (lineas.Count > 0 || !editable)
            {
                foreach (var linea in lineas)
                    grid.Rows.Add(linea.ProductoId, linea.Producto, linea.Formato ?? "—", linea.Lote ?? "",
                        linea.Caducidad?.ToString("dd/MM/yyyy", Componentes.Es) ?? "", linea.Cantidad,
                        linea.Apto ? Si : No);
            }
            else
            {
                foreach (var linea in servicio.ObtenerProductosSolicitados(devolucion.Id))
                    grid.Rows.Add(linea.ProductoId, linea.Producto, linea.Formato ?? "—", linea.Lote ?? "",
                        "", linea.Cantidad, null!);   // apto: sin elegir
            }
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo cargar la revisión de la devolución.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        Componentes.AjustarAltoTabla(grid);
        grid.ClearSelection();
        hayCambios = false;
    }

    // Validación de la tabla

    private void ValidarCelda(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        string texto = e.FormattedValue?.ToString()?.Trim() ?? "";
        string error = "";

        if (e.ColumnIndex == ColCantidad && texto != "" && !(int.TryParse(texto, out int n) && n > 0))
            error = "Escribe un número entero mayor que 0.";
        else if (e.ColumnIndex == ColCaducidad && texto != "" && Fecha(texto) == null)
            error = "Escribe la fecha como dd/mm/aaaa.";
        else if (e.ColumnIndex != ColCantidad && e.ColumnIndex != ColCaducidad)
            return;

        grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = error;
        e.Cancel = error != "";
    }

    private static int? ProductoId(DataGridViewRow fila) => fila.Cells[ColCodigo].Value as int?;

    private static int? Numero(object? valor) =>
        int.TryParse(valor?.ToString()?.Trim(), out int n) ? n : null;

    private static DateTime? Fecha(object? valor) =>
        DateTime.TryParse(valor?.ToString()?.Trim(), Componentes.Es, DateTimeStyles.None, out var fecha)
            ? fecha.Date : null;

    private static bool? Apto(object? valor) => (valor as string) switch
    {
        Si => true,
        No => false,
        _ => null
    };

    private static string? Texto(object? valor) =>
        string.IsNullOrWhiteSpace(valor?.ToString()) ? null : valor.ToString()!.Trim();

    // Marca la celda que hay que corregir en cada línea. Devuelve cuántas líneas tienen error.
    private int MarcarErrores()
    {
        int conError = 0;
        foreach (DataGridViewRow fila in grid.Rows)
        {
            QuitarError(fila);

            int? productoId = ProductoId(fila);
            int? cantidad = Numero(fila.Cells[ColCantidad].Value);
            bool? apto = Apto(fila.Cells[ColApto].Value);

            string? error = DevolucionServicio.ErrorLinea(productoId, Texto(fila.Cells[ColLote].Value), cantidad, apto);
            if (error == null) continue;

            int columna = productoId == null ? ColCodigo
                : cantidad is null or <= 0 ? ColCantidad
                : apto == null ? ColApto
                : ColLote;

            var celda = fila.Cells[columna];
            celda.ErrorText = error;
            celda.Style.BackColor = Tema.FondoError;
            conError++;
        }
        return conError;
    }

    private static void QuitarError(DataGridViewRow fila)
    {
        foreach (int col in new[] { ColCodigo, ColLote, ColCantidad, ColApto })
        {
            fila.Cells[col].ErrorText = "";
            fila.Cells[col].Style.BackColor = Color.Empty;
        }
    }

    // Finalizar

    private void FinalizarRevision()
    {
        if (!grid.EndEdit()) return;   // la celda en edición tiene un valor no válido

        campos.LimpiarErrores();
        Control? primerError = null;

        string? revisadaPor = Texto(txtRevisadaPor.Text);
        if (revisadaPor == null)
        {
            campos.MarcarError(txtRevisadaPor, "Indica quién ha revisado la mercancía.");
            primerError = txtRevisadaPor;
        }

        int conError = MarcarErrores();
        lblErrorLineas.Visible = conError > 0 || grid.Rows.Count == 0;
        lblErrorLineas.Text = grid.Rows.Count == 0
            ? "Apunta al menos un producto con «Añadir línea»."
            : conError == 1
                ? "Hay 1 línea incompleta. Pasa el ratón por el icono rojo para ver qué falta."
                : $"Hay {conError} líneas incompletas. Pasa el ratón por los iconos rojos para ver qué falta.";

        if (primerError != null || lblErrorLineas.Visible)
        {
            primerError?.Focus();
            return;
        }

        if (MessageBox.Show(this,
                "Al finalizar, la revisión pasa a solo consulta. Podrás reabrirla mientras no se registre el abono.\n\n" +
                "¿Finalizar la revisión?",
                "Finalizar revisión", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var lineas = grid.Rows.Cast<DataGridViewRow>()
            .Select(f => new LineaRevisada(
                ProductoId(f)!.Value,
                Texto(f.Cells[ColLote].Value),
                Fecha(f.Cells[ColCaducidad].Value),
                Numero(f.Cells[ColCantidad].Value)!.Value,
                Apto(f.Cells[ColApto].Value)!.Value))
            .ToList();

        var datos = new DatosRevision(devolucion.Id, revisadaPor!, Texto(txtAgencia.Text),
            Texto(txtObservaciones.Text), lineas);

        try
        {
            servicio.FinalizarRevision(datos);
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

    // La revisión solo se guarda al finalizar: si se cierra antes, se avisa
    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (MessageBox.Show(this,
                "Lo apuntado en la revisión solo se guarda al finalizarla.\n" +
                "Si sales ahora se perderá (las fotos sí se han guardado).\n\n¿Salir de todos modos?",
                "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            e.Cancel = true;
            DialogResult = DialogResult.None;
        }
    }

}
