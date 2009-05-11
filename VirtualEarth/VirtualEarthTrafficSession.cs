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
    public class VirtualEarthTrafficSession : HttpMapSession
    {
        public override bool HasAlpha
        {
            get
            {
                return true;
            }
        }

        protected override Uri GetUriForKey(Key key)
        {
            StringBuilder builder = new StringBuilder(key.Zoom);

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
            int server = (key.X + key.Y) % 2;
            return new Uri(string.Format("http://t{0}.traffic.virtualearth.net/Flow/t{1}.png?tc=8321627", server, tile));
        }
    }
}