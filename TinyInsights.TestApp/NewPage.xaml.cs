using System.Diagnostics;

namespace TinyInsights.TestApp;

public partial class NewPage : ContentPage
{
    public NewPage()
    {
        InitializeComponent();

        Appearing += (_, _) =>
        {
            Debug.WriteLine("NewPage Appearing event");
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Debug.WriteLine("NewPage OnAppearing override");
    }

    private async void Button_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NewPage1());
    }
}