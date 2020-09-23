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

using MoonPdfLibCore.MuPdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MoonPdfLibCore
{
    /// <summary>
    /// Common interface for the two different display types, single pages (SinglePageMoonPdfPanel) and continuous pages (ContinuousMoonPdfPanel)
    /// </summary>
    internal interface IMoonPdfPanel
    {
        #region Public Properties

        float CurrentZoom { get; }
        UserControl Instance { get; }
        ScrollViewer ScrollViewer { get; }

        #endregion Public Properties

        #region Public Methods

        int GetCurrentPageIndex(ViewType viewType);

        void GotoNextPage();

        void GotoPage(int pageNumber);

        void GotoPreviousPage();

        void Load(IPdfSource source, string password = null);

        void Unload();

        void Zoom(double zoomFactor);

        void ZoomIn();

        void ZoomOut();

        void ZoomToHeight();

        void ZoomToWidth();

        #endregion Public Methods
    }
}