using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace TiledMaps
{
    public class GoogleTerrainSession : HttpMapSession
    {
        static int myCurrentTileServer = 0;
        protected override Uri GetUriForKey(Key key)
        {
            return new Uri(string.Format("http://mt{0}.google.com/mt?n=404&v=w2p.75&x={1}&y={2}&zoom={3}", (myCurrentTileServer++) % 4, key.X, key.Y, 17 - key.Zoom));
        }
    }
}
