using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.UI.Controls;
using UIKit;
using System.Drawing;

namespace HackathoughtsApp
{
    public partial class ViewController : UIViewController
    {
        MapViewModel _mapViewModel = new MapViewModel();
        MapView _mapView;
        private MapPoint _lostLocation;
        private GraphicsOverlay _bufferOverlay;
        private GraphicsOverlay _barrierOverlay;
        private GraphicsOverlay _interestOverlay;
        private GraphicsOverlay _lostOverlay;

        public ViewController(IntPtr handle) : base(handle)
        {
            // Listen for changes on the view model
            _mapViewModel.PropertyChanged += MapViewModel_PropertyChanged;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Create a new map view, set its map, and provide the coordinates for laying it out
            _mapView = new MapView()
            {
                Map = _mapViewModel.Map // Use the map from the view-model
            };

            _mapView.GeoViewTapped += MyMapView_GeoViewTapped;

            //Create graphics overlays and add them
            _bufferOverlay = new GraphicsOverlay();
            _barrierOverlay = new GraphicsOverlay();
            _interestOverlay = new GraphicsOverlay();
            _lostOverlay = new GraphicsOverlay();

            _mapView.GraphicsOverlays.Add(_bufferOverlay);
            _mapView.GraphicsOverlays.Add(_barrierOverlay);
            _mapView.GraphicsOverlays.Add(_interestOverlay);
            _mapView.GraphicsOverlays.Add(_lostOverlay);

            // location where user marks dog lost
            //_lostLocation = new MapPoint();

            //get redlands boundary

            // Create URI to the used feature service.
            Uri serviceUri = new Uri("https://services.arcgis.com/FLM8UAw9y5MmuVTV/ArcGIS/rest/services/CityLimits_Redlands/FeatureServer/0");

            // Create new FeatureLayer by URL.
            FeatureLayer redlandsBoundary = new FeatureLayer(serviceUri);

            // Add layer to the map.
            _mapView.Map.OperationalLayers.Add(redlandsBoundary);

            

            // Add the MapView to the Subview
            View.AddSubview(_mapView);
        }

        public override void ViewDidLayoutSubviews()
        {
            // Fill the screen with the map
            _mapView.Frame = new CoreGraphics.CGRect(0, 0, View.Bounds.Width, View.Bounds.Height);

            base.ViewDidLayoutSubviews();
        }

        private async void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            // Get the tapped point.
            _lostLocation = e.Location;

            _lostOverlay.Graphics.Clear();
            _lostOverlay.Graphics.Add(new Graphic(_lostLocation, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.X, Color.Blue, 30.0)));


        }

        private void MapViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Update the map view with the view model's new map
            if (e.PropertyName == "Map" && _mapView != null)
                _mapView.Map = _mapViewModel.Map;
        }
    }
}