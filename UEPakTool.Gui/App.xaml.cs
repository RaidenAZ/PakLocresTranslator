using System.Windows;
using System.Windows.Threading;
using UEPakTool.Core;

namespace UEPakTool.Gui;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局未处理异常捕获
        DispatcherUnhandledException += (s, args) =>
        {
            DebugLog.Write($"!!! UI线程异常: {args.Exception.GetType().Name}: {args.Exception.Message}\n{args.Exception.StackTrace}");
            MessageBox.Show($"未处理的异常:\n{args.Exception.Message}\n\n详情已写入日志文件。", "程序错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown();
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            DebugLog.Write($"!!! AppDomain异常: {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}");
            if (args.IsTerminating)
                DebugLog.Write("!!! AppDomain 即将终止进程");
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            DebugLog.Write($"!!! Task异常: {args.Exception.GetType().Name}: {args.Exception.Message}\n{args.Exception.StackTrace}");
            args.SetObserved();
        };
    }
}
