namespace Aero.Cms;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)a
    {
        return new Window(new MainPage()) { Title = "Aero.Cms" };
    }
}
