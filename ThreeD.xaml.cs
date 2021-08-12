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

        private Visual3D m_teapot;
        private Visual3D m_beam;
        //private Visual3D m_beam2;


        public void grab()
        {
            //Left = 0;
            //Top = 0;
            //Height = SystemParameters.PrimaryScreenHeight;
            //Width =  SystemParameters.PrimaryScreenWidth;


            EllipsoidVisual3D e3d = new EllipsoidVisual3D();
            e3d.Material= MaterialHelper.CreateMaterial(Colors.Gray, 0.5);
            e3d.BackMaterial = null;
            e3d.RadiusX = e3d.RadiusY = e3d.RadiusZ = 1.0;

            m_teapot = e3d;
            var beamMatrial = MaterialHelper.CreateMaterial(Colors.White, 0.5);
            var beam = new MeshGeometryVisual3D();

            beam.Material = beamMatrial;
            beam.Transform = new TranslateTransform3D(0, 0, 0);
            beam.BackMaterial = null;

            m_beam = beam;

            viewport3D.Children.Add(m_teapot);
            viewport3D.Children.Add(m_beam);
            Show();
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

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
//            vertLine.Margin = new Thickness(e.GetPosition(this).X, 0,0,0);
//            horizLine.Margin = new Thickness(0,e.GetPosition(this).Y, 0, 0);
        }

        private void HelixViewport3D_MouseMove(object sender, MouseEventArgs e)
        {
            viewport3D.Children.Remove(m_teapot);
            viewport3D.Children.Remove(m_beam);

            var pt = viewport3D.FindNearestPoint(e.GetPosition(viewport3D));
            if (pt != null)
            {
                Point3D point = (Point3D)pt;

                Visual3D vis = viewport3D.FindNearestVisual(e.GetPosition(viewport3D));

//                Console.WriteLine("Item position: " + pt.ToString());
                Transform3D at = new TranslateTransform3D(((Vector3D)pt));


                m_teapot.Transform = at;


                var mb = new MeshBuilder(true);
                mb.AddCylinder(point, MainWindow.m_spots[0].Location, .1, 8);

                ((MainWindow)App.Current.MainWindow).PointSpot(point);

                ((MeshGeometryVisual3D)m_beam).MeshGeometry = mb.ToMesh();

                viewport3D.Children.Add(m_beam);

                viewport3D.Children.Add(m_teapot);
            }

        }

        private void ThreeD1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

    }

}
