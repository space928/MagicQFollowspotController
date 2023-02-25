using HelixToolkit.Wpf;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MidiApp
{

    public partial class ThreeD : Window
    {
        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        public ThreeD()
        {
            InitializeComponent();
            //viewport3D.PanGesture = new MouseGesture(MouseAction.LeftClick);

            //viewport3D.PanGesture2 = null;

        }

        private MeshElement3D m_spotSphere;
        private MeshElement3D m_beam;
        private Material m_spotMaterial;
        private Material m_spotMaterialSelected;
        private bool m_mousedown = false;
        CameraPosition[] CameraSaveStates = new CameraPosition[5];

        public void SetActive(bool active)
        {
            if (active)
            {
                ActiveMarker.Stroke = Brushes.Red;
                ActiveMarker.StrokeThickness = 4;
            }
            else
            {
                ActiveMarker.Stroke = null;
            }

        }

        public void Grab()
        {
            //Left = 0;
            //Top = 0;
            //Height = SystemParameters.PrimaryScreenHeight;
            //Width =  SystemParameters.PrimaryScreenWidth;


            EllipsoidVisual3D e3d = new EllipsoidVisual3D();
            m_spotMaterial = MaterialHelper.CreateMaterial(new SolidColorBrush(Colors.Gray), 0, 0, false);
            MaterialHelper.ChangeOpacity(m_spotMaterial, 0.5);

            m_spotMaterialSelected = MaterialHelper.CreateMaterial(new SolidColorBrush(Colors.PaleVioletRed), 0, 0, false);
            MaterialHelper.ChangeOpacity(m_spotMaterialSelected, 0.5);

            e3d.Material = m_spotMaterial;
            e3d.BackMaterial = null;
            e3d.RadiusX = e3d.RadiusY = e3d.RadiusZ = 1.0;

            m_spotSphere = e3d;
            var beamMatrial = MaterialHelper.CreateMaterial(Colors.White, 0.5);
            var beam = new MeshGeometryVisual3D
            {
                Material = beamMatrial,
                Transform = new TranslateTransform3D(0, 0, 0),
                BackMaterial = null
            };

            m_beam = beam;

            viewport3D.Children.Add(m_spotSphere);
            viewport3D.Children.Add(m_beam);

            UpdateModel();
            SetCameraView(0);
            Show();
        }

        public void UpdateModel()
        {
            ((MainViewModel)(DataContext)).UpdateModel();

            if(MainWindow.appResources.cameraPositions != null)
                Array.Copy(MainWindow.appResources.cameraPositions, CameraSaveStates,
                    Math.Min(CameraSaveStates.Length, MainWindow.appResources.cameraPositions.Length));
        }

        private static void SetCursor(int x, int y)
        {
            SetCursorPos(x, y);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            //            SetCursor((int)Width / 2, (int)Height / 2);
            //   Logger.Log("Catured: " + CaptureMouse());

            //            vertLine.Height = Height;
            //            horizLine.Width = Width;

            //          Focus();
        }

        private double MoveSpot(int spot_number, Point p)
        {
            viewport3D.Children.Remove(m_spotSphere);
            viewport3D.Children.Remove(m_beam);
            var pt = new Point3D();

            //            ((MainViewModel)(DataContext)).hideLights();

            var hitList = Viewport3DHelper.FindHits(viewport3D.Viewport, p);
            foreach (var hit in hitList)
            {
                if (hit.Visual != null)
                {
                    var b = ((MainViewModel)(DataContext)).m_Bars;

                    Geometry3D geo = hit.Mesh;

                    GeometryModel3D mod = (GeometryModel3D)hit.Model;

                    var tt = mod.Bounds.Equals(b.Bounds);

                    bool boxHit = false;
                    foreach (var box in ((MainViewModel)(DataContext)).m_BoxesModelGroup.Children)
                        boxHit |= mod.Bounds.Equals(box.Bounds);

                    if (boxHit ||
                        mod.Bounds.Equals(((MainViewModel)(DataContext)).m_Theatre.Bounds))
                    {
                        pt = hit.Position;
                        break;
                    }
                }
            }


            //pt ?= viewport3D.FindNearestPoint(p);
            if (pt != null)
            {
                Point3D point = pt;

                Transform3D at = new TranslateTransform3D(((Vector3D)point));

                m_spotSphere.Transform = at;

                var mb = new MeshBuilder(true);
                mb.AddCylinder(point, MainWindow.m_spots[spot_number].Location, .1, 8);

                double movement = (point - MainWindow.m_spots[spot_number].Target).Length;

                MainWindow.m_spots[spot_number].Target = point;

                ((MeshGeometryVisual3D)m_beam).MeshGeometry = mb.ToMesh();

                // ((MainViewModel)(DataContext)).showLights();
                viewport3D.Children.Add(m_beam);
                viewport3D.Children.Add(m_spotSphere);
                return movement;
            }

            ((MainViewModel)(DataContext)).ShowLights();
            return 0.0;
        }

        public double Macro_moveSpot(int spot_number)
        {
            var p = viewport3D.Camera.Position;
            var v = viewport3D.Camera.LookDirection;

            viewport3D.Camera.Position = MainWindow.m_spots[spot_number].Location;

            var nv = Spherical.FromSpherical(1, MainWindow.m_spots[spot_number].Tilt, MainWindow.m_spots[spot_number].Pan);
            viewport3D.Camera.LookDirection = (Vector3D)nv;

            double moved = MoveSpot(spot_number, new Point(viewport3D.ActualWidth / 2, viewport3D.ActualHeight / 2));

            viewport3D.Camera.Position = p;
            viewport3D.Camera.LookDirection = v;
            return moved;
        }

        public double DMX_moveSpot(int spot_number)
        {
            if (!m_mousedown && (spot_number < 0))
            {
                var p = viewport3D.Camera.Position;
                var v = viewport3D.Camera.LookDirection;

                viewport3D.Camera.Position = MainWindow.m_spots[0].Location;

                var nv = Spherical.FromSpherical(1, MainWindow.m_spots[0].Tilt, MainWindow.m_spots[0].Pan);
                viewport3D.Camera.LookDirection = (Vector3D)nv;

                double moved = MoveSpot(0, new Point(viewport3D.ActualWidth / 2, viewport3D.ActualHeight / 2));

                viewport3D.Camera.Position = p;
                viewport3D.Camera.LookDirection = v;
                return moved;
            }

            return 0;
        }

        private void ThreeD1_KeyDown(object sender, KeyEventArgs e)
        {
            int cameraSlot = -1;

            if (e.Key == Key.Escape)
            {
                Hide();
            }

            switch (e.Key)
            {
                case Key.D1:
                case Key.NumPad1:
                    cameraSlot = 0;
                    break;

                case Key.D2:
                case Key.NumPad2:
                    cameraSlot = 1;
                    break;

                case Key.D3:
                case Key.NumPad3:
                    cameraSlot = 2;
                    break;

                case Key.D4:
                case Key.NumPad4:
                    cameraSlot = 3;
                    break;

                case Key.D5:
                case Key.NumPad5:
                    cameraSlot = 4;
                    break;

                case Key.M:
                    PlaceMarker();
                    break;

                case Key.S:
                    ((MainWindow)App.Current.MainWindow).SaveMarkers();
                    break;

                case Key.L:
                    ((MainWindow)App.Current.MainWindow).LoadMarkers();
                    ((MainViewModel)(DataContext)).MakeMarker();
                    break;
            }

            if (cameraSlot >= 0)
            {
                MainWindow win = ((MainWindow)App.Current.MainWindow);

                if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    // Save State
                    var camera = viewport3D.Camera as PerspectiveCamera;

                    if (camera != null)
                    {
                        CameraPosition c = new()
                        {
                            location = camera.Position,
                            direction = (Point3D)camera.LookDirection,
                            fov = camera.FieldOfView,
                            upDirection = (Point3D)camera.UpDirection
                        };
                        CameraSaveStates[cameraSlot] = c;

                        // Update the camera data in the resource dictionary
                        MainWindow.appResources.cameraPositions = new CameraPosition[CameraSaveStates.Length];
                        Array.Copy(CameraSaveStates, MainWindow.appResources.cameraPositions, CameraSaveStates.Length);
                    }
                }
                else
                {
                    SetCameraView(cameraSlot);
                }

            }
        }

        public void PlaceMarker()
        {
            Point3D target = MainWindow.m_spots[0].Target;

            foreach (Marker existing in MainWindow.m_markers)
            {
                if (existing.position.DistanceTo(target) < 1.0)
                {
                    MainWindow.m_markers.Remove(existing);
                    ((MainViewModel)(DataContext)).MakeMarker();
                    return;
                }
            }

            Marker m = new();
            m.clientID = MainWindow.clientID;
            m.markerID = MainWindow.m_markers.Count;
            m.position = target;

            MainWindow.m_markers.Add(m);

            ((MainViewModel)(DataContext)).MakeMarker();
        }

        public void SetCameraView(int view)
        {
            // Load State
            var camera = viewport3D.Camera as PerspectiveCamera;

            if (view < CameraSaveStates.Length)
            {

                camera.Position = CameraSaveStates[view].location;
                camera.LookDirection = (Vector3D)CameraSaveStates[view].direction;
                camera.FieldOfView = CameraSaveStates[view].fov;
                camera.UpDirection = (Vector3D)CameraSaveStates[view].upDirection;
            }
        }

        private void HelixViewport3D_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && MainWindow.LeadSpot >= 0)
            {
                double moved = MoveSpot(MainWindow.LeadSpot, e.GetPosition(viewport3D));
                Point3D p = MainWindow.m_spots[MainWindow.LeadSpot].Target;

                for (int i = 0; i < MainWindow.m_spots.Count; i++)
                {
                    MainWindow.m_spots[i].Target = p;
                }

                ((MainWindow)App.Current.MainWindow).PointSpots();
            }
        }

        private void Viewport3D_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                m_spotSphere.Material = m_spotMaterialSelected;
                m_mousedown = true;
                viewport3D.IsZoomEnabled = false;
                HelixViewport3D_MouseMove(sender, e);
            }
        }

        private void HelixViewport3D_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                m_spotSphere.Material = m_spotMaterial;
                viewport3D.IsZoomEnabled = true;
                m_mousedown = false;
            }
        }

        private void ThreeD1_Closed(object sender, EventArgs e)
        {
            if (App.Current != null && App.Current.MainWindow != null)
                ((MainWindow)App.Current.MainWindow).m_threeDWindow = null;
        }

        private void HelixViewport3D_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = false;
            for (int i = 0; i < MainWindow.m_spots.Count; i++)
            {
                if (e.Delta > 0)
                {
                    if (MainWindow.m_spots[i].Zoom < 255)
                        MainWindow.m_spots[i].Zoom += 1;
                }
                else if (e.Delta < 0)
                {
                    if (MainWindow.m_spots[i].Zoom > 0)
                        MainWindow.m_spots[i].Zoom -= 1;
                }

            }
             ((MainWindow)App.Current.MainWindow).PointSpots();
        }
    }
}
