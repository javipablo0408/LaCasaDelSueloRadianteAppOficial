namespace LaCasaDelSueloRadianteApp.Controls;


public partial class MiniImg : ContentView
{
    public static readonly BindableProperty UrlProperty =
        BindableProperty.Create(nameof(Url), typeof(string), typeof(MiniImg),
                                propertyChanged: OnUrlChanged);

    public static readonly BindableProperty AspectProperty =
        BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(MiniImg), Aspect.AspectFill,
                                propertyChanged: OnAspectChanged);

    static void OnUrlChanged(BindableObject bo, object oldValue, object newValue)
    {
        if (bo is MiniImg m && newValue is string url)
            m.Img.Source = url;
    }

    static void OnAspectChanged(BindableObject bo, object oldValue, object newValue)
    {
        if (bo is MiniImg m && newValue is Aspect aspect)
            m.Img.Aspect = aspect;
    }

    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    public event EventHandler<string>? Tapped;

    public MiniImg() => InitializeComponent();

    void HandleTap(object sender, TappedEventArgs e) => Tapped?.Invoke(this, Url);
}