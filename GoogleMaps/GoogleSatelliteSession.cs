using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;

namespace TiledMaps
{
    public class GoogleSatelliteSession : HttpMapSession
    {
        static int myCurrentTileServer = 0;
        protected override Uri GetUriForKey(Key key)
        {
            StringBuilder builder = new StringBuilder(key.Zoom);

            for (int i = 0; i < key.Zoom; i++)
            {
                char c = 'q';
                if (key.X % 2 == 1)
                    c++;
                if (key.Y % 2 == 1)
                {
                    c++;
                    if (c == 'r')
                    {
                        c++;
                        c++;
                    }
                }
                key.X /= 2;
                key.Y /= 2;
                builder.Insert(0, c.ToString());
            }

            string tile = builder.ToString();
            return new Uri(string.Format("http://khm{0}.google.com/kh?n=404&v=33&t=t{1}", myCurrentTileServer % 4, tile));
        }
    }
}
