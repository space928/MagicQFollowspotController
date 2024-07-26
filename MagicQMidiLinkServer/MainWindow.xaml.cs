using Haukcode.ArtNet.Packets;
using Haukcode.ArtNet.Sockets;
using Haukcode.Sockets;
using Rug.Osc;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ErrorEventArgs = Sanford.Multimedia.ErrorEventArgs;

namespace MidiApp
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisUI.Controls.AdonisWindow
    {
        private readonly string resourceFileName = @"Resources\resources.json";
        public readonly string[] attributeNames = {"Dimmer", "Dim Mode", "Shutter", "Iris", "Pan", "Tilt", "Col1", "Col2",
                                            "Gobo1", "Gobo2", "Rotate1", "Rotate2", "Focus", "Zoom", "FX1 Prism",
                                            "FX2", "Cyan/Red", "Magenta/Green", "Yellow/Blue", "Col Mix / White", "Cont1 (Lamp on/off)", "Cont2 (Reset)", "Macro", "Macro2",
                                            "CTC", "CTO", "Col3 Speed", "Col4 (Amber)", "Gobo3", "Gobo4", "Gobo Rotate 3", "Prism Rot",
                                            "Frost1", "Frost2", "FX3", "FX4", "FX5", "FX6", "FX7", "FX8",
                                            "Cont3 (Beamm Speed)", "Cont4", "Cont5", "Cont6", "Cont7", "Cont8",
                                            "Pos1", "Pos2", "Pos3", "Pos4", "Pos5","Pos6 (Position Speed)",
                                            "Frame 1 (Top left)", "Frame 2 (Top left)", "Frame 3 (Bottom left)", "Frame 4 (Bottom left)", "Frame 5 (Top Right)", "Frame 6 (Top Right)", "Frame 7 (Bottom Right)", "Frame 8 (Bottom Right)",
                                            "Lime/UV", "Col5", "Col6", "Reserved"};


        public static List<FollowSpot> FollowSpots { get; } = new();
        public static AppResourcesData AppResources { get => appResources; }

        private static AppResourcesData appResources;

        public ObservableCollection<string> LogList => Logger.logList;
        private LogWindow logWindow;

        private OscSender sender;
        private OscReceiver receiver;

        private MidiDeviceManager midiDevice;

        private Thread oscListenerThread = null;
        private Thread activity_Thread = null;
        private Thread fsActivity_Thread = null;
        private Thread artNetActivity_Thread = null;
        private ArtNetSocket m_socket = null;
        private ArtNetSocket m_TXsocket = null;

        private Thread mqConnection_Thread = null;

        private DateTime lastStatusMessageTime;
        private Timer statusMessageUpdater;
        private readonly TimeSpan statusMessageTime = TimeSpan.FromSeconds(5);
        private readonly Brush warningBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xcc, 0x10));
        private readonly Brush errorBrush = new SolidColorBrush(Color.FromRgb(0xff, 0x10, 0x20));
        private readonly Brush infoBrush = new SolidColorBrush(Color.FromRgb(0xaa, 0xaa, 0xaa));

        private readonly Thread m_ResourceLoader_Thread = null;

        public readonly Dictionary<string, int> attributes = new();

        private int selectedAttribute = 0;
        private int selectedPlayback = 0;

        public int SelectedAttribute
        {
            get => selectedAttribute;
            set
            {
                if (value < 0)
                    value += attributeNames.Length;
                selectedAttribute = value;

                SendOSCMessage(new OscMessage("/pb/10/" + (selectedAttribute + 1), 1.00f));

                context?.Post(_ =>
                {
                    attrName.Text = attributeNames[selectedAttribute];
                }, null);
            }
        }
        public int SelectedPlayback
        {
            get => selectedPlayback;
            set
            {
                selectedPlayback = value;
            }
        }

        private SynchronizationContext context;

        public MainWindow()
        {
            InitializeComponent();

            Logger.Log($"Starting MagicQ Midi Link Server");
            var assembly = Assembly.GetExecutingAssembly();
            string copyright = string.IsNullOrEmpty(assembly.Location) ? "" : FileVersionInfo.GetVersionInfo(assembly.Location).LegalCopyright;
            Logger.Log($"  version: {assembly.GetName().Version}; {copyright}");

            //Logger.Log("Starting Followspot Server...");

            context = SynchronizationContext.Current;

            for (int i = 0; i < attributeNames.Length; i++)
            {
                attributes.Add(attributeNames[i], i);
            }

            ReloadAppResources();
            m_ResourceLoader_Thread = new(new ThreadStart(ResourceLoaderLoop));
            m_ResourceLoader_Thread.IsBackground = true;
            m_ResourceLoader_Thread.Start();

            midiDevice = new(this);

            try
            {
                MQ_IPAddress = IPAddress.Parse(appResources.network.magicQIP);
                ARTNET_RXIPAddress = IPAddress.Parse(appResources.network.artNet.rxIP);
                ARTNET_RXSubNetMask = IPAddress.Parse(appResources.network.artNet.rxSubNetMask);
                ARTNET_TXIPAddress = IPAddress.Parse(appResources.network.artNet.txIP);
                ARTNET_TXSubNetMask = IPAddress.Parse(appResources.network.artNet.txSubNetMask);
                ARTNET_TXUseBroadcast = appResources.network.artNet.broadcast;
                ARTNET_TXUniverse = appResources.network.artNet.universe;
            }
            catch (Exception e)
            {
                MessageBox.Show("Cannot parse resource file\n" + e.Message, "Resource File Problem", MessageBoxButton.OK, MessageBoxImage.Stop);
                Logger.Log("Cannot parse resource file\n" + e, Severity.FATAL);
                Close();
            }
        }

        public void SetNetworkSettings(NetworkSettings settings)
        {
            appResources.network = settings;
            MQ_IPAddress = IPAddress.Parse(appResources.network.magicQIP);
            ARTNET_RXIPAddress = IPAddress.Parse(appResources.network.artNet.rxIP);
            ARTNET_RXSubNetMask = IPAddress.Parse(appResources.network.artNet.rxSubNetMask);
            ARTNET_TXIPAddress = IPAddress.Parse(appResources.network.artNet.txIP);
            ARTNET_TXSubNetMask = IPAddress.Parse(appResources.network.artNet.txSubNetMask);
            ARTNET_TXUseBroadcast = appResources.network.artNet.broadcast;
            ARTNET_TXUniverse = appResources.network.artNet.universe;

            SaveAppResource();
            SetStatusMessage("Network configuration changed, restarting app...");
            Application.Current.Shutdown();
            System.Windows.Forms.Application.Restart();
        }

        IPAddress MQ_IPAddress = null;
        IPAddress ARTNET_RXIPAddress = null;
        IPAddress ARTNET_RXSubNetMask = null;
        IPAddress ARTNET_TXIPAddress = null;
        IPAddress ARTNET_TXSubNetMask = null;
        bool ARTNET_TXUseBroadcast = true;
        int ARTNET_TXUniverse = 0;

        public void SaveAppResource()
        {
            if (File.Exists(resourceFileName))
            {
                try
                {
                    string res = File.ReadAllText(resourceFileName);
                    File.WriteAllText(resourceFileName + ".bak", res);
                }
                catch (IOException e)
                {
                    Logger.Log($"Couldn't save backup file: {e}", Severity.ERROR);
                }
            }

            try
            {
                JsonSerializerOptions options = new(AppResourcesData.JsonSerializerOptions)
                {
                    WriteIndented = true,
                };
                File.WriteAllText(resourceFileName, JsonSerializer.Serialize(appResources, options));
            }
            catch (IOException e)
            {
                Logger.Log($"Couldn't save app resources: {e}", Severity.ERROR);
            }
        }

        public bool ReloadAppResources()
        {
            try
            {
                var res = File.ReadAllText(resourceFileName);
                var data = JsonSerializer.Deserialize<AppResourcesData>(res, AppResourcesData.JsonSerializerOptions);
                if (data.fileFormatVersion != AppResourcesData.FILE_FORMAT_VERSION)
                {
                    MessageBox.Show($"Resource file has an invalid file format version: {data.fileFormatVersion} expected: {AppResourcesData.FILE_FORMAT_VERSION}.",
                        "Resource File Version Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                    Logger.Log($"Resource file has an invalid file format version: {data.fileFormatVersion} expected: {AppResourcesData.FILE_FORMAT_VERSION}.", Severity.FATAL);
                    Environment.Exit(-1);
                }
                SetStatusMessage("Updated app resources!");
                appResources = data;
                return true;
            }
            catch (JsonException e)
            {
                MessageBox.Show("Couldn't parse resource file\n" + e, "Resource File Parse Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                // Environment.Exit(-1);
                Logger.Log("Couldn't parse resource file\n" + e, Severity.WARNING);
                return false;
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("Cannot find resource file\n" + resourceFileName + "\nAn empty resource file will be created!", "File Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
                Logger.Log("Cannot find resource file\n" + resourceFileName + "\nAn empty resource file will be created!", Severity.FATAL);
                appResources = new();
                SaveAppResource();
                Environment.Exit(-1);
                return false;
            }
            catch (IOException)
            {
                Logger.Log("Encountered IO exception while loading resources!", Severity.WARNING);
                return false;
            }
        }

        public void ResourceLoaderLoop()
        {
            DateTime time = File.GetLastWriteTime(resourceFileName);

            while (true)
            {
                try
                {
                    DateTime latestTime = File.GetLastWriteTime(resourceFileName);

                    if (latestTime > time)
                    {
                        Thread.Sleep(200);
                        if (ReloadAppResources())
                        {
                            time = latestTime;
                            LoadLightDefinitions();
                            foreach (var client in clientHandlers)
                            {
                                client?.ReconfigureClient();
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (ThreadInterruptedException)
                {
                    //Logger.Log($"Resource loader interrupted: \n{ex.Message}", Severity.WARNING);
                }
            }
        }

        #region Activity Monitors

        Brush GreenFill = null;
        Brush RedFill = null;
        Brush WhiteFill = null;

        readonly Stopwatch activityTimer = Stopwatch.StartNew();

        void ActivityMonitor()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(100);
                    context?.Post(delegate (object dummy)
                        {
                            if (activityTimer.ElapsedMilliseconds > 200)
                            {
                                ActivityLED.Fill = WhiteFill;
                            }
                        }, null);
                }
                catch (ThreadInterruptedException)
                {
                    //Logger.Log($"Activity monitor interrupted: \n{ex.Message}", Severity.WARNING);
                }
            }
        }

        public void Activity(int type)
        {
            activityTimer.Restart();
            context?.Post(_ =>
            {
                ActivityLED.Fill = GreenFill;
            }, null);
        }

        public static Color HSL2RGB(double h, double sl, double l)
        {
            double v;
            double r, g, b;

            h /= 360.0;
            sl /= 100.0;
            l /= 100.0;

            r = l;   // default to gray
            g = l;
            b = l;
            v = (l <= 0.5) ? (l * (1.0 + sl)) : (l + sl - l * sl);
            if (v > 0)
            {
                double m;
                double sv;
                int sextant;
                double fract, vsf, mid1, mid2;

                m = l + l - v;
                sv = (v - m) / v;
                h *= 6.0;
                sextant = (int)h;
                fract = h - sextant;
                vsf = v * sv * fract;
                mid1 = m + vsf;
                mid2 = v - vsf;
                switch (sextant)
                {
                    case 0:
                        r = v;
                        g = mid1;
                        b = m;
                        break;
                    case 1:
                        r = mid2;
                        g = v;
                        b = m;
                        break;
                    case 2:
                        r = m;
                        g = v;
                        b = mid1;
                        break;
                    case 3:
                        r = m;
                        g = mid2;
                        b = v;
                        break;
                    case 4:
                        r = mid1;
                        g = m;
                        b = v;
                        break;
                    case 5:
                        r = v;
                        g = m;
                        b = mid2;
                        break;
                }
            }
            Color rgb = new()
            {
                A = 0xff,
                R = (byte)(r * 255.0f),
                G = (byte)(g * 255.0f),
                B = (byte)(b * 255.0f),
            };
            return rgb;
        }

        Stopwatch FSactivityTimer = Stopwatch.StartNew();

        double[] clientHues = { 0, 120, 240, 300, 60 };

        SolidColorBrush[][] clientColours = new SolidColorBrush[5][];

        public ClientHandler[] clientHandlers = new ClientHandler[5];

        Button[] clientButtons = new Button[5];

        void FSActivityMonitor()
        {
            clientButtons[0] = Client0;
            clientButtons[1] = Client1;
            clientButtons[2] = Client2;
            clientButtons[3] = Client3;
            clientButtons[4] = Client4;

            context?.Post(_ =>
            {
                for (int i = 0; i < clientHues.Length; i++)
                {
                    SolidColorBrush[] colors = new SolidColorBrush[3];

                    colors[0] = new SolidColorBrush(HSL2RGB(clientHues[i], 20.0, 25.0));
                    colors[1] = new SolidColorBrush(HSL2RGB(clientHues[i], 50.0, 35.0));
                    colors[2] = new SolidColorBrush(HSL2RGB(clientHues[i], 100.0, 50.0));

                    clientColours[i] = colors;
                }
            }, null);

            while (true)
            {
                try
                {
                    Thread.Sleep(100);
                    context?.Post(_ =>
                    {
                        if (FSactivityTimer.ElapsedMilliseconds > 200)
                        {
                            for (int i = 0; i < clientHues.Length; i++)
                            {
                                if (clientHandlers[i] != null)
                                {
                                    clientButtons[i].Background = clientColours[i][1];
                                }
                                else
                                {
                                    clientButtons[i].Background = clientColours[i][0];
                                }
                            }
                        }
                    }, null);
                }
                catch (ThreadInterruptedException)
                {
                    //Logger.Log($"FSActivity monitor interrupted: \n{ex.Message}", Severity.WARNING);
                }
            }
        }

        public void FSactivity(int clientID)
        {
            FSactivityTimer.Restart();
            context?.Post(_ =>
            {
                if (clientID >= 0)
                    clientButtons[clientID].Background = clientColours[clientID][2];
            }, null);
        }


        Stopwatch ArtNetactivityTimer = Stopwatch.StartNew();

        void ArtNetActivityMonitor()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(100);
                    context?.Post(_ =>
                    {
                        if (ArtNetactivityTimer.ElapsedMilliseconds > 200)
                        {
                            ArtNetActivityLED.Fill = WhiteFill;
                        }
                    }, null);
                }
                catch (ThreadInterruptedException)
                {
                    //Logger.Log($"ArtNet Activity monitor interrupted: \n{ex.Message}", Severity.WARNING);
                }
            }
        }

        public void ArtNetactivity(int type)
        {
            ArtNetactivityTimer.Restart();
            context?.Post(_ =>
            {
                ArtNetActivityLED.Fill = GreenFill;
            }, null);
        }

        Stopwatch connectionTimer = Stopwatch.StartNew();

        void MqConnection_Monitor()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(500);
                    context?.Post(_ =>
                    {
                        if (connectionTimer.ElapsedMilliseconds > 1000)
                        {
                            ConnectionLED.Fill = WhiteFill;
                        }
                    }, null);
                }
                catch (ThreadInterruptedException)
                {
                    //Logger.Log($"MQ Activity monitor interrupted: \n{ex.Message}", Severity.WARNING);
                }
            }
        }

        public void ActivityMQ(int type)
        {
            connectionTimer.Restart();
            context?.Post(_ =>
            {
                ConnectionLED.Fill = GreenFill;
            }, null);
        }
        #endregion

        void OSCListenLoop()
        {
            bool justRecieved = false;

            while (receiver != null)
            {
                try
                {
                    justRecieved = false;

                    if (midiDevice?.IsConnected ?? false)
                    {
                        OscPacket pkt = receiver.Receive();
                        justRecieved = true;

                        Logger.Log("OSC Packet: " + pkt.ToString());
                        Activity(1);
                        ActivityMQ(1);

                        OscMessage msg = OscMessage.Parse(pkt.ToString());

                        var parts = msg.Address.Split('/');

                        if (parts.Length > 0)
                        {
                            if (parts[1].Equals("exec"))
                            {
                                ParseOSCExecMessage(msg, parts);
                            }
                            else if (parts[1].Equals("pb"))
                            {
                                ParseOSCPlaybackMessage(msg, parts);
                            }
                            else if (parts[1].Equals("fspot"))
                            {
                                ParseOSCFspotMessage(msg, parts);
                            }
                            else if (parts[1].Equals("sel"))
                            {
                                ParseOSCSelMessage(parts);
                            }
                        }
                        else
                        {
                            throw new Exception($"Malformed OSC message: '{msg}'");
                        }
                    }

                    if (!justRecieved)
                        Thread.Sleep(100);
                }
                catch (ThreadInterruptedException)
                { }
                catch (Exception e)
                {
                    Logger.Log($"Error while parsing OSC message: \n{e}", Severity.WARNING);
                }
            }
        }

        private void ParseOSCSelMessage(string[] parts)
        {
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[2], out int selectedPB))
                {
                    selectedPlayback = selectedPB;
                    midiDevice?.SendSelMessage(selectedPB);
                    Logger.Log("selectedPlayback: " + selectedPlayback);
                }
            }
        }

        private void ParseOSCFspotMessage(OscMessage msg, string[] parts)
        {
            // fspot/start/<mouse1 spots list>$message+<mouse2 spots list>$message@<viewID>
            if (parts.Length >= 2)
            {
                if (parts[2] == "start")
                {
                    string ids = msg.ToString();
                    int viewID = -1;
                    ids = ids.Replace("\"", "");

                    if (!ids.EndsWith("/"))
                    {
                        int viewpos = ids.LastIndexOf('@');
                        if (viewpos > 0)
                        {
                            viewID = int.Parse(ids.Substring(viewpos + 1));
                            ids = ids[..viewpos];
                        }
                        else
                        {
                            viewpos = ids.Length;
                        }

                        ids = ids[(ids.LastIndexOf('/') + 1)..];

                        foreach (FollowSpot spot in FollowSpots)
                        {
                            spot.MouseControlID = -1;
                        }

                        int mouse = 0;

                        foreach (string s in ids.Split('+'))
                        {
                            string spots = s;
                            string message = "";
                            int message_Pos = spots.IndexOf("$");
                            if (message_Pos != -1)
                            {
                                message = spots.Substring(message_Pos + 1);
                                spots = spots.Substring(0, message_Pos);
                            }

                            foreach (string i in spots.Split(','))
                            {
                                string idspot = i;
                                int zoom_Pos = idspot.IndexOf("z");
                                if (zoom_Pos != -1)
                                {
                                    //message = idspot.Substring(zoom_Pos + 1);
                                    idspot = idspot.Substring(0, zoom_Pos);
                                }

                                int headId = int.Parse(idspot);
                                foreach (FollowSpot spot in FollowSpots)
                                {
                                    if (spot.Head == headId)
                                    {
                                        spot.MouseControlID = mouse;
                                        spot.MouseControlID = mouse;
                                    }
                                }
                            }
                            SendClientMessage(mouse, message, spots, 3);

                            mouse++;
                        }
                    }
                }
                else if (parts[2] == "stop")
                {
                    foreach (FollowSpot spot in FollowSpots)
                    {
                        spot.MouseControlID = -1;
                    }

                    SendClientMessage(0, "", "", 3);
                    SendClientMessage(1, "", "", 3);
                    SendClientMessage(2, "", "", 3);
                }
                // fspot/message/message+message
                // fspot/message
                else if (parts[2] == "message")
                {

                    string ids = msg.ToString();
                    ids = ids.Replace("\"", "");

                    if (!ids.EndsWith("/"))
                    {
                        ids = ids.Substring(ids.LastIndexOf('/') + 1);
                        if (parts.Length == 3)
                        {
                            ids = "";
                        }
                        int mouse = 0;

                        foreach (string message in ids.Split('+'))
                        {
                            if (message != "$")
                                SendClientMessage(mouse, message, "0", 3);
                        }
                    }
                }
            }
        }

        private void ParseOSCPlaybackMessage(OscMessage msg, string[] parts)
        {
            //  /pb/1, 0.796078f
            //  /pb/1/flash, 1
            //  /pb/3, 0f
            //  /pb/3/flash, 0
            if (parts.Length < 2 || !int.TryParse(parts[2], out int playback))
                throw new Exception($"Malformed OSC message (while parsing playback): '{msg.Address}'");

            if (parts.Length > 3)
            {
                if (parts[3] == "flash")
                {

                }
            }
            else
            {
                midiDevice?.SendFaderMessage(msg, playback);
            }
        }

        private void ParseOSCExecMessage(OscMessage msg, string[] parts)
        {
            //  /exec/1/1, 0f
            //  /exec/1/57, 0f
            if (parts.Length < 2 || !int.TryParse(parts[2], out int page))
                throw new Exception($"Malformed OSC message (while parsing page): '{msg.Address}'");
            if (parts.Length < 3 || !int.TryParse(parts[3], out int execNum))
                throw new Exception($"Malformed OSC message (while parsing execNum): '{msg.Address}'");

            midiDevice?.SendExecMessage(msg, page-1, execNum-1);
        }

        public void SendOSCMessage(OscMessage msg)
        {
            sender?.Send(msg);
        }

        public void SendClientMessage(int clientID, string message, string spots, int timeout)
        {
            ClientMessage msg = new();

            msg.clientID = clientID;
            msg.message = WebUtility.UrlDecode(message);
            msg.spots = new int[spots.Split(',').Length];
            msg.zooms = new int[msg.spots.Length];
            msg.HeightOffsets = new double[msg.spots.Length];
            msg.timeout = timeout;

            int p = 0;
            foreach (string s in spots.Split(','))
            {
                if (s.Length > 0)
                {
                    string idspot = s;
                    string num = "";
                    int zoom_Pos = idspot.IndexOf("z");
                    int ho_Pos = 0;
                    if (zoom_Pos != -1)
                    {
                        num = idspot[(zoom_Pos + 1)..];
                        ho_Pos = num.IndexOf("h");
                        if (ho_Pos != -1)
                        {
                            msg.HeightOffsets[p] = double.Parse(num.Substring(ho_Pos + 1));

                            num = num[..ho_Pos];
                        }
                        msg.zooms[p] = int.Parse(num);
                        idspot = idspot[..zoom_Pos];
                    }

                    msg.spots[p++] = int.Parse(idspot);
                }
            }
            if (p == 0)
                msg.spots = null;

            byte[] msgBytes = JsonSerializer.SerializeToUtf8Bytes(msg, AppResourcesData.JsonSerializerOptions);
            Span<byte> header = stackalloc byte[3];
            header[0] = (byte)MessageType.Message; // Message
            header[1] = (byte)(msgBytes.Length / 256);
            header[2] = (byte)(msgBytes.Length & 0xFF);

            if (clientID < clientHandlers.Length)
            {
                ClientHandler ch = clientHandlers[clientID];

                if (ch != null)
                {
                    try
                    {
                        Socket client = ch.Client;

                        if (client.Connected)
                        {
                            client.Send(header, SocketFlags.None);
                            client.Send(msgBytes, SocketFlags.None);
                        }
                    }
                    catch
                    {
                        ch.Shutdown();
                    }
                }
            }
        }

        private void Window_Loaded(object source, RoutedEventArgs e)
        {
            context = SynchronizationContext.Current;

            midiDevice ??= new(this);
            if (!midiDevice.IsConnected)
            {
                midiDevice.LookForXTouch();
                if (!midiDevice.IsConnected)
                {
                    Close();
                    return;
                }
            }

            attrName.Text = attributeNames[SelectedAttribute];

            try
            {
                GreenFill = new RadialGradientBrush(Color.FromRgb(0x1D, 0xFF, 0x1D), Color.FromRgb(0x00, 0xB9, 0x00));
                RedFill = new RadialGradientBrush(Color.FromRgb(0xFF, 0x1D, 0x1D), Color.FromRgb(0xE0, 0x00, 0x00));
                WhiteFill = new RadialGradientBrush(Color.FromRgb(0x60, 0x80, 0x60), Color.FromRgb(0x20, 0x60, 0x20));

                if (fsActivity_Thread == null)
                {
                    fsActivity_Thread = new(new ThreadStart(FSActivityMonitor));
                    fsActivity_Thread.IsBackground = true;
                    fsActivity_Thread.Start();
                }

                if (activity_Thread == null)
                {
                    activity_Thread = new(new ThreadStart(ActivityMonitor));
                    activity_Thread.IsBackground = true;
                    activity_Thread.Start();
                }

                if (artNetActivity_Thread == null)
                {
                    artNetActivity_Thread = new(new ThreadStart(ArtNetActivityMonitor));
                    artNetActivity_Thread.IsBackground = true;
                    artNetActivity_Thread.Start();
                }

                if (mqConnection_Thread == null)
                {
                    mqConnection_Thread = new(new ThreadStart(MqConnection_Monitor));
                    mqConnection_Thread.IsBackground = true;
                    mqConnection_Thread.Start();
                }

                Logger.EnableSync();

                SetupMQListener();
                midiDevice?.SetupMidiDevice();
                LoadLightDefinitions();

                ipInputMQ.Content = MQ_IPAddress;
                ipInputTX.Content = ARTNET_TXIPAddress;

                oscListenerThread = new(new ThreadStart(OSCListenLoop));
                oscListenerThread.IsBackground = true;
                oscListenerThread.Start();

                StartListeningToClients(MQ_IPAddress);

                FollwSpot_dataGrid.ItemsSource = FollowSpots;

                ArtNetListner();

                reconnectMidiButton.Click += (o, e) =>
                {
                    SetStatusMessage("Attempting to reconnect MIDI device...");

                    var oldDevice = midiDevice;
                    midiDevice = null;
                    oldDevice.Dispose();
                    midiDevice = new(this);
                    midiDevice.LookForXTouch();
                    midiDevice.SetupMidiDevice();

                    //SetStatusMessage("Reconnected!"); ;
                };
                reconnectMQButton.Click += (o, e) =>
                {
                    SetStatusMessage("Attempting to reconnect to MagicQ...");

                    SetupMQListener();

                    //SetStatusMessage("Reconnected!");
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
                Logger.Log(ex, Severity.FATAL);
                Close();
            }

            Logger.OnWarning += (o, e) =>
            {
                context.Post(_ =>
                {
                    lastStatusMessageTime = DateTime.UtcNow;
                    statusLabel.Content = e.LogEntry;
                    statusLabel.Foreground = warningBrush;
                }, null);
            };
            Logger.OnError += (o, e) =>
            {
                context.Post(_ =>
                {
                    lastStatusMessageTime = DateTime.UtcNow;
                    statusLabel.Content = e.LogEntry;
                    statusLabel.Foreground = errorBrush;
                }, null);
            };

            statusMessageUpdater = new Timer(_ =>
            {
                context.Post(_ =>
                {
                    if (DateTime.UtcNow - lastStatusMessageTime > statusMessageTime)
                    {
                        statusLabel.Content = $"MagicQ MIDI Link version {Assembly.GetExecutingAssembly().GetName().Version}";
                        statusLabel.Foreground = infoBrush;
                    }
                }, null);
            }, null, 0, 1000);

            statusBar.MouseDown += (o, e) =>
            {
                if (logWindow != null)
                {
                    logWindow.Close();
                    logWindow = null;
                }

                logWindow = new()
                {
                    DataContext = this,
                };
                logWindow.Show();
            };

            // this.Topmost = true;
        }

        public void SetStatusMessage(string message, [CallerMemberName] string caller = "")
        {
            Logger.Log(message, Severity.INFO, caller);
            context?.Post(_ =>
            {
                if (statusLabel == null)
                    return;
                lastStatusMessageTime = DateTime.UtcNow;
                statusLabel.Content = message;
                statusLabel.Foreground = infoBrush;
            }, null);
        }

        private static void LoadLightDefinitions()
        {
            FollowSpots.Clear();
            foreach (var v in appResources.lights)
            {
                FollowSpot spot = new()
                {
                    Head = v.head,
                    Universe = v.universe,
                    Address = v.address,
                    MouseControlID = -1
                };
                spot.SetFixtureType(v.fixture, appResources);
                spot.Location = new Point3D(v.xOffset, appResources.lightingBars[v.bar].offset, appResources.lightingBars[v.bar].height + 0.1);

                FollowSpots.Add(spot);
            }
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

        public static bool SpotsOnMouseControl()
        {
            for (int i = 0; i < FollowSpots.Count; i++)
            {
                if (FollowSpots[i].MouseControlID >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        void ArtNet_NewPacket(object sender, NewPacketEventArgs<ArtNetPacket> e)
        {
            //Logger.Log($"Received ArtNet packet with OpCode: {e.Packet.OpCode} from {e.Source}");
            if (!SpotsOnMouseControl())
            {
                ArtNetactivity(1);

                if (e.Packet.OpCode == Haukcode.ArtNet.ArtNetOpCodes.Dmx)
                {
                    ArtNetDmxPacket dmx = (ArtNetDmxPacket)e.Packet;
                    context.Post(delegate (object dummy)
                    {
                        foreach (FollowSpot spot in FollowSpots)
                        {
                            if (dmx.Universe == spot.Universe)
                            {
                                double pan = Math.Round(((dmx.DmxData[spot.Address + (spot.FixtureType.panLowChannel - 2)] * 256)
                                    + dmx.DmxData[spot.Address + (spot.FixtureType.panLowChannel - 1)]) / 65535.0
                                    * spot.FixtureType.panRange - (spot.FixtureType.panRange / 2), 3);
                                double tilt = Math.Round(((dmx.DmxData[spot.Address + (spot.FixtureType.tiltLowChannel - 2)] * 256)
                                    + dmx.DmxData[spot.Address + (spot.FixtureType.tiltLowChannel - 1)]) / 65535.0
                                    * (spot.FixtureType.tiltRange) - (spot.FixtureType.tiltRange / 2), 3);

                                if (spot.FixtureType.panTiltSwap)
                                    (pan, tilt) = (tilt, pan);

                                if (spot.FixtureType.panInvert)
                                    pan = -pan;

                                if (spot.FixtureType.tiltInvert)
                                    tilt = -tilt;

                                spot.Pan = pan;
                                spot.Tilt = tilt;
                            }
                        }

                        //if (dmx.Universe == m_spots[0].Universe - 1)
                        //{
                        //                        Follow_Spot spot = m_spots[0];

                        //                        int p = (dmx.DmxData[spot.Address - 1] * 256) + dmx.DmxData[spot.Address];
                        //                        int t = (dmx.DmxData[spot.Address + 1] * 256) + dmx.DmxData[spot.Address + 2];

                        //Logger.Log("P: {0}, T:{1}", p, t);

                        //}



                        // Tell clients

                    }, null);

                    if ((SpotsOnMouseControl()) && ((dmx.Universe != (short)ARTNET_TXUniverse)))
                    {
                        UpdateDMX(dmx.DmxData, (byte)(dmx.Universe - ARTNET_TXUniverse));
                    }
                }
            }

        }

        void InformClients()
        {
            byte[] msgBytes = JsonSerializer.SerializeToUtf8Bytes(FollowSpots, AppResourcesData.JsonSerializerOptions);
            Span<byte> header = stackalloc byte[3];
            header[0] = (byte)MessageType.SpotUpdate; // Position Update
            header[1] = (byte)(msgBytes.Length / 256);
            header[2] = (byte)(msgBytes.Length & 0xFF);

            for (int c = 0; c < clientHandlers.Length; c++)
            {
                ClientHandler ch = clientHandlers[c];

                if (ch != null)
                {
                    try
                    {
                        Socket client = ch.Client;

                        if (client.Connected)
                        {
                            client.Send(header, SocketFlags.None);
                            client.Send(msgBytes, SocketFlags.None);
                        }
                    }
                    catch
                    {
                        ch.Shutdown();
                    }
                }

            }

        }

        void ArtNetListner()
        {
            m_socket = new ArtNetSocket();
            m_TXsocket = new ArtNetSocket();

            m_socket.NewPacket += ArtNet_NewPacket;

            //            var addresses = GetAddressesFromInterfaceType();
            //            var addr = addresses.ToArray()[2];
            try
            {
                m_socket.Open(ARTNET_RXIPAddress, ARTNET_RXSubNetMask);

                //            addr = addresses.ToArray()[0];
                m_TXsocket.Open(ARTNET_TXIPAddress, ARTNET_TXSubNetMask);
            }
            catch (Exception e)
            {
                HandleNetworkError(e, "artnet");
                ArtNetListner();
            }
            //            m_TXsocket.Open(addr.Address, addr.NetMask);
            m_TXsocket.EnableBroadcast = ARTNET_TXUseBroadcast;
        }

        private static void HandleNetworkError(Exception e, string netType = "network")
        {
            Logger.Log($"Couldn't connect to {netType}! Check the network configuration is valid! \n{e}", Severity.FATAL);
            var res = MessageBox.Show($"Couldn't connect to {netType}! Check the network configuration is valid!\n" +
                "Would you like to open the network configuration wizard?", "Connection Error", MessageBoxButton.YesNo, MessageBoxImage.Stop);
            if (res == MessageBoxResult.No)
                Environment.Exit(-1);

            // Open the network configuration window
            NetworkSetupWizard setupWizard = new();

            setupWizard.ShowDialog();

            // The app should be restarted automatically if the network configuration is successful, if it isn't force exit here.
            Environment.Exit(-1);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            midiDevice?.Dispose();
            statusMessageUpdater?.Dispose();

            if (receiver != null)
            {
                receiver.Close();
                receiver = null;
            }

            oscListenerThread?.Interrupt();
            Application.Current.Shutdown();
        }

        private void SetupMQListener()
        {
            try
            {
                receiver?.Dispose();
                receiver = new OscReceiver(AppResources.network.oscRXPort);
                receiver.Connect();

                sender?.Dispose();
                sender = new OscSender(MQ_IPAddress, AppResources.network.oscTXPort);
                sender.Connect();

                sender.Send(new OscMessage("/feedback/pb+exec"));

                Thread.Sleep(50); // Lazy race-condition avoidal lol
                SetStatusMessage("Connected to MagicQ!");
            }
            catch (Exception ex)
            {
                HandleNetworkError(ex, "OSC");
            }
        }

        private void AdonisWindow_PreviewLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            var window = (Window)sender;
            window.Topmost = (bool)alwaysOnTop.IsChecked;
        }

        private void alwaysOnTop_Unchecked(object sender, RoutedEventArgs e)
        {
            AdonisWindow_PreviewLostKeyboardFocus(this, null);
        }

        private void alwaysOnTop_Checked(object sender, RoutedEventArgs e)
        {
            AdonisWindow_PreviewLostKeyboardFocus(this, null);
        }

        private byte ArtNetSequence = 0;
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

            UpdateDMX();
        }

        public void UpdateDMX(byte[] packet, byte universe)
        {
            ArtNetSequence++;

            if (m_TXsocket.EnableBroadcast)
            {
                m_TXsocket.Send(new ArtNetDmxPacket
                {
                    Sequence = ArtNetSequence,
                    Physical = 1,
                    Universe = (short)(ARTNET_TXUniverse + universe - 1),
                    DmxData = packet
                });
            }
            else
            {
                RdmEndPoint address = new RdmEndPoint(ARTNET_TXIPAddress);
                m_TXsocket.Send(new ArtNetDmxPacket
                {
                    Sequence = ArtNetSequence,
                    Physical = 1,
                    Universe = (short)(ARTNET_TXUniverse + universe - 1),
                    DmxData = packet
                }, address);
            }
            ArtNetactivity(2);
        }

        public void UpdateDMX()
        {
            HashSet<int> universes = new HashSet<int>();

            foreach (FollowSpot spot in FollowSpots)
            {
                universes.Add(spot.Universe);
            }

            foreach (byte universe in universes)
            {
                byte[] packet = new byte[512];

                foreach (FollowSpot spot in FollowSpots)
                {
                    if (universe == spot.Universe)
                    {
                        double pan = spot.Pan + spot.FixtureType.panOffset;
                        double tilt = spot.Tilt + spot.FixtureType.tiltOffset;

                        if (spot.FixtureType.panTiltSwap)
                            (pan, tilt) = (tilt, pan);

                        if (spot.FixtureType.panInvert)
                            pan = -pan;

                        if (spot.FixtureType.tiltInvert)
                            tilt = -tilt;

                        int PanDMX = (int)Math.Round((((pan + (spot.FixtureType.panRange / 2)) / spot.FixtureType.panRange) * 65535.0), 0);
                        int TiltDMX = (int)Math.Round((((tilt + (spot.FixtureType.tiltRange / 2)) / spot.FixtureType.tiltRange) * 65535.0), 0);

                        packet[spot.Address + (spot.FixtureType.panLowChannel - 2)] = (byte)(PanDMX / 256);
                        packet[spot.Address + (spot.FixtureType.panLowChannel - 1)] = (byte)(PanDMX % 256);
                        packet[spot.Address + (spot.FixtureType.tiltLowChannel - 2)] = (byte)(TiltDMX / 256);
                        packet[spot.Address + (spot.FixtureType.tiltLowChannel - 1)] = (byte)(TiltDMX % 256);
                        if (spot.FixtureType.zoomControl)
                            packet[spot.Address + (spot.FixtureType.zoomChannel - 2)] = (byte)(spot.Zoom);
                    }
                }

                UpdateDMX(packet, universe);
            }
        }

        // Thread signal.  
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        Thread listenThread;
        Socket listener;

        public void StartListeningToClients(IPAddress ipAddress)
        {
            // Establish the local endpoint for the socket.  
            // The DNS name of the computer  
            // running the listener is "host.contoso.com".  
            //            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //            IPAddress ipAddress = MQ_IPAddress;
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11000);

            // Create a TCP/IP socket.  
            listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                if (listenThread == null)
                {
                    listenThread = new Thread(new ThreadStart(ClientListenLoop));
                    listenThread.IsBackground = true;
                    listenThread.Start();
                }

            }
            catch (Exception e)
            {
                Logger.Log(e.ToString(), Severity.FATAL);
            }
        }

        public void ClientListenLoop()
        {
            try
            {
                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    Logger.Log("Waiting for a connection...");
                    Socket client = listener.Accept();
                    SetStatusMessage($"Connected to followspot client: {(IPEndPoint)client.RemoteEndPoint}");
                    ClientHandler ch = new (client, this);

                    ch.Start();
                }
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString(), Severity.FATAL);
            }
        }

    }

    [Serializable]
    public struct ClientMessage
    {
        public int clientID;
        public string message;
        public int[] spots;
        public int[] zooms;
        public double[] HeightOffsets;
        public int timeout;
    }
}