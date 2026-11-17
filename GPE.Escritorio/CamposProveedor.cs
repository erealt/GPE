namespace GPE.Escritorio;

// Piezas comunes de las pantallas de proveedores (lista, ficha del proveedor y ficha del producto):
// textos de estado, desplegables de notas, fechas opcionales y huecos de la rejilla.
public static class CamposProveedor
{
    public const string SinNota = "—";

    // Estado en los desplegables de las fichas
    public const string Activo = "Activo";
    public const string DeBaja = "De baja";

    // Estado en las tablas (etiqueta corta)
    public const string Baja = "Baja";

    // Filtro de estado de las listas
    public const string FiltroActivos = "Activos";
    public const string FiltroBaja = "De baja";
    public const string FiltroTodos = "Todos";

    // Notas: "—" (sin nota) y los valores de min a max

    public static ComboBox ComboNota(int min, int max, string vacio = SinNota)
    {
        var combo = Desplegable();
        combo.Items.Add(vacio);
        for (int i = min; i <= max; i++)
            combo.Items.Add(i.ToString());
        return combo;
    }

    public static void PonerNota(ComboBox combo, byte? nota)
    {
        int i = nota == null ? -1 : combo.Items.IndexOf(nota.Value.ToString());
        combo.SelectedIndex = i < 0 ? 0 : i;
    }

    public static byte? LeerNota(ComboBox combo) =>
        combo.SelectedIndex <= 0 ? null : byte.Parse((string)combo.SelectedItem!);

    // Activo / De baja

    public static ComboBox ComboEstado()
    {
        var combo = Desplegable();
        combo.Items.AddRange(new object[] { Activo, DeBaja });
        return combo;
    }

    public static void PonerEstado(ComboBox combo, bool baja) => combo.SelectedItem = baja ? DeBaja : Activo;

    public static bool LeerBaja(ComboBox combo) => combo.SelectedItem as string == DeBaja;

    // Tipo: "—" y la lista. Un tipo escrito de otra forma en el Excel también se puede dejar como está.

    public static ComboBox ComboTipo(IEnumerable<string> tipos, string? actual)
    {
        var combo = Desplegable();
        combo.Items.Add(SinNota);
        combo.Items.AddRange(tipos.Cast<object>().ToArray());
        if (actual != null && !combo.Items.Contains(actual))
            combo.Items.Add(actual);
        return combo;
    }

    public static void PonerTipo(ComboBox combo, string? tipo) => combo.SelectedItem = tipo ?? SinNota;

    public static string? LeerTipo(ComboBox combo) =>
        combo.SelectedItem is string tipo && tipo != SinNota ? tipo : null;

    // Fechas opcionales: la casilla sin marcar es "sin fecha"

    public static DateTimePicker Fecha() => new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true };

    public static void PonerFecha(DateTimePicker control, DateTime? fecha)
    {
        if (fecha != null) control.Value = fecha.Value;
        control.Checked = fecha != null;
    }

    public static DateTime? LeerFecha(DateTimePicker control) => control.Checked ? control.Value.Date : null;

    public static TextBox Multilinea() => new() { Multiline = true, Height = 60, ScrollBars = ScrollBars.Vertical };

    private static ComboBox Desplegable() => new() { DropDownStyle = ComboBoxStyle.DropDownList };

    // Cambios sin guardar

    // Todos los controles donde se escribe o se elige algo
    public static IEnumerable<Control> Entradas(Control padre)
    {
        foreach (Control hijo in padre.Controls)
        {
            if (hijo is TextBox { ReadOnly: false } or ComboBox or DateTimePicker)
                yield return hijo;
            foreach (var nieto in Entradas(hijo))
                yield return nieto;
        }
    }

    // Llama a accion con cada cambio en esas entradas, menos en las que se excluyen (p. ej. un filtro)
    public static void AlCambiar(Control padre, Action accion, params Control[] excepto)
    {
        foreach (var control in Entradas(padre).Where(c => !excepto.Contains(c)))
        {
            switch (control)
            {
                case ComboBox combo: combo.SelectedIndexChanged += (s, e) => accion(); break;
                case DateTimePicker fecha: fecha.ValueChanged += (s, e) => accion(); break;
                default: control.TextChanged += (s, e) => accion(); break;
            }
        }
    }

    // Rejilla de las tarjetas

    // Dato calculado: etiqueta arriba y, debajo, lo que se le añada
    public static FlowLayoutPanel PanelDato(string etiqueta)
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
            Font = Tema.FuentePequenaNegrita,
            Margin = new Padding(0, 3, 3, 3)   // alineada con lo de debajo
        });
        return dato;
    }

    // Nota calculada (etiqueta de color, se rellena con PintarNota) con una explicación debajo si hace falta
    public static Control DatoNota(string etiqueta, Label nota, Label? explicacion = null)
    {
        var dato = PanelDato(etiqueta);
        nota.AutoSize = false;
        nota.Size = new Size(80, 28);
        nota.TextAlign = ContentAlignment.MiddleCenter;
        nota.Font = Tema.FuenteNegrita;
        nota.Margin = new Padding(0, 4, 0, 0);
        dato.Controls.Add(nota);
        if (explicacion != null)
        {
            explicacion.AutoSize = true;
            explicacion.ForeColor = Tema.Suave;
            explicacion.Font = Tema.FuenteNota;
            explicacion.MaximumSize = new Size(280, 0);
            explicacion.Margin = new Padding(0, 4, 0, 0);
            dato.Controls.Add(explicacion);
        }
        return dato;
    }

    // Número o texto calculado
    public static Control DatoTexto(string etiqueta, Label valor, int anchoMaximo = 280)
    {
        var dato = PanelDato(etiqueta);
        valor.AutoSize = true;
        valor.Font = Tema.FuenteNegrita;
        valor.ForeColor = Tema.Texto;
        valor.MaximumSize = new Size(anchoMaximo, 0);
        valor.Margin = new Padding(0, 8, 0, 0);
        dato.Controls.Add(valor);
        return dato;
    }

    // Campo que ocupa varias columnas
    public static void Ancho(TableLayoutPanel rejilla, Control campo, int columnas)
    {
        rejilla.Controls.Add(campo);
        rejilla.SetColumnSpan(campo, columnas);
    }

    // Hueco vacío: lo siguiente empieza al principio de la fila de abajo
    public static Control Hueco() => new Panel { Size = Size.Empty, Margin = Padding.Empty };

    public static T Marcada<T>(T tarjeta, Color color) where T : Control
    {
        EstiloProveedores.MarcarTarjeta(tarjeta, color);
        return tarjeta;
    }

    // Etiqueta de color de una nota (A/B/C, BAJO/MEDIO/ALTO)
    public static void PintarNota(Label etiqueta, string? nota)
    {
        var (fondo, color) = Tema.ColorNotaProveedor(nota ?? "");
        etiqueta.Text = nota ?? SinNota;
        etiqueta.BackColor = fondo;
        etiqueta.ForeColor = color;
    }
}
