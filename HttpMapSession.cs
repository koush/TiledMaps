using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;
using System.IO;

namespace TiledMaps
{
    public abstract class HttpMapSession : TiledMapSession
    {
        protected abstract Uri GetUriForKey(Key key);

        string myCachePath = string.Empty;
        public HttpMapSession()
        {
            try
            {
                myCachePath = Path.GetDirectoryName(new Uri(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath);
            }
            catch (Exception)
            {
            }
            myCachePath = Path.Combine(myCachePath, "MapCache");
            myCachePath = Path.Combine(myCachePath, GetType().Name);

            try
            {
                Directory.CreateDirectory(myCachePath);
            }
            catch (Exception)
            {
            }
        }

        string GetTilePathForKey(Key key)
        {
            return Path.Combine(myCachePath, string.Format("{0}-{1}-{2}", key.X, key.Y, key.Zoom));
        }

        public override TileData GetTile(TiledMapSession.Key key, IMapRenderer renderer, System.Threading.WaitCallback callback, object state)
        {
            TileData tileData = null;
            // try to get the tile
            if (TileCache.TryGetValue(key, out tileData))
            {
                if (tileData != InvalidTile)
                    return tileData;
                TileCache.Remove(key);
            }

            // check if it is in the file cache
            string tilePath = GetTilePathForKey(key);
            if (File.Exists(tilePath))
            {
                FileInfo finfo = new FileInfo(tilePath);
                if (DateTime.Now - finfo.CreationTime > new TimeSpan(2, 0, 0, 0))
                {
                    // tile is old, expire it
                    File.Delete(tilePath);
                }
                else
                {
                    TileCache[key] = null;
                    ThreadPool.QueueUserWorkItem((o) =>
                        {
                            try
                            {
                                using (FileStream fstream = new FileStream(tilePath, FileMode.Open))
                                {
                                    IMapDrawable bitmap = renderer.GetBitmapFromStream(this, fstream);
                                    TileCache[key] = new TileData(bitmap);
                                    if (callback != null)
                                        callback(state);
                                }
                            }
                            catch (Exception)
                            {
                                File.Delete(tilePath);
                            }
                        });
                    return null;
                }
            }

            // check if its a bad key
            Uri uri = GetUriForKey(key);
            if (uri != null)
            {
                // mark tile as being downloaded
                TileCache[key] = null;
                GetTileData data = new GetTileData();
                data.Renderer = renderer;
                data.Key = key;
                data.Callback = callback;
                data.State = state;
                data.Uri = uri;
                ThreadPool.QueueUserWorkItem(new WaitCallback(GetTile), data);
                //GetTile(data);
            }
            return tileData;
        }

        void CleanupTileData(GetTileData data)
        {
            using (data)
            {
                if (TileCache != null)
                {
                    TileCache[data.Key] = InvalidTile;
                    //TileCache.Remove(data.Key);
                }
            }
        }

        void ReadCallback(IAsyncResult result)
        {
            GetTileData data = (GetTileData)result.AsyncState;

            try
            {
                int read = data.ResponseStream.EndRead(result);
                if (read > 0)
                {
                    data.MemoryStream.Write(data.Buffer, 0, read);
                    data.ResponseStream.BeginRead(data.Buffer, 0, data.Buffer.Length, new AsyncCallback(ReadCallback), data);
                }
                else
                {
                    using (data)
                    {
                        string tilePath = GetTilePathForKey(data.Key);
                        using (FileStream file = new FileStream(tilePath, FileMode.Create, FileAccess.Write))
                        {
                            file.Write(data.MemoryStream.GetBuffer(), 0, (int)data.MemoryStream.Length);
                        }

                        data.MemoryStream.Seek(0, SeekOrigin.Begin);
                        IMapDrawable pbitmap = data.Renderer.GetBitmapFromStream(this, data.MemoryStream);
                        if (pbitmap == null)
                            throw new Exception();
                        TileCache[data.Key] = new TileData(pbitmap);
                        data.Callback(data.State);
                    }
                }
            }
            catch (Exception e)
            {
                CleanupTileData(data);
            }
        }

        void GetResponseCallback(IAsyncResult result)
        {
            GetTileData data = (GetTileData)result.AsyncState;
            try
            {
                data.Response = data.Request.EndGetResponse(result) as HttpWebResponse;
                data.MemoryStream = new MemoryStream();
                data.ResponseStream = data.Response.GetResponseStream();
                data.Buffer = new byte[1 << 16];

                data.ResponseStream.BeginRead(data.Buffer, 0, data.Buffer.Length, new AsyncCallback(ReadCallback), data);
            }
            catch (Exception e)
            {
                CleanupTileData(data);
            }
        }

        void GetTile(GetTileData data)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(GetUriForKey(data.Key));
                request.Timeout = 15000;
                request.Method = "GET";
                request.UserAgent = "Windows-RSS-Platform/1.0 (MSIE 7.0; Windows NT 5.1)";
                data.Request = request;
                request.BeginGetResponse(new AsyncCallback(GetResponseCallback), data);
            }
            catch (Exception e)
            {
                CleanupTileData(data);
            }
        }

        struct GetTileData : IDisposable
        {
            public WaitCallback Callback;
            public object State;
            public HttpWebRequest Request;
            public HttpWebResponse Response;
            public MemoryStream MemoryStream;
            public Stream ResponseStream;
            public byte[] Buffer;
            public Key Key;
            public Uri Uri;
            public IMapRenderer Renderer;

            #region IDisposable Members

            public void Dispose()
            {
                using (Response)
                {
                    Response = null;
                }
                using (ResponseStream)
                {
                    ResponseStream = null;
                }
                using (MemoryStream)
                {
                    MemoryStream = null;
                }
                Request = null;
                Buffer = null;
                Callback = null;
                State = null;
            }

            #endregion
        }

        void GetTile(object o)
        {
            using (GetTileData data = (GetTileData)o)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(data.Uri);
                    request.Timeout = 15000;
                    request.Method = "GET";
                    request.UserAgent = "Windows-RSS-Platform/1.0 (MSIE 7.0; Windows NT 5.1)";

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                            throw new Exception("Error while downloading tile.");

                        if (TileCache != null)
                        {
                            using (Stream s = response.GetResponseStream())
                            {
                                using (MemoryStream mem = new MemoryStream())
                                {
                                    int read = 0;
                                    byte[] buffer = new byte[10000];
                                    do
                                    {
                                        read = s.Read(buffer, 0, buffer.Length);
                                        mem.Write(buffer, 0, read);
                                    }
                                    while (read != 0);
                                    mem.Seek(0, SeekOrigin.Begin);

                                    string tilePath = GetTilePathForKey(data.Key);
                                    using (FileStream file = new FileStream(tilePath, FileMode.Create, FileAccess.Write))
                                    {
                                        file.Write(mem.GetBuffer(), 0, (int)mem.Length);
                                    }

                                    IMapDrawable pbitmap = data.Renderer.GetBitmapFromStream(this, mem);
                                    if (pbitmap == null)
                                        throw new Exception();
                                    TileCache[data.Key] = new TileData(pbitmap);
                                }
                            }
                            if (data.Callback != null)
                                data.Callback(data.State);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (TileCache != null)
                    {
                        //TileCache.Remove(data.Key);
                        TileCache[data.Key] = InvalidTile;
                    }
                }
            }
        }  
    }
}
