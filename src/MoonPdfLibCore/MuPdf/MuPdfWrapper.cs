/*! MoonPdfLibCore - Provides a WPF user control to display PDF files
Copyright (C) 2013  (see AUTHORS file)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
!*/

using MoonPdfLibCore.Helper;

/*
 * 2013 - Modified and extended version of W. Jordan's code (see AUTHORS file)
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CC = System.Runtime.InteropServices.CallingConvention;

namespace MoonPdfLibCore.MuPdf
{
    public interface IPdfSource
    { }

    internal struct BBox
    {
        #region Internal Fields

        internal int Left, Top, Right, Bottom;

        #endregion Internal Fields
    }

    internal struct Matrix
    {
        #region Internal Fields

        internal static readonly Matrix Identity = new Matrix(1, 0, 0, 1, 0, 0);
        internal float A, B, C, D, E, F;

        #endregion Internal Fields

        #region Internal Constructors

        internal Matrix(float a, float b, float c, float d, float e, float f)
        {
            this.A = a;
            this.B = b;
            this.C = c;
            this.D = d;
            this.E = e;
            this.F = f;
        }

        #endregion Internal Constructors

        #region Internal Methods

        internal static Matrix Concat(Matrix one, Matrix two)
        {
            return new Matrix(
                one.A * two.A + one.B * two.C,
                one.A * two.B + one.B * two.D,
                one.C * two.A + one.D * two.C,
                one.C * two.B + one.D * two.D,
                one.E * two.A + one.F * two.C + two.E,
                one.E * two.B + one.F * two.D + two.F);
        }

        internal static Matrix Rotate(float theta)
        {
            float s;
            float c;

            while (theta < 0)
                theta += 360;
            while (theta >= 360)
                theta -= 360;

            if (Math.Abs(0 - theta) < Single.Epsilon)
            {
                s = 0;
                c = 1;
            }
            else if (Math.Abs(90.0f - theta) < Single.Epsilon)
            {
                s = 1;
                c = 0;
            }
            else if (Math.Abs(180.0f - theta) < Single.Epsilon)
            {
                s = 0;
                c = -1;
            }
            else if (Math.Abs(270.0f - theta) < Single.Epsilon)
            {
                s = -1;
                c = 0;
            }
            else
            {
                s = (float)Math.Sin(theta * Math.PI / 180f);
                c = (float)Math.Cos(theta * Math.PI / 180f);
            }

            return new Matrix(c, s, -s, c, 0, 0);
        }

        internal static Matrix Scale(float x, float y)
        {
            return new Matrix(x, 0, 0, y, 0, 0);
        }

        internal static Matrix Shear(float h, float v)
        {
            return new Matrix(1, v, h, 1, 0, 0);
        }

        internal static Matrix Translate(float tx, float ty)
        {
            return new Matrix(1, 0, 0, 1, tx, ty);
        }

        internal Matrix RotateTo(float theta)
        {
            return Concat(this, Rotate(theta));
        }

        internal Matrix ScaleTo(float x, float y)
        {
            return Concat(this, Scale(x, y));
        }

        internal Matrix ShearTo(float x, float y)
        {
            return Concat(this, Shear(x, y));
        }

        internal Point Transform(Point p)
        {
            Point t;
            t.X = p.X * this.A + p.Y * this.C + this.E;
            t.Y = p.X * this.B + p.Y * this.D + this.F;
            return t;
        }

        internal Rectangle Transform(Rectangle rect)
        {
            Point s, t, u, v;

            if (rect.IsInfinite)
                return rect;

            s.X = rect.Left; s.Y = rect.Top;
            t.X = rect.Left; t.Y = rect.Bottom;
            u.X = rect.Right; u.Y = rect.Bottom;
            v.X = rect.Right; v.Y = rect.Top;
            s = this.Transform(s);
            t = this.Transform(t);
            u = this.Transform(u);
            v = this.Transform(v);
            rect.Left = Min4(s.X, t.X, u.X, v.X);
            rect.Top = Min4(s.Y, t.Y, u.Y, v.Y);
            rect.Right = Max4(s.X, t.X, u.X, v.X);
            rect.Bottom = Max4(s.Y, t.Y, u.Y, v.Y);
            return rect;
        }

        internal Matrix TranslateTo(float tx, float ty)
        {
            return Concat(this, Translate(tx, ty));
        }

        #endregion Internal Methods

        #region Private Methods

        private static float Max4(float a, float b, float c, float d)
        {
            return Math.Max(Math.Max(a, b), Math.Max(c, d));
        }

        private static float Min4(float a, float b, float c, float d)
        {
            return Math.Min(Math.Min(a, b), Math.Min(c, d));
        }

        #endregion Private Methods
    }

    internal struct NativePage
    {
        #region Internal Fields

        internal Matrix Ctm;
        internal Rectangle MediaBox;
        internal int Rotate;

        #endregion Internal Fields
    }

    internal struct Point
    {
        #region Internal Fields

        internal float X, Y;

        #endregion Internal Fields
    }

    internal struct Rectangle
    {
        #region Internal Fields

        internal float Left, Top, Right, Bottom;

        #endregion Internal Fields

        #region Internal Properties

        internal float Height { get { return this.Bottom - this.Top; } }
        internal bool IsEmpty { get { return Left == Right || Top == Bottom; } }
        internal bool IsInfinite { get { return Left > Right || Top > Bottom; } }
        internal float Width { get { return this.Right - this.Left; } }

        #endregion Internal Properties
    }

    public static class MuPdfWrapper
    {
        #region Public Enums

        public enum ColorSpace
        {
            Rgb,
            Bgr,
            Cmyk,
            Gray
        }

        #endregion Public Enums

        #region Public Methods

        /// <summary>
        /// Return the total number of pages for a give PDF.
        /// </summary>
        public static int CountPages(IPdfSource source, string password = null)
        {
            using (var stream = new PdfFileStream(source))
            {
                ValidatePassword(stream.Context, stream.Document, password);

                return NativeMethods.CountPages(stream.Context, stream.Document); // gets the number of pages in the document
            }
        }

        /// <summary>
        /// Extracts a PDF page as a Bitmap for a given pdf filename and a page number.
        /// </summary>
        /// <param name="pageNumber">Page number, starting at 1</param>
        /// <param name="zoomFactor">Used to get a smaller or bigger Bitmap, depending on the specified value</param>
        /// <param name="password">The password for the pdf file (if required)</param>
        public static Bitmap ExtractPage(IPdfSource source, int pageNumber, float zoomFactor = 1.0f, string password = null)
        {
            var pageNumberIndex = Math.Max(0, pageNumber - 1); // pages start at index 0

            using (var stream = new PdfFileStream(source))
            {
                ValidatePassword(stream.Context, stream.Document, password);

                IntPtr p = NativeMethods.LoadPage(stream.Context, stream.Document, pageNumberIndex); // loads the page
                var bmp = RenderPage(stream.Context, p, zoomFactor);
                NativeMethods.DropPage(stream.Context, p); // releases the resources consumed by the page

                return bmp;
            }
        }

        /// <summary>
        /// Gets the page bounds for all pages of the given PDF. If a relevant rotation is supplied, the bounds will
        /// be rotated accordingly before returning.
        /// </summary>
        /// <param name="rotation">The rotation that should be applied</param>
        /// <param name="password">The password for the pdf file (if required)</param>
        /// <returns></returns>
        public static System.Windows.Size[] GetPageBounds(IPdfSource source, ImageRotation rotation = ImageRotation.None, string password = null)
        {
            Func<double, double, System.Windows.Size> sizeCallback = (width, height) => new System.Windows.Size(width, height);

            if (rotation == ImageRotation.Rotate90 || rotation == ImageRotation.Rotate270)
                sizeCallback = (width, height) => new System.Windows.Size(height, width); // switch width and height

            using (var stream = new PdfFileStream(source))
            {
                ValidatePassword(stream.Context, stream.Document, password);

                var pageCount = NativeMethods.CountPages(stream.Context, stream.Document); // gets the number of pages in the document
                var resultBounds = new System.Windows.Size[pageCount];

                for (int i = 0; i < pageCount; i++)
                {
                    IntPtr p = NativeMethods.LoadPage(stream.Context, stream.Document, i); // loads the page
                    Rectangle pageBound = NativeMethods.BoundPage(stream.Context, p);

                    resultBounds[i] = sizeCallback(pageBound.Width, pageBound.Height);

                    NativeMethods.DropPage(stream.Context, p); // releases the resources consumed by the page
                }

                return resultBounds;
            }
        }

        public static bool NeedsPassword(IPdfSource source)
        {
            using (var stream = new PdfFileStream(source))
            {
                return NeedsPassword(stream.Context, stream.Document);
            }
        }

        #endregion Public Methods

        #region Private Methods

        private static bool NeedsPassword(IntPtr ctx, IntPtr doc)
        {
            return NativeMethods.NeedsPassword(ctx, doc) != 0;
        }

        private static Bitmap RenderPage(IntPtr context, IntPtr page, float zoomFactor)
        {
            Rectangle pageBound = NativeMethods.BoundPage(context, page);
            Matrix ctm = new Matrix();
            IntPtr pix = IntPtr.Zero;
            IntPtr dev = IntPtr.Zero;

            var currentDpi = DpiHelper.GetCurrentDpi();
            var zoomX = zoomFactor * (currentDpi.HorizontalDpi / DpiHelper.DEFAULT_DPI);
            var zoomY = zoomFactor * (currentDpi.VerticalDpi / DpiHelper.DEFAULT_DPI);

            // gets the size of the page and multiplies it with zoom factors
            int width = (int)(pageBound.Width * zoomX);
            int height = (int)(pageBound.Height * zoomY);

            // sets the matrix as a scaling matrix (zoomX,0,0,zoomY,0,0)
            ctm.A = zoomX;
            ctm.D = zoomY;

            // creates a pixmap the same size as the width and height of the page
#if UNSAFE
			pix = NativeMethods.NewPixmap (context, NativeMethods.FindDeviceColorSpace (context, ColorSpace.Rgb), width, height, IntPtr.Zero, 0);
#else
            // use BGR color space to save byte conversions
            pix = NativeMethods.NewPixmap(context, NativeMethods.FindDeviceColorSpace(context, ColorSpace.Bgr), width, height, IntPtr.Zero, 0);
#endif
            // sets white color as the background color of the pixmap
            NativeMethods.ClearPixmap(context, pix, 0xFF);

            // creates a drawing device
            var im = Matrix.Identity;
            dev = NativeMethods.NewDrawDevice(context, im, pix);
            // draws the page on the device created from the pixmap
            NativeMethods.RunPage(context, page, dev, ctm, IntPtr.Zero);

            NativeMethods.CloseDevice(context, dev);
            NativeMethods.DropDevice(context, dev); // frees the resources consumed by the device
            dev = IntPtr.Zero;

            // creates a colorful bitmap of the same size of the pixmap
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var imageData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, bmp.PixelFormat);
#if UNSAFE
			// note: unsafe conversion from pixmap to bitmap
			// without the overhead of P/Invokes, the following code can run faster than the safe-conversion code below
			unsafe { // converts the pixmap data to Bitmap data
				byte* ptrSrc = (byte*)NativeMethods.GetSamples (context, pix); // gets the rendered data from the pixmap
				byte* ptrDest = (byte*)imageData.Scan0;
				for (int y = 0; y < height; y++) {
					byte* pl = ptrDest;
					byte* sl = ptrSrc;
					for (int x = 0; x < width; x++) {
						//Swap these here instead of in MuPDF because most pdf images will be rgb or cmyk.
						//Since we are going through the pixels one by one anyway swap here to save a conversion from rgb to bgr.
						pl[2] = sl[0]; //b-r
						pl[1] = sl[1]; //g-g
						pl[0] = sl[2]; //r-b
						pl += 3;
						sl += 3;
					}
					ptrDest += imageData.Stride;
					ptrSrc += width * 3;
				}
			}
#else
            // note: Safe-conversion from pixmap to bitmap
            var source = NativeMethods.GetSamples(context, pix);
            var target = imageData.Scan0;
            for (int y = 0; y < height; y++)
            {
                // copy memory line by line
                NativeMethods.RtlMoveMemory(target, source, width * 3);
                target = (IntPtr)(target.ToInt64() + imageData.Stride);
                source = (IntPtr)(source.ToInt64() + width * 3);
            }
#endif
            bmp.UnlockBits(imageData);

            NativeMethods.DropPixmap(context, pix);
            bmp.SetResolution(currentDpi.HorizontalDpi, currentDpi.VerticalDpi);

            return bmp;
        }

        private static void ValidatePassword(IntPtr ctx, IntPtr doc, string password)
        {
            if (NeedsPassword(ctx, doc) && NativeMethods.AuthenticatePassword(ctx, doc, password) == 0)
                throw new MissingOrInvalidPdfPasswordException();
        }

        #endregion Private Methods

        #region Private Classes

        private static class NativeMethods
        {
            #region Private Fields

            private const string DLL = "MuPDFLib.dll";
            private const uint FZ_STORE_DEFAULT = 256 << 20;

            // note: modify the version number to match the FZ_VERSION definition in "fitz\version.h" file
            private const string FZ_VERSION = "1.17.0";

            #endregion Private Fields

            #region Public Methods

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_authenticate_password")]
            public static extern int AuthenticatePassword(IntPtr ctx, IntPtr doc, string password);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "pdf_bound_page")]
            public static extern Rectangle BoundPage(IntPtr ctx, IntPtr page);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_clear_pixmap_with_value")]
            public static extern void ClearPixmap(IntPtr ctx, IntPtr pix, int byteValue);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_close_device")]
            public static extern void CloseDevice(IntPtr ctx, IntPtr dev);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "pdf_count_pages")]
            public static extern int CountPages(IntPtr ctx, IntPtr doc);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_drop_device")]
            public static extern void DropDevice(IntPtr ctx, IntPtr dev);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "pdf_drop_document")]
            public static extern IntPtr DropDocument(IntPtr ctx, IntPtr doc);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_drop_page")]
            public static extern void DropPage(IntPtr ctx, IntPtr page);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_drop_pixmap")]
            public static extern void DropPixmap(IntPtr ctx, IntPtr pix);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_drop_stream")]
            public static extern IntPtr DropStream(IntPtr ctx, IntPtr stm);

            public static IntPtr FindDeviceColorSpace(IntPtr context, ColorSpace colorspace)
            {
                switch (colorspace)
                {
                    case ColorSpace.Rgb: return GetRgbColorSpace(context);
                    case ColorSpace.Bgr: return GetBgrColorSpace(context);
                    case ColorSpace.Cmyk: return GetCmykColorSpace(context);
                    case ColorSpace.Gray: return GetGrayColorSpace(context);
                    default: throw new NotImplementedException(colorspace + " not supported.");
                }
            }

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_device_bgr")]
            public static extern IntPtr GetBgrColorSpace(IntPtr ctx);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_device_cmyk")]
            public static extern IntPtr GetCmykColorSpace(IntPtr ctx);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_device_gray")]
            public static extern IntPtr GetGrayColorSpace(IntPtr ctx);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_device_rgb")]
            public static extern IntPtr GetRgbColorSpace(IntPtr ctx);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_pixmap_samples")]
            public static extern IntPtr GetSamples(IntPtr ctx, IntPtr pix);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "pdf_load_page")]
            public static extern IntPtr LoadPage(IntPtr ctx, IntPtr doc, int pageNumber);

            [DllImport(DLL, EntryPoint = "fz_needs_password", CallingConvention = CallingConvention.Cdecl)]
            public static extern int NeedsPassword(IntPtr ctx, IntPtr doc);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_new_draw_device")]
            public static extern IntPtr NewDrawDevice(IntPtr ctx, Matrix matrix, IntPtr pix);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_new_pixmap")]
            public static extern IntPtr NewPixmap(IntPtr ctx, IntPtr colorspace, int width, int height, IntPtr separation, int alpha);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "pdf_open_document_with_stream")]
            public static extern IntPtr OpenDocumentStream(IntPtr ctx, IntPtr stm);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_open_file_w", CharSet = CharSet.Unicode)]
            public static extern IntPtr OpenFile(IntPtr ctx, string fileName);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_open_memory", CharSet = CharSet.Unicode)]
            public static extern IntPtr OpenStream(IntPtr ctx, IntPtr data, int len);

            [DllImport("kernel32.dll")]
            public static extern void RtlMoveMemory(IntPtr dest, IntPtr src, int byteCount);

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "pdf_run_page")]
            public static extern void RunPage(IntPtr ctx, IntPtr page, IntPtr dev, Matrix transform, IntPtr cookie);

            #endregion Public Methods

            #region Internal Methods

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_drop_context")]
            internal static extern void DropContext(IntPtr ctx);

            internal static IntPtr NewContext()
            {
                var c = NewContext(IntPtr.Zero, IntPtr.Zero, FZ_STORE_DEFAULT, FZ_VERSION);
                if (c == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Version number mismatch.");
                }
                return c;
            }

            #endregion Internal Methods

            #region Private Methods

            [DllImport(DLL, CallingConvention = CC.Cdecl, EntryPoint = "fz_new_context_imp", BestFitMapping = false)]
            private static extern IntPtr NewContext(IntPtr alloc, IntPtr locks, uint max_store, [MarshalAs(UnmanagedType.LPStr)] string fz_version);

            #endregion Private Methods
        }

        /// <summary>
        /// Helper class for an easier disposing of unmanaged resources
        /// </summary>
        private sealed class PdfFileStream : IDisposable
        {
            #region Private Fields

            private const uint FZ_STORE_DEFAULT = 256 << 20;

            #endregion Private Fields

            #region Public Constructors

            public PdfFileStream(IPdfSource source)
            {
                if (source is FileSource fs)
                {
                    Context = NativeMethods.NewContext(); // Creates the context
                    Stream = NativeMethods.OpenFile(Context, fs.Filename); // opens file as a stream
                    Document = NativeMethods.OpenDocumentStream(Context, Stream); // opens the document
                }
                else if (source is MemorySource ms)
                {
                    Context = NativeMethods.NewContext(); // Creates the context
                    GCHandle pinnedArray = GCHandle.Alloc(ms.Bytes, GCHandleType.Pinned);
                    IntPtr pointer = pinnedArray.AddrOfPinnedObject();
                    Stream = NativeMethods.OpenStream(Context, pointer, ms.Bytes.Length); // opens file as a stream
                    Document = NativeMethods.OpenDocumentStream(Context, Stream); // opens the document
                    pinnedArray.Free();
                }
            }

            #endregion Public Constructors

            #region Public Properties

            public IntPtr Context { get; private set; }
            public IntPtr Document { get; private set; }
            public IntPtr Stream { get; private set; }

            #endregion Public Properties

            #region Public Methods

            public void Dispose()
            {
                NativeMethods.DropDocument(Context, Document); // releases the resources
                NativeMethods.DropStream(Context, Stream);
                NativeMethods.DropContext(Context);
            }

            #endregion Public Methods
        }

        #endregion Private Classes
    }

#pragma warning disable 0649
#pragma warning restore 0649

    public class FileSource : IPdfSource
    {
        #region Public Constructors

        public FileSource(string filename)
        {
            this.Filename = filename;
        }

        #endregion Public Constructors

        #region Public Properties

        public string Filename { get; private set; }

        #endregion Public Properties
    }

    public class MemorySource : IPdfSource
    {
        #region Public Constructors

        public MemorySource(byte[] bytes)
        {
            this.Bytes = bytes;
        }

        #endregion Public Constructors

        #region Public Properties

        public byte[] Bytes { get; private set; }

        #endregion Public Properties
    }

    public class MissingOrInvalidPdfPasswordException : Exception
    {
        #region Public Constructors

        public MissingOrInvalidPdfPasswordException()
            : base("A password for the pdf document was either not provided or is invalid.")
        { }

        #endregion Public Constructors
    }
}