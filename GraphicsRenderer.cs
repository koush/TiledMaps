using System;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TiledMaps
{
    public class GraphicsRenderer : IMapRenderer
    {
        public Graphics Graphics
        {
            get;
            set;
        }

        SolidBrush myBrush;
        Pen myPen;

        #region IMapRenderer Members

        public void Draw(IMapDrawable drawable, Rectangle destRect, Rectangle sourceRect)
        {
            IGraphicsDrawable graphicsDrawable = drawable as IGraphicsDrawable;
            graphicsDrawable.Draw(Graphics, destRect, sourceRect);
        }

        public void FillRectangle(Color color, Rectangle rect)
        {
            if (myBrush == null || myBrush.Color != color)
                myBrush = new SolidBrush(color);
            Graphics.FillRectangle(myBrush, rect);
        }

        public void DrawLines(float lineWidth, Color color, Point[] points)
        {
            if (myPen == null || myPen.Color != color)
                myPen = new Pen(color);
            Graphics.DrawLines(myPen, points);
        }

        #endregion

        public IMapDrawable LoadBitmap(Stream stream)
        {
            return LoadBitmap(stream, true);
        }

        public IMapDrawable LoadBitmap(Stream stream, bool hasAlpha)
        {
            if (hasAlpha && Environment.OSVersion.Platform == PlatformID.WinCE)
                return new WinCEImagingBitmap(stream);
            return new StandardBitmap(stream);
        }

        #region IDisposable Members

        public void Dispose()
        {
        }

        public IMapDrawable GetBitmapFromStream(TiledMapSession session, Stream stream)
        {
            return LoadBitmap(stream, session.HasAlpha);
        }

        #endregion
    }

    /// <summary>
    /// IGraphicsDrawable is a type of IMapDrawable that can draw to a
    /// System.Drawing.Graphics instance.
    /// </summary>
    public interface IGraphicsDrawable : IMapDrawable
    {
        void Draw(Graphics graphics, Rectangle destRect, Rectangle sourceRect);
    }

    public class TextMapDrawable : IGraphicsDrawable
    {
        static Bitmap myMeasureBitmap = new Bitmap(1, 1, PixelFormat.Format16bppRgb565);
        static Graphics myMeasureGraphics = Graphics.FromImage(myMeasureBitmap);

        public float MaxWidth
        {
            get;
            set;
        }

        public float MaxHeight
        {
            get;
            set;
        }

        Brush myBrush;
        public Brush Brush
        {
            get
            {
                return myBrush;
            }
            set
            {
                myBrush = value;
            }
        }

        bool myDirty = true;
        string myText;
        public string Text
        {
            get
            {
                return myText;
            }
            set
            {
                myText = value;
                myDirty = true;
            }
        }

        Font myFont;
        public Font Font
        {
            get
            {
                return myFont;
            }
            set
            {
                myDirty = true;
                myFont = value;
            }
        }

        #region IGraphicsBitmap Members

        public void Draw(Graphics graphics, Rectangle destRect, Rectangle sourceRect)
        {
            // just ignore source rect, doesn't mean anything in this context.
            if (CalculateDimensions() && myBrush != null)
                graphics.DrawString(myText, myFont, myBrush, destRect.X, destRect.Y);
        }

        #endregion

        bool CalculateDimensions()
        {
            bool valid = !string.IsNullOrEmpty(myText) && myFont != null;

            if (myDirty)
            {
                myDirty = false;
                if (valid)
                {
                    SizeF size = myMeasureGraphics.MeasureString(myText, myFont);
                    myWidth = (int)Math.Ceiling(size.Width);
                    myHeight = (int)Math.Ceiling(size.Height);
                }
                else
                {
                    myWidth = 0;
                    myHeight = 0;
                }
            }
            return valid;
        }

        #region IMapBitmap Members

        int myWidth;
        public int Width
        {
            get
            {
                CalculateDimensions();
                return myWidth;
            }
        }

        int myHeight;
        public int Height
        {
            get
            {
                CalculateDimensions();
                return myHeight;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
        }

        #endregion
    }

    public class WinCEImagingBitmap : IGraphicsDrawable
    {
        IImage myImage;
        ImageInfo myInfo;
        double myScaleFactorX = 0;
        double myScaleFactorY = 0;
        IntPtr myBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RECT)));
        static IImagingFactory myImagingFactory;

        public WinCEImagingBitmap(Stream stream)
        {
            // this class should only be used in WinCE
            System.Diagnostics.Debug.Assert(Environment.OSVersion.Platform == PlatformID.WinCE);

            if (myImagingFactory == null)
                myImagingFactory = (IImagingFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("327ABDA8-072B-11D3-9D7B-0000F81EF32E")));

            int bytesLength;
            byte[] bytes;
            MemoryStream memStream = stream as MemoryStream;
            if (memStream != null)
            {
                bytesLength = (int)memStream.Length;
                bytes = memStream.GetBuffer();
            }
            else
            {
                bytesLength = (int)stream.Length;
                bytes = new byte[bytesLength];
                stream.Read(bytes, 0, bytesLength);
            }

            uint hresult = myImagingFactory.CreateImageFromBuffer(bytes, (uint)bytesLength, BufferDisposalFlag.BufferDisposalFlagNone, out myImage);
            myImage.GetImageInfo(out myInfo);
            myScaleFactorX = 1 / myInfo.Xdpi * 2540;
            myScaleFactorY = 1 / myInfo.Ydpi * 2540;

            IBitmapImage bitmap;
            myImagingFactory.CreateBitmapFromImage(myImage, 0, 0, PixelFormatID.PixelFormat32bppARGB, InterpolationHint.InterpolationHintDefault, out bitmap);
            Marshal.FinalReleaseComObject(myImage);
            myImage = bitmap as IImage;
        }

        #region IMapBitmap Members

        public int Width
        {
            get
            {
                return myInfo.Width;
            }
        }

        public int Height
        {
            get
            {
                return myInfo.Height;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (myImage != null)
            {
                Marshal.FinalReleaseComObject(myImage);
                myImage = null;
            }
            if (myBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(myBuffer);
            }
        }

        #endregion

        #region IGraphicsBitmap Members

        public void Draw(Graphics graphics, Rectangle destRect, Rectangle sourceRect)
        {
            RECT dst = new RECT(destRect);

            if (destRect == sourceRect)
            {
                IntPtr hdc = graphics.GetHdc();
                myImage.Draw(hdc, ref dst, IntPtr.Zero);
                graphics.ReleaseHdc(hdc);
            }
            else
            {
                RECT src = new RECT(sourceRect);
                src.Left = (int)(src.Left * myScaleFactorX);
                src.Top = (int)(src.Top * myScaleFactorY);
                src.Right = (int)(src.Right * myScaleFactorX);
                src.Bottom = (int)(src.Bottom * myScaleFactorY);
                Marshal.StructureToPtr(src, myBuffer, false);
                IntPtr hdc = graphics.GetHdc();
                myImage.Draw(hdc, ref dst, myBuffer);
                graphics.ReleaseHdc(hdc);
            }
        }

        #endregion
    }

    public class StandardBitmap : IGraphicsDrawable
    {
        Bitmap myBitmap;

        public Bitmap Bitmap
        {
            get { return myBitmap; }
            set { myBitmap = value; }
        }

        ImageAttributes myImageAttributes;
        public StandardBitmap(Bitmap bitmap, ImageAttributes imageAttributes)
        {
            if (Environment.OSVersion.Platform == PlatformID.WinCE)
            {
                // reduce the bpp to native WinCE resolution for performance
                myBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format16bppRgb565);
                using (Graphics g = Graphics.FromImage(myBitmap))
                {
                    g.DrawImage(bitmap, 0, 0);
                }
            }
            else
            {
                myBitmap = bitmap;
            }
            myImageAttributes = imageAttributes;
        }

        public StandardBitmap(Stream bitmapStream, ImageAttributes imageAttributes)
            : this(new Bitmap(bitmapStream), imageAttributes)
        {
        }

        public StandardBitmap(Stream bitmapStream)
            : this(bitmapStream, null)
        {
        }

        public StandardBitmap(Bitmap bitmap)
            : this(bitmap, null)
        {
        }

        #region ITileBitmap Members

        public int Width
        {
            get { return myBitmap.Width; }
        }

        public int Height
        {
            get { return myBitmap.Height; }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (myBitmap != null)
            {
                myBitmap.Dispose();
                myBitmap = null;
            }
        }

        #endregion

        #region IGraphicsBitmap Members

        public void Draw(Graphics graphics, Rectangle destRect, Rectangle sourceRect)
        {
            if (myImageAttributes == null)
                graphics.DrawImage(myBitmap, destRect, sourceRect, GraphicsUnit.Pixel);
            else
                graphics.DrawImage(myBitmap, destRect, sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height, GraphicsUnit.Pixel, myImageAttributes);
        }

        #endregion
    }
}
