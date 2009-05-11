using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace TiledMaps
{
    public class GoogleTrafficSession : HttpMapSession
    {
        protected override Uri GetUriForKey(Key key)
        {
            return new Uri(string.Format("http://www.google.com/mapstt?zoom={0}&x={1}&y={2}", key.Zoom, key.X, key.Y));
        }

        public override bool HasAlpha
        {
            get
            {
                return true;
            }
        }
    }
}
