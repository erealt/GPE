using GPE.Datos;
using System.Drawing.Drawing2D;

namespace GPE.Escritorio;

// Ventana principal: menú lateral con los módulos, barra superior con el usuario y,
// a la derecha, el módulo elegido. Al arrancar se ve el inicio.
// Para añadir un módulo: una entrada más en CrearModulos.
public class FormPrincipal : Form
{
    // Un módulo de la aplicación: su opción del menú y, la primera vez que se elige, su contenido
    private class Modulo(string nombre, string descripcion, Func<string?> resumen, Func<Control> crear)
    {
        public string Nombre { get; } = nombre;
        public string Descripcion { get; } = descripcion;   // la frase de su tarjeta en el inicio
        public Func<string?> Resumen { get; } = resumen;     // dato del momento para la tarjeta
        public Func<Control> Crear { get; } = crear;
        public Control? Contenido { get; set; }              // null hasta que se abre por primera vez
        public Button? Boton { get; set; }
    }

    private readonly Panel zonaModulo = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel menu = new();
    private readonly List<Modulo> modulos;
    private readonly ModuloDevoluciones devoluciones = new();
    private Modulo? actual;

    public FormPrincipal()
    {
        Text = "GPE";
        Size = new Size(1400, 800);
        MinimumSize = new Size(1350, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        modulos = CrearModulos();

        Controls.Add(zonaModulo);
        Controls.Add(CrearBarraSuperior());
        Controls.Add(CrearMenuLateral());

        // Las devoluciones se meten en la ventana desde el principio (ocultas) para que el
        // aviso de abonos retrasados funcione aunque no se haya entrado en el módulo
        var moduloDevoluciones = modulos.First(m => m.Contenido == devoluciones);
        devoluciones.Visible = false;
        zonaModulo.Controls.Add(devoluciones);

        Mostrar(modulos[0]);

        devoluciones.PideMostrarse += () => Mostrar(moduloDevoluciones);
        Shown += (s, e) => devoluciones.MostrarAvisoAbonos();
        // Al volver de otra ventana, se muestra el aviso que se había saltado
        Activated += (s, e) => { if (devoluciones.AvisoPendiente) BeginInvoke(devoluciones.MostrarAvisoAbonos); };
    }

    private List<Modulo> CrearModulos()
    {
        var lista = new List<Modulo>();

        lista.Add(new Modulo("Inicio", "", () => null,
            () => new ModuloInicio(lista.Skip(1).Select(m => (m.Nombre, m.Descripcion, m.Resumen,
                (Action)(() => Mostrar(m)))).ToList())));

        lista.Add(new Modulo("Devoluciones",
            "Seguimiento de las devoluciones de clientes, de la solicitud al abono.",
            ModuloInicio.ResumenDevoluciones,
            () => devoluciones) { Contenido = devoluciones });

        lista.Add(new Modulo("No conformidades",
            "Incidencias, no conformidades y acciones de mejora de calidad.",
            ModuloInicio.ResumenNoConformidades,
            () => new ModuloNoConformidades()));

        return lista;
    }

    private void Mostrar(Modulo modulo)
    {
        if (modulo.Contenido == null)
        {
            modulo.Contenido = modulo.Crear();
            modulo.Contenido.Dock = DockStyle.Fill;
            zonaModulo.Controls.Add(modulo.Contenido);
        }

        // Los módulos no se cierran al cambiar: así se conservan los filtros y la posición
        foreach (var m in modulos)
            if (m.Contenido != null) m.Contenido.Visible = m == modulo;

        actual = modulo;
        Text = modulo == modulos[0] ? "GPE" : "GPE - " + modulo.Nombre;
        MarcarElegido();
    }

    // ---------- Menú lateral ----------

    private Control CrearMenuLateral()
    {
        var lateral = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = Color.White
        };
        // Línea a la derecha que lo separa del contenido
        lateral.Paint += (s, e) =>
        {
            using var linea = new Pen(Tema.Linea);
            e.Graphics.DrawLine(linea, lateral.Width - 1, 0, lateral.Width - 1, lateral.Height);
        };

        var logo = new Label
        {
            Text = "GPE",
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(24, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Tema.Acento
        };

        menu.Dock = DockStyle.Fill;
        menu.FlowDirection = FlowDirection.TopDown;
        menu.WrapContents = false;
        menu.Padding = new Padding(0, 15, 0, 0);
        foreach (var modulo in modulos)
            menu.Controls.Add(modulo.Boton = BotonMenu(modulo));

        lateral.Controls.Add(menu);
        lateral.Controls.Add(logo);
        return lateral;
    }

    private Button BotonMenu(Modulo modulo)
    {
        var boton = new Button
        {
            Text = modulo.Nombre,
            Size = new Size(219, 46),
            Margin = new Padding(0, 0, 0, 2),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Tema.CabeceraTabla },
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        boton.Click += (s, e) => Mostrar(modulo);

        // Franja morada a la izquierda de la opción elegida (los colores los pone MarcarElegido)
        boton.Paint += (s, e) =>
        {
            if (modulo != actual) return;
            using var franja = new SolidBrush(Tema.Acento);
            e.Graphics.FillRectangle(franja, 0, 0, 4, boton.Height);
        };
        return boton;
    }

    private static readonly Font FuenteMenu = new("Segoe UI", 10.5f);
    private static readonly Font FuenteMenuElegido = new("Segoe UI", 10.5f, FontStyle.Bold);

    private void MarcarElegido()
    {
        foreach (var m in modulos)
        {
            if (m.Boton is not Button boton) continue;
            bool elegido = m == actual;
            boton.BackColor = elegido ? Tema.AcentoSuave : Color.White;
            boton.FlatAppearance.MouseOverBackColor = elegido ? Tema.AcentoSuave : Tema.CabeceraTabla;
            boton.ForeColor = elegido ? Tema.Acento : Tema.Texto;
            boton.Font = elegido ? FuenteMenuElegido : FuenteMenu;
        }
    }

    // ---------- Barra superior ----------

    private static Panel CrearBarraSuperior()
    {
        var barra = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(0, 0, 30, 0)
        };

        var lblFecha = new Label
        {
            Dock = DockStyle.Right,
            Width = 220,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Tema.Suave,
            Text = DateTime.Now.ToString("d 'de' MMMM 'de' yyyy", Componentes.Es)
        };

        // Iniciales dentro de un círculo (se dibujan a mano en Paint)
        string iniciales = Sesion.UsuarioActual?.Iniciales ?? "";
        var lblUsuario = new Label
        {
            Dock = DockStyle.Right,
            Width = 50,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        lblUsuario.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int lado = 34;
            var circulo = new Rectangle((lblUsuario.Width - lado) / 2, (lblUsuario.Height - lado) / 2, lado, lado);
            using var relleno = new SolidBrush(Tema.AcentoSuave);
            e.Graphics.FillEllipse(relleno, circulo);
            TextRenderer.DrawText(e.Graphics, iniciales, lblUsuario.Font, circulo, Tema.Acento,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        var lblNombre = new Label
        {
            Dock = DockStyle.Right,
            AutoSize = false,
            Width = 200,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Tema.Texto,
            Text = Sesion.UsuarioActual?.Nombre ?? ""
        };

        barra.Controls.Add(lblFecha);
        barra.Controls.Add(lblNombre);
        barra.Controls.Add(lblUsuario);
        return barra;
    }
}
