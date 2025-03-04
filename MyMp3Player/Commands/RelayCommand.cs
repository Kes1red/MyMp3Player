using System;
using System.Windows.Input;

namespace MyMp3Player.Commands
{
    /// <summary>
    /// Простая реализация ICommand для обработки команд в WPF приложении
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        /// <summary>
        /// Создает новый экземпляр команды
        /// </summary>
        /// <param name="execute">Действие, которое будет выполнено при вызове команды</param>
        /// <param name="canExecute">Функция, определяющая, может ли команда быть выполнена</param>
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Определяет, может ли команда быть выполнена
        /// </summary>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        /// <summary>
        /// Выполняет команду
        /// </summary>
        public void Execute(object parameter)
        {
            _execute();
        }

        /// <summary>
        /// Событие, которое вызывается при изменении возможности выполнения команды
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}