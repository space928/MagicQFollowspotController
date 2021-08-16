using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace MidiApp
{

    public partial class ThreeD : Window
    {
        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);


        public ThreeD()
        {
            InitializeComponent();
        }

        private MeshElement3D m_spotSphere;
        private MeshElement3D m_beam;
        private Material m_spotMaterial;
        private Material m_spotMaterialSelected;
        private bool m_mousedown = false;
        //private Visual3D m_beam2;
        CameraState[] CameraSaveStates = new CameraState[5];

        public void grab()
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
            var beam = new MeshGeometryVisual3D();

            beam.Material = beamMatrial;
            beam.Transform = new TranslateTransform3D(0, 0, 0);
            beam.BackMaterial = null;

            m_beam = beam;

            viewport3D.Children.Add(m_spotSphere);
            viewport3D.Children.Add(m_beam);

            UpdateModel();
            setCameraView(0);
            Show();
        }

        public void UpdateModel()
        {
            ((MainViewModel)(DataContext)).updateModel();

            if (MainWindow.AppResources.CameraPositions != null)
            {
                for (int i = 0; i < CameraSaveStates.Length; i++)
                {
                    var p = MainWindow.AppResources.CameraPositions[i];
                    if (p != null)
                    {
                        CameraSaveStates[i] = new CameraState();
                        CameraSaveStates[i].Location = (Point3D)(p.Location);
                        CameraSaveStates[i].Direction = (Vector3D)(p.Direction);
                        CameraSaveStates[i].UpDirection = (Vector3D)(p.UpDirection);
                        CameraSaveStates[i].FOV = (double)p.FOV;
                    }
                }
            }

        }

        private static void SetCursor(int x, int y)
        {

            SetCursorPos(x, y);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
//            SetCursor((int)Width / 2, (int)Height / 2);
            //   Console.WriteLine("Catured: " + CaptureMouse());

            //            vertLine.Height = Height;
            //            horizLine.Width = Width;

  //          Focus();
        }

        private double MoveSpot(int spot_number, Point p)
        {
            viewport3D.Children.Remove(m_spotSphere);
            viewport3D.Children.Remove(m_beam);
            ((MainViewModel)(DataContext)).hideLights();

            var pt = viewport3D.FindNearestPoint(p);
            if (pt != null)
            {
                Point3D point = (Point3D)pt;

                Transform3D at = new TranslateTransform3D(((Vector3D)point));

                m_spotSphere.Transform = at;

                var mb = new MeshBuilder(true);
                mb.AddCylinder(point, MainWindow.m_spots[spot_number].Location, .1, 8);

                double movement = (point - MainWindow.m_spots[spot_number].Target).Length;

                MainWindow.m_spots[spot_number].Target = point;

                ((MeshGeometryVisual3D)m_beam).MeshGeometry = mb.ToMesh();

                ((MainViewModel)(DataContext)).showLights();
                viewport3D.Children.Add(m_beam);
                viewport3D.Children.Add(m_spotSphere);
                return movement;
            }

            ((MainViewModel)(DataContext)).showLights();
            return 0.0;
        }

        public double DMX_moveSpot(int spot_number)
        {
            if (!m_mousedown && (spot_number<0))
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
            }

            if (cameraSlot>=0)
            {
                MainWindow win = ((MainWindow)App.Current.MainWindow);

                if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    // Save State
                    var camera = viewport3D.Camera as PerspectiveCamera;

                    if (camera != null)
                    {
                        var c = new CameraState();
                        c.Location = camera.Position;
                        c.Direction = camera.LookDirection;
                        c.FOV = camera.FieldOfView;
                        c.UpDirection = camera.UpDirection;
                        CameraSaveStates[cameraSlot] = c;

                        string cameras = Newtonsoft.Json.JsonConvert.SerializeObject(CameraSaveStates);
                        var ds = Newtonsoft.Json.JsonConvert.DeserializeObject(cameras);

                        MainWindow.AppResources.Remove("CameraPositions");

                        MainWindow.AppResources.Add("CameraPositions", (JToken)ds);
                        win.saveAppResource();
                    }

                }
                else
                {
                    setCameraView(cameraSlot);
                }

            }
        }

        public void setCameraView(int view)
        {
            // Load State
            var camera = viewport3D.Camera as PerspectiveCamera;

            if ((view < CameraSaveStates.Length) && (CameraSaveStates[view]!=null)) {

                camera.Position = CameraSaveStates[view].Location;
                camera.LookDirection = CameraSaveStates[view].Direction;
                camera.FieldOfView = CameraSaveStates[view].FOV;
                camera.UpDirection = CameraSaveStates[view].UpDirection;
            }
        }
        private void HelixViewport3D_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && (((MainWindow)App.Current.MainWindow).leadSpot() >= 0))
            {
                double moved = MoveSpot(((MainWindow)App.Current.MainWindow).leadSpot(), e.GetPosition(viewport3D));
                Point3D p = MainWindow.m_spots[((MainWindow)App.Current.MainWindow).leadSpot()].Target;

                for (int i = 0; i < MainWindow.m_spots.Count; i++)
                {
                    MainWindow.m_spots[i].Target = p;
                }

                ((MainWindow)App.Current.MainWindow).PointSpots();
            }
        }

        private void viewport3D_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                m_spotSphere.Material = m_spotMaterialSelected;
                m_mousedown = true;
                HelixViewport3D_MouseMove(sender, e);
            }
        }

        private void HelixViewport3D_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                m_spotSphere.Material = m_spotMaterial;
                m_mousedown = false;
            }
        }

        private void ThreeD1_Closed(object sender, EventArgs e)
        {
          if (App.Current != null && App.Current.MainWindow != null)
            ((MainWindow)App.Current.MainWindow).m_threeDWindow = null;
        }
    }

    class CameraState
    {
        public Point3D Location { get; set; }
        public Vector3D Direction { get; set; }
        public Vector3D UpDirection { get; set; }
        public double FOV { get; set; }

    }

}
