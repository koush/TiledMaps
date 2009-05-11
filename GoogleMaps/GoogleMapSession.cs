using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Net;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;

namespace TiledMaps
{
    public class GoogleMapSession : HttpMapSession
    {
        static int myCurrentTileServer = 0;
        protected override Uri GetUriForKey(Key key)
        {
            // http://mt0.google.com/mt?v=w2.83&hl=en&x=0&y=0&z=0&s=
            return new Uri(string.Format("http://mt{0}.google.com/mt?v=w2.88&hl=en&x={1}&s=&y={2}&z={3}", (myCurrentTileServer++) % 4, key.X, key.Y, key.Zoom));
            //return new Uri(string.Format("http://khm.google.com/maptilecompress?t=1&c=10&&hl=en&x={1}&s=&y={2}&z={3}", (myCurrentTileServer++) % 4, key.X, key.Y, key.Zoom));
        }
    }
}