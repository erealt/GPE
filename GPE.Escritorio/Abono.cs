using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

public class FormAbono : PantallaFase
{
    public const int PasoAbono = Fase.AbonoRealizado, PasoCierre = Fase.Cierre;



    private readonly DevolucionServicio servicio = new();
    private DevolucionFila devolucion;
    private int paso;
    private AbonoGuardado? abono;
    private List<LineaRevision> lineas = new();

    // Se crean de nuevo cada vez que se reconstruye la pantalla
    private FlowLayoutPanel panelResultado = new();
    private RadioButton rbTotal = new(), rbParcial = new(), rbNinguno = new();
    private TextBox txtNumero = new();
    private DateTimePicker dtpFecha = new();
    private NumericUpDown numImporte = new();
    private TextBox txtObservaciones = new();
    private ControlAdjuntos documento = null!;
    private CamposFormulario campos = new();

    private bool hayCambios;   // para avisar al cerrar sin registrar

    private bool Editable => devolucion.Fase == Fase.RevisadaAlmacen;

    public FormAbono(DevolucionFila devolucion, int paso)
    {
        this.devolucion = devolucion;
        this.paso = paso;

        Text = $"Abono · {devolucion.Numero}";
        Size = new Size(1150, 900);
        MinimumSize = new Size(950, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        FormClosing += AvisarCambiosSinGuardar;

        Construir();
    }

    // Se vuelve a llamar al reabrir el abono
    private void Construir()
    {
        SuspendLayout();
        Controls.Clear();
        CancelButton = null;
        campos = new CamposFormulario();
        hayCambios = false;

        try
        {
            lineas = servicio.ObtenerLineasRevision(devolucion.Id);
            abono = servicio.ObtenerAbono(devolucion.Id);
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudieron cargar los datos de la devolución.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        var (scroll, pila) = Componentes.Pagina();

        bool cerrada = devolucion.Fase >= Fase.Cierre;
        pila.Controls.Add(Componentes.Cabecera(
            $"DEVOLUCIONES / {devolucion.Numero} / ABONO",
            cerrada ? "Devolución cerrada" : "Abono y cierre",
            cerrada
                ? $"Abono de la devolución {devolucion.Numero} de {devolucion.Cliente}."
                : $"Registra el abono de la devolución {devolucion.Numero} de {devolucion.Cliente} para cerrarla."));
        pila.Controls.Add(Progreso(devolucion, paso));

        var resumen = new List<(string, string)>
        {
            ("CLIENTE", devolucion.Cliente),
            ("ALBARÁN", devolucion.Albaran ?? "—"),
            ("MOTIVO", devolucion.Motivo),
            ("FECHA LLEGADA", Componentes.Fecha(devolucion.FechaLlegada))
        };
        if (cerrada) resumen.Add(("FECHA CIERRE", Componentes.Fecha(abono?.FechaCierre)));
        pila.Controls.Add(Componentes.Resumen(resumen.ToArray()));

        if (devolucion.Fase < Fase.RevisadaAlmacen)
        {
            pila.Controls.Add(Componentes.Tarjeta("La revisión del almacén no ha terminado",
                "El abono se registra cuando el almacén finaliza la revisión de la mercancía."));
            pila.Controls.Add(Componentes.Acciones(BotonCerrar()));
        }
        else
        {
            pila.Controls.Add(CrearTarjetaRevision());
            pila.Controls.Add(CrearTarjetaAbono());
            pila.Controls.Add(Editable ? CrearAccionesRegistro() : CrearAccionesConsulta());
            CargarAbono();
        }

        Controls.Add(scroll);
        ResumeLayout();
    }

    // ---------- Resumen de la revisión ----------

    private Control CrearTarjetaRevision()
    {
        var tarjeta = Componentes.Tarjeta("Resultado de la revisión",
            "Lo que el almacén ha recibido y lo que está en buen estado, para decidir el abono.");

        tarjeta.Controls.Add(CrearTablaRevision());

        string? observaciones = null;
        try
        {
            observaciones = servicio.ObtenerObservacionesRevision(devolucion.Id);
        }
        catch (DbException)
        {
            // Las observaciones son informativas: sin ellas la pantalla sigue siendo útil
        }
        if (!string.IsNullOrWhiteSpace(observaciones))
        {
            tarjeta.Controls.Add(new Label
            {
                Text = "Observaciones del almacén",
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 15, 0, 4)
            });
            tarjeta.Controls.Add(new Label
            {
                Text = observaciones,
                AutoSize = true,
                MaximumSize = new Size(1000, 0),
                ForeColor = Tema.Texto
            });
        }

        tarjeta.Controls.Add(new Label
        {
            Text = "Fotos de la revisión (doble clic para verlas)",
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Margin = new Padding(0, 15, 0, 4)
        });
        tarjeta.Controls.Add(new ControlAdjuntos(devolucion.Id, TipoAdjunto.Foto, "Añadir foto")
        {
            Dock = DockStyle.Top,
            Height = 180,
            SoloLectura = true
        });

        return tarjeta;
    }

    private DataGridView CrearTablaRevision()
    {
        var grid = new DataGridView { Dock = DockStyle.Top, ReadOnly = true };
        Componentes.EstiloTabla(grid, altoFila: 36);

        foreach (var (titulo, peso, numero) in new[]
                 {
                     ("PRODUCTO", 26f, false), ("LOTE", 11f, false), ("SOLICITADA", 11f, true),
                     ("RECIBIDA", 10f, true), ("APTA", 9f, true), ("NO APTA", 10f, true),
                     ("OBSERVACIONES", 30f, false)
                 })
        {
            var columna = new DataGridViewTextBoxColumn
            {
                HeaderText = titulo,
                FillWeight = peso,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (numero) columna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(columna);
        }

        foreach (var linea in lineas)
        {
            int? noApta = linea.Recibida - linea.Apta;
            int i = grid.Rows.Add(linea.Producto, linea.Lote ?? "—", linea.Solicitada,
                linea.Recibida?.ToString() ?? "—", linea.Apta?.ToString() ?? "—",
                noApta?.ToString() ?? "—", linea.Observaciones ?? "");

            // Diferencias en ámbar, como en la revisión del almacén
            if (linea.Recibida != linea.Solicitada)
                grid.Rows[i].Cells[3].Style.BackColor = Tema.FondoDiferencia;
            if (noApta > 0)
                grid.Rows[i].Cells[5].Style.BackColor = Tema.FondoDiferencia;
        }

        int total = grid.Rows.Add("Total", "",
            lineas.Sum(l => l.Solicitada), lineas.Sum(l => l.Recibida ?? 0),
            lineas.Sum(l => l.Apta ?? 0), lineas.Sum(l => (l.Recibida ?? 0) - (l.Apta ?? 0)), "");
        grid.Rows[total].DefaultCellStyle.BackColor = Tema.FondoSoloLectura;
        grid.Rows[total].DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);

        Componentes.AjustarAltoTabla(grid);
        grid.ClearSelection();
        return grid;
    }

    // ---------- Datos del abono ----------

    private Control CrearTarjetaAbono()
    {
        var tarjeta = Componentes.Tarjeta("Abono", Editable
            ? "Al registrar el abono la devolución queda cerrada. El documento se guarda al momento."
            : "Abono registrado: solo consulta.");

        panelResultado = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        rbTotal = Opcion("Abono total");
        rbParcial = Opcion("Abono parcial");
        rbNinguno = Opcion("Sin abono");
        panelResultado.Controls.AddRange(new Control[] { rbTotal, rbParcial, rbNinguno });

        txtNumero = new TextBox { MaxLength = 30, PlaceholderText = "Nº del abono o factura rectificativa" };

        dtpFecha = new DateTimePicker { Format = DateTimePickerFormat.Short };

        numImporte = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0,
            Maximum = 99_999_999.99m,   // DECIMAL(10,2)
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right
        };

        txtObservaciones = new TextBox
        {
            Multiline = true,
            Height = 80,
            MaxLength = 4000,
            ScrollBars = ScrollBars.Vertical
        };

        documento = new ControlAdjuntos(devolucion.Id, TipoAdjunto.Abono, "Adjuntar abono")
        {
            Dock = DockStyle.Top,
            Height = 180,
            SoloLectura = !Editable
        };

        var rejilla = Componentes.Rejilla(2);

        var campoResultado = campos.Campo("Resultado", panelResultado);
        rejilla.Controls.Add(campoResultado);
        rejilla.SetColumnSpan(campoResultado, 2);

        rejilla.Controls.Add(campos.Campo("Nº de abono", txtNumero));
        rejilla.Controls.Add(campos.Campo("Fecha del abono", dtpFecha));
        rejilla.Controls.Add(campos.Campo("Importe (€)", numImporte));
        rejilla.Controls.Add(new Panel { Height = 1 });

        var campoObservaciones = campos.Campo("Observaciones", txtObservaciones);
        rejilla.Controls.Add(campoObservaciones);
        rejilla.SetColumnSpan(campoObservaciones, 2);

        var campoDocumento = campos.Campo("Documento del abono (PDF o imagen)", documento);
        rejilla.Controls.Add(campoDocumento);
        rejilla.SetColumnSpan(campoDocumento, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private RadioButton Opcion(string texto)
    {
        var opcion = new RadioButton { Text = texto, AutoSize = true, Margin = new Padding(0, 0, 25, 0) };
        opcion.CheckedChanged += (s, e) =>
        {
            if (!opcion.Checked) return;
            ActualizarCampos();
            campos.OcultarError(panelResultado);
            hayCambios = true;
        };
        return opcion;
    }

    private string? ResultadoElegido() =>
        rbTotal.Checked ? ResultadoAbono.Total
        : rbParcial.Checked ? ResultadoAbono.Parcial
        : rbNinguno.Checked ? ResultadoAbono.Ninguno
        : null;

    // Sin abono no hay número ni importe; en parcial y sin abono el motivo es obligatorio
    private void ActualizarCampos()
    {
        if (!Editable) return;

        string? resultado = ResultadoElegido();
        bool hayAbono = resultado != ResultadoAbono.Ninguno;
        txtNumero.Enabled = numImporte.Enabled = hayAbono;

        txtObservaciones.PlaceholderText = resultado switch
        {
            ResultadoAbono.Parcial => "Obligatorio: por qué no se abona todo",
            ResultadoAbono.Ninguno => "Obligatorio: por qué no se abona",
            _ => "Opcional"
        };
    }

    private void CargarAbono()
    {
        if (abono != null)
        {
            rbTotal.Checked = abono.Resultado == ResultadoAbono.Total;
            rbParcial.Checked = abono.Resultado == ResultadoAbono.Parcial;
            rbNinguno.Checked = abono.Resultado == ResultadoAbono.Ninguno;
            txtNumero.Text = abono.Numero ?? "";
            numImporte.Value = Math.Min(abono.Importe ?? 0, numImporte.Maximum);
            txtObservaciones.Text = abono.Observaciones ?? "";
        }
        else if (lineas.Count > 0 && lineas.All(l => l.Recibida == l.Solicitada && l.Apta == l.Recibida))
        {
            rbTotal.Checked = true;   
        }

        if (Editable)
        {
            // Entre la llegada de la mercancía y hoy
            dtpFecha.MinDate = (devolucion.FechaLlegada ?? devolucion.FechaSolicitud).Date;
            dtpFecha.MaxDate = DateTime.Today < dtpFecha.MinDate ? dtpFecha.MinDate : DateTime.Today;
            DateTime fecha = abono?.Fecha ?? DateTime.Today;
            dtpFecha.Value = fecha < dtpFecha.MinDate ? dtpFecha.MinDate
                : fecha > dtpFecha.MaxDate ? dtpFecha.MaxDate : fecha;
        }
        else
        {
            if (abono != null) dtpFecha.Value = abono.Fecha;
            BloquearCampos();
        }

        ActualizarCampos();

        foreach (var control in new Control[] { txtNumero, txtObservaciones })
            control.TextChanged += (s, e) => hayCambios = true;
        dtpFecha.ValueChanged += (s, e) => hayCambios = true;
        numImporte.ValueChanged += (s, e) => hayCambios = true;
        hayCambios = false;
    }

    private void BloquearCampos()
    {
        foreach (var opcion in new[] { rbTotal, rbParcial, rbNinguno })
            opcion.Enabled = opcion.Checked;   // la elegida se sigue leyendo bien
        dtpFecha.Enabled = false;
        numImporte.Enabled = false;
        foreach (var texto in new[] { txtNumero, txtObservaciones })
        {
            texto.ReadOnly = true;
            texto.BackColor = Color.White;
        }
    }

    // ---------- Botones ----------

    private Control CrearAccionesRegistro()
    {
        var btnRegistrar = Tema.BotonPrincipal("Registrar abono y cerrar", 240);
        btnRegistrar.Click += (s, e) => RegistrarAbono();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;

        var btnReabrir = BotonReabrir("Reabrir revisión", 180, () =>
            Reabrir("Reabrir revisión",
                "La devolución volverá a \"Mercancía recibida\" para poder corregir la revisión. " +
                "Lo que hayas rellenado aquí sin registrar se perderá; el documento del abono se conserva.",
                () => IrAPaso(FormAlmacen.PasoRevision)));

        return Componentes.Acciones(btnRegistrar, btnCancelar, btnReabrir);
    }

    private Control CrearAccionesConsulta() =>
        Componentes.Acciones(BotonCerrar(), BotonReabrir("Reabrir abono", 170, () =>
            Reabrir("Reabrir abono",
                "La devolución dejará de estar cerrada y volverá a \"Revisada almacén\" para poder corregir el abono. " +
                "Los datos del abono se conservan.",
                () => Recargar(PasoAbono))));

    private void Reabrir(string titulo, string explicacion, Action despues)
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

        hayCambios = false;
        despues();
    }

    private void Recargar(int nuevoPaso)
    {
        try
        {
            devolucion = servicio.ObtenerDevolucion(devolucion.Id) ?? devolucion;
        }
        catch (DbException)
        {
            // Si falla la lectura se sigue con los datos que había; FormPrincipal recarga al cerrar
        }
        paso = nuevoPaso;
        Construir();
    }

    // ---------- Registrar ----------

    private void RegistrarAbono()
    {
        // 1. Validaciones: se marcan todos los campos con error a la vez
        campos.LimpiarErrores();
        Control? primerError = null;

        void Error(Control campo, string mensaje)
        {
            campos.MarcarError(campo, mensaje);
            primerError ??= campo;
        }

        string? resultado = ResultadoElegido();
        bool hayAbono = resultado != ResultadoAbono.Ninguno;
        string numero = txtNumero.Text.Trim();
        decimal importe = numImporte.Value;   // al leerlo se valida lo que se haya escrito
        string observaciones = txtObservaciones.Text.Trim();

        if (resultado == null)
            Error(panelResultado, "Indica si se abona todo, una parte o nada.");

        if (resultado != null && hayAbono)
        {
            if (numero == "") Error(txtNumero, "Indica el número del abono.");
            if (importe <= 0) Error(numImporte, "El importe debe ser mayor que 0.");
            if (documento.Cantidad == 0) Error(documento, "Adjunta el documento del abono.");
        }

        if ((resultado is ResultadoAbono.Parcial or ResultadoAbono.Ninguno) && observaciones == "")
            Error(txtObservaciones, resultado == ResultadoAbono.Parcial
                ? "Explica por qué el abono es parcial."
                : "Explica por qué no se abona.");

        if (primerError != null)
        {
            primerError.Focus();
            return;
        }

        // 2. Confirmación
        string pregunta = hayAbono
            ? $"Se registrará el abono {numero} de {importe.ToString("C", Componentes.Es)} " +
              $"con fecha {dtpFecha.Value.ToString("dd/MM/yyyy", Componentes.Es)}"
            : "Se registrará que esta devolución no tiene abono";

        // Un nº de abono repetido suele ser un error al copiarlo, pero un mismo abono
        // puede cubrir varias devoluciones: solo se avisa, no se impide
        string avisoRepetido = "";
        if (hayAbono)
        {
            try
            {
                if (servicio.DevolucionConNumeroAbono(numero, devolucion.Id) is string otra)
                    avisoRepetido = $"⚠  El abono {numero} ya está registrado en la devolución {otra}. " +
                                    "Compruébalo, salvo que el mismo abono cubra las dos.\n\n";
            }
            catch (DbException)
            {
                // Es solo un aviso: si no se puede comprobar, se sigue sin él
            }
        }

        if (MessageBox.Show(this,
                avisoRepetido + pregunta + " y la devolución quedará cerrada.\n" +
                "Si hay algún error podrás reabrir el abono.\n\n¿Registrar y cerrar?",
                "Registrar abono y cerrar", MessageBoxButtons.YesNo,
                avisoRepetido == "" ? MessageBoxIcon.Question : MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        // 3. Guardar
        var datos = new DatosAbono(
            devolucion.Id,
            resultado!,
            hayAbono ? numero : null,
            dtpFecha.Value.Date,
            hayAbono ? importe : null,
            observaciones == "" ? null : observaciones);

        try
        {
            servicio.RegistrarAbono(datos, devolucion.FechaLlegada);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo registrar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo registrar el abono.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    // Los datos del abono solo se guardan al registrar: si se cierra antes, se avisa
    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (MessageBox.Show(this,
                "Los datos del abono solo se guardan al registrarlo.\n" +
                "Si sales ahora se perderán (el documento adjunto sí se ha guardado).\n\n¿Salir de todos modos?",
                "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            e.Cancel = true;
            DialogResult = DialogResult.None;
        }
    }
}
