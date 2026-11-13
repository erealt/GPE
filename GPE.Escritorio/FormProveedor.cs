using System.Data.Common;
using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Ficha de un proveedor, nuevo o ya guardado: sus datos, su evaluación y su riesgo de fraude se escriben aquí.
// Debajo, la tabla con sus productos (solo consulta de momento). Las notas se recalculan al cambiar los datos.
public class FormProveedor : Form
{
    private const int ColumnaEstado = 3;
    private const int ColumnaMuestreo = 4;
    private const int ColumnaRiesgo = 5;

    private const string EstadoActivos = "Activos";
    private const string EstadoBaja = "De baja";
    private const string EstadoTodos = "Todos";
    private const string Activo = "Activo";
    private const string DeBaja = "De baja";
    internal const string SinNota = "—";
    private const string IgualEstatus = "Igual que el estatus";

    private readonly ProveedorServicio servicio = new();
    private readonly CamposFormulario campos = new();
    private readonly Proveedor original;
    private List<ProductoProveedor> productos;
    private Proveedor actual;   // lo que hay escrito ahora, para las notas
    private bool hayCambios;

    private readonly TextBox txtNumero = new();
    private readonly TextBox txtNombre = new();
    private readonly DateTimePicker dtpAlta = new();
    private readonly ComboBox cmbEstado = new();
    private readonly ComboBox cmbTipo = new();
    private readonly DateTimePicker dtpRevision = new();
    private readonly TextBox txtCertificados = new();
    private readonly TextBox txtEnsayo = new();
    private readonly TextBox txtAuditoria = new();
    private readonly TextBox txtRfa = new();
    private readonly ComboBox cmbImpacto = ComboNota(1, 3);
    private readonly ComboBox cmbEstatus = ComboNota(0, 3);
    private readonly ComboBox cmbReclamaciones = ComboNota(0, 2);
    private readonly ComboBox cmbRelacion = ComboNota(0, 3, IgualEstatus);
    private readonly ComboBox cmbHistorial = ComboNota(0, 3);
    private readonly ComboBox cmbSubcontratado = ComboNota(0, 3);

    private readonly Label lblEvaluacion = new();
    private readonly Label lblQueSupone = new();
    private readonly TableLayoutPanel franja = new();
    private readonly DataGridView gridProductos = new();
    private readonly ComboBox cmbEstadoProductos = new();

    public int IdGuardado { get; private set; }

    // proveedor == null: proveedor nuevo
    public FormProveedor(ProveedorConProductos? proveedor)
    {
        bool nuevo = proveedor == null;
        original = proveedor?.Proveedor ?? new Proveedor(0, SiguienteNumero(), "", DateTime.Today, false, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null);
        productos = proveedor?.Productos ?? new();
        actual = original;

        Text = nuevo ? "Nuevo proveedor" : $"Proveedor {original.Numero} - {original.Nombre}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 650);
        Componentes.AjustarAPantalla(this, 0.85);
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var (scroll, pila) = Componentes.Pagina();

        if (nuevo)
            pila.Controls.Add(Componentes.Cabecera("PROVEEDORES", "Nuevo proveedor", "Rellena sus datos y guárdalo."));
        else
        {
            string subtitulo = string.Join("  ·  ", new[] { original.Tipo, original.Baja ? "De baja" : "Activo" }.OfType<string>());
            pila.Controls.Add(Componentes.Cabecera($"PROVEEDORES  ·  Nº {original.Numero?.ToString() ?? "—"}", original.Nombre, subtitulo));
        }

        // Pestañas debajo del nombre: los datos del proveedor o la lista de sus productos,
        // para no tenerlo todo en una página. El resumen de arriba se ve en las dos.
        var tarjetasDatos = new[]
        {
            Marcada(TarjetaDatos(), EstiloProveedores.RayaDatos),
            Marcada(TarjetaEvaluacion(), EstiloProveedores.RayaEvaluacion),
            Marcada(TarjetaRiesgo(), EstiloProveedores.RayaRiesgo)
        };
        var tarjetaProductos = Marcada(TarjetaProductos(), EstiloProveedores.RayaProductos);
        pila.Controls.Add(CrearPestanas(tarjetasDatos, tarjetaProductos, scroll));

        if (original.Revisar != null)
            pila.Controls.Add(AvisoDatosDistintos(original.Revisar));

        PrepararFranja();
        pila.Controls.Add(franja);
        foreach (var tarjeta in tarjetasDatos)
            pila.Controls.Add(tarjeta);
        pila.Controls.Add(tarjetaProductos);
        tarjetaProductos.Visible = false;

        var btnGuardar = Tema.BotonPrincipal(nuevo ? "Guardar proveedor" : "Guardar cambios", 190);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 140);
        btnCancelar.Click += (s, e) => Close();
        pila.Controls.Add(Componentes.Acciones(btnGuardar, btnCancelar));

        Controls.Add(scroll);

        Poner(original);
        ActualizarNotas();

        // A partir de aquí, cualquier cambio cuenta como "sin guardar" y recalcula las notas
        foreach (var control in Entradas(this))
        {
            EventHandler cambio = (s, e) =>
            {
                if (control == cmbEstadoProductos) return;   // el filtro de la tabla no es un dato
                hayCambios = true;
                ActualizarNotas();
            };
            switch (control)
            {
                case ComboBox combo: combo.SelectedIndexChanged += cambio; break;
                case DateTimePicker fecha: fecha.ValueChanged += cambio; break;
                default: control.TextChanged += cambio; break;
            }
        }
        FormClosing += AvisarCambiosSinGuardar;

        // El primer campo coge el foco y la página bajaría hasta él: se vuelve arriba
        Shown += (s, e) =>
        {
            gridProductos.ClearSelection();
            scroll.AutoScrollPosition = Point.Empty;
            if (nuevo) txtNombre.Focus();
        };
    }

    private int? SiguienteNumero()
    {
        try { return servicio.SiguienteNumero(); }
        catch (DbException) { return null; }   // se puede escribir a mano
    }

    private readonly RadioButton pestanaDatos = new() { Text = "Datos del proveedor" };
    private readonly RadioButton pestanaProductos = new();

    // Mismo aspecto que las pestañas de planta de la rutina de calidad
    private Control CrearPestanas(Control[] tarjetasDatos, Control tarjetaProductos, Panel scroll)
    {
        var pestanas = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 15)
        };
        foreach (var boton in new[] { pestanaDatos, pestanaProductos })
        {
            boton.Appearance = Appearance.Button;
            boton.FlatStyle = FlatStyle.Flat;
            boton.TextAlign = ContentAlignment.MiddleCenter;
            boton.Size = new Size(240, 44);
            boton.Margin = new Padding(0, 0, 6, 0);
            boton.Cursor = Cursors.Hand;
            boton.CheckedChanged += (s, e) =>
            {
                PintarPestana(boton);
                if (!boton.Checked) return;
                bool productos = boton == pestanaProductos;
                foreach (var tarjeta in tarjetasDatos)
                    tarjeta.Visible = !productos;
                tarjetaProductos.Visible = productos;
                scroll.AutoScrollPosition = Point.Empty;
            };
            pestanas.Controls.Add(boton);
        }
        pestanaDatos.Checked = true;
        ActualizarPestanaProductos();
        PintarPestana(pestanaDatos);
        PintarPestana(pestanaProductos);
        return pestanas;
    }

    private void ActualizarPestanaProductos() =>
        pestanaProductos.Text = original.Id == 0 ? "Productos" : $"Productos ({productos.Count(x => !x.Baja)})";

    private static void PintarPestana(RadioButton boton)
    {
        boton.BackColor = boton.Checked ? Tema.Acento : Color.White;
        boton.ForeColor = boton.Checked ? Color.White : Tema.Texto;
        boton.Font = Tema.FuenteDeOpcion(boton.Checked);
        boton.FlatAppearance.BorderColor = boton.Checked ? Tema.Acento : Tema.Borde;
        boton.FlatAppearance.MouseOverBackColor = boton.Checked ? Tema.Acento : Tema.AcentoSuave;
    }

    private static Control Marcada(Control tarjeta, Color color)
    {
        EstiloProveedores.MarcarTarjeta(tarjeta, color);
        return tarjeta;
    }

    // Tarjetas con los campos

    private Control TarjetaDatos()
    {
        var tarjeta = Componentes.Tarjeta("Datos del proveedor");
        var rejilla = Componentes.Rejilla(4);

        cmbEstado.Items.AddRange(new object[] { Activo, DeBaja });
        cmbTipo.Items.Add(SinNota);
        cmbTipo.Items.AddRange(TipoProveedor.Todos);
        // Un tipo escrito de otra forma en el Excel también se puede dejar como está
        if (original.Tipo != null && !cmbTipo.Items.Contains(original.Tipo))
            cmbTipo.Items.Add(original.Tipo);
        foreach (var combo in new[] { cmbEstado, cmbTipo })
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var fecha in new[] { dtpAlta, dtpRevision })
        {
            fecha.Format = DateTimePickerFormat.Short;
            fecha.ShowCheckBox = true;   // sin marcar = sin fecha
        }

        var nombre = campos.Campo("Nombre", txtNombre);
        rejilla.Controls.Add(campos.Campo("Nº", txtNumero));
        rejilla.Controls.Add(nombre);
        rejilla.SetColumnSpan(nombre, 2);
        rejilla.Controls.Add(campos.Campo("Estado", cmbEstado));
        rejilla.Controls.Add(campos.Campo("Tipo", cmbTipo));
        rejilla.Controls.Add(campos.Campo("Fecha alta", dtpAlta));
        rejilla.Controls.Add(campos.Campo("Fecha revisión", dtpRevision));
        rejilla.Controls.Add(new Panel { Size = Size.Empty, Margin = Padding.Empty });   // salta al principio de la fila
        rejilla.Controls.Add(campos.Campo("Certificados", txtCertificados));
        rejilla.Controls.Add(campos.Campo("Ensayo trazabilidad", txtEnsayo));
        rejilla.Controls.Add(campos.Campo("Auditoría", txtAuditoria));
        rejilla.Controls.Add(campos.Campo("RFA", txtRfa));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Evaluación de frecuencia de revisión del proveedor
    private Control TarjetaEvaluacion()
    {
        var tarjeta = Componentes.Tarjeta("Evaluación del proveedor",
            "Impacto + estatus + reclamaciones: de 1 a 3 es A, de 4 a 6 es B y de 7 a 9 es C.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Impacto", cmbImpacto));
        rejilla.Controls.Add(campos.Campo("Estatus", cmbEstatus));
        rejilla.Controls.Add(campos.Campo("Reclamaciones", cmbReclamaciones));

        // La evaluación no se escribe: sale de las tres notas
        var dato = PanelDato("Evaluación");
        lblEvaluacion.AutoSize = false;
        lblEvaluacion.Size = new Size(46, 28);
        lblEvaluacion.TextAlign = ContentAlignment.MiddleCenter;
        lblEvaluacion.Font = Tema.FuenteNegrita;
        lblEvaluacion.Margin = new Padding(0, 4, 0, 0);
        dato.Controls.Add(lblEvaluacion);
        lblQueSupone.AutoSize = true;
        lblQueSupone.ForeColor = Tema.Suave;
        lblQueSupone.Font = Tema.FuenteNota;
        lblQueSupone.MaximumSize = new Size(280, 0);
        lblQueSupone.Margin = new Padding(0, 4, 0, 0);
        dato.Controls.Add(lblQueSupone);
        rejilla.Controls.Add(dato);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Riesgo del proveedor para cometer fraude: multiplica el riesgo de cada producto
    private Control TarjetaRiesgo()
    {
        var tarjeta = Componentes.Tarjeta("Riesgo del proveedor para cometer fraude",
            "El valor más alto de los tres multiplica el O × D de cada producto para sacar su riesgo de fraude.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Relación", cmbRelacion));
        rejilla.Controls.Add(campos.Campo("Historial", cmbHistorial));
        rejilla.Controls.Add(campos.Campo("P. subcontratado", cmbSubcontratado));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Desplegable de una nota: "—" (sin nota) y los valores de min a max
    internal static ComboBox ComboNota(int min, int max, string vacio = SinNota)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.Add(vacio);
        for (int i = min; i <= max; i++)
            combo.Items.Add(i.ToString());
        return combo;
    }

    internal static void PonerNota(ComboBox combo, byte? nota)
    {
        int i = nota == null ? -1 : combo.Items.IndexOf(nota.Value.ToString());
        combo.SelectedIndex = i < 0 ? 0 : i;
    }

    internal static byte? LeerNota(ComboBox combo) =>
        combo.SelectedIndex <= 0 ? null : byte.Parse((string)combo.SelectedItem!);

    internal static void PonerFecha(DateTimePicker control, DateTime? fecha)
    {
        if (fecha != null) control.Value = fecha.Value;
        control.Checked = fecha != null;
    }

    internal static DateTime? LeerFecha(DateTimePicker control) => control.Checked ? control.Value.Date : null;

    private void Poner(Proveedor p)
    {
        txtNumero.Text = p.Numero?.ToString() ?? "";
        txtNombre.Text = p.Nombre;
        cmbEstado.SelectedItem = p.Baja ? DeBaja : Activo;
        cmbTipo.SelectedItem = p.Tipo ?? SinNota;
        PonerFecha(dtpAlta, p.FechaAlta);
        PonerFecha(dtpRevision, p.FechaRevision);
        txtCertificados.Text = p.Certificados ?? "";
        txtEnsayo.Text = p.EnsayoTrazabilidad ?? "";
        txtAuditoria.Text = p.Auditoria ?? "";
        txtRfa.Text = p.Rfa ?? "";
        PonerNota(cmbImpacto, p.Impacto);
        PonerNota(cmbEstatus, p.Estatus);
        PonerNota(cmbReclamaciones, p.Reclamaciones);
        PonerNota(cmbRelacion, p.Relacion);
        PonerNota(cmbHistorial, p.Historial);
        PonerNota(cmbSubcontratado, p.Subcontratado);
    }

    // Lo que hay escrito. El Nº, si no es un número, queda vacío (Guardar lo marca como error).
    private Proveedor Leer() => original with
    {
        Numero = int.TryParse(txtNumero.Text.Trim(), out int numero) ? numero : null,
        Nombre = txtNombre.Text,
        Baja = cmbEstado.SelectedItem as string == DeBaja,
        Tipo = cmbTipo.SelectedItem as string is string tipo && tipo != SinNota ? tipo : null,
        FechaAlta = LeerFecha(dtpAlta),
        FechaRevision = LeerFecha(dtpRevision),
        Certificados = txtCertificados.Text,
        EnsayoTrazabilidad = txtEnsayo.Text,
        Auditoria = txtAuditoria.Text,
        Rfa = txtRfa.Text,
        Impacto = LeerNota(cmbImpacto),
        Estatus = LeerNota(cmbEstatus),
        Reclamaciones = LeerNota(cmbReclamaciones),
        Relacion = LeerNota(cmbRelacion),
        Historial = LeerNota(cmbHistorial),
        Subcontratado = LeerNota(cmbSubcontratado)
    };

    // Evaluación, recuadros de arriba y notas de los productos con lo que hay escrito
    private void ActualizarNotas()
    {
        actual = Leer();
        string? evaluacion = ProveedorServicio.Evaluacion(actual);
        var (fondo, color) = Tema.ColorNotaProveedor(evaluacion ?? "");
        lblEvaluacion.Text = evaluacion ?? "—";
        lblEvaluacion.BackColor = fondo;
        lblEvaluacion.ForeColor = color;
        lblQueSupone.Text = evaluacion == null
            ? "Falta el impacto, el estatus o las reclamaciones"
            : ProveedorServicio.QueSupone(evaluacion);

        ActualizarFranja();
        gridProductos.Invalidate();
    }

    // Guardado

    private void Guardar()
    {
        campos.LimpiarErrores();
        var leido = ProveedorServicio.Normalizar(Leer());
        Control? primerError = null;

        if (txtNumero.Text.Trim() != "" && leido.Numero == null)
        {
            campos.MarcarError(txtNumero, "Escribe solo números.");
            primerError = txtNumero;
        }

        List<(string Campo, string Mensaje)> errores;
        try
        {
            errores = servicio.Errores(leido);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo comprobar el Nº del proveedor.", ex);
            return;
        }

        var controles = new Dictionary<string, Control>
        {
            [nameof(Proveedor.Numero)] = txtNumero,
            [nameof(Proveedor.Nombre)] = txtNombre,
            [nameof(Proveedor.Certificados)] = txtCertificados,
            [nameof(Proveedor.EnsayoTrazabilidad)] = txtEnsayo,
            [nameof(Proveedor.Auditoria)] = txtAuditoria,
            [nameof(Proveedor.Rfa)] = txtRfa
        };
        foreach (var (campo, mensaje) in errores)
        {
            campos.MarcarError(controles[campo], mensaje);
            primerError ??= controles[campo];
        }
        if (primerError != null)
        {
            primerError.Focus();
            return;
        }

        try
        {
            IdGuardado = servicio.Guardar(leido);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede guardar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar el proveedor.", ex);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (!Mensajes.DescartarCambios(this, "Hay cambios sin guardar. ¿Salir sin guardarlos?"))
            e.Cancel = true;
    }

    // Todos los controles donde se escribe o se elige algo
    internal static IEnumerable<Control> Entradas(Control padre)
    {
        foreach (Control hijo in padre.Controls)
        {
            if (hijo is TextBox { ReadOnly: false } or ComboBox or DateTimePicker)
                yield return hijo;
            foreach (var nieto in Entradas(hijo))
                yield return nieto;
        }
    }

    // Cuatro recuadros arriba con lo más importante, para verlo sin leer las tarjetas

    private void PrepararFranja()
    {
        franja.Dock = DockStyle.Fill;
        franja.AutoSize = true;
        franja.ColumnCount = 4;
        franja.RowCount = 1;
        franja.Margin = new Padding(0, 0, 0, 20);
        for (int i = 0; i < 4; i++)
            franja.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        franja.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    }

    private void ActualizarFranja()
    {
        var p = actual;
        string? evaluacion = ProveedorServicio.Evaluacion(p);
        string? riesgo = ProveedorServicio.RiesgoFraudeMasAlto(new ProveedorConProductos(p, productos));
        int activos = productos.Count(x => !x.Baja);
        int deBaja = productos.Count - activos;

        var (fondoEval, colorEval) = Tema.ColorNotaProveedor(evaluacion ?? "");
        var (fondoRiesgo, colorRiesgo) = Tema.ColorNotaProveedor(riesgo ?? "");

        franja.SuspendLayout();
        foreach (Control c in franja.Controls.Cast<Control>().ToList())
            c.Dispose();
        franja.Controls.Clear();
        franja.Controls.Add(Recuadro("Evaluación", evaluacion ?? "—",
            evaluacion == null ? "Faltan datos para calcularla" : $"Impacto + estatus + reclamaciones = {(p.Impacto ?? 0) + (p.Estatus ?? 0) + (p.Reclamaciones ?? 0)}",
            fondoEval, colorEval, ultimo: false), 0, 0);
        franja.Controls.Add(Recuadro("Riesgo de fraude más alto", riesgo ?? "—",
            productos.Count == 0 ? "Todavía no tiene productos" : "De sus productos activos",
            fondoRiesgo, colorRiesgo, ultimo: false), 1, 0);
        franja.Controls.Add(Recuadro("Productos activos", activos.ToString(),
            deBaja > 0 ? $"y {deBaja} de baja" : "Ninguno de baja", Color.White, Tema.Texto, ultimo: false), 2, 0);
        franja.Controls.Add(Recuadro("Reclamaciones", Nota(p.Reclamaciones),
            "Nota de 0 a 2", Color.White, Tema.Texto, ultimo: true), 3, 0);
        franja.ResumeLayout();
    }

    private static Control Recuadro(string etiqueta, string valor, string nota, Color fondo, Color color, bool ultimo)
    {
        var recuadro = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = fondo,
            Padding = new Padding(20, 15, 20, 15),
            Margin = new Padding(0, 0, ultimo ? 0 : 15, 0)
        };
        recuadro.Controls.Add(new Label
        {
            Text = etiqueta.ToUpper(Componentes.Es),
            AutoSize = true,
            ForeColor = fondo == Color.White ? Tema.Suave : color,
            Font = Tema.FuenteEtiqueta
        });
        recuadro.Controls.Add(new Label
        {
            Text = valor,
            AutoSize = true,
            ForeColor = color,
            Font = EstiloProveedores.FuenteCifra,
            Margin = new Padding(0, 6, 0, 2)
        });
        recuadro.Controls.Add(new Label
        {
            Text = nota,
            AutoSize = true,
            ForeColor = fondo == Color.White ? Tema.Suave : color,
            Font = Tema.FuenteNota
        });
        return recuadro;
    }

    // Datos que en el Excel eran distintos según la fila, y cuál se guardó
    private static Control AvisoDatosDistintos(string revisar)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Tema.AvisoFondo,
            Padding = new Padding(20, 15, 20, 15),
            Margin = new Padding(0, 0, 0, 20)
        };
        panel.Controls.Add(new Label
        {
            Text = "⚠  En el Excel este proveedor tenía datos distintos según la fila",
            AutoSize = true,
            Font = Tema.FuenteNegrita,
            ForeColor = Tema.Aviso,
            Margin = new Padding(0, 0, 0, 6)
        });
        panel.Controls.Add(new Label
        {
            Text = revisar + "\n\nComprueba los datos: al guardar, este aviso se quita.",
            AutoSize = true,
            ForeColor = Tema.Texto,
            MaximumSize = new Size(1100, 0)
        });
        return panel;
    }

    // Productos: se añaden y se cambian en su propia ventana, que guarda al momento

    private readonly Label lblSubtituloProductos = new();

    private string SubtituloProductos()
    {
        int activos = productos.Count(x => !x.Baja);
        int deBaja = productos.Count - activos;
        return productos.Count == 0
            ? "Este proveedor todavía no tiene productos."
            : $"{activos} {(activos == 1 ? "activo" : "activos")}" + (deBaja > 0 ? $" y {deBaja} de baja" : "")
              + ". El muestreo y el riesgo de fraude son de cada producto. Doble clic en uno para cambiarlo.";
    }

    private Control TarjetaProductos()
    {
        // Un producto necesita el proveedor guardado
        if (original.Id == 0)
            return Componentes.Tarjeta("Productos", "Guarda el proveedor y vuelve a abrirlo para añadirle productos.");

        // Como el filtro del Excel: al abrir se ven solo los productos activos
        cmbEstadoProductos.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbEstadoProductos.Width = 140;
        cmbEstadoProductos.Margin = new Padding(0, 3, 10, 0);
        cmbEstadoProductos.Items.AddRange(new object[] { EstadoActivos, EstadoBaja, EstadoTodos });
        cmbEstadoProductos.SelectedItem = productos.Count(x => !x.Baja) > 0 || productos.Count == 0 ? EstadoActivos : EstadoTodos;
        cmbEstadoProductos.SelectedIndexChanged += (s, e) => MostrarProductos();

        var btnAnadir = Tema.BotonPrincipal("+  Añadir producto", 190);
        btnAnadir.Margin = Padding.Empty;
        btnAnadir.Click += (s, e) => AbrirProducto(null);

        var acciones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        acciones.Controls.Add(cmbEstadoProductos);
        acciones.Controls.Add(btnAnadir);
        var tarjeta = Componentes.Tarjeta("Productos", SubtituloProductos(), acciones, lblSubtituloProductos);

        gridProductos.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && gridProductos.Rows[e.RowIndex].Tag is ProductoProveedor x)
                AbrirProducto(x);
        };
        gridProductos.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || gridProductos.CurrentRow?.Tag is not ProductoProveedor x) return;
            e.Handled = true;
            AbrirProducto(x);
        };

        Componentes.EstiloTabla(gridProductos, altoFila: 44);
        gridProductos.Dock = DockStyle.Top;
        gridProductos.ReadOnly = true;
        gridProductos.MultiSelect = false;
        gridProductos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridProductos.ShowCellToolTips = true;
        gridProductos.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        gridProductos.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        gridProductos.CellPainting += PintarEtiquetas;
        gridProductos.CellFormatting += TextoCalculado;
        EstiloProveedores.TablaMarcada(gridProductos);

        (string Nombre, int Ancho)[] columnas =
        {
            ("CÓDIGO", 130), ("DESCRIPCIÓN", 320), ("TIPO PRODUCTO", 160), ("ESTADO", 100),
            ("MUESTREO", 110), ("RIESGO FRAUDE", 140), ("ACTUACIÓN", 230)
        };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = gridProductos.Columns.Add(nombre, nombre);
            gridProductos.Columns[i].MinimumWidth = ancho;
            gridProductos.Columns[i].FillWeight = ancho;
            gridProductos.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        gridProductos.Columns[1].FillWeight = 600;

        MostrarProductos();
        tarjeta.Controls.Add(gridProductos);
        return tarjeta;
    }

    // x == null: producto nuevo. Las notas se calculan con lo que hay escrito en esta ficha.
    private void AbrirProducto(ProductoProveedor? x)
    {
        using var ficha = new FormProductoProveedor(actual, x);
        if (ficha.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            productos = servicio.ObtenerProductos(original.Id);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "El producto se ha guardado, pero no se pudo volver a leer la lista de productos.", ex);
            return;
        }
        lblSubtituloProductos.Text = SubtituloProductos();
        ActualizarPestanaProductos();
        MostrarProductos();
        ActualizarFranja();
    }

    private void MostrarProductos()
    {
        string estado = cmbEstadoProductos.SelectedItem as string ?? EstadoTodos;
        gridProductos.Rows.Clear();
        // Con "Todos", primero los activos y los de baja al final
        foreach (var x in productos.OrderBy(x => x.Baja))
            if (estado == EstadoTodos || x.Baja == (estado == EstadoBaja))
                gridProductos.Rows.Add(CrearFila(x));

        Componentes.AjustarAltoTabla(gridProductos);
        gridProductos.ClearSelection();
    }

    private DataGridViewRow CrearFila(ProductoProveedor x)
    {
        // Las filas creadas a mano no cogen el alto de RowTemplate, y AjustarAltoTabla cuenta con él
        var fila = new DataGridViewRow { Tag = x, Height = gridProductos.RowTemplate.Height };
        fila.CreateCells(gridProductos,
            x.Codigo ?? "—",
            x.Descripcion ?? "—",
            x.TipoProducto ?? "—",
            x.Baja ? "Baja" : "Activo",
            "", "", "");   // muestreo, riesgo y actuación dependen de las notas del proveedor: TextoCalculado

        if (x.Baja)
            fila.DefaultCellStyle.ForeColor = Tema.Suave;
        return fila;
    }

    // Muestreo, riesgo y actuación se calculan al pintar, con las notas del proveedor que hay escritas
    private void TextoCalculado(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || gridProductos.Rows[e.RowIndex].Tag is not ProductoProveedor x) return;
        var p = actual;
        var celda = gridProductos.Rows[e.RowIndex].Cells[e.ColumnIndex];

        switch (e.ColumnIndex)
        {
            case ColumnaMuestreo:
                string? muestreo = ProveedorServicio.FrecuenciaMuestreo(p, x);
                e.Value = muestreo ?? "—";
                celda.ToolTipText = muestreo == null
                    ? "Falta el impacto, el estatus o el control en recepción"
                    : $"Impacto {Nota(ProveedorServicio.ImpactoProducto(p, x))} + estatus {Nota(p.Estatus)}"
                      + $" + control recepción {Nota(x.ControlRecepcion)} + reclamaciones {Nota(x.Reclamaciones)}"
                      + $"\n{ProveedorServicio.AlcanceMuestreo(muestreo)}";
                break;
            case ColumnaRiesgo:
                string? riesgo = ProveedorServicio.RiesgoFraude(p, x);
                e.Value = riesgo ?? "—";
                celda.ToolTipText = riesgo == null
                    ? "Faltan las notas de fraude del producto"
                    : $"O × D = {ProveedorServicio.OcurrenciaPorDeteccion(x)} (vulnerabilidad {ProveedorServicio.RiesgoVulnerabilidad(x)})"
                      + $"\nPuntuación = {ProveedorServicio.Puntuacion(p, x)}";
                break;
            case ColumnaRiesgo + 1:
                e.Value = ProveedorServicio.Actuacion(ProveedorServicio.RiesgoFraude(p, x)) ?? "—";
                break;
        }
    }

    // Estado, muestreo y riesgo se dibujan como etiquetas de color
    private void PintarEtiquetas(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null) return;
        if (gridProductos.Rows[e.RowIndex].Tag is not ProductoProveedor x) return;

        string? texto = e.ColumnIndex switch
        {
            ColumnaEstado => x.Baja ? "Baja" : "Activo",
            ColumnaMuestreo => ProveedorServicio.FrecuenciaMuestreo(actual, x),
            ColumnaRiesgo => ProveedorServicio.RiesgoFraude(actual, x),
            _ => null
        };
        if (texto == null) return;

        // Estado: verde si está activo y gris si es de baja
        var (fondo, color) = e.ColumnIndex == ColumnaEstado
            ? Tema.ColorNotaProveedor(x.Baja ? "" : "A")
            : Tema.ColorNotaProveedor(texto);
        Componentes.PintarEtiqueta(e, texto, fondo, color);
    }

    // Piezas de la ficha

    private static string Nota(byte? nota) => nota?.ToString() ?? "—";

    internal static FlowLayoutPanel PanelDato(string etiqueta)
    {
        var dato = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 15, 18)
        };
        dato.Controls.Add(new Label
        {
            Text = etiqueta,
            AutoSize = true,
            Font = Tema.FuentePequenaNegrita
        });
        return dato;
    }
}
