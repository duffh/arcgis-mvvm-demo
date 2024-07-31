namespace MauiArcGISMVVM
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("geoElementSelectionPage", typeof(GeoElementSelectionPage));
        }
    }
}
