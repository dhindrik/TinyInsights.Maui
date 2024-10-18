
namespace TinyInsights.TestApp;

public partial class App : Application
{
    private readonly AppShell shell;
    private readonly IServiceProvider serviceProvider;

    public App(AppShell shell, IServiceProvider serviceProvider)
    {
        InitializeComponent();

        this.shell = shell;
        this.serviceProvider = serviceProvider;
    }

    protected override void OnResume()
    {
        base.OnResume();

        var insights = serviceProvider.GetRequiredService<IInsights>();
        insights.CreateNewSession();
    }


    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(shell);
    }
}