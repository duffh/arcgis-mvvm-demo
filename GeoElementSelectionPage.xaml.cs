namespace MauiArcGISMVVM;

public partial class GeoElementSelectionPage : ContentPage
{
	public GeoElementSelectionPage(MapViewModel mapViewModel)
	{
		InitializeComponent();

		this.BindingContext = mapViewModel;
	}
}