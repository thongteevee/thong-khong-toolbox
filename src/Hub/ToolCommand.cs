using System;
using System.Windows.Input;

namespace Toolbox
{
    internal sealed class ToolCommand : ICommand
    {
        private readonly Action execute;
        internal ToolCommand(Action action) { execute = action; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { execute(); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
