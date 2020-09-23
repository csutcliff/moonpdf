/*! MoonPdfCore - A WPF-based PDF Viewer application that uses the MoonPdfLibCore library
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
using System.Windows.Input;

namespace MoonPdfCore
{
    public class FullscreenCommand : BaseCommand
    {
        #region Private Fields

        private FullscreenHandler fullscreenHandler;
        private MainWindow wnd;

        #endregion Private Fields

        #region Public Constructors

        public FullscreenCommand(string name, MainWindow wnd, InputGesture inputGesture)
            : base(name, inputGesture)
        {
            this.wnd = wnd;
            this.wnd.PreviewKeyDown += wnd_PreviewKeyDown;
            this.fullscreenHandler = new FullscreenHandler(wnd);
            this.fullscreenHandler.FullscreenChanged += fullscreenHandler_FullscreenChanged;
        }

        #endregion Public Constructors

        #region Public Methods

        public override bool CanExecute(object parameter)
        {
            return wnd.IsPdfLoaded();
        }

        public override void Execute(object parameter)
        {
            if (this.fullscreenHandler.IsFullscreen)
                this.fullscreenHandler.QuitFullscreen();
            else
                this.fullscreenHandler.StartFullscreen();
        }

        #endregion Public Methods

        #region Private Methods

        private void fullscreenHandler_FullscreenChanged(object sender, EventArgs e)
        {
            this.wnd.OnFullscreenChanged(this.fullscreenHandler.IsFullscreen);
        }

        private void wnd_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                this.fullscreenHandler.QuitFullscreen();
        }

        #endregion Private Methods

        #region Private Classes

        private class FullscreenHandler
        {
            #region Private Fields

            private bool isFullscreen;

            private Visibility oldMenuVisibility;

            private WindowState oldWindowState;

            private MainWindow window;

            #endregion Private Fields

            #region Public Constructors

            public FullscreenHandler(MainWindow window)
            {
                this.window = window;
                this.oldWindowState = window.WindowState;
                this.oldMenuVisibility = window.mainMenu.Visibility;
            }

            #endregion Public Constructors

            #region Public Events

            public event EventHandler FullscreenChanged;

            #endregion Public Events

            #region Public Properties

            public bool IsFullscreen
            {
                get { return this.isFullscreen; }
                private set
                {
                    if (value != this.isFullscreen)
                    {
                        this.isFullscreen = value;

                        if (this.FullscreenChanged != null)
                            this.FullscreenChanged(this, EventArgs.Empty);
                    }
                }
            }

            #endregion Public Properties

            #region Public Methods

            public void QuitFullscreen()
            {
                if (!this.IsFullscreen)
                    return;

                this.window.ResizeMode = System.Windows.ResizeMode.CanResize;
                this.window.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
                this.window.WindowState = this.oldWindowState;
                this.window.mainMenu.Visibility = this.oldMenuVisibility;
                this.IsFullscreen = false;
            }

            public void StartFullscreen()
            {
                if (this.IsFullscreen)
                    return;

                this.oldWindowState = this.window.WindowState;
                this.oldMenuVisibility = this.window.mainMenu.Visibility;

                if (this.window.mainMenu.Visibility == System.Windows.Visibility.Visible)
                    this.window.mainMenu.Visibility = System.Windows.Visibility.Collapsed;

                this.window.ResizeMode = System.Windows.ResizeMode.NoResize;
                this.window.WindowStyle = System.Windows.WindowStyle.None;
                this.window.WindowState = System.Windows.WindowState.Maximized;
                this.IsFullscreen = true;
            }

            #endregion Public Methods
        }

        #endregion Private Classes
    }
}