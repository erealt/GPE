namespace GPE.Movil;

// Los avisos de las máquinas: cabecera con el resumen, los pendientes y los resueltos de hoy.
// PRUEBA: mira la API cada 5 segundos mientras la aplicación está abierta; con el móvil bloqueado no avisa
// (para eso hará falta Firebase).
public class AvisosPage : ContentPage
{
    private readonly Label saludo = new() { FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
    private readonly Label textoEstado = new() { Text = "Conectando…", FontSize = 14, TextColor = Colors.White, Opacity = 0.9 };
    private readonly Border punto = new() { WidthRequest = 8, HeightRequest = 8, StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(), VerticalOptions = LayoutOptions.Center };
    private readonly Label inicial = new() { FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Estilo.Acento, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
    private readonly Label miNombre = new() { FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly (Border Caja, Label Numero, Color Color) esperando, trabajando, resueltosHoy;
    private readonly Label cuantosPendientes, cuantosResueltos;
    private readonly Border sinConexion;
    private readonly VerticalStackLayout pendientes = new() { Spacing = 14 };
    private readonly VerticalStackLayout resueltos = new();
    private readonly List<(Aviso Aviso, Label Tiempo, Label Texto)> relojes = new();

    private List<Aviso> avisos = new();
    private string pintado = "";
    private HashSet<int>? vistos;    // para avisar al llegar uno nuevo (no al abrir la aplicación)
    private bool abierta, relojEnMarcha, pidiendoNombre;

    public AvisosPage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Estilo.Fondo;

        esperando = Cifra("Esperando", Estilo.Rojo, Estilo.Rojo);
        trabajando = Cifra("Trabajando", Estilo.Naranja, Estilo.NaranjaTexto);
        resueltosHoy = Cifra("Resueltos hoy", Estilo.Verde, Estilo.Verde);
        var (tituloPendientes, nPendientes) = Estilo.Titulo("Pendientes");
        var (tituloResueltos, nResueltos) = Estilo.Titulo("Resueltos hoy");
        cuantosPendientes = nPendientes;
        cuantosResueltos = nResueltos;

        var resumen = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) },
            ColumnSpacing = 10,
            Margin = new Thickness(0, -48, 0, 0)
        };
        resumen.Add(esperando.Caja, 0);
        resumen.Add(trabajando.Caja, 1);
        resumen.Add(resueltosHoy.Caja, 2);

        var cuerpo = new VerticalStackLayout
        {
            Padding = new Thickness(16, 0, 16, 90),
            Spacing = 10,
            Children = { resumen, tituloPendientes, pendientes, tituloResueltos, resueltos }
        };

        sinConexion = new Border
        {
            BackgroundColor = Estilo.Texto,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(16, 12),
            Margin = new Thickness(16),
            VerticalOptions = LayoutOptions.End,
            IsVisible = false,
            Content = new Label
            {
                FormattedText = new FormattedString
                {
                    Spans =
                    {
                        new Span { Text = "●  ", TextColor = Color.FromArgb("#FFB4B4") },
                        new Span { Text = "Sin conexión con el servidor · reintentando…", TextColor = Colors.White, FontAttributes = FontAttributes.Bold }
                    }
                }
            }
        };

        var pantalla = new Grid();
        pantalla.Add(new ScrollView { Content = new VerticalStackLayout { Children = { Cabecera(), cuerpo } } });
        pantalla.Add(sinConexion);
        Content = pantalla;
        PintarNombre();
    }

    // Cabecera morada: GPE, el mecánico (se pulsa para cambiarlo), el saludo y la conexión
    private View Cabecera()
    {
        var circulo = new Border
        {
            WidthRequest = 28,
            HeightRequest = 28,
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
            Content = inicial
        };
        var yo = new Border
        {
            BackgroundColor = Color.FromRgba(255, 255, 255, 40),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(5, 5, 12, 5),
            HorizontalOptions = LayoutOptions.End,
            MaximumWidthRequest = 220,
            Content = new HorizontalStackLayout { Spacing = 8, Children = { circulo, miNombre } }
        };
        var toque = new TapGestureRecognizer();
        toque.Tapped += async (s, e) => await PedirNombre(primeraVez: false);
        yo.GestureRecognizers.Add(toque);

        var barra = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        barra.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "GPE", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new Label { Text = "Mecánicos", FontSize = 17, TextColor = Colors.White, Opacity = 0.8, VerticalOptions = LayoutOptions.Center }
            }
        }, 0);
        barra.Add(yo, 1);

        return new VerticalStackLayout
        {
            Padding = new Thickness(16, 14, 16, 64),
            Background = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(Estilo.Acento, 0),
                new GradientStop(Estilo.AcentoOscuro, 1)
            }, new Point(0, 0), new Point(1, 1)),
            Children =
            {
                barra,
                new VerticalStackLayout
                {
                    Margin = new Thickness(0, 20, 0, 0),
                    Spacing = 4,
                    Children = { saludo, new HorizontalStackLayout { Spacing = 7, Children = { punto, textoEstado } } }
                }
            }
        };
    }

    // Caja del resumen: número grande con una raya de color arriba
    private static (Border Caja, Label Numero, Color Color) Cifra(string texto, Color raya, Color color)
    {
        var numero = new Label { Text = "0", FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = color };
        var contenido = new Grid { RowDefinitions = { new(4), new(GridLength.Auto) } };
        contenido.Add(new BoxView { Color = raya }, 0, 0);
        contenido.Add(new VerticalStackLayout
        {
            Padding = new Thickness(12, 10, 12, 10),
            Children = { numero, new Label { Text = texto, FontSize = 13, TextColor = Estilo.Suave, Margin = new Thickness(0, 4, 0, 0) } }
        }, 0, 1);
        return (Estilo.Tarjeta(contenido, new Thickness(0), 16), numero, color);
    }

    // Con cero se pone gris, para que se vea a primera vista dónde hay algo
    private static void PonerCifra((Border Caja, Label Numero, Color Color) cifra, int n)
    {
        cifra.Numero.Text = n.ToString();
        cifra.Numero.TextColor = n == 0 ? Estilo.Tenue : cifra.Color;
    }

    private void PintarNombre()
    {
        var nombre = Api.Mecanico;
        miNombre.Text = nombre == "" ? "¿Quién eres?" : nombre;
        inicial.Text = nombre == "" ? "?" : nombre[..1].ToUpperInvariant();
        saludo.Text = nombre == "" ? "Avisos de máquinas" : $"Hola, {nombre.Split(' ')[0]}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        abierta = true;
        if (!relojEnMarcha)
        {
            relojEnMarcha = true;
            Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                var ahora = DateTime.Now;
                foreach (var (aviso, tiempo, texto) in relojes) (tiempo.Text, texto.Text) = Reloj(aviso, ahora);
                if (ahora.Second % 5 == 0) _ = Refrescar();
                relojEnMarcha = abierta;
                return abierta;
            });
        }
        await Refrescar();
        if (string.IsNullOrEmpty(Api.Mecanico)) await PedirNombre(primeraVez: true);
    }

    protected override void OnDisappearing()
    {
        abierta = false;
        base.OnDisappearing();
    }

    private async Task PedirNombre(bool primeraVez)
    {
        if (pidiendoNombre) return;
        pidiendoNombre = true;
        var pagina = new NombrePage(primeraVez);
        await Navigation.PushModalAsync(pagina);
        await pagina.Cerrada;
        pidiendoNombre = false;
        PintarNombre();
        pintado = "";
        vistos = null;
        await Refrescar();
    }

    private async Task Refrescar()
    {
        var lista = await Api.Avisos();
        sinConexion.IsVisible = lista == null;
        punto.BackgroundColor = Color.FromArgb(lista == null ? "#FFB4B4" : "#B9F59C");
        textoEstado.Text = lista == null ? "Sin conexión" : $"Conectado · actualizado a las {DateTime.Now:HH:mm}";
        if (lista == null) return;
        avisos = lista;

        // Uno nuevo: vibra
        var ids = lista.Select(a => a.Id).ToHashSet();
        if (vistos != null && ids.Except(vistos).Any())
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(900)); } catch (Exception) { }
        vistos = ids;

        string estado = string.Join("|", lista.Select(a =>
            $"{a.Id}~{a.Motivo}~{a.Comentario}~{a.Llegada}~{a.Mecanico}~{a.Resuelto}~{a.QueHaHecho}~{a.ResueltoPor}"));
        if (estado == pintado) return;
        pintado = estado;
        Pintar();
    }

    private static (string Tiempo, string Texto) Reloj(Aviso aviso, DateTime ahora) => aviso.Llegada is DateTime llega
        ? (Estilo.Duracion(ahora - llega), $"llegó a las {llega:HH:mm}")
        : (Estilo.Duracion(ahora - aviso.Avisado), $"avisado a las {aviso.Avisado:HH:mm}");

    private void Pintar()
    {
        var abiertos = avisos.Where(a => a.Resuelto == null).OrderBy(a => a.Avisado).ToList();
        var hechos = avisos.Where(a => a.Resuelto != null).OrderByDescending(a => a.Resuelto).ToList();
        PonerCifra(esperando, abiertos.Count(a => a.Llegada == null));
        PonerCifra(trabajando, abiertos.Count(a => a.Llegada != null));
        PonerCifra(resueltosHoy, hechos.Count);
        cuantosPendientes.Text = abiertos.Count.ToString();
        cuantosResueltos.Text = hechos.Count.ToString();

        relojes.Clear();
        pendientes.Children.Clear();
        if (abiertos.Count == 0)
            pendientes.Children.Add(Estilo.Tarjeta(new VerticalStackLayout
            {
                Padding = new Thickness(0, 14),
                Spacing = 2,
                Children =
                {
                    Centrado(Estilo.Circulo("✓", Estilo.VerdeSuave, Estilo.Verde, 56)),
                    new Label { Text = "Todo en marcha", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Estilo.Texto, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 10, 0, 0) },
                    new Label { Text = "No hay avisos pendientes", TextColor = Estilo.Suave, HorizontalOptions = LayoutOptions.Center }
                }
            }));
        foreach (var aviso in abiertos) pendientes.Children.Add(Tarjeta(aviso));

        resueltos.Children.Clear();
        if (hechos.Count == 0)
            resueltos.Children.Add(Estilo.Tarjeta(new Label { Text = "Todavía ninguno", TextColor = Estilo.Tenue, HorizontalOptions = LayoutOptions.Center }, new Thickness(18)));
        else
        {
            var lista = new VerticalStackLayout();
            for (int i = 0; i < hechos.Count; i++)
            {
                if (i > 0) lista.Children.Add(new BoxView { HeightRequest = 1, Color = Estilo.Linea });
                lista.Children.Add(Resuelto(hechos[i]));
            }
            resueltos.Children.Add(Estilo.Tarjeta(lista, new Thickness(0)));
        }
    }

    private static View Centrado(View vista)
    {
        vista.HorizontalOptions = LayoutOptions.Center;
        return vista;
    }

    // Raya roja a la izquierda mientras se espera al mecánico y naranja con él trabajando, como en el escritorio
    private View Tarjeta(Aviso aviso)
    {
        bool trabajando = aviso.Llegada != null;
        var color = trabajando ? Estilo.NaranjaTexto : Estilo.Rojo;

        var estado = Estilo.Pastilla(
            ("● " + (trabajando ? $"Trabajando{(aviso.Mecanico is string m ? " · " + m : "")}" : "Esperando mecánico")).ToUpperInvariant(),
            trabajando ? Estilo.NaranjaSuave : Estilo.RojoSuave, color);

        var (tiempoTexto, relojTexto) = Reloj(aviso, DateTime.Now);
        var tiempo = new Label { Text = tiempoTexto, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = color, HorizontalOptions = LayoutOptions.End };
        var texto = new Label { Text = relojTexto, FontSize = 12, TextColor = Estilo.Tenue, HorizontalOptions = LayoutOptions.End };
        relojes.Add((aviso, tiempo, texto));

        var arriba = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 10 };
        arriba.Add(estado, 0);
        arriba.Add(new VerticalStackLayout { Children = { tiempo, texto } }, 1);

        var boton = Estilo.Boton(trabajando ? "✓  Resuelto" : "He llegado", trabajando ? Estilo.Verde : Estilo.Acento);
        boton.Margin = new Thickness(0, 10, 0, 0);
        boton.Clicked += async (s, e) =>
        {
            boton.IsEnabled = false;
            if (trabajando) await AbrirInforme(aviso);
            else if (!await Api.Llegada(aviso.Id)) await DisplayAlertAsync("Sin conexión", "No se ha podido apuntar. Prueba otra vez.", "Aceptar");
            boton.IsEnabled = true;
            await Refrescar();
        };

        var cuerpo = new VerticalStackLayout
        {
            Spacing = 2,
            Padding = new Thickness(16),
            Children =
            {
                arriba,
                new Label { Text = aviso.Maquina, FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Estilo.Texto, Margin = new Thickness(0, 10, 0, 0) },
                new Label
                {
                    Text = aviso.Motivo ?? "Sin motivo todavía",
                    FontSize = 17,
                    TextColor = aviso.Motivo == null ? Estilo.Tenue : Estilo.Texto,
                    FontAttributes = aviso.Motivo == null ? FontAttributes.Italic : FontAttributes.None
                }
            }
        };
        if (aviso.Maquinista is string maquinista)
            cuerpo.Children.Add(new Border
            {
                BackgroundColor = Estilo.Fondo,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                Padding = new Thickness(11, 5),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalOptions = LayoutOptions.Start,
                Content = new Label
                {
                    FontSize = 14,
                    FormattedText = new FormattedString
                    {
                        Spans =
                        {
                            new Span { Text = "Maquinista ", TextColor = Estilo.Suave },
                            new Span { Text = maquinista, TextColor = Estilo.Texto, FontAttributes = FontAttributes.Bold }
                        }
                    }
                }
            });
        if (aviso.Comentario is string comentario)
            cuerpo.Children.Add(new Border
            {
                BackgroundColor = Estilo.Fondo,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12, 9),
                Margin = new Thickness(0, 8, 0, 0),
                Content = new Label { Text = comentario, FontSize = 15, TextColor = Estilo.Texto }
            });
        cuerpo.Children.Add(boton);

        var conRaya = new Grid { ColumnDefinitions = { new(6), new(GridLength.Star) } };
        conRaya.Add(new BoxView { Color = trabajando ? Estilo.Naranja : Estilo.Rojo }, 0);
        conRaya.Add(cuerpo, 1);
        return Estilo.Tarjeta(conRaya, new Thickness(0));
    }

    // Si la máquina arrancó sin que el mecánico pusiera nada, aquí se puede poner el informe
    private View Resuelto(Aviso aviso)
    {
        var resuelto = aviso.Resuelto!.Value;
        string tiempos = aviso.Llegada is DateTime llega
            ? $"{(aviso.Mecanico is string m ? m + " llegó" : "Llegó")} en {Estilo.Duracion(llega - aviso.Avisado)} · resuelto en {Estilo.Duracion(resuelto - llega)}"
            : "La máquina arrancó sin mecánico";

        var cabecera = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 10 };
        cabecera.Add(new Label { Text = $"{aviso.Maquina} · {aviso.Motivo ?? "Sin motivo"}", FontAttributes = FontAttributes.Bold, TextColor = Estilo.Texto }, 0);
        cabecera.Add(new Label { Text = resuelto.ToString("HH:mm"), TextColor = Estilo.Tenue, FontSize = 14 }, 1);

        var datos = new VerticalStackLayout
        {
            Spacing = 2,
            Children = { cabecera, new Label { Text = tiempos, TextColor = Estilo.Suave, FontSize = 14 } }
        };
        if (aviso.QueHaHecho is string hecho)
        {
            var informe = new VerticalStackLayout { Children = { new Label { Text = hecho, TextColor = Estilo.Texto, FontSize = 15 } } };
            if (aviso.ResueltoPor is string quien) informe.Children.Add(new Label { Text = quien, TextColor = Estilo.Suave, FontSize = 12, Margin = new Thickness(0, 3, 0, 0) });
            datos.Children.Add(new Border
            {
                BackgroundColor = Estilo.Fondo,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12, 9),
                Margin = new Thickness(0, 6, 0, 0),
                Content = informe
            });
        }
        else
        {
            var poner = new Button
            {
                Text = "+ Poner qué se ha hecho",
                BackgroundColor = Estilo.AcentoSuave,
                TextColor = Estilo.Acento,
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                CornerRadius = 10,
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 8, 0, 0)
            };
            poner.Clicked += async (s, e) => await AbrirInforme(aviso);
            datos.Children.Add(poner);
        }

        var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 12, Padding = new Thickness(16, 14) };
        var check = Estilo.Circulo("✓", Estilo.VerdeSuave, Estilo.Verde, 30);
        check.VerticalOptions = LayoutOptions.Start;
        fila.Add(check, 0);
        fila.Add(datos, 1);
        return fila;
    }

    private async Task AbrirInforme(Aviso aviso)
    {
        if (string.IsNullOrEmpty(Api.Mecanico)) await PedirNombre(primeraVez: true);
        await Navigation.PushAsync(new InformePage(aviso));
    }
}
