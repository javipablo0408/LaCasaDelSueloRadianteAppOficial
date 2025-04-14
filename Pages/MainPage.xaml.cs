using Microsoft.Graph.Models;

namespace LaCasaDelSueloRadianteApp;

public partial class MainPage : ContentPage
{
    private GraphService graphService;
    private User user;
    int count = 0;

    public MainPage()
    {
        InitializeComponent();
    }

    private void CounterBtn_Clicked(object sender, EventArgs e)
    {
        count++;
        CounterBtn.Text = $"Clicked {count} times";
        SemanticScreenReader.Announce(CounterBtn.Text);
    }

    private async void GetUserInfoBtn_Clicked(object sender, EventArgs e)
    {
        if (graphService == null)
        {
            graphService = new GraphService();
        }
        user = await graphService.GetMyDetailsAsync();
        HelloLabel.Text = $"Hello, {user.DisplayName}!";

        DisplayNameLabel.Text = user.DisplayName;
        UserFirstNameLabel.Text = user.GivenName;
        UserLastNameLabel.Text = user.Surname;
        UserPrincipalNameLabel.Text = user.UserPrincipalName;
    }
}