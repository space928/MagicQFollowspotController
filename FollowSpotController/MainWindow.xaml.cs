using Haukcode.ArtNet.Packets;
using Haukcode.ArtNet.Sockets;
using Haukcode.Sockets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MidiApp
{
    public partial class MainWindow : AdonisUI.Controls.AdonisWindow
    {
        public static int clientID = -1;
        public static AppResourcesData appResources;

        private static readonly List<FollowSpot> spots = new();
        private static readonly List<Marker> markers = new();
        private static string serverIPAddress;
        private ThreeD threeDWindow = null;

        private Thread artNetactivity_Thread = null;
        private ArtNetSocket m_TXsocket = null;

        private Thread mqConnection_Thread = null;

        private IPAddress ARTNET_RXIPAddress = null;
        private IPAddress ARTNET_RXSubNetMask = null;
        private int ARTNET_RXUniverse = 0;

        private string resourceFileName = @"Markers.json";

        private Brush GreenFill = null;
        private Brush RedFill = null;
        private Brush WhiteFill = null;
        private Stopwatch artNetactivityTimer = Stopwatch.StartNew();
        private Stopwatch connectionTimer = Stopwatch.StartNew();

        private System.Timers.Timer smoothingTimer = null;
        private readonly object timerLock = new();

        SynchronizationContext context;

        /// <summary>
        /// The ID of the spot that's currently active.
        /// </summary>
        public static int LeadSpot
        {
            get
            {
                for (int i = 0; i < spots.Count; i++)
                    if (spots[i].IsLeadSpot)
                        return i;

                return -1;
            }
        }

        public static List<FollowSpot> Spots => spots;
        public static List<Marker> Markers => markers; 
        public static string ServerIPAddress 
        { 
            get { return serverIPAddress; } 
            set { serverIPAddress = value; } 
        } 

        public MainWindow()
        {
            InitializeComponent();
            context = SynchronizationContext.Current;
        }

        public void UpdateResources()
        {
            Logger.Log("Updating app resources...");

            if (appResources.fileFormatVersion != AppResourcesData.FILE_FORMAT_VERSION)
            {
                MessageBox.Show($"Resource file has an invalid file format version: {appResources.fileFormatVersion} expected: {AppResourcesData.FILE_FORMAT_VERSION}.",
                    "Resource File Version Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                Logger.Log($"Resource file has an invalid file format version: {appResources.fileFormatVersion} expected: {AppResourcesData.FILE_FORMAT_VERSION}.", Severity.FATAL);
            }

            ARTNET_RXIPAddress = IPAddress.Parse(appResources.network.artNet.rxIP);
            ARTNET_RXSubNetMask = IPAddress.Parse(appResources.network.artNet.rxSubNetMask);
            ARTNET_RXUniverse = appResources.network.artNet.universe;

            spots.Clear();

            foreach (var v in appResources.lights)
            {
                FollowSpot spot = new()
                {
                    Head = v.head,
                    Universe = v.universe,
                    Address = v.address,
                    IsLeadSpot = false
                };

                if (v.bar < 0 && v.bar >= appResources.lightingBars.Length)
                {
                    MessageBox.Show($"Light with head number {v.head} is assigned to an invalid bar: {v.bar}! There are only {appResources.lightingBars.Length} bars defined!",
                        "Invalid Configuration!", MessageBoxButton.OK, MessageBoxImage.Stop);
                    Logger.Log($"Light with head number {v.head} is assigned to an invalid bar: {v.bar}! There are only {appResources.lightingBars.Length} bars defined!", Severity.FATAL);
                    Environment.Exit(-1);
                }
                var bar = appResources.lightingBars[v.bar];
                spot.Location = new Point3D(v.xOffset, bar.offset, bar.height + 0.1);

                spots.Add(spot);
            }

            // Force restart the 3D window
            context.Post(delegate (object dummy)
            {
                threeDWindow?.Close();

                threeDWindow = new ThreeD();
                threeDWindow.SetActive(false);
                threeDWindow.Grab();
                FollwSpot_dataGrid.ItemsSource = spots;
                threeDWindow?.UpdateModel();
            }, null);
        }

        public void SaveMarkers()
        {
            string res = null;
            try
            {
                res = File.ReadAllText(resourceFileName);
            }
            catch (FileNotFoundException) { }

            try
            {
                if(res != null)
                    File.WriteAllText(resourceFileName + ".bak", res);
                File.WriteAllText(resourceFileName, JsonSerializer.Serialize(markers, AppResourcesData.JsonSerializerOptions));
            } catch(IOException e)
            {
                Logger.Log($"Couldn't save markers: {e}", Severity.ERROR);
            }
        }

        public void LoadMarkers()
        {
            try
            {
                var res = File.ReadAllText(resourceFileName);
                var markers = JsonSerializer.Deserialize<Marker[]>(res, AppResourcesData.JsonSerializerOptions);

                if (markers != null)
                {
                    MainWindow.markers.Clear();
                    MainWindow.markers.AddRange(markers);
                }

            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("Cannot find resource file\n" + resourceFileName, "File Not Found", MessageBoxButton.OK, MessageBoxImage.Stop);
                Close();
            }
        }

        void ArtNetActivityMonitor()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(100);
                    context?.Post(delegate (object dummy)
                        {
                            if (artNetactivityTimer.ElapsedMilliseconds > 200)
                            {
                                ArtNetActivityLED.Fill = WhiteFill;
                            }
                        }, null);
                }
                catch (ThreadInterruptedException)
                {

                }
            }
        }

        public void ArtNetactivity(int type)
        {
            artNetactivityTimer.Restart();
            context?.Post(delegate (object dummy)
                {
                    ArtNetActivityLED.Fill = GreenFill;
                }, null);
        }
        
        void MqConnection_Monitor()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(500);
                    context?.Post(delegate (object dummy)
                        {
                            if (connectionTimer.ElapsedMilliseconds > 1000)
                            {
                                ConnectionLED.Fill = WhiteFill;
                            }
                        }, null);
                }
                catch (ThreadInterruptedException)
                {

                }
            }
        }

        public void ActivityMQ(int type)
        {
            connectionTimer.Restart();
            context?.Post(delegate (object dummy)
                {
                    ConnectionLED.Fill = GreenFill;
                }, null);
        }

        public void Destroy3DWindow()
        {
            threeDWindow = null;
        }

        private void Window_Loaded(object source, RoutedEventArgs e)
        {
            Logger.Log("Starting followspot client...");
            context = SynchronizationContext.Current;

            try
            {
                GreenFill = new RadialGradientBrush(Color.FromRgb(0x1D, 0xFF, 0x1D), Color.FromRgb(0x00, 0xB9, 0x00));
                RedFill = new RadialGradientBrush(Color.FromRgb(0xFF, 0x1D, 0x1D), Color.FromRgb(0xE0, 0x00, 0x00));
                WhiteFill = new RadialGradientBrush(Color.FromRgb(0x60, 0x80, 0x60), Color.FromRgb(0x20, 0x60, 0x20));

                if (artNetactivity_Thread == null)
                {
                    artNetactivity_Thread = new(new ThreadStart(ArtNetActivityMonitor));
                    artNetactivity_Thread.IsBackground = true;
                    artNetactivity_Thread.Start();
                }

                if (mqConnection_Thread == null)
                {
                    mqConnection_Thread = new(new ThreadStart(MqConnection_Monitor));
                    mqConnection_Thread.IsBackground = true;
                    mqConnection_Thread.Start();
                }

                StartClient();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
                Logger.Log(ex, Severity.FATAL);
                Close();
            }

            // this.Topmost = true;
        }


        public static IEnumerable<(IPAddress Address, IPAddress NetMask)> GetAddressesFromInterfaceType(NetworkInterfaceType? interfaceType = null,
    Func<NetworkInterface, bool> predicate = null)
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.SupportsMulticast && (!interfaceType.HasValue || adapter.NetworkInterfaceType == interfaceType) &&
                    adapter.OperationalStatus == OperationalStatus.Up)
                {
                    if (predicate != null)
                        if (!predicate(adapter))
                            continue;

                    IPInterfaceProperties ipProperties = adapter.GetIPProperties();

                    foreach (var ipAddress in ipProperties.UnicastAddresses)
                    {
                        if (ipAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            yield return (ipAddress.Address, ipAddress.IPv4Mask);
                    }
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (threeDWindow == null)
            {
                threeDWindow = new ThreeD();
                threeDWindow.Grab();
            }
            else
            {
                threeDWindow.Show();
            }
        }

        private void AdonisWindow_PreviewLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            var window = (Window)sender;
            window.Topmost = (bool)alwaysOnTop.IsChecked;
        }

        private void AlwaysOnTop_Unchecked(object sender, RoutedEventArgs e)
        {
            AdonisWindow_PreviewLostKeyboardFocus(this, null);
        }

        private void AlwaysOnTop_Checked(object sender, RoutedEventArgs e)
        {
            AdonisWindow_PreviewLostKeyboardFocus(this, null);
        }

        private void FollwSpot_dataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        public void PointSpots()
        {
            //foreach (Follow_Spot spot in m_spots)
            //{
            //    Vector3D p = (spot.Target - spot.Location);
            //    Point3D direction = Spherical.ToSpherical(-p.Y, p.X, p.Z);
            //    direction.Y += 90;

            //    direction = Spherical.MinSphericalMove(new Point3D(1, spot.Tilt, spot.Pan), direction);
            //    spot.Tilt = direction.Y;
            //    spot.Pan = direction.Z;
            //}

            //updateDMX();
            Smoother(null, null);
        }

        void Smoother(object sender, ElapsedEventArgs e)
        {
            bool isMoving = false;
            lock (timerLock)
            {
                if (smoothingTimer == null)
                {
                    smoothingTimer = new System.Timers.Timer();
                    smoothingTimer.Elapsed += Smoother;
                    smoothingTimer.AutoReset = false;
                    smoothingTimer.Interval = 25;
                }
                else
                {
                    smoothingTimer.Stop();
                }

                foreach (FollowSpot spot in spots)
                {
                    double minVelocity = 0.02;
                    Vector3D delta = spot.Target - spot.CurrentTarget;

                    spot.Acceleration = 0.1 * delta - (0.5 * spot.Velocity);

                    spot.Velocity += spot.Acceleration;


                    if (spot.Velocity.Length > minVelocity)
                        isMoving = true;

                    spot.CurrentTarget += spot.Velocity;
                    spot.Velocity *= 0.5;

                    Point3D tgt_offsetted = new(spot.CurrentTarget.X, spot.CurrentTarget.Y, spot.CurrentTarget.Z + spot.HeightOffset + appResources.theatrePhysicalData.heightOffset);

                    Vector3D p = tgt_offsetted - spot.Location;
                    Point3D direction = Spherical.ToSpherical(-p.Y, p.X, p.Z);
                    direction.Y += 90;

                    direction = Spherical.MinSphericalMove(new Point3D(1, spot.Tilt, spot.Pan), direction);
                    spot.Tilt = direction.Y;
                    spot.Pan = direction.Z;
                }

                UpdateDMX();

                if (isMoving)
                {
                    smoothingTimer.Start();
                }
            }
        }

        public void UpdateDMX()
        {
            byte[] message = JsonSerializer.SerializeToUtf8Bytes(spots, AppResourcesData.JsonSerializerOptions);
            Span<byte> messageHeader = stackalloc byte[3];
            messageHeader[0] = 2; // Position Update
            messageHeader[1] = (byte)(message.Length / 256);
            messageHeader[2] = (byte)(message.Length & 0xFF);

            try
            {
                if (client.Connected)
                {
                    client.Send(messageHeader, SocketFlags.None);
                    client.Send(message, SocketFlags.None);
                }
            }
            catch (Exception w)
            {
                Logger.Log("Exception: " + w);
                client.Close();
            }

            //updateDMX(packet);
        }

        // The port number for the remote device.  
        private const int port = 11000;

        // ManualResetEvent instances signal completion.  
        private ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        // The response from the remote device.  
        private string response = string.Empty;

        Thread clientThread;
        Socket client;
        IPEndPoint remoteEP;

        public void StartClient()
        {
            // Connect to a remote device.  
            try
            {
                if (clientThread == null)
                {
                    clientThread = new(new ThreadStart(ClientLoop));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString(), Severity.FATAL);
            }
        }

        void ClientLoop()
        {
            // Connect to the remote endpoint.  
            while (true)
            {
                try
                {
                    context?.Post(delegate (object dummy)
                        {
                            ConnectionLED.Fill = RedFill;
                        }, null);

                    // Create a TCP/IP socket.  
                    IPAddress ipAddress = IPAddress.Parse(serverIPAddress);
                    remoteEP = new IPEndPoint(ipAddress, port);

                    client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    client.Connect(remoteEP);

                    byte[] buffer = new byte[1024];

                    buffer[0] = 1; // Client Connect
                    buffer[1] = (byte)clientID;

                    client.Send(buffer, 2, SocketFlags.None);
                    ActivityMQ(2);

                    //                    if (m_spots.Count>0)
                    //                        m_spots[0].IsLeadSpot = true;

                    int count = 0;
                    do
                    {
                        count = client.Receive(buffer, 1, SocketFlags.None);
                        ActivityMQ(1);
                        switch ((MessageType)buffer[0])
                        {
                            case MessageType.Initialize:
                                Logger.Log("Server Command: " + buffer[0]);
                                break;
                            case MessageType.ConfigureClient:
                                {
                                    client.Receive(buffer, 1, 3, SocketFlags.None);
                                    int length = buffer[1] * 256 + buffer[2];
                                    clientID = buffer[3] + 1;
                                    Logger.Log($"Server Update: {buffer[0]}, client ID: {clientID}");

                                    byte[] rcv_buffer = new byte[length];
                                    int received = 0;
                                    while (received < length)
                                    {
                                        received += client.Receive(rcv_buffer, received, length - received, SocketFlags.None);
                                    }

                                    appResources = JsonSerializer.Deserialize<AppResourcesData>(rcv_buffer, AppResourcesData.JsonSerializerOptions);
                                    UpdateResources();

                                    context?.Post(delegate (object dummy)
                                        {
                                            ControllerID.Content = "" + clientID;
                                        }, null);
                                }
                                break;
                            case MessageType.SpotUpdate: // Spot position update
                                {
                                    client.Receive(buffer, 1, 2, SocketFlags.None);
                                    int res_length = buffer[1] * 256 + buffer[2];
                                    byte[] rcv_buffer = new byte[res_length];
                                    int recieved = 0;
                                    while (recieved < res_length)
                                    {
                                        recieved += client.Receive(rcv_buffer, recieved, res_length - recieved, SocketFlags.None);
                                    }

                                    var newspots = JsonSerializer.Deserialize<FollowSpot[]>(rcv_buffer, AppResourcesData.JsonSerializerOptions);

                                    //Logger.Log("DMX Update:" + newspots[0]);
                                    for (int i = 0; i < spots.Count; i++)
                                    {
                                        if (spots[i].MouseControlID != clientID)
                                        {
                                            spots[i].Pan = newspots[i].Pan;
                                            spots[i].Tilt = newspots[i].Tilt;

                                            spots[i].Target = newspots[i].Target;
                                        }
                                    }
                                }
                                break;

                            case MessageType.Message: //Message box
                                {
                                    client.Receive(buffer, 1, 2, SocketFlags.None);
                                    int res_length = buffer[1] * 256 + buffer[2];
                                    byte[] rcv_buffer = new byte[res_length];
                                    int recieved = 0;
                                    while (recieved < res_length)
                                    {
                                        recieved += client.Receive(rcv_buffer, recieved, res_length - recieved, SocketFlags.None);
                                    }

                                    var message = JsonSerializer.Deserialize<ClientMesage>(rcv_buffer, AppResourcesData.JsonSerializerOptions);

                                    if (threeDWindow != null)
                                    {
                                        context?.Post(delegate (object dummy)
                                            {
                                                int i = 0;
                                                for (i = 0; i < spots.Count; i++)
                                                {
                                                    spots[i].IsLeadSpot = false;
                                                    for (int j = 0; j < message.spots?.Length; j++)
                                                    {
                                                        if (spots[i].Head == message.spots[j])
                                                        {
                                                            spots[i].IsLeadSpot = true;
                                                            spots[i].Zoom = message.zooms[j];
                                                            spots[i].HeightOffset = message.HeightOffsets[j];
                                                            break;
                                                        }
                                                    }
                                                }
                                                ((MainViewModel)(threeDWindow.DataContext)).UpdateLights();
                                                if (message.message.Length > 0)
                                                {
                                                    threeDWindow.MessagePopup.Content = message.message;
                                                    threeDWindow.MessagePopup.Visibility = Visibility.Visible;
                                                }
                                                else
                                                {
                                                    threeDWindow.MessagePopup.Visibility = Visibility.Hidden;
                                                }
                                            }, null);
                                    }
                                }
                                break;

                            default:
                                Logger.Log("Server Command: " + buffer[0]);
                                break;
                        }

                    } while (count > 0);

                    throw new Exception("Read zero");
                }
                catch (Exception e)
                {
                    Logger.Log("Socket Exception:" + e, Severity.ERROR);
                    try
                    {
                        client.Close();
                    }
                    catch (Exception e2)
                    {
                        Logger.Log("Socket Exception2:" + e2, Severity.ERROR);
                    }

                    context?.Post(delegate (object dummy)
                        {
                            ConnectionLED.Fill = RedFill;
                            ControllerID.Content = "?";
                        }, null);
                    Thread.Sleep(1000);
                }
            }
        }

    }

    [Serializable]
    public struct ClientMesage
    {
        public int clientID;
        public string message;
        public int[] spots;
        public int[] zooms;
        public double[] HeightOffsets;
        public int timeout;
    }

    enum MessageType
    {
        Initialize = 0,
        ConfigureClient = 1,
        SpotUpdate = 2,         // Spot position update
        Message = 3            //Message box
    }
}