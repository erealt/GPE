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

    private bool Editable => devolucion.Fase == Fase.RevisadaAlmacen && Sesion.TieneRol(Rol.Administracion);

    public FormAbono(DevolucionFila devolucion, int paso)
    {
        this.devolucion = devolucion;
        this.paso = paso;

        Text = $"Abono · {devolucion.Numero}";
        Size = new Size(1150, 900);
        MinimumSize = new Size(950, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

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
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar los datos de la devolución.", ex);
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
            ("CÓD. CLIENTE", devolucion.CodigoCliente),
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

    // Resumen de la revisión

    private Control CrearTarjetaRevision()
    {
        var tarjeta = Componentes.Tarjeta("Resultado de la revisión",
            "Lo que el almacén ha recibido y si está bien para servir, para decidir el abono.");

        CabeceraRevision? cabecera = null;
        try
        {
            cabecera = servicio.ObtenerCabeceraRevision(devolucion.Id);
        }
        catch (DbException)
        {
            // Son datos informativos: sin ellos la pantalla sigue siendo útil
        }

        if (cabecera != null)
        {
            tarjeta.Controls.Add(new Label
            {
                Text = $"Agencia: {cabecera.Agencia ?? "—"}     ·     Revisada por: {cabecera.RevisadaPor ?? "—"}",
                AutoSize = true,
                ForeColor = Tema.Texto,
                Margin = new Padding(0, 0, 0, 10)
            });
        }

        tarjeta.Controls.Add(CrearTablaRevision());

        string? observaciones = cabecera?.Observaciones;
        if (!string.IsNullOrWhiteSpace(observaciones))
        {
            tarjeta.Controls.Add(new Label
            {
                Text = "Observaciones del almacén",
                AutoSize = true,
                Font = Tema.FuentePequenaNegrita,
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
            Font = Tema.FuentePequenaNegrita,
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
                     ("CÓDIGO", 12f, false), ("DESCRIPCIÓN", 28f, false), ("FORMATO", 15f, false),
                     ("LOTE", 12f, false), ("CADUCIDAD", 12f, false), ("CANTIDAD", 10f, true),
                     ("APTO PARA SERVIR", 13f, false)
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
            int i = grid.Rows.Add(linea.Codigo, linea.Producto, linea.Formato ?? "—", linea.Lote ?? "—",
                Componentes.Fecha(linea.Caducidad), linea.Cantidad, linea.Apto ? "Sí" : "No");

            // Lo que no está bien para servir, en ámbar
            if (!linea.Apto)
                grid.Rows[i].Cells[6].Style.BackColor = Tema.FondoDiferencia;
        }

        int aptas = lineas.Where(l => l.Apto).Sum(l => l.Cantidad);
        int noAptas = lineas.Where(l => !l.Apto).Sum(l => l.Cantidad);
        int total = grid.Rows.Add("Total", "", "", "", "", aptas + noAptas, $"{aptas} sí · {noAptas} no");
        grid.Rows[total].DefaultCellStyle.BackColor = Tema.FondoSoloLectura;
        grid.Rows[total].DefaultCellStyle.Font = Tema.FuenteNegrita;

        Componentes.AjustarAltoTabla(grid);
        grid.ClearSelection();
        return grid;
    }

    // Datos del abono

    private Control CrearTarjetaAbono()
    {
        var tarjeta = Componentes.Tarjeta("Abono", Editable
            ? "Al registrar el abono la devolución queda cerrada. El documento se guarda al momento."
            : !Sesion.TieneRol(Rol.Administracion) ? SoloConsulta(Rol.Administracion)
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
        else if (Editable && lineas.Count > 0 && lineas.All(l => l.Apto))
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

    // Botones

    private Control CrearAccionesRegistro()
    {
        var btnRegistrar = Tema.BotonPrincipal("Registrar abono y cerrar", 240);
        btnRegistrar.Click += (s, e) => RegistrarAbono();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;

        // Reabrir la revisión es cosa del almacén
        if (!Sesion.TieneRol(Rol.Almacen))
            return Componentes.Acciones(btnRegistrar, btnCancelar);

        return Componentes.Acciones(btnRegistrar, btnCancelar, BotonReabrirRevision(
            "Lo que hayas rellenado aquí sin registrar se perderá; el documento del abono se conserva."));
    }

    // Sin poder registrar: el abono ya está registrado o el usuario no es de administración
    private Control CrearAccionesConsulta()
    {
        var botones = new List<Button> { BotonCerrar() };
        if (devolucion.Fase == Fase.Cierre && Sesion.TieneRol(Rol.Administracion))
            botones.Insert(0, BotonReabrir("Reabrir abono", 170, () =>
                Reabrir("Reabrir abono",
                    "La devolución dejará de estar cerrada y volverá a \"Revisada almacén\" para poder corregir el abono. " +
                    "Los datos del abono se conservan.",
                    () => Recargar(PasoAbono))));
        else if (devolucion.Fase == Fase.RevisadaAlmacen && Sesion.TieneRol(Rol.Almacen))
            botones.Insert(0, BotonReabrirRevision("El documento del abono, si ya se ha adjuntado, se conserva."));
        return Componentes.Acciones(botones.ToArray());
    }

    private Button BotonReabrirRevision(string queSeConserva) =>
        BotonReabrir("Reabrir revisión", 180, () =>
            Reabrir("Reabrir revisión",
                "La devolución volverá a \"Mercancía recibida\" para poder corregir la revisión. " + queSeConserva,
                () => IrAPaso(FormAlmacen.PasoRevision)));

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
            Mensajes.Aviso(this, "No se pudo reabrir", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo reabrir.", ex);
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
            // Si falla la lectura se sigue con los datos que había; ModuloDevoluciones recarga al cerrar
        }
        paso = nuevoPaso;
        Construir();
    }

    // Registrar

    private void RegistrarAbono()
    {
        // Se marcan todos los campos con error a la vez
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

        string pregunta = hayAbono
            ? $"Se registrará el abono {numero} de {importe.ToString("C", Componentes.Es)} " +
              $"con fecha {dtpFecha.Value.ToString("dd/MM/yyyy", Componentes.Es)}"
            : "Se registrará que esta devolución no tiene abono";

        // Un abono puede cubrir varias devoluciones: el repetido solo se avisa
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

        if (!Mensajes.Confirmar(this, "Registrar abono y cerrar",
                avisoRepetido + pregunta + " y la devolución quedará cerrada.\n" +
                "Si hay algún error podrás reabrir el abono.\n\n¿Registrar y cerrar?",
                aviso: avisoRepetido != ""))
            return;

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
            Mensajes.Aviso(this, "No se pudo registrar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo registrar el abono.", ex);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    // Los datos del abono solo se guardan al registrar: si se cierra antes, se avisa
    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (!Mensajes.DescartarCambios(this,
                "Los datos del abono solo se guardan al registrarlo.\n" +
                "Si sales ahora se perderán (el documento adjunto sí se ha guardado).\n\n¿Salir de todos modos?"))
        {
            e.Cancel = true;
            DialogResult = DialogResult.None;
        }
    }
}
