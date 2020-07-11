using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime;
using static VAD.Utils;

namespace VAD
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string AppDir = AppDomain.CurrentDomain.BaseDirectory;

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Initialize();
            base.OnStartup(e);
        }
    }
}
