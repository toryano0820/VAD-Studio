using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "service_account.json"),  EnvironmentVariableTarget.Process);
        }
    }
}
