namespace GPE.Movil;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new NavigationPage(new AvisosPage())
		{
			BarBackgroundColor = Colors.White,
			BarTextColor = Estilo.Acento
		});
	}
}
