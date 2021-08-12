using System;
using System.Windows.Media.Media3D;

namespace MidiApp
{
    class Spherical
    {

        public static Point3D FromSpherical(Vector3D v)
        {
            return FromSpherical(v.X, v.Y, v.Z);
        }

        public static Point3D FromSpherical(double r, double tilt, double pan)
        {
            Point3D pt = new Point3D();
            double snt = Math.Sin(tilt * Math.PI / 180);
            double cnt = Math.Cos(tilt * Math.PI / 180);
            double snp = Math.Sin(pan * Math.PI / 180);
            double cnp = Math.Cos(pan * Math.PI / 180);

            pt.X = r * snt * cnp;
            pt.Y = r * cnt;
            pt.Z = -r * snt * snp;

            return pt;
        }

        public static Point3D MinSphericalMove(Point3D v_from, Point3D v_To)
        {
            double tilt_From = v_from.Y;
            double pan_From = v_from.Z;
            double tilt_To = v_To.Y;
            double pan_To = v_To.Z;

            double delta = Math.Abs(pan_From - pan_To);

            if ((pan_To< 90.0) && (Math.Abs(pan_From - (180+ pan_To))<delta))
            {
                pan_To += 180;
                tilt_To = -tilt_To;
            }
            else if ((pan_To > -90.0) && (Math.Abs(pan_From - (-180 + pan_To)) < delta))
            {
                pan_To -= 180;
                tilt_To = -tilt_To;
            }

            return new Point3D(v_To.X, tilt_To, pan_To);
        }

        public static Point3D NormalizeSpherical(Point3D v_from)
        {
            double tilt_From = v_from.Y;
            double pan_From = v_from.Z;

            while (pan_From > 270)
                pan_From -= 360;

            while (pan_From < -270)
                pan_From += 360;

            return new Point3D(v_from.X, tilt_From, pan_From);
        }

        public static Point3D ToSpherical(Vector3D v)
        {
            return ToSpherical(v.X, v.Y, v.Z);
        }

        public static Point3D ToSpherical(double x, double y, double z)
        {
            Point3D pt = new Point3D();

            double r = Math.Sqrt((double)x * (double)x + (double)y * (double)y + (double)z * (double)z);
            double tilt = 0;
            double pan = 0;

            if (r > 0)
            {
                //tilt = Math.Acos((double)y / r);
                tilt = Math.Atan2(z,Math.Sqrt(x * x + y * y));
                pan = Math.Atan2((double)y, (double)x);
            }

            pt.X = r;
            pt.Y = (tilt / Math.PI * 180); // tilt / inclination
            pt.Z = (pan / Math.PI * 180); // pan / azimuth 

            return pt;
        }

    }
}
