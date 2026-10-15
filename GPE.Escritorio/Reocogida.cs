using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio
{
    // Programa (o modifica) la recogida de una devolución. Al guardar,
    // la devolución pasa a "Recogida solicitada".
    public class Recogida : Form
    {
        private static readonly CultureInfo Es = new("es-ES");

        private readonly DevolucionServicio servicio = new();
        private readonly DevolucionFila devolucion;
        private RecogidaGuardada? existente;
        private bool cargaCorrecta;

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

        // ---------- Estructura general ----------

        private Panel CrearContenido()
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(40, 25, 40, 25)
            };

            var pila = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1
            };
            pila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            pila.Controls.Add(CrearCabecera());
            pila.Controls.Add(CrearProgreso());
            pila.Controls.Add(CrearResumen());
            pila.Controls.Add(CrearTarjetaRecogida());
            pila.Controls.Add(CrearAcciones());

            scroll.Controls.Add(pila);
            return scroll;
        }

        private Control CrearCabecera()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Height = 115, Margin = new Padding(0, 0, 0, 10) };

            var lblSubtitulo = new Label
            {
                Text = $"Programa la recogida de la devolución {devolucion.Numero} de {devolucion.Cliente}.",
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = Tema.Suave
            };
            var lblTitulo = new Label
            {
                Text = "Gestión de recogida",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Tema.Texto
            };
            var lblRuta = new Label
            {
                Text = $"DEVOLUCIONES / {devolucion.Numero} / RECOGIDA",
                Dock = DockStyle.Top,
                Height = 25,
                ForeColor = Tema.Suave,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            // El último añadido queda arriba
            panel.Controls.Add(lblSubtitulo);
            panel.Controls.Add(lblTitulo);
            panel.Controls.Add(lblRuta);
            return panel;
        }

        private Control CrearProgreso()
        {
            return new BarraProgresoDevolucion
            {
                FasesCompletadas = devolucion.Fase,
                FaseActual = Math.Min(devolucion.Fase + 1, 6),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 20)
            };
        }

        // Datos de la devolución como referencia mientras se rellena la recogida
        private Control CrearResumen()
        {
            var resumen = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                BackColor = Color.White,
                Padding = new Padding(25, 15, 25, 15),
                Margin = new Padding(0, 0, 0, 20)
            };
            for (int i = 0; i < 4; i++)
                resumen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            resumen.Controls.Add(DatoResumen("CLIENTE", devolucion.Cliente));
            resumen.Controls.Add(DatoResumen("ALBARÁN", devolucion.Albaran ?? "—"));
            resumen.Controls.Add(DatoResumen("MOTIVO", devolucion.Motivo));
            resumen.Controls.Add(DatoResumen("FECHA SOLICITUD", devolucion.FechaSolicitud.ToString("dd MMM yyyy", Es)));
            return resumen;
        }

        private static Control DatoResumen(string etiqueta, string valor)
        {
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 15, 0)
            };
            panel.Controls.Add(new Label
            {
                Text = etiqueta,
                AutoSize = true,
                ForeColor = Tema.Suave,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            });
            panel.Controls.Add(new Label
            {
                Text = valor,
                AutoSize = true,
                ForeColor = Tema.Texto,
                Margin = new Padding(0, 4, 0, 0)
            });
            return panel;
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
                Text = existente == null
                    ? "Indica quién recoge la mercancía, dónde y cuándo."
                    : "Esta devolución ya tiene una recogida programada. Puedes modificarla.",
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
