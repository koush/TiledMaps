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
    /// <summary>
    /// IMapRenderer provides the methods necessary for a TiledMapSession
    /// to draw tiles to compose the map, as well as the other content
    /// that may appear on the map.
    /// </summary>
    public interface IMapRenderer
    {
        /// <summary>
        /// Get a IMapDrawable from a stream that contains a bitmap.
        /// </summary>
        /// <param name="session">The map session requesting the bitmap</param>
        /// <param name="stream">The input stream</param>
        /// <returns>The resultant bitmap</returns>
        IMapDrawable GetBitmapFromStream(TiledMapSession session, Stream stream);

        /// <summary>
        /// Given an IMapDrawable, draw its contents.
        /// </summary>
        /// <param name="drawable">The IMapDrawable to be drawn.</param>
        /// <param name="destRect">The destination rectangle of the drawable.</param>
        /// <param name="sourceRect">The source rectangle of the drawable.</param>
        void Draw(IMapDrawable drawable, Rectangle destRect, Rectangle sourceRect);

        /// <summary>
        /// Draw a filled rectangle on the map.
        /// </summary>
        /// <param name="color">The fill color.</param>
        /// <param name="rect">The destination rectangle.</param>
        void FillRectangle(Color color, Rectangle rect);

        /// <summary>
        /// Draw a line strip on the map.
        /// </summary>
        /// <param name="lineWidth">The width of the line stripe.</param>
        /// <param name="color">The line strip color.</param>
        /// <param name="points">The points which compose the line strip.</param>
        void DrawLines(float lineWidth, Color color, Point[] points);
    }
    
    /// <summary>
    /// IMapDrawable is the interface used by the Tiled Map Client to represent content
    /// onto a IMapRenderer.
    /// IMapDrawable is generally tied to an implementation of IMapRenderer, 
    /// which is responsible for internally representing and rendering the drawable.
    /// </summary>
    public interface IMapDrawable : IDisposable
    {
        /// <summary>
        /// The width of the drawable content.
        /// </summary>
        int Width
        {
            get;
        }

        /// <summary>
        /// The height of the drawable content.
        /// </summary>
        int Height
        {
            get;
        }
    }

    public class TileData
    {
        public TileData()
        {
        }

        public TileData(IMapDrawable bitmap)
        {
            Bitmap = bitmap;
        }

        public int LastUsed
        {
            get;
            set;
        }

        public IMapDrawable Bitmap
        {
            get;
            set;
        }
    }

    public abstract class TiledMapSession : IDisposable
    {
        IMapDrawable myRefreshBitmap;
        public IMapDrawable RefreshBitmap
        {
            get { return myRefreshBitmap; }
            set { myRefreshBitmap = value; }
        }

        const double EARTH_RADIUS = 6378137;
        const double EARTH_CIRCUM = EARTH_RADIUS * 2.0 * Math.PI;
        const double EARTH_HALF_CIRC = EARTH_CIRCUM / 2;

        static double YToLatitudeAtZoom(int y, int zoom)
        {
            double arc = EARTH_CIRCUM / (1 << zoom);
            double metersY = EARTH_HALF_CIRC - (y * arc);
            double a = Math.Exp(metersY * 2 / EARTH_RADIUS);
            double result = RadToDeg(Math.Asin((a - 1) / (a + 1)));
            return result;
        }

        static double XToLongitudeAtZoom(int x, int zoom)
        {
            double arc = EARTH_CIRCUM / (1 << zoom);
            double metersX = (x * arc) - EARTH_HALF_CIRC;
            double result = RadToDeg(metersX / EARTH_RADIUS);
            return result;
        }

        static int LatitudeToYAtZoom(double lat, int zoom)
        {
            double arc = EARTH_CIRCUM / (1 << zoom);
            double result = DegToRad(lat);
            result = Math.Sin(result);
            result = (result + 1) / (1 - result);
            result = Math.Log(result);
            result *= EARTH_RADIUS / 2;
            result = (EARTH_HALF_CIRC - result) / arc;
            return (int)Math.Round(result);
        }

        static int LongitudeToXAtZoom(double lon, int zoom)
        {
            double arc = EARTH_CIRCUM / (1 << zoom);
            double result = DegToRad(lon);
            result *= EARTH_RADIUS;
            result += EARTH_HALF_CIRC;
            result /= arc;
            return (int)Math.Round(result);
        }

        public Geocode CenterRelativePointToGeocode(Point point)
        {
            Geocode ret = new Geocode();
            ret.Longitude = XToLongitudeAtZoom((myCenterTile.X << 8) + myCenterOffset.X + point.X, myZoom + 8);
            ret.Latitude = YToLatitudeAtZoom((myCenterTile.Y << 8) + myCenterOffset.Y + point.Y, myZoom + 8);
            return ret;
        }

        public Point GeocodeToCenterRelativePoint(Geocode geocode)
        {
            int centerXReference = (myCenterTile.X << 8) + myCenterOffset.X;
            int centerYReference = (myCenterTile.Y << 8) + myCenterOffset.Y;
            int px = LongitudeToXAtZoom(geocode.Longitude, myZoom + 8);
            int py = LatitudeToYAtZoom(geocode.Latitude, myZoom + 8);
            return new Point(px - centerXReference, py - centerYReference);
        }

        static double DegToRad(double rad)
        {
            return rad * Math.PI / 180.0;
        }

        static double RadToDeg(double d)
        {
            return d / Math.PI * 180.0;
        }

        static Geocode PointToGeocode(Point point, int zoom)
        {
            return new Geocode(YToLatitudeAtZoom(point.Y, zoom), XToLongitudeAtZoom(point.X, zoom));
        }

        public struct Key
        {
            public static readonly Key Root = new Key();

            public int X;
            public int Y;
            public int Zoom;
            public Key(int x, int y, int zoom)
            {
                X = x;
                Y = y;
                Zoom = zoom;
            }

            public bool IsValid
            {
                get
                {
                    if (X < 0 || Y < 0 || X >= 1 << Zoom || Y >= 1 << Zoom)
                        return false;
                    return true;
                }
            }

            public override int GetHashCode()
            {
                return X ^ Y ^ (Zoom << 24);
            }

            public static implicit operator Point(Key key)
            {
                return new Point(key.X, key.Y);
            }

            public static bool operator ==(Key first, Key second)
            {
                return first.X == second.X && first.Y == second.Y && first.Zoom == second.Zoom;
            }

            public static bool operator !=(Key first, Key second)
            {
                return first.X != second.X || first.Y != second.Y || first.Zoom != second.Zoom;
            }

            public override bool Equals(object obj)
            {
                Key key = (Key)obj;
                return key == this;
            }
        }

        public virtual bool HasAlpha
        {
            get
            {
                return false;
            }
        }

        Color myBackColor = Color.Gray;
        public Color BackColor
        {
            get
            {
                return myBackColor;
            }
            set
            {
                myBackColor = value;
            }
        }

        public abstract TileData GetTile(TiledMapSession.Key key, IMapRenderer renderer, System.Threading.WaitCallback callback, object state);
        static readonly SolidBrush myGrayBrush = new SolidBrush(Color.Gray);

        static bool GeocodeBoxContains(Geocode tlGeo, Geocode brGeo, Geocode geocode)
        {
            return geocode.Latitude > brGeo.Latitude && geocode.Latitude < tlGeo.Latitude && geocode.Longitude > tlGeo.Longitude && geocode.Longitude < brGeo.Longitude;
        }

        static readonly Pen myDirectionsPen = new Pen(Color.FromArgb(unchecked((int)0xC049BBE5)), 6);
        public int DrawMap(IMapRenderer renderer, int x, int y, int width, int height, WaitCallback callback, object state)
        {
            int unavailable = 0;

            // approximate the the top left tile (it may be off by 1), but the loop
            // below will kept it from being drawn
            int midX = x + width / 2 - myCenterOffset.X;
            int midY = y + height / 2 - myCenterOffset.Y;
            int xTiles = (midX - x) / 256 + 1;
            int yTiles = (midY - y) / 256 + 1;
            Key currentXKey = new Key(myCenterTile.X - xTiles, myCenterTile.Y - yTiles, myZoom);
            int xStart = midX - xTiles * 256;
            int yStart = midY - yTiles * 256;
            Rectangle rect = new Rectangle(x, y, width, height);

            int tickCount = Environment.TickCount;
            for (int currentX = xStart; currentX < x + width; currentX += 256, currentXKey.X++)
            {
                Key key = currentXKey;
                for (int currentY = yStart; currentY < y + height; currentY += 256, key.Y++)
                {
                    IMapDrawable tile = null;

                    // find the intersect region of the tile that we are drawing
                    Rectangle tileRect = new Rectangle(currentX, currentY, 256, 256);
                    tileRect.Intersect(rect);
                    Rectangle sourceRect = new Rectangle(tileRect.X - currentX, tileRect.Y - currentY, tileRect.Width, tileRect.Height);

                    // dont draw off the map tiles
                    if (!key.IsValid)
                    {
                        // dont draw gray rect if we're drawing transparent
                        if (!HasAlpha)
                            renderer.FillRectangle(BackColor, tileRect);
                        continue;
                    }

                    // first try to get the tile from the tileData 
                    TileData tileData = GetTile(key, renderer, callback, state);

                    if (tileData != null)
                    {
                        tile = tileData.Bitmap;
                        tileData.LastUsed = tickCount;
                    }

                    if (tile == null)
                    {
                        // tile not available, so try to generate a tile from child tiles
                        unavailable++;

                        Key childKey = new Key(key.X * 2, key.Y * 2, key.Zoom + 1);
                        Key tl = childKey;
                        Key tr = new Key(childKey.X + 1, childKey.Y, childKey.Zoom);
                        Key br = new Key(childKey.X + 1, childKey.Y + 1, childKey.Zoom);
                        Key bl = new Key(childKey.X, childKey.Y + 1, childKey.Zoom);
                        TileData tld;
                        TileData trd;
                        TileData bld;
                        TileData brd;
                        
                        // see if the children are available
                        // we also need to null check, because they could be loading
                        if (TileCache.TryGetValue(tl, out tld) && TileCache.TryGetValue(tr, out trd) && TileCache.TryGetValue(br, out brd) && TileCache.TryGetValue(bl, out bld) 
                            && tld != null && trd != null && bld != null & brd != null 
                            && tld.Bitmap != null && trd.Bitmap != null && bld.Bitmap != null && brd.Bitmap != null)
                        {
                            // children are available, so mark them as recently used
                            tld.LastUsed = trd.LastUsed = bld.LastUsed = brd.LastUsed = tickCount;

                            // calculate the destination rects of each child tile
                            Rectangle tlr = new Rectangle(currentX, currentY, 128, 128);
                            Rectangle trr = new Rectangle(currentX + 128, currentY, 128, 128);
                            Rectangle blr = new Rectangle(currentX, currentY + 128, 128, 128);
                            Rectangle brr = new Rectangle(currentX + 128, currentY + 128, 128, 128);

                            tlr.Intersect(rect);
                            trr.Intersect(rect);
                            blr.Intersect(rect);
                            brr.Intersect(rect);

                            // calculate the source rect of each child tile
                            Rectangle tlsr = new Rectangle(tlr.X - currentX, tlr.Y - currentY, tlr.Width * 2, tlr.Height * 2);
                            Rectangle trsr = new Rectangle(trr.X - currentX - 128, trr.Y  - currentY, trr.Width * 2, trr.Height * 2);
                            Rectangle blsr = new Rectangle(blr.X - currentX, blr.Y - currentY - 128, blr.Width * 2, blr.Height * 2);
                            Rectangle brsr = new Rectangle(brr.X - currentX - 128, brr.Y - currentY - 128, brr.Width * 2, brr.Height * 2);

                            // don't attempt to draw tiles that we don't need to
                            if (tlsr.Width > 0 && tlsr.Height > 0)
                                renderer.Draw(tld.Bitmap, tlr, tlsr);
                            if (trsr.Width > 0 && trsr.Height > 0)
                                renderer.Draw(trd.Bitmap, trr, trsr);
                            if (blsr.Width > 0 && blsr.Height > 0)
                                renderer.Draw(bld.Bitmap, blr, blsr);
                            if (brsr.Width > 0 && brsr.Height > 0)
                                renderer.Draw(brd.Bitmap, brr, brsr);
                            continue;
                        }
                        else
                        {
                            // can't generate from children, so try generating one of the parents
                            Key parent = key;
                            Rectangle parentRect = sourceRect;
                            TileData parentData = null;
                            while (parent.Zoom >= 0 && parentData == null)
                            {
                                parentRect.Width /= 2;
                                parentRect.Height /= 2;
                                parentRect.X /= 2;
                                parentRect.Y /= 2;
                                if (parent.X % 2 == 1)
                                    parentRect.X += 128;
                                if (parent.Y % 2 == 1)
                                    parentRect.Y += 128;
                                parent.X /= 2;
                                parent.Y /= 2;
                                parent.Zoom--;
                                TileCache.TryGetValue(parent, out parentData);
                            }

                            if (parentData != null && parentData.Bitmap != null)
                            {
                                // mark this tile as used recently
                                parentData.LastUsed = tickCount;
                                if (tileRect.Width > 0 && tileRect.Height > 0)
                                    renderer.Draw(parentData.Bitmap, tileRect, parentRect);
                                continue;
                            }
                            else
                            {
                                // tile is being downloaded, and we have no parent or child images we can use to draw a temp
                                // image. let's try to use a refresh bitmap.

                                // tile is not available, and this is a transparent draw,
                                // so dont draw at all
                                if (HasAlpha)
                                    continue;
                                if ((tile = RefreshBitmap) == null)
                                {
                                    renderer.FillRectangle(BackColor, tileRect);
                                    continue;
                                }
                            }
                        }
                    }

                    if (tile != null && tileRect.Width > 0 && tileRect.Height > 0)
                        renderer.Draw(tile, tileRect, sourceRect);
                }
            }

            int pixelLevelZoom = myZoom + 8;
            int centerXReference = myCenterTile.X << 8;
            int centerYReference = myCenterTile.Y << 8;
            Geocode tlGeo = PointToGeocode(new Point(Math.Max(centerXReference + myCenterOffset.X - width / 2, 0), Math.Max(centerYReference + myCenterOffset.Y - height / 2, 0)), pixelLevelZoom);
            Geocode brGeo = PointToGeocode(new Point(Math.Min(centerXReference + myCenterOffset.X + width / 2, 1 << pixelLevelZoom), Math.Min(centerYReference + myCenterOffset.Y + height / 2, 1 << pixelLevelZoom)), pixelLevelZoom);
            int adjustX = midX - centerXReference;
            int adjustY = midY - centerYReference;

            foreach(Route route in myRoutes)
            {
                List<Point> points = new List<Point>();
                Geocode lastOffscreenPoint = Geocode.Null;
                for (int i = 0; i < route.PolyLine.Length; i++)
                {
                    Geocode geocode = route.PolyLine[i];
                    if (myLevelToZoom[route.Levels[i]] > myZoom)
                        continue;

                    // check if we're drawing off the screen
                    if (!GeocodeBoxContains(tlGeo, brGeo, geocode))
                    {
                        // if we're drawing from on screen to off screen, draw it, but note that
                        // we are now off screen
                        if (lastOffscreenPoint == Geocode.Null)
                            points.Add(GeocodeToScreen(geocode, pixelLevelZoom, adjustX, adjustY));

                        lastOffscreenPoint = geocode;
                        continue;
                    }

                    // draw in from off the screen if necessary
                    if (lastOffscreenPoint != Geocode.Null)
                        points.Add(GeocodeToScreen(lastOffscreenPoint, pixelLevelZoom, adjustX, adjustY));
                    // note that we are now in screen space
                    lastOffscreenPoint = Geocode.Null;

                    points.Add(GeocodeToScreen(geocode, pixelLevelZoom, adjustX, adjustY));
                }
                if (points.Count > 1)
                    renderer.DrawLines(route.LineWidth, Color.Cyan, points.ToArray());
            }

            foreach (IMapOverlay overlay in Overlays)
            {
                DrawAtGeocode(tlGeo, brGeo, renderer, overlay.Geocode, pixelLevelZoom, adjustX + overlay.Offset.X, adjustY + overlay.Offset.Y, overlay.Drawable);
            }

            return unavailable;
        }

        void DrawAtGeocode(Geocode tlGeo, Geocode brGeo, IMapRenderer renderer, Geocode geocode, int pixelLevelZoom, int adjustX, int adjustY, IMapDrawable drawable)
        {
            if (!GeocodeBoxContains(tlGeo, brGeo, geocode) || drawable == null)
                return;
            Point p = GeocodeToScreen(geocode, pixelLevelZoom, adjustX, adjustY);
            renderer.Draw(drawable, new Rectangle(p.X - drawable.Width / 2, p.Y - drawable.Height / 2, drawable.Width, drawable.Height), new Rectangle(0, 0, drawable.Width, drawable.Height));
        }
        
        Point GeocodeToScreen(Geocode geocode, int zoom, int adjustX, int adjustY)
        {
            Point p = new Point(LongitudeToXAtZoom(geocode.Longitude, zoom), LatitudeToYAtZoom(geocode.Latitude, zoom));
            p.X += adjustX;
            p.Y += adjustY;
            return p;
        }

        static readonly int[] myLevelToZoom = new int []{ 13, 7, 2, Int32.MinValue };

        public void Pan(int x, int y)
        {
            int newX = myCenterOffset.X - x;
            int newY = myCenterOffset.Y - y;
            Key newCenterTile = new Key(myCenterTile.X, myCenterTile.Y, myZoom);

            while (newX < 0)
            {
                newX += 256;
                newCenterTile.X--;
            }
            while (newX > 256)
            {
                newX -= 256;
                newCenterTile.X++;
            }

            while (newY < 0)
            {
                newY += 256;
                newCenterTile.Y--;
            }
            while (newY > 256)
            {
                newY -= 256;
                newCenterTile.Y++;
            }

            if (!newCenterTile.IsValid)
                return;

            myCenterTile = newCenterTile;
            myCenterOffset = new Point(newX, newY);
        }

        public bool CanZoomIn
        {
            get
            {
                return myZoom < 15;
            }
        }

        public bool CanZoomOut
        {
            get
            {
                return myZoom > 0;
            }
        }

        public void ZoomIn()
        {
            if (myZoom >= 15)
                return;
            Point newCenterTile = new Point(myCenterTile.X * 2, myCenterTile.Y * 2);
            if (myCenterOffset.X >= 128)
                newCenterTile.X++;
            if (myCenterOffset.Y >= 128)
                newCenterTile.Y++;
            myCenterTile = newCenterTile;
            myCenterOffset.X = (myCenterOffset.X % 128) * 2;
            myCenterOffset.Y = (myCenterOffset.Y % 128) * 2;
            myZoom++;
        }


        public void ZoomOut()
        {
            if (myZoom <= 0)
                return;

            bool left = myCenterTile.X % 2 == 0;
            bool top = myCenterTile.Y % 2 == 0;
            myZoom--;

            myCenterTile = new Point(myCenterTile.X / 2, myCenterTile.Y / 2);

            myCenterOffset.X = myCenterOffset.X / 2;
            myCenterOffset.Y = myCenterOffset.Y / 2;
            if (!left)
                myCenterOffset.X += 128;
            if (!top)
                myCenterOffset.Y += 128;
        }


        public void FitPOIToDimensions(int width, int height, int maxZoom, params Geocode[] geocodes)
        {
            // find the center
            Geocode topLeft = new Geocode(double.MinValue, double.MaxValue);
            Geocode bottomRight = new Geocode(double.MaxValue, double.MinValue);
            foreach (Geocode geocode in geocodes)
            {
                topLeft.Latitude = Math.Max(topLeft.Latitude, geocode.Latitude);
                topLeft.Longitude = Math.Min(topLeft.Longitude, geocode.Longitude);
                bottomRight.Latitude = Math.Min(bottomRight.Latitude, geocode.Latitude);
                bottomRight.Longitude = Math.Max(bottomRight.Longitude, geocode.Longitude);
            }
            Geocode center = new Geocode((topLeft.Latitude + bottomRight.Latitude) / 2, (topLeft.Longitude + bottomRight.Longitude) / 2);

            // center the map on the center
            myZoom = maxZoom;
            int y = LatitudeToYAtZoom(center.Latitude, myZoom + 8);
            int x = LongitudeToXAtZoom(center.Longitude, myZoom + 8);
            myCenterTile.X = x / 256;
            myCenterTile.Y = y / 256;
            myCenterOffset.X = x % 256;
            myCenterOffset.Y = y % 256;

            int halfWidth = width / 2;
            int halfHeight = height / 2;

            while (myZoom > 0)
            {
                ZoomOut();
                y = LatitudeToYAtZoom(center.Latitude, myZoom + 8);
                x = LongitudeToXAtZoom(center.Longitude, myZoom + 8);
                int tly = LatitudeToYAtZoom(topLeft.Latitude, myZoom + 8);
                int tlx = LongitudeToXAtZoom(topLeft.Longitude, myZoom + 8);
                int bry = LatitudeToYAtZoom(bottomRight.Latitude, myZoom + 8);
                int brx = LongitudeToXAtZoom(bottomRight.Longitude, myZoom + 8);
                if (tlx - x > -halfWidth && tly - y > -halfHeight && brx - x < halfWidth && bry - y < halfHeight)
                    break;
            }
        }

        Dictionary<Key, TileData> myTileCache = new Dictionary<Key, TileData>();
        public static readonly TileData InvalidTile = new TileData();

        public Dictionary<Key, TileData> TileCache
        {
            get { return myTileCache; }
            set { myTileCache = value; }
        }

        public object SynchronizationObject
        {
            get
            {
                return this;
            }
        }

        public virtual void ClearTileCache()
        {
            if (TileCache == null)
                return;
            foreach (TileData data in TileCache.Values)
            {
                if (data != null && data.Bitmap != null)
                    data.Bitmap.Dispose();
            }
            TileCache.Clear();
        }

        public void ClearAgedTiles(int millisecondsSinceLastUse)
        {
            int cutoff = Environment.TickCount - millisecondsSinceLastUse;

            List<Key> keys = new List<Key>(TileCache.Keys);
            foreach (Key key in keys)
            {
                TileData data;
                if (!TileCache.TryGetValue(key, out data) || data == null)
                    continue;

                if (data.LastUsed <= cutoff)
                {
                    if (data.Bitmap != null)
                        data.Bitmap.Dispose();
                    TileCache.Remove(key);
                }
            }
        }

        public void ClearZoomLevels()
        {
            List<Key> keys = new List<Key>(TileCache.Keys);
            foreach (Key key in keys)
            {
                TileData data;
                if (!TileCache.TryGetValue(key, out data) || data == null)
                    continue;
                if (key.Zoom != myZoom)
                {
                    if (data.Bitmap != null)
                        data.Bitmap.Dispose();
                    TileCache.Remove(key);
                }
            }
        }

        public void ClearOlderTiles(int maxTiles)
        {
            List<KeyValuePair<Key, TileData>> pairs = new List<KeyValuePair<Key, TileData>>();
            foreach (Key key in TileCache.Keys)
            {
                TileData data = TileCache[key];

            }
        }

        Point myCenterTile = Point.Empty;
        public Point CenterTile
        {
            get { return myCenterTile; }
            set { myCenterTile = value; }
        }

        int myZoom = 0;
        public int Zoom
        {
            get { return myZoom; }
            set
            {
                myZoom = Math.Max(Math.Min(15, value), 0);
            }
        }

        Point myCenterOffset = new Point(128, 128);
        public Point CenterOffset
        {
            get { return myCenterOffset; }
            set { myCenterOffset = value; }
        }

        public Point Center
        {
            get
            {
                Point ret = new Point();
                ret.X = (myCenterTile.X << 8) + myCenterOffset.X;
                ret.Y = (myCenterTile.Y << 8) + myCenterOffset.Y;
                return ret;
            }
            set
            {
                int cox = value.X % 256;
                int coy = value.Y % 256;
                int ctx = value.X / 256;
                int cty = value.Y / 256;
                Key newCenter = new Key(ctx, cty, myZoom);
                if (!newCenter.IsValid)
                    return;
                myCenterOffset = new Point(cox, coy);
                myCenterTile = new Point(ctx, cty);
            }
        }

        List<Route> myRoutes = new List<Route>();
        public List<Route> Routes
        {
            get
            {
                return myRoutes;
            }
        }

        List<IMapOverlay> myOverlays = new List<IMapOverlay>();

        public List<IMapOverlay> Overlays
        {
            get { return myOverlays; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            ClearTileCache();
            TileCache = null;
            myOverlays = null;
            myRoutes = null;
        }

        #endregion
    }

    public delegate IMapDrawable GetTileBitmapHandler(TiledMapSession session, Stream stream);
}
