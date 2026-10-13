namespace GPE.Escritorio;

public class FormNuevaDevolucion : Form
{
//     // Mismos colores que FormPrincipal (más adelante conviene moverlos a una clase común)
//     private static readonly Color ColorFondo = Color.FromArgb(245, 246, 244);
//     private static readonly Color ColorVerde = Color.FromArgb(22, 101, 74);
//     private static readonly Color ColorTexto = Color.FromArgb(20, 35, 30);
//     private static readonly Color ColorSuave = Color.FromArgb(110, 120, 115);

//     // 1. Datos de la devolución
//     private readonly ComboBox cmbCliente = new();
//     private readonly DateTimePicker dtpSolicitud = new();
//     private readonly ComboBox cmbMotivo = new();
//     private readonly TextBox txtAlbaran = new();
//     private readonly TextBox txtObservaciones = new();

//     // 2. Recogida
//     private readonly ComboBox cmbTransportista = new();
//     private readonly DateTimePicker dtpPrevista = new();
//     private readonly DateTimePicker dtpRecogida = new();
//     private readonly ComboBox cmbEstadoRecogida = new();

//     // 3. Productos (cada fila es un conjunto de controles)
//     private record FilaProducto(
//         TableLayoutPanel Panel,
//         ComboBox Producto,
//         NumericUpDown Cantidad,
//         TextBox Lote,
//         TextBox Observaciones);

//     private readonly TableLayoutPanel contenedorFilas = new();
//     private readonly List<FilaProducto> filas = new();

//     // Datos inventados: más adelante vendrán de la base de datos
//     private readonly Dictionary<string, string> catalogoProductos = new()
//     {
//         { "Bombones surtidos 250 g", "REF-0001" },
//         { "Tableta chocolate negro 100 g", "REF-0002" },
//         { "Trufas de cacao 200 g", "REF-0003" },
//         { "Praliné de avellana 150 g", "REF-0004" }
//     };

//     // 4. Documentación: tipo de documento -> ruta del archivo elegido
//     private readonly Dictionary<string, string> documentos = new();

//     public FormNuevaDevolucion()
//     {
//         Text = "Nueva devolución";
//         Size = new Size(1000, 800);
//         MinimumSize = new Size(800, 600);
//         StartPosition = FormStartPosition.CenterParent;
//         BackColor = ColorFondo;
//         Font = new Font("Segoe UI", 10);

//         PrepararControles();

//         // Con Dock el orden importa: primero lo que rellena, luego el pie
//         Controls.Add(CrearContenido());
//         Controls.Add(CrearPie());
//     }

//     // ---------- Preparación de controles ----------

//     private void PrepararControles()
//     {
//         foreach (var combo in new[] { cmbCliente, cmbMotivo, cmbTransportista, cmbEstadoRecogida })
//             combo.DropDownStyle = ComboBoxStyle.DropDownList;

//         cmbCliente.Items.AddRange(new object[]
//             { "Distribuciones Rivera", "Alimentación Sánchez", "Supermercados Norte", "Comercial del Sur" });
//         cmbMotivo.Items.AddRange(new object[]
//             { "Producto dañado", "Error en pedido", "Caducidad próxima", "Envase defectuoso", "Otro" });
//         cmbTransportista.Items.AddRange(new object[]
//             { "Transportista 1", "Transportista 2", "Transporte propio" });
//         cmbEstadoRecogida.Items.AddRange(new object[]
//             { "Pendiente de solicitar", "Solicitada", "Programada", "Recogida" });
//         cmbEstadoRecogida.SelectedIndex = 0;

//         dtpSolicitud.Format = DateTimePickerFormat.Short;   // por defecto: hoy

//         foreach (var fecha in new[] { dtpPrevista, dtpRecogida })
//         {
//             fecha.Format = DateTimePickerFormat.Short;
//             fecha.ShowCheckBox = true;   // desmarcado = sin fecha todavía
//             fecha.Checked = false;
//         }

//         txtAlbaran.PlaceholderText = "Ej. ALB-45872";
//         txtObservaciones.PlaceholderText = "Añade cualquier información relevante sobre la solicitud...";
//         txtObservaciones.Multiline = true;
//         txtObservaciones.Height = 80;
//         txtObservaciones.ScrollBars = ScrollBars.Vertical;
//     }

//     // ---------- Estructura general ----------

//     private Panel CrearContenido()
//     {
//         var scroll = new Panel
//         {
//             Dock = DockStyle.Fill,
//             AutoScroll = true,
//             Padding = new Padding(40, 25, 40, 25)
//         };

//         // Pila vertical: cada hijo es una fila que ocupa todo el ancho
//         var pila = new TableLayoutPanel
//         {
//             Dock = DockStyle.Top,
//             AutoSize = true,
//             AutoSizeMode = AutoSizeMode.GrowAndShrink,
//             ColumnCount = 1
//         };
//         pila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

//         pila.Controls.Add(CrearCabecera());
//         pila.Controls.Add(CrearTarjetaDatos());
//         pila.Controls.Add(CrearTarjetaRecogida());
//         pila.Controls.Add(CrearTarjetaProductos());
//         pila.Controls.Add(CrearTarjetaDocumentos());

//         scroll.Controls.Add(pila);
//         return scroll;
//     }

//     private Control CrearCabecera()
//     {
//         var panel = new Panel { Dock = DockStyle.Fill, Height = 120, Margin = new Padding(0, 0, 0, 10) };

//         var lblSubtitulo = new Label
//         {
//             Text = "Completa los datos para iniciar el proceso de devolución.",
//             Dock = DockStyle.Top,
//             Height = 28,
//             ForeColor = ColorSuave
//         };
//         var lblTitulo = new Label
//         {
//             Text = "Nueva devolución",
//             Dock = DockStyle.Top,
//             Height = 50,
//             Font = new Font("Segoe UI", 22, FontStyle.Bold),
//             ForeColor = ColorTexto
//         };
//         var lblRuta = new Label
//         {
//             Text = "DEVOLUCIONES / NUEVA",
//             Dock = DockStyle.Top,
//             Height = 25,
//             ForeColor = ColorSuave,
//             Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
//         };

//         // El último añadido queda arriba
//         panel.Controls.Add(lblSubtitulo);
//         panel.Controls.Add(lblTitulo);
//         panel.Controls.Add(lblRuta);
//         return panel;
//     }

//     // ---------- Tarjetas ----------

//     private Control CrearTarjetaDatos()
//     {
//         var tarjeta = CrearTarjeta("1", "Datos de la devolución", "Información general de la solicitud del cliente");

//         var rejilla = Rejilla(2);
//         rejilla.Controls.Add(Campo("Cliente", cmbCliente));
//         rejilla.Controls.Add(Campo("Fecha de solicitud", dtpSolicitud));
//         rejilla.Controls.Add(Campo("Motivo de devolución", cmbMotivo));
//         rejilla.Controls.Add(Campo("Albarán relacionado", txtAlbaran));

//         var campoObservaciones = Campo("Observaciones", txtObservaciones);
//         rejilla.Controls.Add(campoObservaciones);
//         rejilla.SetColumnSpan(campoObservaciones, 2);

//         tarjeta.Controls.Add(rejilla);
//         return tarjeta;
//     }

//     private Control CrearTarjetaRecogida()
//     {
//         var tarjeta = CrearTarjeta("2", "Recogida", "Planificación del transporte y recogida de mercancía");

//         var rejilla = Rejilla(3);
//         rejilla.Controls.Add(Campo("Transportista", cmbTransportista));
//         rejilla.Controls.Add(Campo("Fecha prevista de recogida", dtpPrevista));
//         rejilla.Controls.Add(Campo("Fecha de recogida", dtpRecogida));
//         rejilla.Controls.Add(Campo("Estado de la recogida", cmbEstadoRecogida));

//         tarjeta.Controls.Add(rejilla);
//         return tarjeta;
//     }

//     private Control CrearTarjetaProductos()
//     {
//         var btnAnadir = CrearBotonSecundario("+  Añadir producto", 170);
//         btnAnadir.Click += (s, e) => AnadirFilaProducto();

//         var tarjeta = CrearTarjeta("3", "Productos", "Productos incluidos en la devolución", btnAnadir);

//         contenedorFilas.Dock = DockStyle.Fill;
//         contenedorFilas.AutoSize = true;
//         contenedorFilas.ColumnCount = 1;
//         contenedorFilas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

//         tarjeta.Controls.Add(CrearEncabezadoProductos());
//         tarjeta.Controls.Add(contenedorFilas);

//         AnadirFilaProducto();   // una fila vacía de partida
//         return tarjeta;
//     }

//     private Control CrearTarjetaDocumentos()
//     {
//         var tarjeta = CrearTarjeta("4", "Documentación", "Adjunta los documentos asociados a la devolución");

//         var rejilla = Rejilla(3);
//         foreach (string tipo in new[] { "Albarán", "Hoja de devolución", "Otros documentos" })
//             rejilla.Controls.Add(CrearCajaDocumento(tipo));

//         tarjeta.Controls.Add(rejilla);
//         return tarjeta;
//     }

//     // ---------- Productos ----------

//     private static TableLayoutPanel PlantillaFilaProducto()
//     {
//         // Columnas: producto, referencia, cantidad, lote, observaciones, botón quitar
//         var fila = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 6 };
//         foreach (float peso in new float[] { 30, 14, 14, 14, 28 })
//             fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, peso));
//         fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
//         return fila;
//     }

//     private Control CrearEncabezadoProductos()
//     {
//         var encabezado = PlantillaFilaProducto();
//         encabezado.BackColor = Color.FromArgb(243, 244, 241);
//         encabezado.Height = 35;

//         string[] titulos = { "PRODUCTO", "REFERENCIA", "CANTIDAD ESPERADA", "LOTE", "OBSERVACIONES", "" };
//         for (int i = 0; i < titulos.Length; i++)
//         {
//             encabezado.Controls.Add(new Label
//             {
//                 Text = titulos[i],
//                 Dock = DockStyle.Fill,
//                 Height = 35,
//                 TextAlign = ContentAlignment.MiddleLeft,
//                 ForeColor = ColorSuave,
//                 Font = new Font("Segoe UI", 8, FontStyle.Bold)
//             }, i, 0);
//         }
//         return encabezado;
//     }

//     private void AnadirFilaProducto()
//     {
//         var panel = PlantillaFilaProducto();
//         panel.Margin = new Padding(0, 8, 0, 0);

//         var cmbProducto = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
//         cmbProducto.Items.AddRange(catalogoProductos.Keys.Cast<object>().ToArray());

//         var txtReferencia = new TextBox { ReadOnly = true, PlaceholderText = "REF-0000" };
//         var numCantidad = new NumericUpDown { Minimum = 0, Maximum = 100000 };
//         var txtLote = new TextBox { PlaceholderText = "Lote" };
//         var txtObs = new TextBox { PlaceholderText = "Observaciones" };

//         var btnQuitar = new Button
//         {
//             Text = "✕",
//             FlatStyle = FlatStyle.Flat,
//             ForeColor = Color.Firebrick,
//             Cursor = Cursors.Hand
//         };
//         btnQuitar.FlatAppearance.BorderSize = 0;

//         // Al elegir un producto, se rellena su referencia
//         cmbProducto.SelectedIndexChanged += (s, e) =>
//             txtReferencia.Text = catalogoProductos[cmbProducto.Text];

//         var registro = new FilaProducto(panel, cmbProducto, numCantidad, txtLote, txtObs);

//         btnQuitar.Click += (s, e) =>
//         {
//             if (filas.Count <= 1) return;   // siempre dejamos al menos una fila
//             filas.Remove(registro);
//             contenedorFilas.Controls.Remove(panel);
//             panel.Dispose();
//         };

//         Control[] controles = { cmbProducto, txtReferencia, numCantidad, txtLote, txtObs, btnQuitar };
//         for (int i = 0; i < controles.Length; i++)
//         {
//             controles[i].Dock = DockStyle.Fill;
//             controles[i].Margin = new Padding(0, 0, 8, 0);
//             panel.Controls.Add(controles[i], i, 0);
//         }

//         filas.Add(registro);
//         contenedorFilas.Controls.Add(panel);
//     }

//     // ---------- Documentos ----------

//     private Control CrearCajaDocumento(string tipo)
//     {
//         var boton = new Button
//         {
//             Text = $"{tipo}\nHaz clic para adjuntar\nPDF, JPG o PNG · Máx. 10 MB",
//             Dock = DockStyle.Fill,
//             Height = 110,
//             FlatStyle = FlatStyle.Flat,
//             BackColor = Color.FromArgb(251, 251, 250),
//             ForeColor = ColorTexto,
//             Cursor = Cursors.Hand,
//             Margin = new Padding(0, 0, 12, 0)
//         };
//         boton.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 200);

//         boton.Click += (s, e) =>
//         {
//             using var dialogo = new OpenFileDialog
//             {
//                 Title = $"Adjuntar: {tipo}",
//                 Filter = "Documentos (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png"
//             };
//             if (dialogo.ShowDialog(this) != DialogResult.OK) return;

//             if (new FileInfo(dialogo.FileName).Length > 10 * 1024 * 1024)
//             {
//                 MessageBox.Show(this, "El archivo supera los 10 MB.", "Archivo demasiado grande",
//                     MessageBoxButtons.OK, MessageBoxIcon.Warning);
//                 return;
//             }

//             documentos[tipo] = dialogo.FileName;
//             boton.Text = $"{tipo}\n✓ {Path.GetFileName(dialogo.FileName)}";
//         };

//         return boton;
//     }

//     // ---------- Pie con los botones ----------

//     private Control CrearPie()
//     {
//         var pie = new Panel
//         {
//             Dock = DockStyle.Bottom,
//             Height = 70,
//             BackColor = ColorFondo,
//             Padding = new Padding(40, 10, 40, 10)
//         };
//         var flujo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };

//         var btnCrear = new Button
//         {
//             Text = "✓  Crear devolución",
//             Size = new Size(210, 46),
//             FlatStyle = FlatStyle.Flat,
//             BackColor = ColorVerde,
//             ForeColor = Color.White,
//             Font = new Font("Segoe UI", 10, FontStyle.Bold),
//             Cursor = Cursors.Hand
//         };
//         btnCrear.FlatAppearance.BorderSize = 0;
//         btnCrear.Click += (s, e) => CrearDevolucion();

//         var btnCancelar = CrearBotonSecundario("Cancelar", 120);
//         btnCancelar.DialogResult = DialogResult.Cancel;
//         CancelButton = btnCancelar;   // Esc también cancela

//         // Con RightToLeft, el primero añadido queda más a la derecha
//         flujo.Controls.Add(btnCrear);
//         flujo.Controls.Add(btnCancelar);
//         pie.Controls.Add(flujo);
//         return pie;
//     }

//     private void CrearDevolucion()
//     {
//         if (cmbCliente.SelectedIndex < 0) { Avisar("Selecciona un cliente."); cmbCliente.Focus(); return; }
//         if (cmbMotivo.SelectedIndex < 0) { Avisar("Selecciona el motivo de la devolución."); cmbMotivo.Focus(); return; }
//         if (string.IsNullOrWhiteSpace(txtAlbaran.Text)) { Avisar("Indica el albarán relacionado."); txtAlbaran.Focus(); return; }

//         bool hayProducto = filas.Any(f => f.Producto.SelectedIndex >= 0 && f.Cantidad.Value > 0);
//         if (!hayProducto) { Avisar("Añade al menos un producto con cantidad mayor que 0."); return; }

//         // TODO (tuyo): guardar en la base de datos a través de GPE.Logica
//         MessageBox.Show(this, "Devolución creada (de prueba, todavía no se guarda).", "GPE",
//             MessageBoxButtons.OK, MessageBoxIcon.Information);

//         DialogResult = DialogResult.OK;   // esto cierra el formulario
//     }

//     private void Avisar(string mensaje) =>
//         MessageBox.Show(this, mensaje, "Faltan datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);

//     // ---------- Ayudas para construir la interfaz ----------

//     private TableLayoutPanel CrearTarjeta(string numero, string titulo, string subtitulo, Control? accion = null)
//     {
//         var tarjeta = new TableLayoutPanel
//         {
//             Dock = DockStyle.Fill,
//             AutoSize = true,
//             AutoSizeMode = AutoSizeMode.GrowAndShrink,
//             ColumnCount = 1,
//             BackColor = Color.White,
//             Padding = new Padding(25),
//             Margin = new Padding(0, 0, 0, 20)
//         };
//         tarjeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

//         var cabecera = new TableLayoutPanel
//         {
//             Dock = DockStyle.Fill,
//             AutoSize = true,
//             ColumnCount = 2,
//             Margin = new Padding(0, 0, 0, 15)
//         };
//         cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
//         cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

//         var textos = new Panel { Dock = DockStyle.Fill, Height = 55 };
//         var lblSubtitulo = new Label
//         {
//             Text = subtitulo,
//             Dock = DockStyle.Top,
//             Height = 22,
//             ForeColor = ColorSuave,
//             Font = new Font("Segoe UI", 9)
//         };
//         var lblTitulo = new Label
//         {
//             Text = $"{numero}   {titulo}",
//             Dock = DockStyle.Top,
//             Height = 30,
//             Font = new Font("Segoe UI", 13),
//             ForeColor = ColorTexto
//         };
//         textos.Controls.Add(lblSubtitulo);
//         textos.Controls.Add(lblTitulo);

//         cabecera.Controls.Add(textos, 0, 0);
//         if (accion != null) cabecera.Controls.Add(accion, 1, 0);

//         tarjeta.Controls.Add(cabecera);
//         return tarjeta;
//     }

//     private static TableLayoutPanel Rejilla(int columnas)
//     {
//         var rejilla = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = columnas };
//         for (int i = 0; i < columnas; i++)
//             rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnas));
//         return rejilla;
//     }

//     private static Control Campo(string etiqueta, Control control)
//     {
//         var panel = new TableLayoutPanel
//         {
//             Dock = DockStyle.Fill,
//             AutoSize = true,
//             ColumnCount = 1,
//             Margin = new Padding(0, 0, 15, 15)
//         };
//         panel.Controls.Add(new Label
//         {
//             Text = etiqueta,
//             AutoSize = true,
//             Font = new Font("Segoe UI", 9, FontStyle.Bold),
//             Margin = new Padding(0, 0, 0, 4)
//         });

//         control.Dock = DockStyle.Top;
//         panel.Controls.Add(control);
//         return panel;
//     }

//     private static Button CrearBotonSecundario(string texto, int ancho)
//     {
//         var boton = new Button
//         {
//             Text = texto,
//             Size = new Size(ancho, 46),
//             FlatStyle = FlatStyle.Flat,
//             BackColor = Color.White,
//             ForeColor = ColorTexto,
//             Font = new Font("Segoe UI", 10, FontStyle.Bold),
//             Cursor = Cursors.Hand
//         };
//         boton.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 200);
//         return boton;
//     }
 }