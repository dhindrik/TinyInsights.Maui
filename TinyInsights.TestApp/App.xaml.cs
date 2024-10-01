
namespace TinyInsights.TestApp;

public partial class App : Application
{
    private readonly AppShell shell;

    public App(AppShell shell)
    {
        InitializeComponent();

        this.shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(shell);
    }
}