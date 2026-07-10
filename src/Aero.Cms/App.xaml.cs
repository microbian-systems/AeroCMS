namespace Aero.Cms;

/// <summary>
/// Represents a class for App.
/// </summary>
public partial class App : Application
{
        /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
public App()
    {
        InitializeComponent();
    }

        /// <summary>
    /// CreateWindow method.
    /// </summary>
protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "Aero.Cms" };
    }
}
