using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio
{
    // Programa (o modifica) la recogida de una devolución. Al guardar,
    // la devolución pasa a "Recogida solicitada".
    public class Recogida : PantallaFase
    {
        private static readonly CultureInfo Es = new("es-ES");

        private readonly DevolucionServicio servicio = new();
        private readonly DevolucionFila devolucion;
        private RecogidaGuardada? existente;
        private bool cargaCorrecta;

        public const int PasoRecogida = 2;

        // Cuando la mercancía ya ha llegado (fase 3 o más) la recogida solo se consulta
        private bool SoloLectura => devolucion.Fase >= 3;

        private readonly ComboBox cmbTransportista = new();
        private readonly Label lblTelefonoTransportista = new();
        private readonly DateTimePicker dtpRecogida = new();
        private readonly TextBox txtDireccion = new();
        private readonly TextBox txtTelefono = new();
        private readonly TextBox txtObservaciones = new();

        // Mensajes de error que aparecen debajo de cada campo
        private readonly Dictionary<Control, Label> errores = new();

        public Recogida(DevolucionFila devolucion)
        {
            this.devolucion = devolucion;

            Text = $"Gestión de recogida · {devolucion.Numero}";
            Size = new Size(1100, 820);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Tema.Fondo;
            Font = new Font("Segoe UI", 10);

            PrepararControles();
            if (SoloLectura) BloquearCampos();
            Controls.Add(CrearContenido());
        }

        // ---------- Preparación de controles ----------

        private void PrepararControles()
        {
            cmbTransportista.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTransportista.SelectedIndexChanged += (s, e) =>
            {
                lblTelefonoTransportista.Text = cmbTransportista.SelectedItem is Transportista { Telefono: not null } t
                    ? $"Teléfono de {t.Nombre}: {t.Telefono}"
                    : "";
            };

            lblTelefonoTransportista.AutoSize = true;
            lblTelefonoTransportista.ForeColor = Tema.Suave;
            lblTelefonoTransportista.Font = new Font("Segoe UI", 9);
            lblTelefonoTransportista.Margin = new Padding(0, 4, 0, 0);

            dtpRecogida.Format = DateTimePickerFormat.Short;
            dtpRecogida.MinDate = devolucion.FechaSolicitud.Date;   // no puede ser antes de la solicitud

            txtDireccion.MaxLength = 200;
            txtDireccion.PlaceholderText = "Calle, número, código postal y población";

            txtTelefono.MaxLength = 30;
            txtTelefono.PlaceholderText = "Ej. 600 123 456";

            txtObservaciones.Multiline = true;
            txtObservaciones.Height = 80;
            txtObservaciones.ScrollBars = ScrollBars.Vertical;
            txtObservaciones.PlaceholderText = "Horario de recogida, muelle de carga, persona de contacto...";

            try
            {
                cmbTransportista.Items.AddRange(servicio.ObtenerTransportistas().Cast<object>().ToArray());
                existente = servicio.ObtenerRecogida(devolucion.Id);
                cargaCorrecta = true;
            }
            catch (DbException ex)
            {
                MessageBox.Show(this,
                    "No se pudieron cargar los datos de la recogida.\n\n" + ex.Message,
                    "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (existente is { } guardada)
            {
                // Ya había una recogida: se cargan sus datos para modificarlos
                cmbTransportista.SelectedItem = cmbTransportista.Items.Cast<Transportista>()
                    .FirstOrDefault(t => t.Id == guardada.TransportistaId);
                dtpRecogida.Value = guardada.FechaRecogida < dtpRecogida.MinDate
                    ? dtpRecogida.MinDate : guardada.FechaRecogida;
                txtDireccion.Text = guardada.Direccion;
                txtTelefono.Text = guardada.TelefonoContacto ?? "";
                txtObservaciones.Text = guardada.Observaciones ?? "";
            }
            else
            {
                dtpRecogida.Value = DateTime.Today < dtpRecogida.MinDate ? dtpRecogida.MinDate : DateTime.Today;
            }
            // La fecha de creación no se muestra: la pone SQL Server al insertar (DEFAULT SYSDATETIME())
        }

        private void BloquearCampos()
        {
            cmbTransportista.Enabled = false;
            dtpRecogida.Enabled = false;
            foreach (var texto in new[] { txtDireccion, txtTelefono, txtObservaciones })
            {
                texto.ReadOnly = true;
                texto.BackColor = Color.White;
            }
        }

        // ---------- Estructura general ----------

        private Panel CrearContenido()
        {
            var (scroll, pila) = Componentes.Pagina();

            pila.Controls.Add(Componentes.Cabecera(
                $"DEVOLUCIONES / {devolucion.Numero} / RECOGIDA",
                "Gestión de recogida",
                SoloLectura
                    ? $"Recogida de la devolución {devolucion.Numero} de {devolucion.Cliente}."
                    : $"Programa la recogida de la devolución {devolucion.Numero} de {devolucion.Cliente}."));
            pila.Controls.Add(Progreso(devolucion, PasoRecogida));
            pila.Controls.Add(Componentes.Resumen(
                ("CLIENTE", devolucion.Cliente),
                ("ALBARÁN", devolucion.Albaran ?? "—"),
                ("MOTIVO", devolucion.Motivo),
                ("FECHA SOLICITUD", devolucion.FechaSolicitud.ToString("dd MMM yyyy", Es))));
            pila.Controls.Add(CrearTarjetaRecogida());
            pila.Controls.Add(CrearAcciones());

            return scroll;
        }

        private Control CrearTarjetaRecogida()
        {
            var tarjeta = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = Color.White,
                Padding = new Padding(25),
                Margin = new Padding(0, 0, 0, 20)
            };
            tarjeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            tarjeta.Controls.Add(new Label
            {
                Text = "Datos de la recogida",
                AutoSize = true,
                Font = new Font("Segoe UI", 13),
                ForeColor = Tema.Texto
            });
            tarjeta.Controls.Add(new Label
            {
                Text = SoloLectura
                    ? "La mercancía ya ha llegado: la recogida solo se puede consultar."
                    : existente == null
                        ? "Indica quién recoge la mercancía, dónde y cuándo."
                        : "Esta devolución ya tiene una recogida programada. Puedes modificarla hasta que llegue la mercancía.",
                AutoSize = true,
                ForeColor = Tema.Suave,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 2, 0, 15)
            });

            var rejilla = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            rejilla.Controls.Add(Campo("Transportista", cmbTransportista, lblTelefonoTransportista));
            rejilla.Controls.Add(Campo("Fecha de recogida", dtpRecogida));

            rejilla.Controls.Add(Campo("Dirección de recogida", txtDireccion));
            rejilla.Controls.Add(Campo("Teléfono de contacto", txtTelefono));

            var campoObservaciones = Campo("Observaciones", txtObservaciones);
            rejilla.Controls.Add(campoObservaciones);
            rejilla.SetColumnSpan(campoObservaciones, 2);

            tarjeta.Controls.Add(rejilla);
            return tarjeta;
        }

        private Control CrearAcciones()
        {
            if (SoloLectura)
            {
                var btnCerrar = Tema.BotonSecundario("Cerrar", 130);
                btnCerrar.DialogResult = DialogResult.Cancel;
                CancelButton = btnCerrar;
                return Componentes.Acciones(btnCerrar);
            }

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 20)
            };

            var btnGuardar = Tema.BotonPrincipal(existente == null ? "Solicitar recogida" : "Guardar cambios", 190);
            btnGuardar.Height = 45;
            btnGuardar.Margin = new Padding(0);
            btnGuardar.Enabled = cargaCorrecta;
            btnGuardar.Click += (s, e) => Guardar();

            var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
            btnCancelar.Height = 45;
            btnCancelar.Margin = new Padding(0, 0, 10, 0);
            btnCancelar.DialogResult = DialogResult.Cancel;

            CancelButton = btnCancelar;   // Esc cierra sin guardar

            panel.Controls.Add(btnGuardar);
            panel.Controls.Add(btnCancelar);
            return panel;
        }

        // ---------- Guardado ----------

        private void Guardar()
        {
            // 1. Validaciones: se marcan todos los campos con error a la vez
            LimpiarErrores();
            Control? primerError = null;

            if (cmbTransportista.SelectedItem is not Transportista)
            {
                MarcarError(cmbTransportista, "Selecciona un transportista.");
                primerError ??= cmbTransportista;
            }

            if (string.IsNullOrWhiteSpace(txtDireccion.Text))
            {
                MarcarError(txtDireccion, "Indica la dirección de recogida.");
                primerError ??= txtDireccion;
            }

            string telefono = txtTelefono.Text.Trim();
            if (telefono != "" && !DevolucionServicio.TelefonoValido(telefono))
            {
                MarcarError(txtTelefono, "Teléfono no válido: usa al menos 9 dígitos (se admiten espacios, guiones y +).");
                primerError ??= txtTelefono;
            }

            if (primerError != null)
            {
                primerError.Focus();
                return;
            }

            // 2. Guardar en la base de datos
            var datos = new DatosRecogida(
                devolucion.Id,
                ((Transportista)cmbTransportista.SelectedItem!).Id,
                dtpRecogida.Value,
                txtDireccion.Text.Trim(),
                telefono == "" ? null : telefono,
                string.IsNullOrWhiteSpace(txtObservaciones.Text) ? null : txtObservaciones.Text.Trim());

            try
            {
                servicio.GuardarRecogida(datos, devolucion.FechaSolicitud);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(this, ex.Message, "Revisa los datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (DbException ex)
            {
                MessageBox.Show(this,
                    "No se pudo guardar la recogida.\n\n" + ex.Message,
                    "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        // ---------- Campos y mensajes de error ----------

        private Control Campo(string etiqueta, Control control, Control? ayuda = null)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                Margin = new Padding(0, 0, 15, 15)
            };
            panel.Controls.Add(new Label
            {
                Text = etiqueta,
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4)
            });

            control.Dock = DockStyle.Top;
            panel.Controls.Add(control);
            if (ayuda != null) panel.Controls.Add(ayuda);

            // Al corregir el campo desaparece su error
            var error = new Label
            {
                AutoSize = true,
                ForeColor = Tema.Error,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 4, 0, 0),
                Visible = false
            };
            errores[control] = error;
            panel.Controls.Add(error);

            if (control is ComboBox combo)
                combo.SelectedIndexChanged += (s, e) => error.Visible = false;
            else
                control.TextChanged += (s, e) => error.Visible = false;

            return panel;
        }

        private void MarcarError(Control campo, string mensaje)
        {
            var etiqueta = errores[campo];
            etiqueta.Text = mensaje;
            etiqueta.Visible = true;
        }

        private void LimpiarErrores()
        {
            foreach (var etiqueta in errores.Values)
                etiqueta.Visible = false;
        }
    }
}
