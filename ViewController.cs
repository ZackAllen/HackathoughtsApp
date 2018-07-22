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
using CoreGraphics;

namespace HackathoughtsApp
{
    public partial class ViewController : UIViewController
    {
        MapViewModel _mapViewModel = new MapViewModel();
        MapView _mapView;

        private UIButton _bufferButton = new UIButton();
        private readonly UIToolbar _toolbar = new UIToolbar();

        private UIButton _addFacilitiesButton = new UIButton(UIButtonType.Plain);
        private UIButton _addBarrierButton = new UIButton(UIButtonType.Plain);

        private MapPoint _lostLocation;
        private GraphicsOverlay _bufferOverlay;
        private GraphicsOverlay _barrierOverlay;
        private GraphicsOverlay _interestOverlay;
        private GraphicsOverlay _lostOverlay;

        private FeatureLayer _redlandsBoundary;
        private FeatureLayer _water;
        private FeatureLayer _parks;

        private List<Polygon> _parkPolygons;

        private DateTime _lostTime;
        private DateTime _currentTime;
        private double _radius;

        public ViewController(IntPtr handle) : base(handle)
        {
            // Listen for changes on the view model
            _mapViewModel.PropertyChanged += MapViewModel_PropertyChanged;
        }

        public override void ViewDidLoad()
        {
            Initialize();
            base.ViewDidLoad();

            // Add all of the buttons and link their click methods.
            _addFacilitiesButton.SetTitle("Facilities", UIControlState.Normal);
            _addFacilitiesButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            //_addFacilitiesButton.TouchUpInside += PlaceFacilites_Click;

            _addBarrierButton.SetTitle("Barrier", UIControlState.Normal);
            _addBarrierButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            //_addBarrierButton.TouchUpInside += DrawBarrier_Click;

            View.AddSubviews(_mapView, _toolbar, _addBarrierButton, _addFacilitiesButton );
            
            
        }

        private void Initialize()
        {
            _lostTime = DateTime.Now;
            _currentTime = DateTime.Now.AddHours(0.5);

            SetRadius();

            Console.WriteLine(_radius);

            Console.WriteLine(DateTime.Now.ToString());
            

            // Create a new map view, set its map, and provide the coordinates for laying it out
            _mapView = new MapView()
            {
                Map = _mapViewModel.Map // Use the map from the view-model
            };
            // Create a new ArcGISVectorTiledLayer with the URI Selected by the user
            ArcGISVectorTiledLayer vectorTiledLayer = new ArcGISVectorTiledLayer(new Uri("https://ess.maps.arcgis.com/home/item.html?id=08155fea456d47279946f95134609d05"));

            // Create new Map with basemap 
            _mapView.Map = new Map(new Basemap(vectorTiledLayer));

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
            _redlandsBoundary = new FeatureLayer(serviceUri);


            // Create URI to the used feature service.
            Uri waterUri = new Uri("https://services.arcgis.com/Wl7Y1m92PbjtJs5n/arcgis/rest/services/Hackathoughts/FeatureServer/1");

            _water = new FeatureLayer(waterUri);
            _water.Renderer = new SimpleRenderer(new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, Color.Blue, new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.Black, 2.0)));
            //new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, Color.Blue, new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.Black, 5.0) );

            Uri parkUri = new Uri("https://services.arcgis.com/FLM8UAw9y5MmuVTV/ArcGIS/rest/services/Redlands_Park_Boundaries/FeatureServer/0");

            _parks = new FeatureLayer(parkUri);

            // Add layers to the map.
            _mapView.Map.OperationalLayers.Add(_redlandsBoundary);
            _mapView.Map.OperationalLayers.Add(_water);
            _mapView.Map.OperationalLayers.Add(_parks);


            _water.LoadAsync();
            CenterView();
            
        }

        private async void CenterView()
        {
            await _redlandsBoundary.LoadAsync();
            await _mapView.SetViewpointAsync(new Viewpoint(_redlandsBoundary.FullExtent.Extent));
            // await _water.LoadAsync();

            await _parks.LoadAsync();
            // Holds locations of hospitals around San Diego.

            _parkPolygons =  new List<Polygon>();
            // Create query parameters to select all features.
            QueryParameters queryParams = new QueryParameters()
            {
                WhereClause = "1=1"
            };

            FeatureQueryResult redlandsResult = await _redlandsBoundary.FeatureTable.QueryFeaturesAsync(queryParams);
            List<Polygon> redlandsBound = redlandsResult.ToList().Select(feature => (Polygon)feature.Geometry).ToList();
            //GeometryEngine.Union()
            await _mapView.SetViewpointAsync(new Viewpoint(GeometryEngine.Union(redlandsBound).Extent));

            // Query all features in the facility table.
            FeatureQueryResult facilityResult = await _parks.FeatureTable.QueryFeaturesAsync(queryParams);

            // Add all of the query results to facilities as new Facility objects.
            _parkPolygons.AddRange(facilityResult.ToList().Select(feature => (Polygon)feature.Geometry));

            Console.WriteLine(_parkPolygons.Count);
            //await _mapView.SetViewpointAsync(new Viewpoint(_parkPolygons[0].Extent));

        }

        private void SetRadius()
        {
            _radius = _currentTime.Subtract(_lostTime).TotalHours * 3.0;
            if (_radius > 20.0)
            {
                _radius = 20.0;
            }
        }

        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();

            int yPageOffset = 60;

            // Setup the visual frame for the MapView
            _mapView.Frame = new CoreGraphics.CGRect(0, 0, View.Bounds.Width, View.Bounds.Height);

            // Setup the visual frame for the Toolbar
            _toolbar.Frame = new CoreGraphics.CGRect(0, yPageOffset, View.Bounds.Width, 80);

            _addFacilitiesButton.Frame = new CoreGraphics.CGRect(0, yPageOffset, View.Bounds.Width / 2, 40);
            _addBarrierButton.Frame = new CoreGraphics.CGRect(View.Bounds.Width / 2, yPageOffset, View.Bounds.Width / 2, 40);

        }

        private async void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            // Get the tapped point.
            _lostLocation = e.Location;

            _lostOverlay.Graphics.Clear();
            //_lostOverlay.Graphics.Add(new Graphic(_lostLocation, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.X, Color.Blue, 30.0)));

            Geometry bufferGeometryGeodesic = GeometryEngine.BufferGeodetic(_lostLocation, _radius, LinearUnits.Miles, double.NaN, GeodeticCurveType.Geodesic);
            Graphic geodesicBufferGraphic = new Graphic(bufferGeometryGeodesic, new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.Black, 2.0));

            _bufferOverlay.Graphics.Add(geodesicBufferGraphic);

        }

        private void MapViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Update the map view with the view model's new map
            if (e.PropertyName == "Map" && _mapView != null)
                _mapView.Map = _mapViewModel.Map;
        }
    }
}