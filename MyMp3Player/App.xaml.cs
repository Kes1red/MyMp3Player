using System.Configuration;
using System.Data;
using System.Windows;

namespace MyMp3Player;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Закрываем все существующие процессы с тем же именем
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
        
        foreach (var process in processes)
        {
            if (process.Id != currentProcess.Id)
            {
                process.Kill();
            }
        }
    }
}