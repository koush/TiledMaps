using System.Drawing;
using System.Drawing.Imaging;

namespace TiledMaps
{
    public interface IMapOverlay
    {
        IMapDrawable Drawable
        {
            get;
        }

        Geocode Geocode
        {
            get;
        }

        Point Offset
        {
            get;
        }
    }

    public class MapOverlay : IMapOverlay
    {
        public MapOverlay()
        {
        }

        public MapOverlay(IMapDrawable drawable, Geocode geocode, Point offset)
        {
            myDrawable = drawable;
            myOffset = offset;
            myGeocode = geocode;
        }

        IMapDrawable myDrawable;

        public IMapDrawable Drawable
        {
            get { return myDrawable; }
            set { myDrawable = value; }
        }
        Geocode myGeocode;

        public Geocode Geocode
        {
            get { return myGeocode; }
            set { myGeocode = value; }
        }
        Point myOffset;

        public Point Offset
        {
            get { return myOffset; }
            set { myOffset = value; }
        }
    }
}