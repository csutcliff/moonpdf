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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MoonPdfLibCore.MuPdf;
using MoonPdfLibCore.Helper;
using System.Windows.Threading;
using System.ComponentModel;

namespace MoonPdfLibCore
{
    public partial class MoonPdfPanel : UserControl
    {
        #region Public Fields

        public static readonly DependencyProperty MaxZoomFactorProperty = DependencyProperty.Register("MaxZoomFactor", typeof(double),
                                                                            typeof(MoonPdfPanel), new FrameworkPropertyMetadata(6.0));

        public static readonly DependencyProperty MinZoomFactorProperty = DependencyProperty.Register("MinZoomFactor", typeof(double),
                                                                            typeof(MoonPdfPanel), new FrameworkPropertyMetadata(0.15));

        public static readonly DependencyProperty PageMarginProperty = DependencyProperty.Register("PageMargin", typeof(Thickness),
                                                                            typeof(MoonPdfPanel), new FrameworkPropertyMetadata(new Thickness(0, 2, 4, 2)));

        public static readonly DependencyProperty PageRowDisplayProperty = DependencyProperty.Register("PageRowDisplay", typeof(PageRowDisplayType),
                                                                            typeof(MoonPdfPanel), new FrameworkPropertyMetadata(PageRowDisplayType.SinglePageRow));

        public static readonly DependencyProperty RotationProperty = DependencyProperty.Register("Rotation", typeof(ImageRotation),
                                                                            typeof(MoonPdfPanel), new FrameworkPropertyMetadata(ImageRotation.None));

        public static readonly DependencyProperty ViewTypeProperty = DependencyProperty.Register("ViewType", typeof(ViewType),
                                                                            typeof(MoonPdfPanel), new FrameworkPropertyMetadata(MoonPdfLibCore.ViewType.SinglePage));

        public static readonly DependencyProperty ZoomStepProperty = DependencyProperty.Register("ZoomStep", typeof(double),
                                                                            typeof(MoonPdfPanel), new FrameworkPropertyMetadata(0.25));

        #endregion Public Fields

        #region Private Fields

        private IMoonPdfPanel innerPanel;

        private MoonPdfPanelInputHandler inputHandler;

        private PageRowBound[] pageRowBounds;

        private DispatcherTimer resizeTimer;

        private ZoomType zoomType = ZoomType.Fixed;

        #endregion Private Fields

        #region Public Constructors

        public MoonPdfPanel()
        {
            InitializeComponent();

            this.ChangeDisplayType(this.PageRowDisplay);
            this.inputHandler = new MoonPdfPanelInputHandler(this);

            this.SizeChanged += PdfViewerPanel_SizeChanged;

            resizeTimer = new DispatcherTimer();
            resizeTimer.Interval = TimeSpan.FromMilliseconds(150);
            resizeTimer.Tick += resizeTimer_Tick;
        }

        #endregion Public Constructors

        #region Public Events

        public event EventHandler PageRowDisplayChanged;

        public event EventHandler<PasswordRequiredEventArgs> PasswordRequired;

        public event EventHandler PdfLoaded;

        public event EventHandler ViewTypeChanged;

        public event EventHandler ZoomTypeChanged;

        #endregion Public Events

        #region Public Properties

        public string CurrentPassword { get; private set; }

        public IPdfSource CurrentSource { get; private set; }

        public float CurrentZoom
        {
            get { return this.innerPanel.CurrentZoom; }
        }

        public double HorizontalMargin { get { return this.PageMargin.Right; } }

        public double MaxZoomFactor
        {
            get { return (double)GetValue(MaxZoomFactorProperty); }
            set { SetValue(MaxZoomFactorProperty, value); }
        }

        public double MinZoomFactor
        {
            get { return (double)GetValue(MinZoomFactorProperty); }
            set { SetValue(MinZoomFactorProperty, value); }
        }

        public Thickness PageMargin
        {
            get { return (Thickness)GetValue(PageMarginProperty); }
            set { SetValue(PageMarginProperty, value); }
        }

        public PageRowDisplayType PageRowDisplay
        {
            get { return (PageRowDisplayType)GetValue(PageRowDisplayProperty); }
            set { SetValue(PageRowDisplayProperty, value); }
        }

        public ImageRotation Rotation
        {
            get { return (ImageRotation)GetValue(RotationProperty); }
            set { SetValue(RotationProperty, value); }
        }

        public int TotalPages { get; private set; }

        public MoonPdfLibCore.ViewType ViewType
        {
            get { return (MoonPdfLibCore.ViewType)GetValue(ViewTypeProperty); }
            set { SetValue(ViewTypeProperty, value); }
        }

        public double ZoomStep
        {
            get { return (double)GetValue(ZoomStepProperty); }
            set { SetValue(ZoomStepProperty, value); }
        }

        public ZoomType ZoomType
        {
            get { return this.zoomType; }
            private set
            {
                if (this.zoomType != value)
                {
                    this.zoomType = value;

                    if (ZoomTypeChanged != null)
                        ZoomTypeChanged(this, EventArgs.Empty);
                }
            }
        }

        #endregion Public Properties

        #region Internal Properties

        internal PageRowBound[] PageRowBounds { get { return this.pageRowBounds; } }

        internal ScrollViewer ScrollViewer
        {
            get { return this.innerPanel.ScrollViewer; }
        }

        #endregion Internal Properties

        #region Public Methods

        public int GetCurrentPageNumber()
        {
            if (this.innerPanel == null)
                return -1;

            return this.innerPanel.GetCurrentPageIndex(this.ViewType) + 1;
        }

        public void GotoFirstPage()
        {
            this.GotoPage(1);
        }

        public void GotoLastPage()
        {
            this.GotoPage(this.TotalPages);
        }

        public void GotoNextPage()
        {
            this.innerPanel.GotoNextPage();
        }

        public void GotoPage(int pageNumber)
        {
            this.innerPanel.GotoPage(pageNumber);
        }

        public void GotoPreviousPage()
        {
            this.innerPanel.GotoPreviousPage();
        }

        public void Open(IPdfSource source, string password = null)
        {
            var pw = password;

            if (this.PasswordRequired != null && MuPdfWrapper.NeedsPassword(source) && pw == null)
            {
                var e = new PasswordRequiredEventArgs();
                this.PasswordRequired(this, e);

                if (e.Cancel)
                    return;

                pw = e.Password;
            }

            this.LoadPdf(source, pw);
            this.CurrentSource = source;
            this.CurrentPassword = pw;

            if (this.PdfLoaded != null)
                this.PdfLoaded(this, EventArgs.Empty);
        }

        public void OpenFile(string pdfFilename, string password = null)
        {
            if (!File.Exists(pdfFilename))
                throw new FileNotFoundException(string.Empty, pdfFilename);

            this.Open(new FileSource(pdfFilename), password);
        }

        public void Rotate(ImageRotation rotation)
        {
            var currentPage = this.innerPanel.GetCurrentPageIndex(this.ViewType) + 1;
            this.LoadPdf(this.CurrentSource, this.CurrentPassword);
            this.innerPanel.GotoPage(currentPage);
        }

        public void RotateLeft()
        {
            if ((int)this.Rotation > 0)
                this.Rotation = (ImageRotation)this.Rotation - 1;
            else
                this.Rotation = ImageRotation.Rotate270;
        }

        public void RotateRight()
        {
            if (this.Rotation != ImageRotation.Rotate270)
                this.Rotation = (ImageRotation)this.Rotation + 1;
            else
                this.Rotation = ImageRotation.None;
        }

        /// <summary>
        /// Sets the ZoomType back to Fixed
        /// </summary>
        public void SetFixedZoom()
        {
            this.ZoomType = MoonPdfLibCore.ZoomType.Fixed;
        }

        public void TogglePageDisplay()
        {
            this.PageRowDisplay = (this.PageRowDisplay == PageRowDisplayType.SinglePageRow) ? PageRowDisplayType.ContinuousPageRows : PageRowDisplayType.SinglePageRow;
        }

        public void Unload()
        {
            this.CurrentSource = null;
            this.CurrentPassword = null;
            this.TotalPages = 0;

            this.innerPanel.Unload();

            if (this.PdfLoaded != null)
                this.PdfLoaded(this, EventArgs.Empty);
        }

        public void Zoom(double zoomFactor)
        {
            this.innerPanel.Zoom(zoomFactor);
            this.ZoomType = ZoomType.Fixed;
        }

        public void ZoomIn()
        {
            this.innerPanel.ZoomIn();
            this.ZoomType = ZoomType.Fixed;
        }

        public void ZoomOut()
        {
            this.innerPanel.ZoomOut();
            this.ZoomType = ZoomType.Fixed;
        }

        public void ZoomToHeight()
        {
            this.innerPanel.ZoomToHeight();
            this.ZoomType = ZoomType.FitToHeight;
        }

        public void ZoomToWidth()
        {
            this.innerPanel.ZoomToWidth();
            this.ZoomType = MoonPdfLibCore.ZoomType.FitToWidth;
        }

        #endregion Public Methods

        #region Internal Methods

        internal int GetPagesPerRow()
        {
            return this.ViewType == MoonPdfLibCore.ViewType.SinglePage ? 1 : 2;
        }

        #endregion Internal Methods

        #region Protected Methods

        /// <summary>
        /// Will only be triggered if the AllowDrop-Property is set to true
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                var filename = filenames.FirstOrDefault();

                if (filename != null && File.Exists(filename))
                {
                    string pw = null;

                    if (MuPdfWrapper.NeedsPassword(new FileSource(filename)))
                    {
                        if (this.PasswordRequired == null)
                            return;

                        var args = new PasswordRequiredEventArgs();
                        this.PasswordRequired(this, args);

                        if (args.Cancel)
                            return;

                        pw = args.Password;
                    }

                    try
                    {
                        this.OpenFile(filename, pw);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format("An error occured: " + ex.Message));
                    }
                }
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property.Name.Equals("PageRowDisplay"))
                ChangeDisplayType((PageRowDisplayType)e.NewValue);
            else if (e.Property.Name.Equals("Rotation"))
                this.Rotate((ImageRotation)e.NewValue);
            else if (e.Property.Name.Equals("ViewType"))
                this.ApplyChangedViewType((ViewType)e.OldValue);
        }

        #endregion Protected Methods

        #region Private Methods

        private void ApplyChangedViewType(ViewType oldViewType)
        {
            UpdateAndReload(() => { }, oldViewType);

            if (this.ViewTypeChanged != null)
                this.ViewTypeChanged(this, EventArgs.Empty);
        }

        private PageRowBound[] CalculatePageRowBounds(Size[] singlePageBounds, ViewType viewType)
        {
            var pagesPerRow = Math.Min(GetPagesPerRow(), singlePageBounds.Length); // if multiple page-view, but pdf contains less pages than the pages per row
            var finalBounds = new List<PageRowBound>();
            var verticalBorderOffset = (this.PageMargin.Top + this.PageMargin.Bottom);

            if (viewType == MoonPdfLibCore.ViewType.SinglePage)
            {
                finalBounds.AddRange(singlePageBounds.Select(p => new PageRowBound(p, verticalBorderOffset, 0)));
            }
            else
            {
                var horizontalBorderOffset = this.HorizontalMargin;

                for (int i = 0; i < singlePageBounds.Length; i++)
                {
                    if (i == 0 && viewType == MoonPdfLibCore.ViewType.BookView)
                    {
                        finalBounds.Add(new PageRowBound(singlePageBounds[0], verticalBorderOffset, 0));
                        continue;
                    }

                    var subset = singlePageBounds.Take(i, pagesPerRow).ToArray();

                    // we get the max page-height from all pages in the subset and the sum of all page widths of the subset plus the offset between the pages
                    finalBounds.Add(new PageRowBound(new Size(subset.Sum(f => f.Width), subset.Max(f => f.Height)), verticalBorderOffset, horizontalBorderOffset * (subset.Length - 1)));
                    i += (pagesPerRow - 1);
                }
            }

            return finalBounds.ToArray();
        }

        private void ChangeDisplayType(PageRowDisplayType pageRowDisplayType)
        {
            UpdateAndReload(() =>
                {
                    // we need to remove the current innerPanel
                    this.pnlMain.Children.Clear();

                    if (pageRowDisplayType == PageRowDisplayType.SinglePageRow)
                        this.innerPanel = new SinglePageMoonPdfPanel(this);
                    else
                        this.innerPanel = new ContinuousMoonPdfPanel(this);

                    this.pnlMain.Children.Add(this.innerPanel.Instance);
                }, this.ViewType);

            if (this.PageRowDisplayChanged != null)
                this.PageRowDisplayChanged(this, EventArgs.Empty);
        }

        private void LoadPdf(IPdfSource source, string password)
        {
            var pageBounds = MuPdfWrapper.GetPageBounds(source, this.Rotation, password);
            this.pageRowBounds = CalculatePageRowBounds(pageBounds, this.ViewType);
            this.TotalPages = pageBounds.Length;
            this.innerPanel.Load(source, password);
        }

        private void PdfViewerPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.CurrentSource == null)
                return;

            resizeTimer.Stop();
            resizeTimer.Start();
        }

        private void resizeTimer_Tick(object sender, EventArgs e)
        {
            resizeTimer.Stop();

            if (this.CurrentSource == null)
                return;

            if (this.ZoomType == ZoomType.FitToWidth)
                ZoomToWidth();
            else if (this.ZoomType == ZoomType.FitToHeight)
                ZoomToHeight();
        }

        private void UpdateAndReload(Action updateAction, ViewType viewType)
        {
            var currentPage = -1;
            var zoom = 1.0f;

            if (this.CurrentSource != null)
            {
                currentPage = this.innerPanel.GetCurrentPageIndex(viewType) + 1;
                zoom = this.innerPanel.CurrentZoom;
            }

            updateAction();

            if (currentPage > -1)
            {
                Action reloadAction = () =>
                    {
                        this.LoadPdf(this.CurrentSource, this.CurrentPassword);
                        this.innerPanel.Zoom(zoom);
                        this.innerPanel.GotoPage(currentPage);
                    };

                if (this.innerPanel.Instance.IsLoaded)
                    reloadAction();
                else
                {
                    // we need to wait until the controls are loaded and then reload the pdf
                    this.innerPanel.Instance.Loaded += (s, e) => { reloadAction(); };
                }
            }
        }

        #endregion Private Methods
    }

    public class PasswordRequiredEventArgs : EventArgs
    {
        #region Public Properties

        public bool Cancel { get; set; }
        public string Password { get; set; }

        #endregion Public Properties
    }
}