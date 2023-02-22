using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace MidiApp
{
    [Serializable]
    public class FollowSpot : INotifyPropertyChanged
    {
        private double pan;
        private double tilt;

        [NonSerialized]
        private FixtureProfile fixtureProfile;

        [NonSerialized]
        private int mouseControlID = -1;

        private int zoom = 128;  // Megapointe mode 2: Channel 30

        private double heightOffset = 0.0;

        private Point3D target;
        private Point3D currentTarget;

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        public Point3D Location { get; set; }
        public int Head { get; set; }
        public int Universe { get; set; }
        public int Address { get; set; }
        public Vector3D Velocity { get; set; }
        public Vector3D Acceleration { get; set; }

        public string DMX_Base
        {
            get => Universe.ToString() + "-" + Address.ToString();
        }

        public Point3D Target { get => target; set { target = value; OnPropertyChanged(); } }
        public Point3D CurrentTarget { get => currentTarget; set { currentTarget = value; OnPropertyChanged(); } }
        public double Pan { get => pan; set { pan = value; OnPropertyChanged(); } }
        public double Tilt { get => tilt; set { tilt = value; OnPropertyChanged(); } }
        public int MouseControlID { get => mouseControlID; set { mouseControlID = value; OnPropertyChanged(); } }
        public int Zoom { get => zoom; set { zoom = value; OnPropertyChanged(); } }
        public double HeightOffset { get => heightOffset; set { heightOffset = value; OnPropertyChanged(); } }

        public FixtureProfile FixtureType { get => fixtureProfile; }

        public void SetFixtureType (string type, AppResourcesData appResources)
        {
            // TODO: Replace this with a dictionary
            fixtureProfile = appResources.fixtureProfiles.First(x=>x.name==type);
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }

    public class FixtureType
    {
        private double m_PanRange;
        private double m_TiltRange;
        private bool m_PanInvert;
        private bool m_TiltInvert;
        private int m_PanDMX_BASE;
        private int m_TiltDMX_BASE;
        private int m_ZoomDMX_BASE;
        private double m_Zoom0;
        private double m_Zoom255;


        public FixtureType(String type)
        {
            switch (type){
                case "Pointe":
                case "MegaPointe":
                    m_PanRange = 540.0;
                    m_TiltRange = 265.0;
                    m_PanInvert = false;
                    m_TiltInvert = false;
                    m_PanDMX_BASE = 1;
                    m_TiltDMX_BASE = 3;
                    m_ZoomDMX_BASE = 30;
                    m_Zoom0 = 42.0; 
                    m_Zoom255 = 3.0;
                    break;

                case "Alpha":
                    m_PanRange = 540.0;
                    m_TiltRange = 240.0;
                    m_PanInvert = true;
                    m_TiltInvert = false;
                    m_PanDMX_BASE = 26;
                    m_TiltDMX_BASE = 28;
                    m_ZoomDMX_BASE = 22;
                    m_Zoom0 = 55.0;
                    m_Zoom255 = 7.6;
                    break;

                case "Mythos":
                case "Mythos2":
                    m_PanRange = 540.0;
                    m_TiltRange = 244.0;
                    m_PanInvert = true;
                    m_TiltInvert = false;
                    m_PanDMX_BASE = 23;
                    m_TiltDMX_BASE = 25;
                    m_ZoomDMX_BASE = 19;
                    m_Zoom0 = 47.3;
                    m_Zoom255 = 6.5;
                    break;

                case "ERA300":
                    m_PanRange = 540.0;
                    m_TiltRange = 260.0;
                    m_PanInvert = true;
                    m_TiltInvert = false;
                    m_PanDMX_BASE = 16;
                    m_TiltDMX_BASE = 18;
                    m_ZoomDMX_BASE = 14;
                    m_Zoom0 = 28.0;
                    m_Zoom255 = 13.0;
                    break;

                default:
                    throw new ArgumentException("Unknow fixture type: " + type);
            }
        }

        public double PanRange { get => m_PanRange; }
        public double TiltRange { get => m_TiltRange; }
        public bool PanInvert { get => m_PanInvert; }
        public bool TiltInvert { get => m_TiltInvert; }
        public int PanDMX_BASE { get => m_PanDMX_BASE; }
        public int TiltDMX_BASE { get => m_TiltDMX_BASE; }
        public int ZoomDMX_BASE { get => m_ZoomDMX_BASE; }
    }
}
