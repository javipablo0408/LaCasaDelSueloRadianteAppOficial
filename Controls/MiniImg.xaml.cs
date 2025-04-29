namespace LaCasaDelSueloRadianteApp.Controls;

public partial class MiniImg : ContentView
{
    public static readonly BindableProperty UrlProperty =
        BindableProperty.Create(nameof(Url), typeof(string), typeof(MiniImg),
                                propertyChanged: OnUrlChanged);

    static void OnUrlChanged(BindableObject bo, object oldValue, object newValue)
    {
        if (bo is MiniImg m && newValue is string url)
            m.Img.Source = url;
    }

    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public event EventHandler<string>? Tapped;

    public MiniImg() => InitializeComponent();

    void HandleTap(object sender, TappedEventArgs e) => Tapped?.Invoke(this, Url);
}