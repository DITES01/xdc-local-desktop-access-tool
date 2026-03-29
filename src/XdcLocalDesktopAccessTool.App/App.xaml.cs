using System;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace XdcLocalDesktopAccessTool.App
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            mainWindow.Show();
            mainWindow.Activate();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowFatal(e.Exception, "Unhandled UI Exception");
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception.");
            ShowFatal(ex, "Unhandled Domain Exception");
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            ShowFatal(e.Exception, "Unobserved Task Exception");
            e.SetObserved();
        }

        private static void ShowFatal(Exception ex, string title)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("The application hit an unexpected error.");
                sb.AppendLine();
                sb.AppendLine("Message:");
                sb.AppendLine(ex.Message);
                sb.AppendLine();
                sb.AppendLine("Type:");
                sb.AppendLine(ex.GetType().FullName ?? "Unknown");
                sb.AppendLine();
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace ?? "(no stack trace)");
                sb.AppendLine();

                if (ex.InnerException != null)
                {
                    sb.AppendLine("Inner Exception:");
                    sb.AppendLine(ex.InnerException.GetType().FullName ?? "Unknown");
                    sb.AppendLine(ex.InnerException.Message);
                    sb.AppendLine(ex.InnerException.StackTrace ?? "(no inner stack trace)");
                }

                MessageBox.Show(
                    sb.ToString(),
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                MessageBox.Show("A fatal error occurred.", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
