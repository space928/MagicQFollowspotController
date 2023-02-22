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
            UpdateModel();
        }

        public Model3DGroup m_Lights;
        public GeometryModel3D m_Bars;
        public GeometryModel3D m_Markers;
        public GeometryModel3D m_Theatre;
        public GeometryModel3D m_Box;
        public Model3DGroup m_BoxesModelGroup;

        public void UpdateModel()
        {
            double[,] stagecurve = {
                {1.000,0.000},
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
            m_BoxesModelGroup = new Model3DGroup();

            // Create a mesh builder and add a box to it
            var meshBuilder = new MeshBuilder(false, false);

            meshBuilder.AddQuad(new (0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0));

            // Create a mesh from the builder (and freeze it)
            var mesh = meshBuilder.ToMesh(true);

            // Create some materials
            var blueMaterial = MaterialHelper.CreateMaterial(Colors.Blue);

            // X - left right, Y - up / down, Z - in / out

            var res = MainWindow.appResources;
            var theatre = res.theatrePhysicalData;

            var mb = new MeshBuilder(true, false);

            var p0 = new Point3D(-theatre.width / 2, 0, 0);
            var p1 = new Point3D(-theatre.width / 2, 0, theatre.roof);
            var p2 = new Point3D(-theatre.width / 2, -theatre.stairStart, theatre.roof);
            var p3 = new Point3D(-theatre.width / 2, -theatre.stairStart - theatre.stairLength, theatre.roof);
            var p4 = new Point3D(-theatre.width / 2, -theatre.stairStart - theatre.stairLength, theatre.stairHeight);
            var p5 = new Point3D(-theatre.width / 2, -theatre.stairStart, 0);

            var p6 = new Point3D(theatre.width / 2, 0, 0);
            var p7 = new Point3D(theatre.width / 2, -theatre.stairStart, 0);
            var p8 = new Point3D(theatre.width / 2, -theatre.stairStart - theatre.stairLength, theatre.stairHeight);

            var p9 = new Point3D(theatre.width / 2, -theatre.stairStart - theatre.stairLength, theatre.roof);
            var p10 = new Point3D(theatre.width / 2, -theatre.stairStart, theatre.roof);
            var p11 = new Point3D(theatre.width / 2, 0, theatre.roof);

            var p12 = new Point3D(-theatre.width / 2, theatre.length - theatre.stairStart - theatre.stairLength, theatre.roof);
            var p13 = new Point3D(-theatre.width / 2, theatre.length - theatre.stairStart - theatre.stairLength, 0);

            var p14 = new Point3D(theatre.width / 2, theatre.length - theatre.stairStart - theatre.stairLength, 0);
            var p15 = new Point3D(theatre.width / 2, theatre.length - theatre.stairStart - theatre.stairLength, theatre.roof);

            // Back wall
            mb.AddQuad(p12, p13, p14, p15);

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

            List<Point> points = new();

            for (int i = 0; i < stagecurve.Length / 2; i++)
            {
                points.Add(new Point(stagecurve[i, 0] * theatre.stageWidth / 2, (stagecurve[i, 1] - 1) * theatre.stageDepth - theatre.stageStart));
            }

            points.Add(new Point(-theatre.width / 2, -theatre.stageDepth - theatre.stageStart));
            points.Add(new Point(-theatre.width / 2, -10));
            points.Add(new Point(theatre.width / 2, -10));
            points.Add(new Point(theatre.width / 2, -theatre.stageDepth - theatre.stageStart));
            points.Add(new Point(stagecurve[0, 0] * theatre.stageWidth / 2, (stagecurve[0, 1] - 1) * theatre.stageDepth - theatre.stageStart));

            Vector3D xaxis = new(1, 0, 0);

            mb.AddExtrudedSegments(points, xaxis, new(0, 0, 0), new Point3D(0, 0, theatre.stageHeight));

            List<Point3D> points3D = new();

            foreach (Point point in points)
            {
                points3D.Add(new Point3D(-point.X, -point.Y, theatre.stageHeight));
            }

            Vector3D[] normals = new Vector3D[points3D.Count];
            for (int i = 0; i < points3D.Count; i++)
                normals[i] = new(0, 1, 0);

            mb.AddTriangleFan(points3D, normals);

            m_Theatre = new GeometryModel3D 
            { 
                Geometry = mb.ToMesh(), 
                Transform = new TranslateTransform3D(0, 0, 0), 
                Material = blueMaterial, 
                BackMaterial = null 
            };
            m_Theatre.SetName("Theatre");
            modelGroup.Children.Add(m_Theatre);

            mb = new(true);

            foreach (var boxDef in res.boxes)
            {
                Vector3D sz = (Vector3D)boxDef.dimensions;
                Vector3D pos = (Vector3D)boxDef.location;
                Vector3D rot = (Vector3D)boxDef.rotation;

                Transform3D transX = new RotateTransform3D(new AxisAngleRotation3D(new(1, 0, 0), rot.X));
                Transform3D transY = new RotateTransform3D(new AxisAngleRotation3D(new(0, 1, 0), rot.Y));
                Transform3D transZ = new RotateTransform3D(new AxisAngleRotation3D(new(0, 0, 1), rot.Z));
                Transform3D translate = new TranslateTransform3D(pos);

                Matrix3D transform = Matrix3D.Multiply(Matrix3D.Multiply(Matrix3D.Multiply(transX.Value, transY.Value), transZ.Value), translate.Value);


                mb.AddBox(new(0, 0, 0), sz.X, sz.Y, sz.Z);
                m_Box = new GeometryModel3D 
                { 
                    Geometry = mb.ToMesh(), 
                    Transform = new MatrixTransform3D(transform), 
                    Material = blueMaterial, 
                    BackMaterial = null 
                };
                m_Box.SetName("Box");
                m_BoxesModelGroup.Children.Add(m_Box);

            }
            modelGroup.Children.Add(m_BoxesModelGroup);

            mb = new(true);
            foreach(var bar in res.lightingBars)
            {
                mb.AddBox(new Point3D(0, bar.offset, bar.height + 0.1), bar.width, .1, .1);
            }

            m_Bars = new GeometryModel3D 
            { 
                Geometry = mb.ToMesh(), 
                Transform = new TranslateTransform3D(0, 0, 0), 
                Material = blueMaterial, 
                BackMaterial = null 
            };
            m_Bars.SetName("bars");
            modelGroup.Children.Add(m_Bars);

            MakeMarker();
            modelGroup.Children.Add(m_Markers);

            // Set the property, which will be bound to the Content property of the ModelVisual3D (see MainWindow.xaml)
            Model = modelGroup;
            UpdateLights();
        }

        public void UpdateLights()
        {
            var redMaterial = MaterialHelper.CreateMaterial(Colors.Red);
            var yellowMaterial = MaterialHelper.CreateMaterial(Colors.Yellow);

            if ((m_Lights != null) && (Model != null))
                ((Model3DGroup)Model).Children.Remove(m_Lights);

            m_Lights = new();
            foreach (FollowSpot spot in MainWindow.m_spots)
            {
                var colour = spot.IsLeadSpot ? yellowMaterial : redMaterial;
                MeshBuilder mb = new(true);
                mb.AddBox(spot.Location, 0.2, 0.2, 0.2);
                var light = new GeometryModel3D 
                { 
                    Geometry = mb.ToMesh(), 
                    Transform = new TranslateTransform3D(0, 0, 0), 
                    Material = colour, 
                    BackMaterial = null 
                };
                light.SetName("Lights");
                m_Lights.Children.Add(light);
            }

            ((Model3DGroup)Model).Children.Add(m_Lights);
        }

        public void MakeMarker()
        {
            var yellowMaterial = MaterialHelper.CreateMaterial(Colors.Yellow);
            if (Model != null)
            {
                ((Model3DGroup)Model).Children.Remove(m_Markers);
            }
            MeshBuilder mb = new(true);

            foreach (Marker marker in MainWindow.m_markers)
            {
                mb.AddSphere(marker.position, 0.2);
            }

            m_Markers = new GeometryModel3D 
            { 
                Geometry = mb.ToMesh(), 
                Transform = new TranslateTransform3D(0, 0, 0), 
                Material = yellowMaterial, 
                BackMaterial = null 
            };
            m_Markers.SetName("Markers");
            if (Model != null)
            {
                ((Model3DGroup)Model).Children.Add(m_Markers);
            }

        }

        public void HideLights()
        {
            ((Model3DGroup)Model).Children.Remove(m_Lights);
            ((Model3DGroup)Model).Children.Remove(m_Bars);
            ((Model3DGroup)Model).Children.Remove(m_Markers);
        }

        public void ShowLights()
        {
            ((Model3DGroup)Model).Children.Add(m_Lights);
            ((Model3DGroup)Model).Children.Add(m_Bars);
            ((Model3DGroup)Model).Children.Add(m_Markers);
        }

        private static Transform3D MakeTransform(double X, double Y, double Z, double rotX, double rotY, double rotZ)
        {
            Transform3D t = new TranslateTransform3D(X, Y, Z);

            Transform3D r = new RotateTransform3D(new AxisAngleRotation3D(new(1, 0, 0), rotX));
            r = Transform3DHelper.CombineTransform(r, new RotateTransform3D(new AxisAngleRotation3D(new(0, 1, 0), rotY)));
            r = Transform3DHelper.CombineTransform(r, new RotateTransform3D(new AxisAngleRotation3D(new(0, 0, 1), rotZ)));

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
            PropertyChanged?.Invoke(this, new(name));
        }
    }
}