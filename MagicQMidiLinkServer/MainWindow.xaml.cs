using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Haukcode.ArtNet.Packets;
using Haukcode.ArtNet.Sockets;
using Haukcode.Sockets;
using Rug.Osc;
using Sanford.Multimedia.Midi;
using System.IO;
using ErrorEventArgs = Sanford.Multimedia.ErrorEventArgs;
using System.Text.Json;
using System.Windows.Interop;

namespace MidiApp
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisUI.Controls.AdonisWindow
    {

#if DEBUG
        //static string MIDI_DEVICE_NAME = "X-TOUCH";
        //        static string MIDI_DEVICE_NAME = "Launchpad Pro";
        static string MIDI_DEVICE_NAME = "loopMIDI";
#else
        static string MIDI_DEVICE_NAME = "loopMIDI";
#endif
        private readonly string resourceFileName = @"Resources\resources.json";
        readonly string[] attributeNames = {"Dimmer", "Dim Mode", "Shutter", "Iris", "Pan", "Tilt", "Col1", "Col2",
                                            "Gobo1", "Gobo2", "Rotate1", "Rotate2", "Focus", "Zoom", "FX1 Prism",
                                            "FX2", "Cyan/Red", "Magenta/Green", "Yellow/Blue", "Col Mix / White", "Cont1 (Lamp on/off)", "Cont2 (Reset)", "Macro", "Macro2",
                                            "CTC", "CTO", "Col3 Speed", "Col4 (Amber)", "Gobo3", "Gobo4", "Gobo Rotate 3", "Prism Rot",
                                            "Frost1", "Frost2", "FX3", "FX4", "FX5", "FX6", "FX7", "FX8",
                                            "Cont3 (Beamm Speed)", "Cont4", "Cont5", "Cont6", "Cont7", "Cont8",
                                            "Pos1", "Pos2", "Pos3", "Pos4", "Pos5","Pos6 (Position Speed)",
                                            "Frame 1 (Top left)", "Frame 2 (Top left)", "Frame 3 (Bottom left)", "Frame 4 (Bottom left)", "Frame 5 (Top Right)", "Frame 6 (Top Right)", "Frame 7 (Bottom Right)", "Frame 8 (Bottom Right)",
                                            "Lime/UV", "Col5", "Col6", "Reserved"};


        public static List<FollowSpot> FollowSpots { get; } = new();
        public static AppResourcesData appResources;

        private OscSender sender;
        private OscReceiver receiver;

        private const int SysExBufferSize = 128;

        private InputDevice inDevice = null;
        private OutputDevice outDevice = null;

        private Thread m_Thread = null;
        private Thread activity_Thread = null;
        private Thread fsActivity_Thread = null;
        private Thread artNetActivity_Thread = null;
        private ArtNetSocket m_socket = null;
        private ArtNetSocket m_TXsocket = null;

        private Thread mqConnection_Thread = null;

        private Thread m_ResourceLoader_Thread = null;

        private Dictionary<string, int> attributes = new Dictionary<string, int>();

        private int selected_Attribute = 0;

        private SynchronizationContext context;

        private bool[] buttons;
        private float[] faders;
        private bool[] encoderState;

        public int selectedPlayback = 0;

        public MainWindow()
        {
            InitializeComponent();

            buttons = new bool[256];
            faders = new float[256];
            encoderState = new bool[256];

            //String strHostName = Dns.GetHostName();
            //IPHostEntry iphostentry = Dns.GetHostEntry(strHostName);

            //foreach (IPAddress ipaddress in iphostentry.AddressList)
            //{
            //    if (ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            //        ipInputMQ.Items.Add(ipaddress.ToString());

            //    if (ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            //        ipInputTX.Items.Add(ipaddress.ToString());

            //}

            context = SynchronizationContext.Current;

            for (int i = 0; i < attributeNames.Length; i++)
            {
                attributes.Add(attributeNames[i], i);
            }

            appResources = GetAppResource();
            m_ResourceLoader_Thread = new Thread(new ThreadStart(ResourceLoaderLoop));
            m_ResourceLoader_Thread.IsBackground = true;
            m_ResourceLoader_Thread.Start();

            try
            {
                MQ_IPAddress = IPAddress.Parse((string)appResources.network.magicQIP);
                ARTNET_RXIPAddress = IPAddress.Parse((string)appResources.network.artNet.rxIP);
                ARTNET_RXSubNetMask = IPAddress.Parse((string)appResources.network.artNet.rxSubNetMask);
                ARTNET_TXIPAddress = IPAddress.Parse((string)appResources.network.artNet.txIP);
                ARTNET_TXSubNetMask = IPAddress.Parse((string)appResources.network.artNet.txSubNetMask);
                ARTNET_TXUseBroadcast = (bool)appResources.network.artNet.broadcast;
                ARTNET_TXUniverse = (int)appResources.network.artNet.universe;
            }
            catch (Exception e)
            {
                MessageBox.Show("Cannot parse resource file\n" + e.Message, "Resource File Problem", MessageBoxButton.OK, MessageBoxImage.Stop);
                Close();
            }
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
            try
            {
                string res = File.ReadAllText(resourceFileName);
                File.WriteAllText(resourceFileName + ".bak", res);
            } catch (FileNotFoundException)
            {

            }

            JsonSerializerOptions options = new(AppResourcesData.JsonSerializerOptions)
            { 
                WriteIndented = true,
            };

            File.WriteAllText(resourceFileName, JsonSerializer.Serialize(appResources, options));
        }

        public AppResourcesData GetAppResource()
        {
            try
            {
                var res = File.ReadAllText(resourceFileName);
                var data = JsonSerializer.Deserialize<AppResourcesData>(res, AppResourcesData.JsonSerializerOptions);
                if (data.fileFormatVersion != AppResourcesData.FILE_FORMAT_VERSION)
                {
                    MessageBox.Show($"Resource file has an invalid file format version: {data.fileFormatVersion} expected: {AppResourcesData.FILE_FORMAT_VERSION}.", 
                        "Resource File Version Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                    Environment.Exit(-1);
                }
                return data;
            }  catch (JsonException e)
            {
                MessageBox.Show("Couldn't parse resource file\n" + e, "Resource File Parse Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                Environment.Exit(-1);
                return default;
            } catch (FileNotFoundException)
            {
                MessageBox.Show("Cannot find resource file\n" + resourceFileName + "\nAn empty resource file will be created!", "File Not Found", 
                    MessageBoxButton.OK, MessageBoxImage.Stop);
                appResources = new();
                SaveAppResource();
                Environment.Exit(-1);
                return default;
            }
        }

        public void ResourceLoaderLoop()
        {
            DateTime time = System.IO.File.GetLastWriteTime(resourceFileName);

            while (true)
            {
                try
                {
                    DateTime latestTime = System.IO.File.GetLastWriteTime(resourceFileName);

                    if (latestTime > time)
                    {
                        appResources = GetAppResource();
                        time = latestTime;
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (ThreadInterruptedException)
                {

                }
            }
        }

        public void LookForXTouch()
        {
            XtouchSearcher.DoWorkWithModal(this, progress =>
            {

                while (inDevice == null)
                {
                    if (inDevice == null)
                    {
                        progress.Report("Searching.");
                        for (var d = 0; d < InputDevice.DeviceCount; d++)
                        {
                            Debug.WriteLine("Midi Input Device: " + InputDevice.GetDeviceCapabilities(d).name);

                            if (InputDevice.GetDeviceCapabilities(d).name.Contains(MIDI_DEVICE_NAME))
                            {
                                inDevice = new InputDevice(d);
                                break;
                            }
                        }
                        Thread.Sleep(500);
                    }


                    if (outDevice == null)
                    {
                        progress.Report("Searching..");
                        for (var d = 0; d < OutputDevice.DeviceCount; d++)
                        {
                            Debug.WriteLine("Midi Output Device: " + OutputDevice.GetDeviceCapabilities(d).name);
                            if (OutputDevice.GetDeviceCapabilities(d).name.Contains(MIDI_DEVICE_NAME))
                            {
                                outDevice = new OutputDevice(d);
                                break;
                            }
                        }
                        Thread.Sleep(500);
                    }
                }

            });
        }

        Brush GreenFill = null;
        Brush RedFill = null;
        Brush WhiteFill = null;

        Stopwatch activityTimer = Stopwatch.StartNew();

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

                }

            }
        }

        public void Activity(int type)
        {
            activityTimer.Restart();
            context?.Post(delegate (object dummy)
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
            Color rgb = new Color();
            rgb.A = 0xff;
            rgb.R = Convert.ToByte(r * 255.0f);
            rgb.G = Convert.ToByte(g * 255.0f);
            rgb.B = Convert.ToByte(b * 255.0f);
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

            if (context != null)
            {
                context.Post(delegate (object dummy)
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
            }

            while (true)
            {
                try
                {
                    Thread.Sleep(100);
                    if (context != null)
                    {
                        context.Post(delegate (object dummy)
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
                }
                catch (ThreadInterruptedException)
                {

                }

            }
        }

        public void FSactivity(int clientID)
        {
            FSactivityTimer.Restart();
            if (context != null)
            {
                context.Post(delegate (object dummy)
                {
                    if (clientID >= 0)
                        clientButtons[clientID].Background = clientColours[clientID][2];
                }, null);
            }
        }


        Stopwatch ArtNetactivityTimer = Stopwatch.StartNew();

        void ArtNetActivityMonitor()
        {

            while (true)
            {
                try
                {
                    Thread.Sleep(100);
                    context?.Post(delegate (object dummy)
                        {
                            if (ArtNetactivityTimer.ElapsedMilliseconds > 200)
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
            ArtNetactivityTimer.Restart();
            context?.Post(delegate (object dummy)
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

        void ListenLoop()
        {
            bool justRecieved = false;

            while (receiver != null)
            {
                try
                {
                    justRecieved = false;

                    if (outDevice != null)
                    {
                        OscPacket pkt = receiver.Receive();
                        justRecieved = true;

                        Debug.WriteLine("OSC Packet: " + pkt.ToString());
                        Activity(1);
                        ActivityMQ(1);

                        //  /exec/1/1, 0f
                        //  /exec/1/57, 0f
                        //  /pb/1, 0.796078f
                        //  /pb/1/flash, 1
                        //  /pb/3, 0f
                        //  /pb/3/flash, 0

                        OscMessage msg = OscMessage.Parse(pkt.ToString());

                        var parts = msg.Address.Split('/');

                        if (parts[1].Equals("exec"))
                        {
                            int page = Convert.ToInt32(parts[2]) - 1;
                            int execNum = Convert.ToInt32(parts[3]) - 1;
                            bool value = ((float)(msg[0]) > 0.5f);
                            int buttonId = 64 * page + execNum;

                            if (buttons[buttonId] != value)
                            {
                                buttons[buttonId] = value;

                                ChannelMessageBuilder builder = new()
                                {
                                    Command = ChannelCommand.NoteOn,
                                    Data1 = (execNum + 16),
                                    Data2 = buttons[buttonId] ? 1 : 0
                                };
                                builder.Build();
                                outDevice.Send(builder.Result);
                                // Debug.WriteLine("SetButton: " + (buttonId).ToString() + " " + buttons[buttonId]);
                            }

                        }
                        else if (parts[1].Equals("pb"))
                        {
                            int playback = Convert.ToInt32(parts[2]);
                            if (parts.Length > 3)
                            {
                                if (parts[3].Equals("flash"))
                                {

                                }
                            }
                            else
                            {
                                float value = ((float)(msg[0]));
                                if ((int)(127.0f * faders[playback]) != (int)(127.0f * value))
                                {
                                    faders[playback] = value;

                                    ChannelMessageBuilder builder = new ChannelMessageBuilder();
                                    builder.Command = ChannelCommand.Controller;
                                    builder.MidiChannel = 0;
                                    if (playback >= 10)
                                    {
                                        //for (var j = 0; j < 9; j++)
                                        //{
                                        //    builder.MidiChannel = 1;
                                        //    builder.Data1 = playback + j;
                                        //    builder.Data2 = (byte)(j*2);
                                        //    builder.Build();
                                        //    outDevice.Send(builder.Result);
                                        //    builder.MidiChannel = 0;
                                        //    builder.Data1 = playback+j;
                                        //    builder.Data2 = (byte)(value * 127.0f);
                                        //    builder.Build();
                                        //    outDevice.Send(builder.Result);
                                        //}
                                    }
                                    else
                                    {
                                        builder.Data1 = playback;
                                        builder.Data2 = (byte)(value * 127.0f);
                                        builder.Build();
                                        outDevice.Send(builder.Result);
                                    }

                                    //Debug.WriteLine("Set Fader: " + (playback).ToString() + " " + value);
                                }

                            }

                        }
                        // fspot/start/<mouse1 spots list>$message+<mouse2 spots list>$message@<viewID>
                        else if (parts[1].Equals("fspot"))
                        {
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

                                                int headId = Int32.Parse(idspot);
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
                        else if (parts[1].Equals("sel"))
                        {
                            if (parts.Length >= 2)
                            {
                                if (Int32.TryParse(parts[2], out int selected_pb))
                                {
                                    ChannelMessageBuilder builder = new ChannelMessageBuilder();
                                    for (int j = 0; j < 9; j++)
                                    {
                                        buttons[j + 40 - 16] = false;
                                    }
                                    buttons[selected_pb + 39 - 16] = true;
                                    selectedPlayback = selected_pb;
                                    Debug.WriteLine("selectedPlayback: " + selectedPlayback);
                                    builder.Command = ChannelCommand.NoteOn;
                                    for (int j = 0; j < 9; j++)
                                    {
                                        builder.Data1 = j + 40;
                                        builder.Data2 = buttons[j + 40 - 16] ? 2 : 0;
                                        builder.Build();
                                        outDevice.Send(builder.Result);
                                    }
                                }
                            }
                        }

                    }
                }
                catch (System.Exception e)
                {
                    Debug.WriteLine("Exception Thrown" + e);
                }

                try
                {
                    if (!justRecieved)
                        Thread.Sleep(100);
                }
                catch (System.Threading.ThreadInterruptedException)
                {

                }
            }
        }

        public void SendClientMessage(int clientID, string message, string spots, int timeout)
        {
            ClientMesage msg = new();

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


        public void Mover(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Debug.WriteLine("Mouse position" + e.GetPosition(this));
        }

        private void Window_Loaded(object source, RoutedEventArgs e)
        {

            context = SynchronizationContext.Current;

            if (inDevice == null)
            {
                LookForXTouch();

                if (inDevice == null)
                {
                    Close();
                    return;
                }
            }

            attrName.Text = attributeNames[selected_Attribute];

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

                setupMQListener();

                inDevice.ChannelMessageReceived += HandleChannelMessageReceived;
                inDevice.SysCommonMessageReceived += HandleSysCommonMessageReceived;
                inDevice.SysExMessageReceived += HandleSysExMessageReceived;
                inDevice.SysRealtimeMessageReceived += HandleSysRealtimeMessageReceived;
                inDevice.Error += new EventHandler<Sanford.Multimedia.ErrorEventArgs>(inDevice_Error);

                if (!MIDI_DEVICE_NAME.Contains("Loop"))
                    inDevice.StartRecording();

                ChannelMessageBuilder builder = new();

                builder.Command = ChannelCommand.Controller;
                builder.Data1 = 127;
                builder.Data2 = 0;
                builder.Build();
                outDevice.Send(builder.Result);

                builder.Command = ChannelCommand.ProgramChange;
                builder.MidiChannel = 1;
                builder.Data1 = 0;
                builder.Data2 = 0;
                builder.Build();
                outDevice.Send(builder.Result);

                builder.Command = ChannelCommand.Controller;

                for (var i = 0; i < 127; i++)
                {
                    builder.Data1 = i;
                    builder.Data2 = 0;
                    builder.Build();
                    outDevice.Send(builder.Result);
                }

                builder.Command = ChannelCommand.NoteOn;
                builder.Data1 = 0;
                builder.Data2 = 1;
                builder.Build();
                outDevice.Send(builder.Result);

                for (var i = 0; i < 39; i++)
                {
                    builder.Data1 = i;
                    builder.Data2 = (i == 32) ? 2 : 0;
                    builder.Build();
                    outDevice.Send(builder.Result);
                }

                selectedPlayback = 9;

                builder.Command = ChannelCommand.NoteOn;
                builder.Data1 = 38;
                builder.Data2 = 2;
                builder.Build();
                outDevice.Send(builder.Result);

                ipInputMQ.Content = MQ_IPAddress;
                ipInputTX.Content = ARTNET_TXIPAddress;

                m_Thread = new(new ThreadStart(ListenLoop));
                m_Thread.IsBackground = true;
                m_Thread.Start();

                StartListening(MQ_IPAddress);

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
                FollwSpot_dataGrid.ItemsSource = FollowSpots;

                ArtNetListner();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
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
            //Debug.WriteLine($"Received ArtNet packet with OpCode: {e.Packet.OpCode} from {e.Source}");
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

                        //Debug.WriteLine("P: {0}, T:{1}", p, t);

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

            m_socket.Open(ARTNET_RXIPAddress, ARTNET_RXSubNetMask);

            //            addr = addresses.ToArray()[0];
            m_TXsocket.Open(ARTNET_TXIPAddress, ARTNET_TXSubNetMask);
            //            m_TXsocket.Open(addr.Address, addr.NetMask);
            m_TXsocket.EnableBroadcast = ARTNET_TXUseBroadcast;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (inDevice != null)
            {
                inDevice.Close();
            }

            if (outDevice != null)
            {
                outDevice.Close();
            }


            if (receiver != null)
            {
                receiver.Close();
                receiver = null;
            }

            if (m_Thread != null)
                m_Thread.Interrupt();
            Application.Current.Shutdown();

        }

        private void inDevice_Error(object source, ErrorEventArgs e)
        {
            MessageBox.Show(e.Error.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        private void HandleChannelMessageReceived(object source, ChannelMessageEventArgs e)
        {

            Activity(0);
            Debug.WriteLine("Channel Message: " + e.Message.Command.ToString() + ", " + e.Message.Data1 + ", " + e.Message.Data2);
            if ((e.Message.Command == ChannelCommand.Controller) && (e.Message.Data1 < 10))
            {
                sender.Send(new OscMessage("/pb/" + e.Message.Data1, e.Message.Data2 / 127.0f));
                faders[e.Message.Data1] = e.Message.Data2 / 127.0f;
            }
            else if ((e.Message.Command == ChannelCommand.Controller) && (e.Message.Data1 >= 10))
            {
                int attribute = (e.Message.Data1 - 10);
                int delta = e.Message.Data2;

                if (e.Message.Data1 == 25)
                {
                    attribute = selected_Attribute;
                }

                if (e.Message.Data1 == 24)
                {
                    delta = (e.Message.Data2 < 64) ? 1 : -1;

                    selected_Attribute = (selected_Attribute + delta) % (attributeNames.Length);

                    if (selected_Attribute < 0)
                        selected_Attribute += attributeNames.Length;

                    sender.Send(new OscMessage("/pb/10/" + (selected_Attribute + 1), 1.00f));

                    context.Post(delegate (object dummy)
                    {
                        attrName.Text = attributeNames[selected_Attribute];
                    }, null);

                }
                else if (e.Message.Data2 < 64)
                {
                    if (encoderState[e.Message.Data1 - 10])
                    {
                        delta *= 2;
                    }

                    sender.Send(new OscMessage("/rpc", "\\07," + (attribute) + "," + (delta) + "H"));
                    //Debug.WriteLine("Controller: " + "/rpc " + "\\07, " + (attribute) + ", " + (delta) + "," + ((encoderState[e.Message.Data1 - 10]) ? "1" : "0") + "H");
                }
                else
                {
                    delta = e.Message.Data2 - 64;
                    if (encoderState[e.Message.Data1 - 10])
                    {
                        delta *= 2;
                    }
                    sender.Send(new OscMessage("/rpc", "\\08," + (attribute) + "," + (delta) + "H"));
                    //Debug.WriteLine("Controller: " +"/rpc " + "\\08, " + (attribute) + ", " + (delta) + "," +((encoderState[e.Message.Data1 - 10]) ?"1":"0")+ "H");
                }

                //Debug.WriteLine("Controller: " + e.Message.Data1 + ":" + e.Message.Data2);

            }
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 < 16) && (e.Message.Data2 == 127))
            {
                encoderState[e.Message.Data1] = !encoderState[e.Message.Data1];
                ChannelMessageBuilder builder = new ChannelMessageBuilder();
                builder.Command = ChannelCommand.Controller;
                int encoderId = e.Message.Data1 + 10;

                builder.MidiChannel = 1;
                builder.Data1 = encoderId;
                builder.Data2 = encoderState[e.Message.Data1] ? 0 : 4;
                builder.Build();
                outDevice.Send(builder.Result);
                builder.MidiChannel = 0;
                builder.Data1 = encoderId;
                builder.Data2 = encoderState[e.Message.Data1] ? 0 : 64;
                builder.Build();
                outDevice.Send(builder.Result);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 16) && (e.Message.Data1 <= 39) && (e.Message.Data2 == 127))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();
                buttons[e.Message.Data1 - 16] = !buttons[e.Message.Data1 - 16];

                sender.Send(new OscMessage("/exec/" + (e.Message.Data1 - 15), buttons[e.Message.Data1 - 16] ? 1 : 0));
                //  Debug.WriteLine("Send: /exec/" + (e.Message.Data1 - 15).ToString() + buttons[e.Message.Data1-16]);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOff) && (e.Message.Data1 >= 16) && (e.Message.Data1 <= 39) && (e.Message.Data2 == 0))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();

                builder.Command = ChannelCommand.NoteOn;
                builder.Data1 = (e.Message.Data1);
                builder.Data2 = buttons[e.Message.Data1 - 16] ? 1 : 0;
                builder.Build();
                outDevice.Send(builder.Result);
                // Debug.WriteLine("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
                //                sender.Send(new OscMessage("/feedback/exec"));
            }
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 40) && (e.Message.Data1 <= 48) && (e.Message.Data2 >= 50))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();
                for (int j = 0; j < 9; j++)
                {
                    buttons[j + 40 - 16] = false;
                }
                buttons[e.Message.Data1 - 16] = true;
                selectedPlayback = e.Message.Data1 - 39;
                Debug.WriteLine("selectedPlayback: " + selectedPlayback);
                builder.Command = ChannelCommand.NoteOn;
                for (int j = 0; j < 9; j++)
                {
                    builder.Data1 = j + 40;
                    builder.Data2 = buttons[j + 40 - 16] ? 2 : 0;
                    builder.Build();
                    outDevice.Send(builder.Result);
                }
                //  Debug.WriteLine("Send: /exec/" + (e.Message.Data1 - 15).ToString() + buttons[e.Message.Data1-16]);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOff) && (e.Message.Data1 >= 40) && (e.Message.Data1 <= 48) && (e.Message.Data2 == 0))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();

                builder.Command = ChannelCommand.NoteOn;
                for (int j = 0; j < 9; j++)
                {
                    builder.Data1 = j + 40;
                    builder.Data2 = buttons[j + 40 - 16] ? 2 : 0;
                    builder.Build();
                    outDevice.Send(builder.Result);
                }
                //Debug.WriteLine("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 49) && (e.Message.Data1 <= 54) && (e.Message.Data2 >= 50))
            {
                if (selectedPlayback > 0)
                {
                    switch (e.Message.Data1)
                    {
                        case 49:
                            sender.Send(new OscMessage("/rpc", selectedPlayback + "B"));
                            break;
                        case 50:
                            sender.Send(new OscMessage("/rpc", selectedPlayback + "F"));
                            break;
                        case 51:
                            sender.Send(new OscMessage("/rpc", "\\31H"));
                            break;
                        case 52:
                            sender.Send(new OscMessage("/rpc", "\\30H"));
                            break;
                        case 53:
                            sender.Send(new OscMessage("/rpc", selectedPlayback + "S"));
                            sender.Send(new OscMessage("/rpc", selectedPlayback + "R"));
                            break;
                        case 54:
                            sender.Send(new OscMessage("/rpc", selectedPlayback + "G"));
                            break;
                    }
                }

                //Debug.WriteLine("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
                //                sender.Send(new OscMessage("/feedback/exec"));

            }
            else if ((e.Message.Command == ChannelCommand.NoteOff) && (e.Message.Data1 == 54))
            {
                ChannelMessageBuilder b2 = new ChannelMessageBuilder();

                b2.Command = ChannelCommand.NoteOn;
                b2.MidiChannel = 1;

                b2.Data1 = 38;
                b2.Data2 = 2;
                b2.Build();
                outDevice.Send(b2.Result);

            }

        }


        private void HandleSysExMessageReceived(object source, SysExMessageEventArgs e)
        {
            context.Post(delegate (object dummy)
            {
                string result = "\n\n"; ;

                foreach (byte b in e.Message)
                {
                    result += string.Format("{0:X2} ", b);
                }

                //                sysExRichTextBox.AppendText(result);
            }, null);
        }

        private void HandleSysCommonMessageReceived(object source, SysCommonMessageEventArgs e)
        {
        }


        Stopwatch FWatch = Stopwatch.StartNew();
        double FLastMillis;
        double FDiff;
        double FDiffTimeStamp;
        int counter;
        int FLastTimestamp;

        private void HandleSysRealtimeMessageReceived(object source, SysRealtimeMessageEventArgs e)
        {
            counter++;
            if (counter % 24 == 0)
            {
                var millis = FWatch.Elapsed.TotalMilliseconds;
                FDiff = 60000 / (millis - FLastMillis);
                FLastMillis = millis;

                var timestamp = e.Message.Timestamp;
                FDiffTimeStamp = 60000.0 / (timestamp - FLastTimestamp);
                FLastTimestamp = timestamp;
            }
        }

        private void setupMQListener()
        {
            int port = 8000;
            if (receiver != null)
            {
                receiver.Dispose();
            }
            receiver = new OscReceiver(9000);
            receiver.Connect();

            if (m_Thread != null)
            {
                m_Thread.Abort();
                m_Thread = new(new ThreadStart(ListenLoop));
                m_Thread.IsBackground = true;
                m_Thread.Start();
            }

            if (sender != null)
            {
                sender.Dispose();
            }

            sender = new OscSender(MQ_IPAddress, port);
            sender.Connect();

            sender.Send(new OscMessage("/feedback/pb+exec"));
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
                        double pan = spot.Pan;
                        double tilt = spot.Tilt;

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
                        if(spot.FixtureType.zoomControl)
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

        public void StartListening(IPAddress ipAddress)
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
                    listenThread = new Thread(new ThreadStart(listenLoop));
                    listenThread.IsBackground = true;
                    listenThread.Start();
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public void listenLoop()
        {
            try
            {
                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    Debug.WriteLine("Waiting for a connection...");
                    Socket client = listener.Accept();
                    Debug.WriteLine("Connected ...");
                    ClientHandler ch = new ClientHandler(client, this);

                    ch.Start();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
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
}