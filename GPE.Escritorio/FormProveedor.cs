using System.Data.Common;
using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Ficha de un proveedor, nuevo o ya guardado. Pestaña "Datos del proveedor": sus datos, su evaluación y su riesgo
// de fraude; las notas se recalculan al cambiar algo. Pestaña "Productos": ProductosProveedor.
public class FormProveedor : Form
{
    private readonly ProveedorServicio servicio = new();
    private readonly CamposFormulario campos = new();
    private readonly Proveedor original;
    private readonly ProductosProveedor productos;
    private readonly CertificadosProveedor certificados;
    private Proveedor actual;   // lo que hay escrito ahora, para las notas
    private bool hayCambios;

    private readonly TextBox txtNumero = new();
    private readonly TextBox txtNombre = new();
    private readonly DateTimePicker dtpAlta = CamposProveedor.Fecha();
    private readonly ComboBox cmbEstado = CamposProveedor.ComboEstado();
    private readonly ComboBox cmbTipo;
    private readonly DateTimePicker dtpRevision = CamposProveedor.Fecha();
    private readonly DateTimePicker dtpEnsayo = CamposProveedor.Fecha();
    private readonly CheckBox chkEnsayoNoAplica = new();
    private readonly TextBox txtEnsayoMotivo = new();
    private readonly DateTimePicker dtpAuditoria = CamposProveedor.Fecha();
    private readonly CheckBox chkAuditoriaNoAplica = new();
    private readonly TextBox txtAuditoriaMotivo = new();
    private readonly TextBox txtRfa = new() { MaxLength = 4 };
    private readonly ComboBox cmbImpacto = CamposProveedor.ComboNota(1, 3);
    private readonly ComboBox cmbEstatus = CamposProveedor.ComboNota(0, 3);
    private readonly Label lblReclamaciones = new();
    private byte notaReclamaciones;   // sale de sus NC; con un proveedor nuevo, 0
    private readonly ComboBox cmbRelacion = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbHistorial = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbSubcontratado = CamposProveedor.ComboNota(0, 3);

    private readonly Label lblEvaluacion = new();

    // Debajo de Reclamaciones: al pulsar, las NC enlazadas
    private readonly Button btnNc = Tema.BotonSecundario("Ver NC", 90);
    private List<NoConformidad> ncs = new();
    private readonly ResumenProveedor resumen = new();   // los cuatro recuadros de arriba

    private readonly RadioButton pestanaDatos = new() { Text = "Datos del proveedor" };
    private readonly RadioButton pestanaProductos = new();

    public int IdGuardado { get; private set; }

    // proveedor == null: proveedor nuevo. abrirProductos: empieza en la pestaña Productos.
    public FormProveedor(ProveedorConProductos? proveedor, bool abrirProductos = false)
    {
        bool nuevo = proveedor == null;
        original = proveedor?.Proveedor ?? new Proveedor(0, SiguienteNumero(), "", DateTime.Today, false, null, null,
            null, null, null, null, null, false, null, null, false, null, null, null, null, null, null, null);
        actual = original;
        notaReclamaciones = original.Reclamaciones ?? 0;   // PrepararNc la vuelve a sacar con sus NC
        cmbTipo = CamposProveedor.ComboTipo(TipoProveedor.Todos, original.Tipo);
        productos = new ProductosProveedor(original.Id, proveedor?.Productos ?? new(), () => actual);
        certificados = new CertificadosProveedor(original);
        productos.Cambiados += (s, e) =>
        {
            ActualizarPestanaProductos();
            resumen.Actualizar(actual, productos.Productos);
        };

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
            string subtitulo = string.Join("  ·  ", new[] { original.Tipo, original.Baja ? CamposProveedor.DeBaja : CamposProveedor.Activo }.OfType<string>());
            pila.Controls.Add(Componentes.Cabecera($"PROVEEDORES  ·  Nº {original.Numero?.ToString() ?? CamposProveedor.SinNota}", original.Nombre, subtitulo));
        }

        // Pestañas debajo del nombre: los datos del proveedor o la lista de sus productos,
        // para no tenerlo todo en una página. El resumen de arriba se ve en las dos.
        var tarjetasDatos = new Control[]
        {
            CamposProveedor.Marcada(TarjetaDatos(), EstiloProveedores.RayaDatos),
            CamposProveedor.Marcada(certificados.Tarjeta, EstiloProveedores.RayaDatos),
            CamposProveedor.Marcada(TarjetaEvaluacion(), EstiloProveedores.RayaEvaluacion),
            CamposProveedor.Marcada(TarjetaRiesgo(), EstiloProveedores.RayaRiesgo)
        };
        var tarjetaProductos = CamposProveedor.Marcada(productos.Tarjeta, EstiloProveedores.RayaProductos);
        pila.Controls.Add(CrearPestanas(tarjetasDatos, tarjetaProductos, scroll));

        if (original.Revisar != null)
            pila.Controls.Add(AvisoDatosDistintos(original.Revisar));

        pila.Controls.Add(resumen);
        foreach (var tarjeta in tarjetasDatos)
            pila.Controls.Add(tarjeta);
        pila.Controls.Add(tarjetaProductos);
        tarjetaProductos.Visible = false;
        if (abrirProductos && !nuevo)
            pestanaProductos.Checked = true;

        var btnGuardar = Tema.BotonPrincipal(nuevo ? "Guardar proveedor" : "Guardar cambios", 190);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 140);
        btnCancelar.Click += (s, e) => Close();
        pila.Controls.Add(Componentes.Acciones(btnGuardar, btnCancelar));

        Controls.Add(scroll);

        Poner(original);
        ActualizarNotas();

        // A partir de aquí, cualquier cambio cuenta como "sin guardar" y recalcula las notas
        CamposProveedor.AlCambiar(this, () =>
        {
            hayCambios = true;
            ActualizarNotas();
        }, productos.Filtro);
        FormClosing += AvisarCambiosSinGuardar;

        // El primer campo coge el foco y la página bajaría hasta él: se vuelve arriba
        Shown += (s, e) =>
        {
            productos.QuitarSeleccion();
            certificados.QuitarSeleccion();
            scroll.AutoScrollPosition = Point.Empty;
            if (nuevo) txtNombre.Focus();
        };
    }

    // Nota de reclamaciones: no se escribe, sale de las NC de los últimos 12 meses (ProveedorServicio.NotaReclamaciones).
    // Debajo, un botón pequeño "Ver NC" con todas las enlazadas (las que lo tienen elegido en su ficha).
    private Control DatoReclamaciones()
    {
        var dato = CamposProveedor.PanelDato("Reclamaciones");
        lblReclamaciones.AutoSize = false;
        lblReclamaciones.Size = new Size(46, 28);
        lblReclamaciones.TextAlign = ContentAlignment.MiddleCenter;
        lblReclamaciones.Font = Tema.FuenteNegrita;
        lblReclamaciones.BackColor = Tema.CabeceraTabla;
        lblReclamaciones.Margin = new Padding(0, 4, 0, 0);
        dato.Controls.Add(lblReclamaciones);

        btnNc.Size = new Size(90, 28);
        btnNc.Font = Tema.FuentePequenaNegrita;
        btnNc.Margin = new Padding(0, 8, 0, 0);
        btnNc.Click += (s, e) => VerNc();
        dato.Controls.Add(btnNc);

        btnNc.Visible = false;   // nuevo, o sin NC
        if (original.Id != 0)
        {
            try
            {
                ncs = new NoConformidadServicio().ObtenerDeProveedor(original.Id);
                PonerNotaReclamaciones();
            }
            catch (DbException)
            {
                // Se queda la nota con la que se abrió la ficha
            }
        }
        lblReclamaciones.Text = notaReclamaciones.ToString();
        return dato;
    }

    private void PonerNotaReclamaciones()
    {
        var (noAfectan, afectan) = ProveedorServicio.ContarReclamaciones(ncs);
        notaReclamaciones = ProveedorServicio.NotaReclamaciones(noAfectan, afectan);
        lblReclamaciones.Text = notaReclamaciones.ToString();
        btnNc.Visible = ncs.Count > 0;
    }

    private void VerNc()
    {
        using var lista = new FormNcProveedor(actual, ncs);
        lista.ShowDialog(this);
        if (!lista.HayCambios) return;
        ncs = lista.Registros;
        PonerNotaReclamaciones();
        ActualizarNotas();   // la evaluación cambia con la nota de reclamaciones
    }

    private int? SiguienteNumero()
    {
        try { return servicio.SiguienteNumero(); }
        catch (DbException) { return null; }   // se puede escribir a mano
    }

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
                bool verProductos = boton == pestanaProductos;
                foreach (var tarjeta in tarjetasDatos)
                    tarjeta.Visible = !verProductos;
                tarjetaProductos.Visible = verProductos;
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
        pestanaProductos.Text = original.Id == 0 ? "Productos" : $"Productos ({productos.Productos.Count(x => !x.Baja)})";

    private static void PintarPestana(RadioButton boton)
    {
        boton.BackColor = boton.Checked ? Tema.Acento : Color.White;
        boton.ForeColor = boton.Checked ? Color.White : Tema.Texto;
        boton.Font = Tema.FuenteDeOpcion(boton.Checked);
        boton.FlatAppearance.BorderColor = boton.Checked ? Tema.Acento : Tema.Borde;
        boton.FlatAppearance.MouseOverBackColor = boton.Checked ? Tema.Acento : Tema.AcentoSuave;
    }

    // Tarjetas con los campos

    private Control TarjetaDatos()
    {
        var tarjeta = Componentes.Tarjeta("Datos del proveedor");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Nº", txtNumero));
        CamposProveedor.Ancho(rejilla, campos.Campo("Nombre", txtNombre), 2);
        rejilla.Controls.Add(campos.Campo("Estado", cmbEstado));
        rejilla.Controls.Add(campos.Campo("Tipo", cmbTipo));
        rejilla.Controls.Add(campos.Campo("Fecha alta", dtpAlta));
        rejilla.Controls.Add(campos.Campo("Fecha revisión", dtpRevision));
        rejilla.Controls.Add(CamposProveedor.Hueco());
        rejilla.Controls.Add(campos.Campo("Ensayo trazabilidad", dtpEnsayo,
            NoAplica(dtpEnsayo, chkEnsayoNoAplica, txtEnsayoMotivo)));
        rejilla.Controls.Add(campos.Campo("Auditoría", dtpAuditoria,
            NoAplica(dtpAuditoria, chkAuditoriaNoAplica, txtAuditoriaMotivo)));
        rejilla.Controls.Add(campos.Campo("RFA (año)", txtRfa));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Debajo de la fecha: casilla "No aplica" y, si se marca, el motivo. Con "No aplica" no se puede poner fecha.
    private static Control NoAplica(DateTimePicker fecha, CheckBox noAplica, TextBox motivo)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(0, 6, 0, 0) };
        noAplica.Text = "No aplica";
        noAplica.AutoSize = true;
        noAplica.Margin = new Padding(0, 0, 0, 4);
        motivo.Dock = DockStyle.Top;
        motivo.MaxLength = 500;
        motivo.PlaceholderText = "Motivo (opcional)";
        panel.Controls.Add(noAplica);
        panel.Controls.Add(motivo);

        void Ajustar()
        {
            motivo.Visible = noAplica.Checked;
            fecha.Enabled = !noAplica.Checked;
            if (noAplica.Checked) fecha.Checked = false;
        }
        noAplica.CheckedChanged += (s, e) => Ajustar();
        Ajustar();
        return panel;
    }

    // Evaluación de frecuencia de revisión del proveedor
    private Control TarjetaEvaluacion()
    {
        var tarjeta = Componentes.Tarjeta("Evaluación del proveedor",
            "Impacto + estatus + reclamaciones: de 1 a 3 es A, de 4 a 6 es B y de 7 a 9 es C.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Impacto", cmbImpacto));
        rejilla.Controls.Add(campos.Campo("Estatus", cmbEstatus));
        rejilla.Controls.Add(DatoReclamaciones());

        // La evaluación no se escribe: sale de las tres notas
        var dato = CamposProveedor.PanelDato("Evaluación");
        lblEvaluacion.AutoSize = false;
        lblEvaluacion.Size = new Size(46, 28);
        lblEvaluacion.TextAlign = ContentAlignment.MiddleCenter;
        lblEvaluacion.Font = Tema.FuenteNegrita;
        lblEvaluacion.Margin = new Padding(0, 4, 0, 0);
        dato.Controls.Add(lblEvaluacion);
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

    private void Poner(Proveedor p)
    {
        txtNumero.Text = p.Numero?.ToString() ?? "";
        txtNombre.Text = p.Nombre;
        CamposProveedor.PonerEstado(cmbEstado, p.Baja);
        CamposProveedor.PonerTipo(cmbTipo, p.Tipo);
        CamposProveedor.PonerFecha(dtpAlta, p.FechaAlta);
        CamposProveedor.PonerFecha(dtpRevision, p.FechaRevision);
        CamposProveedor.PonerFecha(dtpEnsayo, p.EnsayoFecha);
        chkEnsayoNoAplica.Checked = p.EnsayoNoAplica;
        txtEnsayoMotivo.Text = p.EnsayoMotivo ?? "";
        CamposProveedor.PonerFecha(dtpAuditoria, p.AuditoriaFecha);
        chkAuditoriaNoAplica.Checked = p.AuditoriaNoAplica;
        txtAuditoriaMotivo.Text = p.AuditoriaMotivo ?? "";
        txtRfa.Text = p.RfaAnio?.ToString() ?? "";
        CamposProveedor.PonerNota(cmbImpacto, p.Impacto);
        CamposProveedor.PonerNota(cmbEstatus, p.Estatus);
        CamposProveedor.PonerNota(cmbRelacion, p.Relacion);
        CamposProveedor.PonerNota(cmbHistorial, p.Historial);
        CamposProveedor.PonerNota(cmbSubcontratado, p.Subcontratado);
    }

    // Lo que hay escrito. El Nº y el año de RFA, si no son un número, quedan vacíos (Guardar lo marca como error).
    private Proveedor Leer() => original with
    {
        Numero = int.TryParse(txtNumero.Text.Trim(), out int numero) ? numero : null,
        Nombre = txtNombre.Text,
        Baja = CamposProveedor.LeerBaja(cmbEstado),
        Tipo = CamposProveedor.LeerTipo(cmbTipo),
        FechaAlta = CamposProveedor.LeerFecha(dtpAlta),
        FechaRevision = CamposProveedor.LeerFecha(dtpRevision),
        EnsayoFecha = CamposProveedor.LeerFecha(dtpEnsayo),
        EnsayoNoAplica = chkEnsayoNoAplica.Checked,
        EnsayoMotivo = txtEnsayoMotivo.Text,
        AuditoriaFecha = CamposProveedor.LeerFecha(dtpAuditoria),
        AuditoriaNoAplica = chkAuditoriaNoAplica.Checked,
        AuditoriaMotivo = txtAuditoriaMotivo.Text,
        RfaAnio = short.TryParse(txtRfa.Text.Trim(), out short anio) ? anio : null,
        Impacto = CamposProveedor.LeerNota(cmbImpacto),
        Estatus = CamposProveedor.LeerNota(cmbEstatus),
        Reclamaciones = notaReclamaciones,
        Relacion = CamposProveedor.LeerNota(cmbRelacion),
        Historial = CamposProveedor.LeerNota(cmbHistorial),
        Subcontratado = CamposProveedor.LeerNota(cmbSubcontratado)
    };

    // Evaluación, recuadros de arriba y notas de los productos con lo que hay escrito
    private void ActualizarNotas()
    {
        actual = Leer();
        string? evaluacion = ProveedorServicio.Evaluacion(actual);
        CamposProveedor.PintarNota(lblEvaluacion, evaluacion);

        resumen.Actualizar(actual, productos.Productos);
        productos.Repintar();
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
        if (txtRfa.Text.Trim() != "" && leido.RfaAnio == null)
        {
            campos.MarcarError(txtRfa, "Escribe un año de 4 cifras, por ejemplo 2024.");
            primerError ??= txtRfa;
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
            [nameof(Proveedor.EnsayoMotivo)] = dtpEnsayo,
            [nameof(Proveedor.AuditoriaMotivo)] = dtpAuditoria,
            [nameof(Proveedor.RfaAnio)] = txtRfa
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
}
