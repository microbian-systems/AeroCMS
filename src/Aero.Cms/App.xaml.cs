namespace Aero.Cms;

/// <summary>
/// Defines the cross-platform MAUI application and creates its initial window.
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
    /// Creates the application window containing the Blazor hybrid main page.
    /// </summary>
    /// <param name="activationState">The platform activation state; the current implementation does not inspect it.</param>
    /// <returns>A window titled <c>Aero.Cms</c> whose root is a new <see cref="MainPage"/>.</returns>
protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "Aero.Cms" };
    }
}
