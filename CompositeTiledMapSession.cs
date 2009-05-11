using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.IO;

namespace TiledMaps
{
    public class CompositeMapSession : TiledMapSession
    {
        List<TiledMapSession> mySessions = new List<TiledMapSession>();
        bool[] mySessionEnabled;
        Dictionary<Key, int> mySessionBlend = new Dictionary<Key, int>();
        int myTotalBlend = 1;
        bool myClearBlendedTiles = true;
        GraphicsRenderer myRenderer = new GraphicsRenderer();

        public bool ClearBlendedTiles
        {
            get { return myClearBlendedTiles; }
            set { myClearBlendedTiles = value; }
        }

        public void SetSessions(params TiledMapSession[] sessions)
        {
            mySessionBlend.Clear();
            myTotalBlend = 0;
            mySessions = new List<TiledMapSession>(sessions);
            mySessionEnabled = new bool[mySessions.Count];
            for (int i = 0; i < mySessionEnabled.Length; i++)
                mySessionEnabled[i] = false;

            if (mySessions.Count > 0)
            {
                mySessionEnabled[0] = true;
                myTotalBlend = 1;
            }
            base.ClearTileCache();
        }

        public bool this[int index]
        {
            get
            {
                return mySessionEnabled[index];
            }
            set
            {
                mySessionEnabled[index] = value;

                if (value)
                    myTotalBlend++;
                else
                    myTotalBlend--;
                mySessionEnabled[index] = value;
                mySessionBlend.Clear();
                base.ClearTileCache();
            }
        }

        public bool this[TiledMapSession session]
        {
            get
            {
                int index = mySessions.IndexOf(session);
                if (index == -1)
                    throw new ArgumentException("TiledMapSession not found in this CompositeMapSession.");
                return mySessionEnabled[index];
            }
            set
            {
                int index = mySessions.IndexOf(session);
                if (index == -1 || mySessionEnabled[index] == value)
                    return;

                this[index] = value;
            }
        }

        public void ClearInactiveTileCache()
        {
            for (int i = 0; i < mySessions.Count; i++)
            {
                if (!mySessionEnabled[i])
                    mySessions[i].ClearTileCache();
            }
        }

        public override TileData GetTile(TiledMapSession.Key key, IMapRenderer renderer, System.Threading.WaitCallback callback, object state)
        {
            // make sure there's something to blend
            if (mySessions.Count == 0)
                return null;

            // see if we need to update this tile at all
            int currentBlend = 0;
            if (mySessionBlend.TryGetValue(key, out currentBlend) && currentBlend == myTotalBlend)
                return TileCache[key];

            // see how many tiles we can blend
            int canBlend = 0;
            for (int i = 0; i < mySessions.Count; i++)
            {
                if (!mySessionEnabled[i])
                    continue;
                TileData data = mySessions[i].GetTile(key, renderer, callback, state);
                if (data != null && data.Bitmap != null)
                    canBlend++;
            }

            // if there's nothing new to blend, don't
            if (canBlend == currentBlend)
                return null;

            mySessionBlend[key] = canBlend;

            // get the target bitmap ready
            if (RefreshBitmap == null)
                throw new InvalidOperationException("You must provide a RefreshBitmap");
            StandardBitmap refreshBitmap = RefreshBitmap as StandardBitmap;
            int width = refreshBitmap.Width;
            int height = refreshBitmap.Height;
            StandardBitmap bitmap = new StandardBitmap(new Bitmap(width, height));
            using (Graphics graphics = Graphics.FromImage(bitmap.Bitmap))
            {
                refreshBitmap.Draw(graphics, new Rectangle(0, 0, width, height), new Rectangle(0, 0, width, height));
            }

            // draw the bitmaps
            using (Graphics graphics = Graphics.FromImage(bitmap.Bitmap))
            {
                for (int i = 0; i < mySessions.Count; i++)
                {
                    TiledMapSession session = mySessions[i];
                    if (!mySessionEnabled[i])
                        continue;
                    TileData tile = mySessions[i].GetTile(key, renderer, callback, state);
                    if (tile == null)
                        continue;

                    IGraphicsDrawable tileBitmap = tile.Bitmap as IGraphicsDrawable;
                    tileBitmap.Draw(graphics, new Rectangle(0, 0, 256, 256), new Rectangle(0, 0, 256, 256));

                    if (ClearBlendedTiles && canBlend == myTotalBlend)
                    {
                        tileBitmap.Dispose();
                        mySessions[i].TileCache.Remove(key);
                    }
                }
            }

            TileData ret;
            if (!TileCache.TryGetValue(key, out ret))
            {
                ret = new TileData();
                TileCache.Add(key, ret);
            }
            else if (ret.Bitmap != null)
            {
                ret.Bitmap.Dispose();
                ret.Bitmap = null;
            }

            // TODO: optimize to do a lock/read load
            MemoryStream mem = new MemoryStream();
            bitmap.Bitmap.Save(mem, System.Drawing.Imaging.ImageFormat.Bmp);
            mem.Seek(0, SeekOrigin.Begin);
            ret.Bitmap = myRenderer.LoadBitmap(mem, false);

            return ret;
        }

        public override void ClearTileCache()
        {
            mySessionBlend.Clear();
            base.ClearTileCache();
            foreach (TiledMapSession session in mySessions)
            {
                session.ClearTileCache();
            }
        }
    }
}
