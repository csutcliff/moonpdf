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
using MoonPdfLibCore.MuPdf;
using System;
using System.Collections.Generic;
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

namespace MoonPdfLibCore
{
    internal partial class SinglePageMoonPdfPanel : UserControl, IMoonPdfPanel
    {
        #region Private Fields

        private int currentPageIndex = 0;
        private PdfImageProvider imageProvider;
        private MoonPdfPanel parent;
        private ScrollViewer scrollViewer;

        #endregion Private Fields

        // starting at 0

        #region Public Constructors

        public SinglePageMoonPdfPanel(MoonPdfPanel parent)
        {
            InitializeComponent();
            this.parent = parent;
            this.SizeChanged += SinglePageMoonPdfPanel_SizeChanged;
        }

        #endregion Public Constructors

        #region Public Properties

        public float CurrentZoom
        {
            get
            {
                if (this.imageProvider != null)
                    return this.imageProvider.Settings.ZoomFactor;

                return 1.0f;
            }
        }

        UserControl IMoonPdfPanel.Instance
        {
            get { return this; }
        }

        ScrollViewer IMoonPdfPanel.ScrollViewer
        {
            get { return this.scrollViewer; }
        }

        #endregion Public Properties

        #region Public Methods

        public int GetCurrentPageIndex(ViewType viewType)
        {
            return this.currentPageIndex;
        }

        void IMoonPdfPanel.GotoNextPage()
        {
            var nextPageIndex = PageHelper.GetNextPageIndex(this.currentPageIndex, this.parent.TotalPages, this.parent.ViewType);

            if (nextPageIndex == -1)
                return;

            this.currentPageIndex = nextPageIndex;

            this.SetItemsSource();

            if (this.scrollViewer != null)
                this.scrollViewer.ScrollToTop();
        }

        void IMoonPdfPanel.GotoPage(int pageNumber)
        {
            currentPageIndex = pageNumber - 1;
            this.SetItemsSource();

            if (this.scrollViewer != null)
                this.scrollViewer.ScrollToTop();
        }

        void IMoonPdfPanel.GotoPreviousPage()
        {
            var prevPageIndex = PageHelper.GetPreviousPageIndex(this.currentPageIndex, this.parent.ViewType);

            if (prevPageIndex == -1)
                return;

            this.currentPageIndex = prevPageIndex;

            this.SetItemsSource();

            if (this.scrollViewer != null)
                this.scrollViewer.ScrollToTop();
        }

        public void Load(IPdfSource source, string password = null)
        {
            this.scrollViewer = VisualTreeHelperEx.FindChild<ScrollViewer>(this);
            this.imageProvider = new PdfImageProvider(source, this.parent.TotalPages,
                new PageDisplaySettings(this.parent.GetPagesPerRow(), this.parent.ViewType, this.parent.HorizontalMargin, this.parent.Rotation), false, password);

            currentPageIndex = 0;

            if (this.scrollViewer != null)
                this.scrollViewer.Visibility = System.Windows.Visibility.Visible;

            if (this.parent.ZoomType == ZoomType.Fixed)
                this.SetItemsSource();
            else if (this.parent.ZoomType == ZoomType.FitToHeight)
                this.ZoomToHeight();
            else if (this.parent.ZoomType == ZoomType.FitToWidth)
                this.ZoomToWidth();
        }

        public void Unload()
        {
            this.scrollViewer.Visibility = System.Windows.Visibility.Collapsed;
            this.scrollViewer.ScrollToHorizontalOffset(0);
            this.scrollViewer.ScrollToVerticalOffset(0);
            currentPageIndex = 0;

            this.imageProvider = null;
        }

        public void Zoom(double zoomFactor)
        {
            this.ZoomInternal(zoomFactor);
        }

        public void ZoomIn()
        {
            ZoomInternal(this.CurrentZoom + this.parent.ZoomStep);
        }

        public void ZoomOut()
        {
            ZoomInternal(this.CurrentZoom - this.parent.ZoomStep);
        }

        public void ZoomToHeight()
        {
            var scrollBarHeight = this.scrollViewer.ComputedHorizontalScrollBarVisibility == System.Windows.Visibility.Visible ? SystemParameters.HorizontalScrollBarHeight : 0;
            var zoomFactor = (this.parent.ActualHeight - scrollBarHeight) / this.parent.PageRowBounds[this.currentPageIndex].SizeIncludingOffset.Height;
            var pageBound = this.parent.PageRowBounds[this.currentPageIndex];

            if (scrollBarHeight == 0 && ((pageBound.Size.Width * zoomFactor) + pageBound.HorizontalOffset) >= this.parent.ActualWidth)
                scrollBarHeight += SystemParameters.HorizontalScrollBarHeight;

            zoomFactor = (this.parent.ActualHeight - scrollBarHeight) / this.parent.PageRowBounds[this.currentPageIndex].SizeIncludingOffset.Height;

            ZoomInternal(zoomFactor);
        }

        public void ZoomToWidth()
        {
            var scrollBarWidth = this.scrollViewer.ComputedVerticalScrollBarVisibility == System.Windows.Visibility.Visible ? SystemParameters.VerticalScrollBarWidth : 0;
            var zoomFactor = (this.parent.ActualWidth - scrollBarWidth) / this.parent.PageRowBounds[this.currentPageIndex].SizeIncludingOffset.Width;
            var pageBound = this.parent.PageRowBounds[this.currentPageIndex];

            if (scrollBarWidth == 0 && ((pageBound.Size.Height * zoomFactor) + pageBound.VerticalOffset) >= this.parent.ActualHeight)
                scrollBarWidth += SystemParameters.VerticalScrollBarWidth;

            scrollBarWidth += 2; // Magic number, sorry :)
            zoomFactor = (this.parent.ActualWidth - scrollBarWidth) / this.parent.PageRowBounds[this.currentPageIndex].SizeIncludingOffset.Width;

            ZoomInternal(zoomFactor);
        }

        #endregion Public Methods

        #region Private Methods

        private void SetItemsSource()
        {
            var startIndex = PageHelper.GetVisibleIndexFromPageIndex(this.currentPageIndex, this.parent.ViewType);
            this.itemsControl.ItemsSource = this.imageProvider.FetchRange(startIndex, this.parent.GetPagesPerRow()).FirstOrDefault();
        }

        private void SinglePageMoonPdfPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.scrollViewer = VisualTreeHelperEx.FindChild<ScrollViewer>(this);
        }

        private void ZoomInternal(double zoomFactor)
        {
            if (zoomFactor > this.parent.MaxZoomFactor)
                zoomFactor = this.parent.MaxZoomFactor;
            else if (zoomFactor < this.parent.MinZoomFactor)
                zoomFactor = this.parent.MinZoomFactor;

            this.imageProvider.Settings.ZoomFactor = (float)zoomFactor;

            this.SetItemsSource();
        }

        #endregion Private Methods
    }
}