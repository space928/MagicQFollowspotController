using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MidiApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {

            if (e.Args.Length >= 1)
            {
                MidiApp.MainWindow.serverIP_Addres = e.Args[0];

                if (e.Args.Length >= 2)
                {
                    MidiApp.MainWindow.clientID = Int32.Parse (e.Args[1]);
                }
            }
            else
            {
                MessageBox.Show("No Server IP specified on command line.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
                Environment.Exit(0);
            }
        }
    }
}
