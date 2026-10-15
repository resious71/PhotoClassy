using System.Windows;
using System.Windows.Threading;

namespace PhotoClassy
{
    public partial class App : Application
    {
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"예기치 않은 오류가 발생했습니다.\n\n{e.Exception.Message}", "PhotoClassy",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
