using System;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using ThinkGeo.MapSuite.Core;
using ThinkGeo.MapSuite.DesktopEdition;


namespace  MovingVehicleWithChangingLabel
{
    public partial class TestForm : Form
    {
        private Timer timer;
        private StreamReader mGPSData = new StreamReader(@"..\..\data\GPSinfo.txt");
        private GeoImage carGeoImageUp = new GeoImage(@"..\..\data\car_up_blue.png");
        private GeoImage carGeoImageDown = new GeoImage(@"..\..\data\car_down_blue.png");
        private double previousLong;
        private double previousLat;

        public TestForm()
        {
            InitializeComponent();
            timer = new Timer();
        }

        private void TestForm_Load(object sender, EventArgs e)
        {
            //Sets timer properties
            timer.Interval = 750;
            timer.Tick += new EventHandler(timer_Tick);

            winformsMap1.MapUnit = GeographyUnit.DecimalDegree;
            winformsMap1.CurrentExtent = new RectangleShape(-97.7591,30.3126,-97.7317,30.2964);
            winformsMap1.BackgroundOverlay.BackgroundBrush = new GeoSolidBrush(GeoColor.FromArgb(255, 198, 255, 255));

            //Displays the World Map Kit as a background.
            ThinkGeo.MapSuite.DesktopEdition.WorldMapKitWmsDesktopOverlay worldMapKitDesktopOverlay = new ThinkGeo.MapSuite.DesktopEdition.WorldMapKitWmsDesktopOverlay();
            winformsMap1.Overlays.Add(worldMapKitDesktopOverlay);

            //Adds the inMemoryFeatureLayer for car icon and label.
            InMemoryFeatureLayer inMemoryFeatureLayer = new InMemoryFeatureLayer();
            //Adds column to InMemoryFeatureLayer.
            inMemoryFeatureLayer.Open();
            inMemoryFeatureLayer.Columns.Add(new FeatureSourceColumn("VehiclePosition"));
            inMemoryFeatureLayer.Close();
            //Sets PointStyle and TextStyle for the InMemoryFeatureLayer.
            inMemoryFeatureLayer.ZoomLevelSet.ZoomLevel01.DefaultPointStyle.PointType = PointType.Bitmap;
            inMemoryFeatureLayer.ZoomLevelSet.ZoomLevel01.DefaultTextStyle = TextStyles.CreateSimpleTextStyle("VehiclePosition", "Arial", 8, DrawingFontStyles.Bold,
                                                                                 GeoColor.StandardColors.Black, 20, 0);
            inMemoryFeatureLayer.ZoomLevelSet.ZoomLevel01.DefaultTextStyle.Mask = new AreaStyle(new GeoPen(GeoColor.StandardColors.DarkGray,1), 
                                                                                  new GeoSolidBrush(GeoColor.FromArgb(150,GeoColor.StandardColors.LightGoldenrodYellow)));
            inMemoryFeatureLayer.ZoomLevelSet.ZoomLevel01.ApplyUntilZoomLevel = ApplyUntilZoomLevel.Level20;
            //Adds the Feature for the car.
            inMemoryFeatureLayer.InternalFeatures.Add("Car", new Feature(new PointShape())); 

            
            LayerOverlay dynamicOverlay = new LayerOverlay();
            dynamicOverlay.Layers.Add("CarLayer", inMemoryFeatureLayer);
            winformsMap1.Overlays.Add("DynamicOverlay", dynamicOverlay);

            winformsMap1.Refresh();

            timer.Start();
        }


        void timer_Tick(object sender, EventArgs e)
        {
            //Gets the GPS info from the textfile.
            DataTable carData = GetCarData();

            float angleOffset;
            double angle;

            LayerOverlay dynamicOverlay = (LayerOverlay)winformsMap1.Overlays["DynamicOverlay"];
            InMemoryFeatureLayer inMemoryFeatureLayer = (InMemoryFeatureLayer)dynamicOverlay.Layers["CarLayer"];
            //InMemoryFeatureLayer labelInMemoryFeatureLayer = (InMemoryFeatureLayer)dynamicOverlay.Layers["CarLabel"];
            PointShape pointShape = inMemoryFeatureLayer.InternalFeatures[0].GetShape() as PointShape;

            // Get the Row of Data we are working with.
            DataRow carDataRow = carData.Rows[0];

            double Lat = Convert.ToDouble(carDataRow["LAT"]);
            double Long = Convert.ToDouble(carDataRow["LONG"]);

            //Gets the angle based on the current GPS position and the previous one to get the direction of the vehicle.
            angle = GetAngleFromTwoVertices(new Vertex(previousLong, previousLat), new Vertex(Long, Lat));

            //Gets the correct icon depending on the direction of the vehicle.
            if (previousLong < Long)
            {
                inMemoryFeatureLayer.ZoomLevelSet.ZoomLevel01.DefaultPointStyle.Image = carGeoImageDown;
                angleOffset = 180;
            }
            else
            {
                inMemoryFeatureLayer.ZoomLevelSet.ZoomLevel01.DefaultPointStyle.Image = carGeoImageUp;
                angleOffset = 360;
            }
            inMemoryFeatureLayer.ZoomLevelSet.ZoomLevel01.DefaultPointStyle.RotationAngle = angleOffset - (float)angle; 
            
            pointShape.X = Long;
            pointShape.Y = Lat;
            pointShape.Id = "Car";

            //Updates the column "VehiclePosition" of the feature to the current Longitude/Latitude.
            Feature feature = inMemoryFeatureLayer.InternalFeatures[0];
            feature.ColumnValues["VehiclePosition"] = DecimalDegreesHelper.GetDegreesMinutesSecondsStringFromDecimalDegreePoint(pointShape); 

            //Updates the PointShape of the Feature.
            inMemoryFeatureLayer.Open();
            inMemoryFeatureLayer.EditTools.BeginTransaction();
            inMemoryFeatureLayer.EditTools.Update(pointShape);
            inMemoryFeatureLayer.EditTools.CommitTransaction();
            inMemoryFeatureLayer.Close();

            previousLong = Long;
            previousLat = Lat;

            winformsMap1.Refresh(dynamicOverlay);
           
        }

        //We assume that the angle is based on a third point that is on top of b on the same x axis.
        private double GetAngleFromTwoVertices(Vertex b, Vertex c)
        {
            double alpha = 0;
            double tangentAlpha = (c.Y - b.Y) / (c.X - b.X);
            double Peta = Math.Atan(tangentAlpha);

            if (c.X > b.X)
            {
                alpha = 90 - (Peta * (180 / Math.PI));
            }
            else if (c.X < b.X)
            {
                alpha = 270 - (Peta * (180 / Math.PI));
            }
            else
            {
                if (c.Y > b.Y) alpha = 0;
                if (c.Y < b.Y) alpha = 180;
            }
            return alpha;
        }

        private DataTable GetCarData()
        {
            DataTable datatable = new DataTable();
            datatable.Columns.Add("LAT");
            datatable.Columns.Add("LONG");

            string strLattitude = "";
            string strLongitude = "";
           
            // Read the next line from the text file with GPS data in it.
            string strCurrentText = mGPSData.ReadLine();

            if (strCurrentText == "")
            {
                mGPSData.BaseStream.Seek(0, SeekOrigin.Begin);
                strCurrentText = mGPSData.ReadLine();
            }

            while (strCurrentText != null)
            {
                // Every other line is a "/" and we want to skip those.
                if (strCurrentText.Trim() != "/")
                {
                    string[] strSplit = strCurrentText.Split(','); // (':');
                    strLongitude = strSplit[0];
                    strLattitude = strSplit[1];
                    break;
                }
                strCurrentText = mGPSData.ReadLine();
            }

            object[] objs = new object[2] { strLattitude, strLongitude}; 

            datatable.Rows.Add(objs);

            return datatable;
        }

      
        private void winformsMap1_MouseMove(object sender, MouseEventArgs e)
        {
            //Displays the X and Y in screen coordinates.
            statusStrip1.Items["toolStripStatusLabelScreen"].Text = "X:" + e.X + " Y:" + e.Y;

            //Gets the PointShape in world coordinates from screen coordinates.
            PointShape pointShape = ExtentHelper.ToWorldCoordinate(winformsMap1.CurrentExtent, new ScreenPointF(e.X, e.Y), winformsMap1.Width, winformsMap1.Height);

            //Displays world coordinates.
            statusStrip1.Items["toolStripStatusLabelWorld"].Text = "(world) X:" + Math.Round(pointShape.X, 4) + " Y:" + Math.Round(pointShape.Y, 4);
        }
        
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    }
}
