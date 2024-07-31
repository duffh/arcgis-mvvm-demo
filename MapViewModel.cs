// Copyright 2024 Esri
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esri.ArcGISRuntime.ArcGISServices;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Mapping.Labeling;
using Esri.ArcGISRuntime.Reduction;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Toolkit.Maui;
using System.Collections.ObjectModel;
using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace MauiArcGISMVVM;

/// <summary>
/// Provides map data to an application
/// </summary>
public partial class MapViewModel : ObservableObject
{
    // Hold a reference to the feature layer.
    private FeatureLayer? _featureLayer;
    private readonly string _serviceUrl = "https://services.arcgis.com/V6ZHFr6zdgNZuVG0/arcgis/rest/services/Places_of_Worship_India/FeatureServer/0";

    // Initialize the batch size and geoelement display index.
    private readonly int _batchSize = 60;
    private int _currentGeoElementDisplayIndex = 0;

    public MapViewModel()
    {
        SetupMap();
    }

    private async void SetupMap()
    {
        try
        {
            // Create a feature layer using the feature service url.
            _featureLayer = new FeatureLayer(new Uri(_serviceUrl));
            await _featureLayer.LoadAsync();

            // Instantiate the map using the extent of the feature layer as an initial viewpoint.
            Map = new Map(BasemapStyle.ArcGISLightGray) { InitialViewpoint = new Viewpoint(_featureLayer.FullExtent!) };

            // Add a listener to the map loaded event to display the UI when the map is loaded.
            Map.Loaded += Map_Loaded;

            // Add the feature layer to the operational layers collection of the map.
            Map.OperationalLayers.Add(_featureLayer);

            ConfigureFeatureReduction();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void Map_Loaded(object? sender, EventArgs e)
    {
        IsMapLoaded = true;
    }

    private void ConfigureFeatureReduction()
    {
        if (_featureLayer != null)
        {
            // Clone the renderer used in the feature layer to create a clustering feature reduction.
            Renderer? layerRenderer = _featureLayer.Renderer?.Clone();

            ClusteringFeatureReduction featureReduction = new ClusteringFeatureReduction(layerRenderer!);

            // Create a simple label expression to display cluster labels.
            var simpleLabelExpression = new SimpleLabelExpression("[cluster_count]");
            var textSymbol = new TextSymbol() { Color = System.Drawing.Color.Black, Size = 11d, FontWeight = Esri.ArcGISRuntime.Symbology.FontWeight.Bold, };
            var labelDefinition = new LabelDefinition(simpleLabelExpression, textSymbol) { Placement = LabelingPlacement.PointCenterCenter };
            featureReduction.LabelDefinitions.Add(labelDefinition);

            // Set the feature reduction max scale.
            featureReduction.MaxScale = 1.1e4;

            // Set the feature reduction on the feature layer.
            _featureLayer.FeatureReduction = featureReduction;

            // Enable labels.
            _featureLayer.LabelsEnabled = true;
        }
    }

    public GeoViewController Controller { get; } = new GeoViewController();

    #region Commands
    [RelayCommand]
    public async Task GeoViewTapped(GeoViewInputEventArgs eventArgs) => await Identify(eventArgs.Position, eventArgs.Location);

    public async Task Identify(Point location, MapPoint? mapLocation)
    {
        try
        {
            // Clear any previously selected features, collections, callouts.
            _featureLayer!.ClearSelection();
            Controller.DismissCallout();
            _features.Clear();
            _currentGeoElementDisplayIndex = 0;
            GeoElementsToDisplay.Clear();

            // Identify the tapped observation.
            IdentifyLayerResult? results = await Controller.IdentifyLayerAsync(_featureLayer, location, 10, false);

            // Return if no geoelements are found.
            if (results is null || results.GeoElements.Count == 0) return;

            // If the tapped observation is an AggregateGeoElement then select it.
            if (results.GeoElements[0] is AggregateGeoElement aggregateGeoElement)
            {
                GettingTappedCluster = true;

                // Get the contained GeoElements.
                IReadOnlyList<GeoElement> geoElements = await aggregateGeoElement.GetGeoElementsAsync();

                // Cast the GeoElements to a list of ArcGISFeature objects.
                _features = geoElements.Select(geoElement => geoElement as ArcGISFeature).ToList()!;

                // Update the label for a tapped AggregateGeoElement.
                _geoElementSelectedLabel = $"The tapped cluster contains {_features.Count} geoelements.";

                // Load a subset of the contained features into the observable collection.
                if (_features.Count > _batchSize)
                {
                    var subset = new List<ArcGISFeature>(_features);
                    subset.RemoveRange(_batchSize - 1, subset.Count - _batchSize);

                    // Load all attributes of not loaded features in the subset.
                    await (_featureLayer!.FeatureTable as ServiceFeatureTable)!.LoadOrRefreshFeaturesAsync(subset);

                    // Update the collection with view models wrapping the features.
                    GeoElementsToDisplay = new ObservableCollection<PlaceOfWorshipViewModel>(subset.Select(feature => new PlaceOfWorshipViewModel(++_currentGeoElementDisplayIndex, feature)));
                }
                else
                {
                    // Load all attributes of not loaded features in the subset.
                    await (_featureLayer!.FeatureTable as ServiceFeatureTable)!.LoadOrRefreshFeaturesAsync(_features);

                    // Update the collection with view models wrapping the features.
                    GeoElementsToDisplay = new ObservableCollection<PlaceOfWorshipViewModel>(_features.Select(feature => new PlaceOfWorshipViewModel(++_currentGeoElementDisplayIndex, feature)));
                }

                // Open a modal page to display the tapped GeoElement.
                await Shell.Current.GoToAsync("geoElementSelectionPage");

                GettingTappedCluster = false;

            }
            else if (results.GeoElements[0] is ArcGISFeature feature)
            {
                // Update the label for a tapped ArcGISFeature.
                _geoElementSelectedLabel = "The tapped geoelement is an ArcGISFeature.";

                // Select and load the feature.
                _featureLayer.SelectFeature(feature);
                await feature.LoadAsync();

                // Create a view model with the tapped feature and update the collection.
                var geoElementViewModel = new PlaceOfWorshipViewModel(1, feature);
                GeoElementsToDisplay = new ObservableCollection<PlaceOfWorshipViewModel>(new List<PlaceOfWorshipViewModel> { geoElementViewModel });

                // Open a modal page to display the tapped GeoElement.
                await Shell.Current.GoToAsync("geoElementSelectionPage");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (!LoadingMoreItems)
        {
            LoadingMoreItems = true;

            // Get the number of remaining items that have not been added to the collection.
            int remainingItems = _features.Count - _currentGeoElementDisplayIndex;

            IEnumerable<ArcGISFeature> subset = new List<ArcGISFeature>();

            // Depending on the number of remaining items either take another subset from the list of features or take all the remaining features.
            if (remainingItems > _batchSize)
            {
                subset = _features.Skip(_currentGeoElementDisplayIndex).Take(_batchSize);
            }
            else
            {
                subset = _features.Skip(_currentGeoElementDisplayIndex);
            }

            // Load all attributes of not loaded features in the subset.
            await (_featureLayer!.FeatureTable as ServiceFeatureTable)!.LoadOrRefreshFeaturesAsync(subset);

            // Update the collection with view models wrapping the features.
            foreach (var item in subset)
            {
                GeoElementsToDisplay.Add(new PlaceOfWorshipViewModel(++_currentGeoElementDisplayIndex, item));
            }

            LoadingMoreItems = false;
        }
    }

    [RelayCommand]
    public async Task SelectedGeoElementChanged(PlaceOfWorshipViewModel selectedItem)
    {
        if (selectedItem == null) return;
        
        // Clear any currently selected features.
        _featureLayer?.ClearSelection();

        // Get the map point for the selected view model.
        MapPoint mapPoint = (MapPoint)selectedItem.ArcGISFeature.Geometry!;

        // Close the modal page.
        await Shell.Current.GoToAsync("..");

        // Select the feature associated with the selected view model.
        _featureLayer?.SelectFeature(selectedItem.ArcGISFeature);

        // Navigate to the selected feature and show a callout.
        // Scale is set to a value smaller than the layers feature reduction max scale so that clusters do not render when the navigation completes.
        await Controller.SetViewpointAsync(new Viewpoint(mapPoint, 1e4), TimeSpan.FromSeconds(1));

        // Use an arcade expression to fallback to a default value if the properties used in the callout are null.
        var calloutDefn = new Esri.ArcGISRuntime.UI.CalloutDefinition(selectedItem.ArcGISFeature, "DefaultValue($feature.name, 'null')", "DefaultValue($feature.religion, 'null')");
        Controller.ShowCalloutForGeoElement(selectedItem.ArcGISFeature, default, calloutDefn);
    }

    [RelayCommand]
    public async Task SelectedRegionChanged(int selectedIndex)
    {
        // Get the full extent of the feature layer.
        Viewpoint viewpoint = new Viewpoint(_featureLayer!.FullExtent!);

        // Get the spatial reference of the feature layer.
        SpatialReference spatialReference = SpatialReference.Create(3857);

        double scale = 10.5e6;

        switch (selectedIndex)
        {
            case 0:
                viewpoint = new Viewpoint(new MapPoint(8597888, 3622342, spatialReference), scale);
                break;
            case 1:
                viewpoint = new Viewpoint(new MapPoint(8617655, 1603703, spatialReference), scale);
                break;
            case 2:
                viewpoint = new Viewpoint(new MapPoint(9947837, 2859542, spatialReference), scale);
                break;
            case 3:
                viewpoint = new Viewpoint(new MapPoint(7966035, 2803998, spatialReference), scale);
                break;
            default:
                break;
        }

        // Set the viewpoint based on the desired selection.
        await Controller.SetViewpointAsync(viewpoint);

        // Reset the selected region for the next navigation.
        SelectedRegion = null;
    }

    [RelayCommand]
    public async Task CloseModalPage() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    public void LegendButtonPressed()
    {
        ShowLegend = !ShowLegend;

        LegendButtonText = ShowLegend ? "Hide legend" : "Show legend";
    }
    #endregion

    #region Properties

    [ObservableProperty]
    private object? _selectedRegion;

    [ObservableProperty]
    private bool _loadingMoreItems;

    [ObservableProperty]
    private bool _gettingTappedCluster;

    List<ArcGISFeature> _features = [];

    [ObservableProperty]
    private double _popupOpacity = 0;

    [ObservableProperty]
    private Map? _map;

    [ObservableProperty]
    bool _isMapLoaded;

    [ObservableProperty]
    public bool _enableClustering = true;

    partial void OnEnableClusteringChanged(bool oldValue, bool newValue)
    {
        if (_featureLayer?.FeatureReduction is null) return;
        _featureLayer.FeatureReduction.IsEnabled = newValue;
    }

    [ObservableProperty]
    ObservableCollection<PlaceOfWorshipViewModel> _geoElementsToDisplay = [];

    [ObservableProperty]
    string? _errorMessage;

    [ObservableProperty]
    bool _showErrorMessage;

    [ObservableProperty]
    bool _showLegend;

    [ObservableProperty]
    string _legendButtonText = "Show legend";

    partial void OnErrorMessageChanged(string? oldValue, string? newValue)
    {
        ShowErrorMessage = !string.IsNullOrEmpty(newValue);
    }

    private string _geoElementSelectedLabel = string.Empty;

    public string GeoElementSelectedLabel
    {
        get { return _geoElementSelectedLabel; }
    }
    #endregion
}
