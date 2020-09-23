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
using System.Windows.Input;

namespace MoonPdfCore
{
    public abstract class BaseCommand : ICommand
    {
        #region Private Fields

        private static Dictionary<Key, string> keyReplacements = new Dictionary<Key, string>()
        {
            {Key.Add, "+"},
            {Key.Subtract, "-"}
        };

        private static Dictionary<ModifierKeys, string> modifierText = new Dictionary<ModifierKeys, string>()
        {
            {ModifierKeys.None,""},
            {ModifierKeys.Control,"Ctrl+"},
            {ModifierKeys.Control|ModifierKeys.Shift,"Ctrl+Shift+"},
            {ModifierKeys.Control|ModifierKeys.Alt,"Ctrl+Alt+"},
            {ModifierKeys.Control|ModifierKeys.Shift|ModifierKeys.Alt,"Ctrl+Shift+Alt+"},
            {ModifierKeys.Windows,"Windows+"}
        };

        #endregion Private Fields

        #region Protected Constructors

        protected BaseCommand(string name, InputGesture inputGesture)
        {
            this.Name = name;

            if (inputGesture != null)
            {
                this.InputBinding = new System.Windows.Input.InputBinding(this, inputGesture);

                if (inputGesture is KeyGesture)
                {
                    var kg = (KeyGesture)inputGesture;
                    var keyText = keyReplacements.ContainsKey(kg.Key) ? keyReplacements[kg.Key] : kg.Key.ToString();

                    this.GestureText = modifierText[kg.Modifiers] + keyText;
                }
            }
        }

        #endregion Protected Constructors

        #region Public Events

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        #endregion Public Events

        #region Public Properties

        public string GestureText { get; private set; }
        public InputBinding InputBinding { get; set; }
        public string Name { get; private set; }

        #endregion Public Properties

        #region Public Methods

        public abstract bool CanExecute(object parameter);

        public abstract void Execute(object parameter);

        #endregion Public Methods
    }
}