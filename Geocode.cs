using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Net;
using System.Drawing.Imaging;

namespace TiledMaps
{
    public struct Geocode
    {
        public static readonly Geocode Null = new Geocode(double.PositiveInfinity, double.PositiveInfinity);
        double myLatitude;

        public double Latitude
        {
            get { return myLatitude; }
            set { myLatitude = value; }
        }
        double myLongitude;

        public double Longitude
        {
            get { return myLongitude; }
            set { myLongitude = value; }
        }

        public Geocode(double latitude, double longitude)
        {
            myLatitude = latitude;
            myLongitude = longitude;
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}", myLatitude, myLongitude);
        }

        public static bool operator ==(Geocode left, Geocode right)
        {
            return left.myLongitude == right.myLongitude && left.myLatitude == right.myLatitude;
        }

        public static bool operator !=(Geocode left, Geocode right)
        {
            return left.myLongitude != right.myLongitude || left.myLatitude != right.myLatitude;
        }
        public override bool Equals(object obj)
        {
            Geocode other = (Geocode)obj;
            return this == other;
        }

        public override int GetHashCode()
        {
            return myLatitude.GetHashCode() + myLongitude.GetHashCode();
        }
    }
}