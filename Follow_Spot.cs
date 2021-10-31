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
    [Serializable()]
    public class Follow_Spot : INotifyPropertyChanged
    {
        private double pan;
        private double tilt;

        [field: NonSerialized()]
        private int mouseControlID = -1;

        private Point3D target;
        private Point3D currentTarget;

        [field: NonSerialized()]
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

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
