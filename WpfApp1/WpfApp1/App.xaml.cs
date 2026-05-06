using System;
using System.Windows;
using WpfApp1.Helpers;

namespace WpfApp1
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            BackupHelper.RunAutoBackup();
        }
    }
}