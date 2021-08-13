// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MainViewModel.cs" company="Helix Toolkit">
//   Copyright (c) 2014 Helix Toolkit contributors
// </copyright>
// <summary>
//   Provides a ViewModel for the Main window.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace MidiApp
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Media3D;

    using HelixToolkit.Wpf;

    /// <summary>
    /// Provides a ViewModel for the Main window.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel()
        {
            updateModel();
        }

        public void updateModel()
        { 
            double[,] stagecurve = {{1.000,0.000},
                                    {0.957,0.358},
                                    {0.917,0.504},
                                    {0.854,0.622},
                                    {0.780,0.709},
                                    {0.696,0.766},
                                    {0.610,0.818},
                                    {0.527,0.868},
                                    {0.440,0.913},
                                    {0.353,0.945},
                                    {0.266,0.969},
                                    {0.176,0.985},
                                    {0.087,0.994},
                                    {0.000,1.000},
                                    {-0.087,0.994},
                                    {-0.176,0.985},
                                    {-0.266,0.969},
                                    {-0.353,0.945},
                                    {-0.440,0.913},
                                    {-0.527,0.868},
                                    {-0.610,0.818},
                                    {-0.696,0.766},
                                    {-0.780,0.709},
                                    {-0.854,0.622},
                                    {-0.917,0.504},
                                    {-0.957,0.358},
                                    {-1.000,0.000}
                                    };

            // Create a model group
            var modelGroup = new Model3DGroup();

            // Create a mesh builder and add a box to it
            var meshBuilder = new MeshBuilder(false, false);

            meshBuilder.AddQuad(new Point3D(0, 0, 0), new Point3D(10, 0, 0), new Point3D(10, 10, 0), new Point3D(0, 10, 0));

            // Create a mesh from the builder (and freeze it)
            var mesh = meshBuilder.ToMesh(true);

            // Create some materials
            var greenMaterial = MaterialHelper.CreateMaterial(Colors.Green);
            var redMaterial = MaterialHelper.CreateMaterial(Colors.Red);
            var blueMaterial = MaterialHelper.CreateMaterial(Colors.Blue);
            var insideMaterial = MaterialHelper.CreateMaterial(Colors.Yellow);

            // X - left right, Y - up / down, Z - in / out

            double Width = 18.75;
            double Length = 26;
            double Roof = 10;
            double StairLength = 12.9;
            double StairStart = 3;
            double StairHeight = 3.9;

            double StageHeight = 1.0;
            double StageStartt = -1.9;
            double StageWidth = 12;
            double StageDepth = 3;

            double Bar0Height = 6.5;

            double BarAudienceHeight = 7.5;
            double BarAudienceOffset = -4.7;

            double Bar1Height = 7.5;
            double Bar1Offset = 5.7;

            double Bar2Height = 7.5;
            double Bar2Offset = 7;

            try
            {
                Width = MainWindow.AppResources.Width;
                Length = MainWindow.AppResources.Length;
                Roof = MainWindow.AppResources.Roof;
                StairLength = MainWindow.AppResources.StairLength;
                StairStart = MainWindow.AppResources.StairStart;
                StairHeight = MainWindow.AppResources.StairHeight;

                StageHeight = MainWindow.AppResources.StageHeight;
                StageStartt = MainWindow.AppResources.StageStartt;
                StageWidth = MainWindow.AppResources.StageWidth;
                StageDepth = MainWindow.AppResources.StageDepth;

                Bar0Height = MainWindow.AppResources.Bar0Height;

                BarAudienceHeight = MainWindow.AppResources.BarAudienceHeight;
                BarAudienceOffset = MainWindow.AppResources.BarAudienceOffset;

                Bar1Height = MainWindow.AppResources.Bar1Height;
                Bar1Offset = MainWindow.AppResources.Bar1Offset;

                Bar2Height = MainWindow.AppResources.Bar2Height;
                Bar2Offset = MainWindow.AppResources.Bar2Offset;

            }
            catch 
            {
                
            }


            var mb = new MeshBuilder(true, false);

            var p0 = new Point3D(-Width/2, 0, 0);
            var p1 = new Point3D(-Width / 2, 0, Roof);
            var p2 = new Point3D(-Width / 2, -StairStart, Roof);
            var p3 = new Point3D(-Width / 2, -StairStart- StairLength, Roof);
            var p4 = new Point3D(-Width / 2, -StairStart - StairLength, StairHeight);
            var p5 = new Point3D(-Width / 2, -StairStart, 0);

            var p6 = new Point3D(Width / 2, 0, 0);
            var p7 = new Point3D(Width / 2, -StairStart, 0);
            var p8 = new Point3D(Width / 2, -StairStart - StairLength, StairHeight);

            var p9 = new Point3D(Width / 2, -StairStart - StairLength, Roof);
            var p10 = new Point3D(Width / 2, -StairStart, Roof);
            var p11 = new Point3D(Width / 2, 0, Roof);

            var p12 = new Point3D(-Width / 2, Length - StairStart - StairLength, Roof);
            var p13 = new Point3D(-Width / 2, Length - StairStart - StairLength, 0);

            var p14 = new Point3D(Width / 2, Length - StairStart - StairLength, 0);
            var p15 = new Point3D(Width / 2, Length - StairStart - StairLength, Roof);


            // Left wall
            mb.AddQuad(p0, p1, p2, p5);
            mb.AddQuad(p2, p3, p4, p5);
            mb.AddQuad(p1, p0, p13, p12);


            // Floor
            mb.AddQuad(p0, p5, p7, p6);
            mb.AddQuad(p5, p4, p8, p7);
            mb.AddQuad(p0, p6, p14, p13);

            // Right Wall
            mb.AddQuad(p6, p7, p10, p11);
            mb.AddQuad(p7, p8, p9, p10);
            mb.AddQuad(p6, p11, p15, p14);

            List<Point> points = new List<Point>();

            for (int i =0; i< stagecurve.Length/2; i++)
            {
                points.Add(new Point(stagecurve[i,0] * StageWidth/2, (stagecurve[i, 1]-1)* StageDepth - StageStartt));
            }

            points.Add(new Point(-Width / 2, -StageDepth - StageStartt));
            points.Add(new Point(-Width / 2, -10));
            points.Add(new Point(Width / 2, -10));
            points.Add(new Point(Width / 2, -StageDepth - StageStartt));
            points.Add(new Point(stagecurve[0, 0] * StageWidth / 2, (stagecurve[0, 1] - 1) * StageDepth - StageStartt));

            Vector3D xaxis = new Vector3D(1, 0, 0);

            mb.AddExtrudedSegments(points, xaxis, new Point3D(0,0,0), new Point3D(0, 0, StageHeight));

            List<Point3D> points3D = new List<Point3D>();

            foreach (Point point in points)
            {
                points3D.Add(new Point3D(-point.X, -point.Y, StageHeight));
            }

            List<Vector3D> normals = new List<Vector3D>();

            foreach (var point in points3D)
            {
                normals.Add(new Vector3D(0, 1, 0));
            }

            mb.AddTriangleFan(points3D, normals);

            mb.AddBox(new Point3D(0, BarAudienceOffset, BarAudienceHeight + 0.1), Width * 0.7, .1, .1);
            mb.AddBox(new Point3D(0, 0, Bar0Height+0.1), Width*0.7, .1, .1);
            mb.AddBox(new Point3D(0, Bar1Offset, Bar1Height + 0.1), Width * 0.9, .1, .1);
            mb.AddBox(new Point3D(0, Bar2Offset, Bar2Height + 0.1), Width * 0.9, .1, .1);
            

            modelGroup.Children.Add(new GeometryModel3D { Geometry = mb.ToMesh(), Transform = new TranslateTransform3D(0, 0, 0), Material = blueMaterial, BackMaterial = null });

            mb = new MeshBuilder(true);
            foreach (Follow_Spot spot in MainWindow.m_spots)
            {

                mb.AddBox(spot.Location, 0.2,0.2,0.2);
            }

            modelGroup.Children.Add(new GeometryModel3D { Geometry = mb.ToMesh(), Transform = new TranslateTransform3D(0, 0, 0), Material = redMaterial, BackMaterial = null });


            // Set the property, which will be bound to the Content property of the ModelVisual3D (see MainWindow.xaml)
            Model = modelGroup;
        }


        private Transform3D makeTransform(double X, double Y, double Z, double rotX, double rotY, double rotZ)
        {
            Transform3D t = new TranslateTransform3D(X, Y, Z);

            Transform3D r = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1,0,0), rotX));
            r = Transform3DHelper.CombineTransform(r, new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), rotY)));
            r = Transform3DHelper.CombineTransform(r, new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), rotZ)));

            r = Transform3DHelper.CombineTransform(r, t);

            return r;
        }

        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        /// <value>The model.</value>

        private Model3D model;
        public Model3D Model { get => model; set { model = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}