using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using UIKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;

namespace HackathoughtsApp
{
    public partial class ViewController : UIViewController
    {
        private string connUrl = "Data Source=MININT-BTM4NR3.esri.com;Initial Catalog=WheresFido;Integrated Security=True";

        private MapViewModel _mapViewModel = new MapViewModel();
        private MapView _mapView;

        private UIButton _bufferButton = new UIButton();
        private readonly UIToolbar _toolbar = new UIToolbar();

        private UIButton _lostPetButton = new UIButton(UIButtonType.Plain);
        private UIButton _foundPetButton = new UIButton(UIButtonType.Plain);
        private UIButton _sightingButton = new UIButton(UIButtonType.Plain);

        private readonly UISlider _mySlider = new UISlider();

        private MapPoint _lostLocation;
        private MapPoint _selectedLocation;
        private GraphicsOverlay _bufferOverlay;
        private GraphicsOverlay _barrierOverlay;
        private GraphicsOverlay _interestOverlay;
        private GraphicsOverlay _lostOverlay;

        private FeatureLayer _redlandsBoundary;
        private FeatureLayer _water;
        private FeatureLayer _parks;
        private FeatureLayer _buildings;

        private List<Polygon> _parkPolygons;

        private DateTime _lostTime;
        private DateTime _currentTime;
        private double _radius;

        private MapPoint _shelter;

        private double _dogSpeed = 0.5;

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
            _lostPetButton.SetTitle("Lost Pet", UIControlState.Normal);
            _lostPetButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            _lostPetButton.TouchUpInside += LostPet_ClickAsync;

            _foundPetButton.SetTitle("Sighting", UIControlState.Normal);
            _foundPetButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            _foundPetButton.TouchUpInside += Sighting_Click;

            _sightingButton.SetTitle("Found Pet", UIControlState.Normal);
            _sightingButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            _sightingButton.TouchUpInside += FoundPet_ClickAsync;

            _mySlider.SetValue((float)0.5, true);
            _mySlider.ValueChanged += MyHeightSlider_ValueChanged;

            View.AddSubviews(_mapView, _toolbar, _foundPetButton, _lostPetButton, _sightingButton, _mySlider);
        }

        private void MyHeightSlider_ValueChanged(object sender, EventArgs e)
        {
            // Scale the slider value; its default range is 0-10.
            _dogSpeed= _mySlider.Value;
            Console.WriteLine(_dogSpeed.ToString());
            SetRadius();
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

            _shelter = new MapPoint(-117.203551, 34.060069, SpatialReferences.Wgs84);
            var shelterPicture = new PictureMarkerSymbol(new Uri("http://static.arcgis.com/images/Symbols/SafetyHealth/Hospital.png"))
            { Height=40, Width=40
            };

            _interestOverlay.Graphics.Add(new Graphic(_shelter, shelterPicture));

            _mapView.GraphicsOverlays.Add(_bufferOverlay);
            _mapView.GraphicsOverlays.Add(_barrierOverlay);
            _mapView.GraphicsOverlays.Add(_interestOverlay);
            _mapView.GraphicsOverlays.Add(_lostOverlay);

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

            _parkPolygons = new List<Polygon>();
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

            
            //await _mapView.SetViewpointAsync(new Viewpoint(_parkPolygons[0].Extent));
        }

        private void SetRadius()
        {
            _radius = _currentTime.Subtract(_lostTime).TotalHours * 6.0 *_dogSpeed;
            Console.WriteLine(_radius);
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

            _lostPetButton.Frame = new CoreGraphics.CGRect(0, yPageOffset, View.Bounds.Width / 3, 40);
            _foundPetButton.Frame = new CoreGraphics.CGRect(View.Bounds.Width / 3, yPageOffset, View.Bounds.Width / 3, 40);
            _sightingButton.Frame = new CoreGraphics.CGRect((View.Bounds.Width / 3)*2, yPageOffset, View.Bounds.Width / 3, 40);
            _mySlider.Frame = new CoreGraphics.CGRect(0, yPageOffset+40, View.Bounds.Width, 40);
        }
        private void Sighting_Click(object sender, EventArgs e)
        {
            Console.WriteLine("YEET");
        }
        private async void LostPet_ClickAsync(object sender, EventArgs e)
        {
            if (_selectedLocation == null)
            {
                return;
            }

            _lostLocation = _selectedLocation;

            _lostOverlay.Graphics.Clear();
            //_lostOverlay.Graphics.Add(new Graphic(_lostLocation, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.X, Color.Blue, 30.0)));

            Geometry bufferGeometryGeodesic = GeometryEngine.BufferGeodetic(_lostLocation, _radius, LinearUnits.Miles, double.NaN, GeodeticCurveType.Geodesic);
            Graphic geodesicBufferGraphic = new Graphic(bufferGeometryGeodesic, new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.Black, 2.0));

            _bufferOverlay.Graphics.Add(geodesicBufferGraphic);

            await _mapView.SetViewpointAsync(new Viewpoint(geodesicBufferGraphic.Geometry.Extent));
            /*
            UITableViewController directionsTableController = new UITableViewController
            {
                TableView = { Source = new DirectionsTableSource(_directionsList) }
            };
            NavigationController.PushViewController(directionsTableController, true);
            */
        }

        private async void FoundPet_ClickAsync(object sender, EventArgs e)
        {
            await _mapView.SetViewpointAsync(new Viewpoint(_shelter.Extent));
        }

        private void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            _selectedLocation = e.Location;
            _bufferOverlay.Graphics.Clear();
            _bufferOverlay.Graphics.Add(new Graphic(_selectedLocation, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.X, Color.Red, 15.0)));
        }

        private void MapViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Update the map view with the view model's new map
            if (e.PropertyName == "Map" && _mapView != null)
                _mapView.Map = _mapViewModel.Map;
        }
    }

    class WheresFidoDB
    {
        private string connUrl = "Data Source=MININT-BTM4NR3.esri.com;Initial Catalog=WheresFido;Integrated Security=True";

        /**
         * ONLY CALLED WITHIN THIS CLASS
         * */
        private SqlConnection getConnection()
        {
            return new SqlConnection(connUrl);
        }

        /**
         * Adds new user to the User table after the register button is pressed
         * email: the email they entered; pass: the password the entered (not hashed)
         * lat and lon: get the user inputted home address and get the lat and long values before calling this
         * notifs: user should either turn notifications on or off when they register
         * */
        public Boolean addNewUser(string email, string pass, double lat, double lon, Boolean notifs)
        {
            try
            {
                // Get connection
                SqlConnection conn = getConnection();

                // Create query statement
                string query = "INSERT INTO User VALUES (@email, @pass, @lat, @long, @notifs);";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Insert user info
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@pass", pass);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@long", lon);
                    if (notifs)
                    {
                        cmd.Parameters.AddWithValue("@notifs", 1);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@notifs", 0);
                    }

                    // Open connection
                    conn.Open();

                    // Get results
                    int result = cmd.ExecuteNonQuery();
                    if (result > 0)
                    {
                        // Success!
                        return true;
                    }

                    // Didn't update correctly
                    return false;
                }
            }
            catch (Exception e)
            {
                // something happened
                return false;
            }
        }

        public Boolean createNewLostDogEvent(string ownerEmail, int dogID, string timeLastSeen, double lat, double lon)
        {
            try
            {
                // first get a new ID
                int id = getNewSearchID();
                if (id < 0)
                {
                    // Didn't get ID
                    return false;
                }

                // Get connection
                SqlConnection conn = getConnection();

                // Create query statement
                string query = "INSERT INTO Lost_Dog VALUES (@id, @email, @dogId, @active, @lastseen, @lat, @lon);";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Insert user info
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@email", ownerEmail);
                    cmd.Parameters.AddWithValue("@dogId", dogID);
                    cmd.Parameters.AddWithValue("@active", 1);  // Always 1 when new lost event is created
                    cmd.Parameters.AddWithValue("@lastseen", timeLastSeen);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lon", lon);

                    // Open connection
                    conn.Open();

                    // Get results
                    int result = cmd.ExecuteNonQuery();
                    if (result > 0)
                    {
                        // Success!
                        return true;
                    }

                    // Didn't update correctly
                    return false;
                }
            }
            catch (Exception e)
            {
                // something happened
                return false;
            }
        }

        private int getNewSearchID()
        {
            try
            {
                // Get connection
                SqlConnection conn = getConnection();

                // Create query statement
                string query = "SELECT MAX(SearchID) FROM Lost_Dog;";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Open connection
                    conn.Open();

                    // Get results
                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        return reader.GetInt32(0) + 1;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }
            catch (Exception e)
            {
                // something happened
                return -1;
            }
        }

        public Boolean addSightingEvent(int searchID, string userEmail, string timeSeen)
        {
            try
            {
                // Get connection
                SqlConnection conn = getConnection();

                // Create query statement
                string query = "INSERT INTO Sighting VALUES (@id, @email, @timeseen);";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Insert user info
                    cmd.Parameters.AddWithValue("@id", searchID);
                    cmd.Parameters.AddWithValue("@email", userEmail);
                    cmd.Parameters.AddWithValue("@timeseen", timeSeen);

                    // Open connection
                    conn.Open();

                    // Get results
                    int result = cmd.ExecuteNonQuery();
                    if (result > 0)
                    {
                        // Success!
                        return true;
                    }

                    // Didn't update correctly
                    return false;
                }
            }
            catch (Exception e)
            {
                // something happened
                return false;
            }
        }

        public Boolean foundLostDog(int searchID)
        {
            try
            {
                // Get connection
                SqlConnection conn = getConnection();

                // Create query statement
                string query = "UPDATE Lost_Dog SET IsActive = 0 WHERE SearchID = @id;";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Insert user info
                    cmd.Parameters.AddWithValue("@id", searchID);

                    // Open connection
                    conn.Open();

                    // Get results
                    int result = cmd.ExecuteNonQuery();
                    if (result > 0)
                    {
                        // Success!
                        return true;
                    }

                    // Didn't update correctly
                    return false;
                }
            }
            catch (Exception e)
            {
                // something happened
                return false;
            }
        }

        public Boolean addToWatching(string userEmail, int searchID)
        {
            try
            {
                // Get connection
                SqlConnection conn = getConnection();

                // Create query statement
                string query = "INSERT INTO Watching VALUES (@email, @id);";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Insert user info
                    cmd.Parameters.AddWithValue("@email", userEmail);
                    cmd.Parameters.AddWithValue("@id", searchID);

                    // Open connection
                    conn.Open();

                    // Get results
                    int result = cmd.ExecuteNonQuery();
                    if (result > 0)
                    {
                        // Success!
                        return true;
                    }

                    // Didn't update correctly
                    return false;
                }
            }
            catch (Exception e)
            {
                // something happened
                return false;
            }
        }

    }
}