using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace ytdown;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        PrependBundledToolsToPath();
        base.OnStartup(e);

        Dispatcher.BeginInvoke(async () =>
        {
            // StartupUri로 생성된 메인 창이 화면에 뜬 뒤 조용히 업데이트를 확인합니다.
            await Task.Delay(800);
            if (Current.MainWindow != null)
                await UpdateService.CheckForUpdatesAsync(Current.MainWindow);
        });
    }

    private static void PrependBundledToolsToPath()
    {
        string toolsDir = Path.Combine(AppContext.BaseDirectory, "tools");
        if (!Directory.Exists(toolsDir))
            return;

        string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string prefix = toolsDir + Path.PathSeparator;

        if (!currentPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("PATH", prefix + currentPath);
    }
}
