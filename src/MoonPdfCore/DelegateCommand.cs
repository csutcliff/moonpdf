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
    public class DelegateCommand : BaseCommand
    {
        #region Private Fields

        private Predicate<object> canExecuteFunc;
        private Action<object> executeAction;

        #endregion Private Fields

        #region Public Constructors

        public DelegateCommand(string name, Action<object> executeAction, Predicate<object> canExecuteFunc, InputGesture inputGesture)
            : base(name, inputGesture)
        {
            this.executeAction = executeAction;
            this.canExecuteFunc = canExecuteFunc;
        }

        #endregion Public Constructors

        #region Public Methods

        public override bool CanExecute(object parameter)
        {
            return this.canExecuteFunc != null ? this.canExecuteFunc(parameter) : true;
        }

        public override void Execute(object parameter)
        {
            this.executeAction(parameter);
        }

        #endregion Public Methods
    }
}