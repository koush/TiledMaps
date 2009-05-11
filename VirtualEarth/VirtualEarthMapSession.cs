using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Net;
using System.Reflection;
using System.IO;

namespace TiledMaps
{
    public class VirtualEarthMapSession : HttpMapSession
    {
        protected override Uri GetUriForKey(Key key)
        {
            StringBuilder builder = new StringBuilder(key.Zoom);

            // x = 1, y = 1, z = 2
            
            // 3

            for (int i = 0; i < key.Zoom; i++)
            {
                char c = '0';
                if (key.X % 2 == 1)
                    c++;
                if (key.Y % 2 == 1)
                {
                    c++;
                    c++;
                }
                key.X /= 2;
                key.Y /= 2;
                builder.Insert(0, c.ToString());
            }

            string tile = builder.ToString();
            char server = tile[tile.Length - 1];
            return new Uri(string.Format("http://r{0}.ortho.tiles.virtualearth.net/tiles/r{1}.png?g=97", server, tile));
        }

        public override TileData GetTile(TiledMapSession.Key key, IMapRenderer renderer, System.Threading.WaitCallback callback, object state)
        {
            if (key == Key.Root && !TileCache.ContainsKey(key))
                TileCache.Add(Key.Root, new TileData(new StandardBitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream("TiledMaps.VirtualEarth.msvemap.png"))));
            return base.GetTile(key, renderer, callback, state);
        }
    }
}