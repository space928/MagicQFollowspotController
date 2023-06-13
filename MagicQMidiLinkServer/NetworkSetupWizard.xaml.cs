using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MidiApp
{
    /// <summary>
    /// Interaction logic for NetworkSetupWizard.xaml
    /// </summary>
    public partial class NetworkSetupWizard : Window
    {
        public ObservableCollection<string> Networks { get; init; }
        private (NetworkInterface adapter, UnicastIPAddressInformation ip)[] networkInterfaces;

        public NetworkSetupWizard()
        {
            InitializeComponent();

            Networks = new ObservableCollection<string>();
            FillNetworkInterfaces();
            ComboBoxMagicQIP.ItemsSource = Networks;
            ComboBoxArtnetRXIP.ItemsSource = Networks;
            ComboBoxArtnetTXIP.ItemsSource = Networks;

            ComboBoxMagicQIP.SelectedIndex = IpToIndex(MainWindow.AppResources.network.magicQIP);
            ComboBoxArtnetRXIP.SelectedIndex = IpToIndex(MainWindow.AppResources.network.artNet.rxIP);
            ComboBoxArtnetTXIP.SelectedIndex = IpToIndex(MainWindow.AppResources.network.artNet.txIP);
            TextBoxUniverse.Text = MainWindow.AppResources.network.artNet.universe.ToString();
            CheckBoxBroadcast.IsChecked = MainWindow.AppResources.network.artNet.broadcast;
            TextBoxOSCRXPort.Text = MainWindow.AppResources.network.oscRXPort.ToString();
            TextBoxOSCTXPort.Text = MainWindow.AppResources.network.oscTXPort.ToString();
        }

        private int IpToIndex(string ip)
        {
            for(int i = 0; i < networkInterfaces.Length; i++)
            {
                if (networkInterfaces[i].ip.Address == IPAddress.Parse(ip))
                    return i;
            }
            return -1;
        }

        private string IndexToIp(int index, string defaultIp)
        {
            if(index >= 0 && index < networkInterfaces.Length)
                return networkInterfaces[index].ip.Address.ToString();
            return defaultIp;
        }

        private string IndexToMask(int index, string defaultIp)
        {
            if (index >= 0 && index < networkInterfaces.Length)
                return networkInterfaces[index].ip.IPv4Mask.ToString();
            return defaultIp;
        }

        private void FillNetworkInterfaces()
        {
            //MainWindow.AppResources.network
            networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.OperationalStatus == OperationalStatus.Up
                            && x.SupportsMulticast)
                .SelectMany(x => x.GetIPProperties().UnicastAddresses
                .Where(y=>y.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(y=>(x, y)))
                .ToArray();

            foreach (var network in networkInterfaces)
            {
                Networks.Add($"{network.adapter.Name} (ip={network.ip.Address}) (mask={network.ip.IPv4Mask})");
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            // Copy all the parameters back to the app resources
            if (!byte.TryParse(TextBoxUniverse.Text, out byte universe))
            {
                Logger.Log("Invalid universe specified!", Severity.WARNING);
                MessageBox.Show("Invalid universe specified!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(TextBoxOSCRXPort.Text, out int oscTXPort))
            {
                Logger.Log("Invalid osc rx port specified!", Severity.WARNING);
                MessageBox.Show("Invalid osc rx port specified!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(TextBoxOSCTXPort.Text, out int oscRXPort))
            {
                Logger.Log("Invalid osc tx port specified!", Severity.WARNING);
                MessageBox.Show("Invalid osc tx port specified!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NetworkSettings networkSettings = new() 
            {
                magicQIP = IndexToIp(ComboBoxMagicQIP.SelectedIndex, MainWindow.AppResources.network.magicQIP),
                artNet = new ArtNetSettings()
                {
                    rxIP = IndexToIp(ComboBoxArtnetRXIP.SelectedIndex, MainWindow.AppResources.network.artNet.rxIP),
                    txIP = IndexToIp(ComboBoxArtnetTXIP.SelectedIndex, MainWindow.AppResources.network.artNet.txIP),
                    rxSubNetMask = IndexToMask(ComboBoxArtnetRXIP.SelectedIndex, MainWindow.AppResources.network.artNet.rxIP),
                    txSubNetMask = IndexToMask(ComboBoxArtnetTXIP.SelectedIndex, MainWindow.AppResources.network.artNet.txIP),
                    broadcast = CheckBoxBroadcast.IsChecked ?? false,
                    universe = universe,
                },
                oscRXPort = oscRXPort,
                oscTXPort = oscTXPort
            };

            // Force save app resources as well
            try
            {
                ((MainWindow)App.Current.MainWindow).SetNetworkSettings(networkSettings);
            } catch(Exception ex)
            {
                Logger.Log("Failed to set network settings!\n" + ex, Severity.WARNING);
                MessageBox.Show("Failed to set network settings!\n" + ex, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
