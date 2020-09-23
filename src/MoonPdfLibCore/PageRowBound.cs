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
using System.Linq;
using System.Text;
using System.Windows;

namespace MoonPdfLibCore
{
    internal class PageRowBound
    {
        #region Public Constructors

        public PageRowBound(Size size, double verticalOffset, double horizontalOffset)
        {
            this.Size = size;
            this.VerticalOffset = verticalOffset;
            this.HorizontalOffset = horizontalOffset;
        }

        #endregion Public Constructors

        #region Public Properties

        public double HorizontalOffset { get; private set; }
        public Size Size { get; private set; }

        public Size SizeIncludingOffset
        {
            get { return new Size(this.Size.Width + this.HorizontalOffset, this.Size.Height + this.VerticalOffset); }
        }

        public double VerticalOffset { get; private set; }

        #endregion Public Properties
    }
}