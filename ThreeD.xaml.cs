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
using System.Windows.Interop;
using RawInputSharp;

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

        private List<MeshElement3D> m_spotSphere = new List<MeshElement3D>();
        private List<MeshElement3D> m_beam = new List<MeshElement3D>();

        private Material m_spotMaterial;
        private Material m_spotMaterialSelectedR;
        private Material m_spotMaterialSelectedG;


        private bool m_mousedown = false;
        CameraState[] CameraSaveStates = new CameraState[5];
        IntPtr myhwnd;

        public void setActive(bool active)
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

        public void grab()
        {
            //Left = 0;
            //Top = 0;
            //Height = SystemParameters.PrimaryScreenHeight;
            //Width =  SystemParameters.PrimaryScreenWidth;


            m_spotMaterial = MaterialHelper.CreateMaterial(new SolidColorBrush(Colors.Gray), 0, 0, false);
            MaterialHelper.ChangeOpacity(m_spotMaterial, 0.5);

            m_spotMaterialSelectedR = MaterialHelper.CreateMaterial(new SolidColorBrush(Colors.PaleVioletRed), 0, 0, false);
            m_spotMaterialSelectedG = MaterialHelper.CreateMaterial(new SolidColorBrush(Colors.DarkGreen), 0, 0, false);
            MaterialHelper.ChangeOpacity(m_spotMaterialSelectedR, 0.5);
            MaterialHelper.ChangeOpacity(m_spotMaterialSelectedG, 0.5);
            Material beamMatrial = MaterialHelper.CreateMaterial(Colors.White, 0.5);


            for (int i = 0; i < MainWindow.m_spots.Count; i++)
            {
                EllipsoidVisual3D e3d = new EllipsoidVisual3D();
                e3d.Material = m_spotMaterial;
                e3d.BackMaterial = null;
                e3d.RadiusX = e3d.RadiusY = e3d.RadiusZ = 1.0;
                m_spotSphere.Add(e3d);

                MeshGeometryVisual3D beam = new MeshGeometryVisual3D();
                beam.Material = beamMatrial;
                beam.Transform = new TranslateTransform3D(0, 0, 0);
                beam.BackMaterial = null;

                m_beam.Add(beam);

                //viewport3D.Children.Add(e3d);
                //viewport3D.Children.Add(beam);
            }

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

        private static RawMouseInput rmInput = null;
        private static HwndSourceHook myHook;

        private void Window_Activated(object sender, EventArgs e)
        {
            //            SetCursor((int)Width / 2, (int)Height / 2);
            //   Console.WriteLine("Catured: " + CaptureMouse());

            //            vertLine.Height = Height;
            //            horizLine.Width = Width;

            //          Focus();

            myhwnd = new WindowInteropHelper(this).Handle;

            if (rmInput == null)
            {
                rmInput = new RawMouseInput();
                myHook = new HwndSourceHook(WndProc);
                rmInput.RegisterForWM_INPUT(myhwnd);
            }

            HwndSource source = HwndSource.FromHwnd(myhwnd);
            source.AddHook(myHook);
        }
        private void ThreeD1_Deactivated(object sender, EventArgs e)
        {
 //           HwndSource source = HwndSource.FromHwnd(myhwnd);
//            source.RemoveHook(myHook);
        }


        private const int WM_INPUT = 0x00FF;
        private long last_event_millis = 0;
        private long updatespeed = 5;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_INPUT)
            {
                if (rmInput.UpdateRawMouse(lParam))
                {
                    long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    if (milliseconds > last_event_millis + updatespeed)
                    {
                        handleMouseMove();
                        long new_milis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        long delta = new_milis - milliseconds;
                        //Console.WriteLine($"Processing time {delta}");
                        last_event_millis = new_milis + updatespeed;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private void MoveSpot(int spot_number, Point p)
        {
            //Point3D point3D;
            Point3D? pt = null;


            var hitList = viewport3D.Viewport.FindHits(p);
            int i = 0;
            foreach (var hit in hitList)
            {
                i++;
                if (hit.Visual != null)
                {
                    //                    Console.WriteLine($"Hit name {i}: {hit.Model.GetName()}");
                    if (hit.Model.GetName() != null)
                    {
                        pt = hit.Position;
                        break;
                    }
                }
            }

            if (pt != null)
            {
                Point3D point = (Point3D)pt;

                //Transform3D at = new TranslateTransform3D(((Vector3D)point));

                //m_spotSphere[spot_number].Transform = at;

                //var mb = new MeshBuilder(true);
                // mb.AddCylinder(point, MainWindow.m_spots[spot_number].Location, .1, 8);

                MainWindow.m_spots[spot_number].Target = point;

                //((MeshGeometryVisual3D)m_beam[spot_number]).MeshGeometry = mb.ToMesh();


            }
        }

        public void Macro_moveSpot(int spot_number)
        {
            var p = viewport3D.Camera.Position;
            var v = viewport3D.Camera.LookDirection;

            viewport3D.Camera.Position = MainWindow.m_spots[spot_number].Location;

            var nv = Spherical.FromSpherical(1, MainWindow.m_spots[spot_number].Tilt, MainWindow.m_spots[spot_number].Pan);
            viewport3D.Camera.LookDirection = (Vector3D)nv;

            MoveSpot(spot_number, new Point(viewport3D.ActualWidth / 2, viewport3D.ActualHeight / 2));

            viewport3D.Camera.Position = p;
            viewport3D.Camera.LookDirection = v;
        }

        public void DMX_moveSpot()
        {
            MainWindow win = ((MainWindow)App.Current.MainWindow);

            if (!m_mousedown && (!win.spotsOnMouseControl()))
            {
                var p = viewport3D.Camera.Position;
                var v = viewport3D.Camera.LookDirection;

                for (int s = 0; s < MainWindow.m_spots.Count; s++)
                {
                    viewport3D.Camera.Position = MainWindow.m_spots[s].Location;

                    var nv = Spherical.FromSpherical(1, MainWindow.m_spots[s].Tilt, MainWindow.m_spots[s].Pan);
                    viewport3D.Camera.LookDirection = (Vector3D)nv;

                    MoveSpot(s, new Point(viewport3D.ActualWidth / 2, viewport3D.ActualHeight / 2));
                }

                viewport3D.Camera.Position = p;
                viewport3D.Camera.LookDirection = v;
            }
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

            if (cameraSlot >= 0)
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

            if ((view < CameraSaveStates.Length) && (CameraSaveStates[view] != null))
            {

                camera.Position = CameraSaveStates[view].Location;
                camera.LookDirection = CameraSaveStates[view].Direction;
                camera.FieldOfView = CameraSaveStates[view].FOV;
                camera.UpDirection = CameraSaveStates[view].UpDirection;
            }
        }
        private void HelixViewport3D_MouseMove(object sender, MouseEventArgs e)
        {
            //handleMouseMove(e.LeftButton, e.RightButton, e.MiddleButton, e.GetPosition(viewport3D));
            //handleMouseMove();
        }

        private void handleMouseMove()
        {

            double mouseScale = 0.5;


            if (this.Visibility != Visibility.Visible)
            {
                return;
            }

            Matrix transformToDevice = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;

            Point position = new Point();
            Point screenCoordinates = PointToScreen(new Point(0, 0));

            m_mousedown = ((RawMouse)rmInput.Mice[0]).Buttons[0] || ((RawMouse)rmInput.Mice[1]).Buttons[0];

            if (((RawMouse)rmInput.Mice[0]).X < 0)
            {
                ((RawMouse)rmInput.Mice[0]).X = 0;
            }
            if (((RawMouse)rmInput.Mice[0]).Y < 0)
            {
                ((RawMouse)rmInput.Mice[0]).Y = 0;
            }

            if (((RawMouse)rmInput.Mice[1]).X < 0)
            {
                ((RawMouse)rmInput.Mice[1]).X = 0;
            }
            if (((RawMouse)rmInput.Mice[1]).Y < 0)
            {
                ((RawMouse)rmInput.Mice[1]).Y = 0;
            }

            if (((RawMouse)rmInput.Mice[0]).X * mouseScale > Width)
            {
                ((RawMouse)rmInput.Mice[0]).X = (int)(Width / mouseScale);
            }
            if (((RawMouse)rmInput.Mice[0]).Y * mouseScale > Height)
            {
                ((RawMouse)rmInput.Mice[0]).Y = (int)(Height / mouseScale);
            }

            if (((RawMouse)rmInput.Mice[1]).X * mouseScale > Width)
            {
                ((RawMouse)rmInput.Mice[1]).X = (int)(Width / mouseScale);
            }
            if (((RawMouse)rmInput.Mice[1]).Y * mouseScale > Height)
            {
                ((RawMouse)rmInput.Mice[1]).Y = (int)(Height / mouseScale);
            }


            position.X = ((RawMouse)rmInput.Mice[0]).X * mouseScale;
            position.Y = ((RawMouse)rmInput.Mice[0]).Y * mouseScale;

            //position = transformToDevice.Transform(position);

            Thickness t = RedCursor.Margin;
            t.Left = position.X - 25;
            t.Top = position.Y - 25;
            RedCursor.Margin = t;

            if (((RawMouse)rmInput.Mice[0]).Buttons[0])
            {
                m_spotSphere[0].Material = m_spotMaterialSelectedR;

                for (int i = 0; i < MainWindow.m_spots.Count; i++)
                {
                    if (MainWindow.m_spots[i].MouseControlID == 0)
                    {
                        MoveSpot(i, position);
                        Point3D target = MainWindow.m_spots[i].Target;
                        for (int j = i + 1; j < MainWindow.m_spots.Count; j++)
                        {
                            if (MainWindow.m_spots[j].MouseControlID == 0)
                            {
                                MainWindow.m_spots[j].Target = target;
                            }
                        }

                        i = 99;
                    }
                }

            }

            position.X += screenCoordinates.X;
            position.Y += screenCoordinates.Y;

            SetCursor((int)position.X, (int)position.Y);


            //Point position = new Point();

            position.X = ((RawMouse)rmInput.Mice[1]).X * mouseScale;
            position.Y = ((RawMouse)rmInput.Mice[1]).Y * mouseScale;

            //            position = transformToDevice.Transform(logicalPosition);

            if (position.X < 0)
            {
                position.X = 0;
                ((RawMouse)rmInput.Mice[1]).X = 0;
            }

            if (position.Y < 0)
            {
                position.Y = 0;
                ((RawMouse)rmInput.Mice[1]).Y = 0;
            }



            t = GreenCursor.Margin;
            t.Left = position.X - 25;
            t.Top = position.Y - 25;
            GreenCursor.Margin = t;


            if (((RawMouse)rmInput.Mice[1]).Buttons[0])
            {
                m_spotSphere[1].Material = m_spotMaterialSelectedG;

                for (int i = 0; i < MainWindow.m_spots.Count; i++)
                {
                    if (MainWindow.m_spots[i].MouseControlID == 1)
                    {
                        MoveSpot(i, position);
                        Point3D target = MainWindow.m_spots[i].Target;
                        for (int j = i + 1; j < MainWindow.m_spots.Count; j++)
                        {
                            if (MainWindow.m_spots[j].MouseControlID == 1)
                            {
                                MainWindow.m_spots[j].Target = target;
                            }
                        }

                        i = 99;
                    }
                }

            }

            if (m_mousedown)
            {
                ((MainWindow)App.Current.MainWindow).PointSpots();
            }


        }

        private void viewport3D_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // TODO
                m_spotSphere[0].Material = m_spotMaterialSelectedR;
                m_mousedown = true;
                HelixViewport3D_MouseMove(sender, e);
            }
        }

        private void HelixViewport3D_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                // TODO
                m_spotSphere[0].Material = m_spotMaterial;
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
