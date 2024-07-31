namespace MauiArcGISMVVM;

public partial class MainPage : ContentPage
{
    public MainPage(MapViewModel mapViewModel)
	{
		InitializeComponent();

		this.BindingContext = mapViewModel;
	}
}

