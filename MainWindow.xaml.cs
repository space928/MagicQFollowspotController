using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Haukcode.ArtNet.Packets;
using Haukcode.ArtNet.Sockets;
using Haukcode.Sockets;
using Rug.Osc;
using Sanford.Multimedia;
using Sanford.Multimedia.Midi;
using System.Runtime.Serialization.Formatters.Binary;

namespace MidiApp
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisUI.Controls.AdonisWindow
    {

#if NDEBUG
        //static string MIDI_DEVICE_NAME = "X-TOUCH";
        //        static string MIDI_DEVICE_NAME = "Launchpad Pro";
        static string MIDI_DEVICE_NAME = "LoopBe";
#else
        static string MIDI_DEVICE_NAME = "X-TOUCH";
#endif

        double TILT_RANGE = 270.0;
        bool TILT_INVERT = false;

        double PAN_RANGE = 540.0;
        bool PAN_INVERT = true;

        int PAN_DMX_OFFSET = 22;
        int TILT_DMX_OFFSET = 24;

        public OscSender sender;
        public OscReceiver receiver;

        public const int SysExBufferSize = 128;

        public InputDevice inDevice = null;
        public OutputDevice outDevice = null;

        public Thread m_Thread = null;
        public Thread activity_Thread = null;
        public Thread FSactivity_Thread = null;
        public Thread ArtNetactivity_Thread = null;
        public ArtNetSocket m_socket = null;
        public ArtNetSocket m_TXsocket = null;
        public static List<Follow_Spot> m_spots = new List<Follow_Spot>();

        public Thread mqConnection_Thread = null;

        public string resourceFileName = @"resources.json";
        public static dynamic AppResources;
        public Thread m_ResourceLoader_Thread = null;

        Dictionary<string, int> attributes = new Dictionary<string, int>();

        string[] attributeNames = {"Dimmer", "Dim Mode", "Shutter", "Iris", "Pan", "Tilt", "Col1", "Col2",
                                    "Gobo1", "Gobo2", "Rotate1", "Rotate2", "Focus", "Zoom", "FX1 Prism",
                                    "FX2", "Cyan/Red", "Magenta/Green", "Yellow/Blue", "Col Mix / White", "Cont1 (Lamp on/off)", "Cont2 (Reset)", "Macro", "Macro2",
                                    "CTC", "CTO", "Col3 Speed", "Col4 (Amber)", "Gobo3", "Gobo4", "Gobo Rotate 3", "Prism Rot",
                                    "Frost1", "Frost2", "FX3", "FX4", "FX5", "FX6", "FX7", "FX8",
                                    "Cont3 (Beamm Speed)", "Cont4", "Cont5", "Cont6", "Cont7", "Cont8",
                                    "Pos1", "Pos2", "Pos3", "Pos4", "Pos5","Pos6 (Position Speed)",
                                    "Frame 1 (Top left)", "Frame 2 (Top left)", "Frame 3 (Bottom left)", "Frame 4 (Bottom left)", "Frame 5 (Top Right)", "Frame 6 (Top Right)", "Frame 7 (Bottom Right)", "Frame 8 (Bottom Right)",
                                    "Lime/UV", "Col5", "Col6", "Reserved"
                };

        int selected_Attribute = 0;

        SynchronizationContext context;

        bool[] buttons;
        float[] faders;
        bool[] encoderState;

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

            AppResources = getAppResource();
            m_ResourceLoader_Thread = new Thread(new ThreadStart(resourceLoaderLoop));
            m_ResourceLoader_Thread.IsBackground = true;
            m_ResourceLoader_Thread.Start();

            try
            {
                MQ_IPAddress = IPAddress.Parse((string)AppResources.Network.MAgicQIP);
                ARTNET_RXIPAddress = IPAddress.Parse((string)AppResources.Network.ArtNet.RXIP);
                ARTNET_RXSubNetMask = IPAddress.Parse((string)AppResources.Network.ArtNet.RXSubNetMask);
                ARTNET_TXIPAddress = IPAddress.Parse((string)AppResources.Network.ArtNet.TXIP);
                ARTNET_TXSubNetMask = IPAddress.Parse((string)AppResources.Network.ArtNet.TXSubNetMask);
                ARTNET_TXUseBroadcast = (bool)AppResources.Network.ArtNet.Broadcast;
                ARTNET_TXUniverse = (int)AppResources.Network.ArtNet.Universe;
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

        public void saveAppResource()
        {
            string res = System.IO.File.ReadAllText(resourceFileName);
            System.IO.File.WriteAllText(resourceFileName + ".bak", res);

            System.IO.File.WriteAllText(resourceFileName, Newtonsoft.Json.JsonConvert.SerializeObject(AppResources));
        }

        public dynamic getAppResource()
        {
            try
            {
                var res = System.IO.File.ReadAllText(resourceFileName);
                return Newtonsoft.Json.JsonConvert.DeserializeObject(res);
            }
            catch (System.IO.FileNotFoundException)
            {
                MessageBox.Show("Cannot find resource file\n" + resourceFileName, "File Not Found", MessageBoxButton.OK, MessageBoxImage.Stop);
                Close();
                return null;
            }
        }
        public void resourceLoaderLoop()
        {
            DateTime time = System.IO.File.GetLastWriteTime(resourceFileName);

            while (true)
            {
                try
                {
                    DateTime latestTime = System.IO.File.GetLastWriteTime(resourceFileName);

                    if (latestTime > time)
                    {
                        AppResources = getAppResource();
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
                            Console.WriteLine("Midi Input Device: " + InputDevice.GetDeviceCapabilities(d).name);

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
                            Console.WriteLine("Midi Output Device: " + OutputDevice.GetDeviceCapabilities(d).name);
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
                    if (context != null)
                    {
                        context.Post(delegate (object dummy)
                        {
                            if (activityTimer.ElapsedMilliseconds > 200)
                            {
                                ActivityLED.Fill = WhiteFill;
                            }
                        }, null);
                    }
                }
                catch (ThreadInterruptedException)
                {

                }

            }
        }

        public void activity(int type)
        {
            activityTimer.Restart();
            if (context != null)
            {
                context.Post(delegate (object dummy)
                {
                    ActivityLED.Fill = GreenFill;
                }, null);
            }
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

        double[] clientHues = { 0, 120, 240, 300, 60};

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
                    if (clientID>=0)
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
                    if (context != null)
                    {
                        context.Post(delegate (object dummy)
                        {
                            if (ArtNetactivityTimer.ElapsedMilliseconds > 200)
                            {
                                ArtNetActivityLED.Fill = WhiteFill;
                            }
                        }, null);
                    }
                }
                catch (ThreadInterruptedException)
                {

                }

            }
        }

        public void ArtNetactivity(int type)
        {
            ArtNetactivityTimer.Restart();
            if (context != null)
            {
                context.Post(delegate (object dummy)
                {
                    ArtNetActivityLED.Fill = GreenFill;
                }, null);
            }
        }

        Stopwatch connectionTimer = Stopwatch.StartNew();
        void mqConnection_Monitor()
        {

            while (true)
            {
                try
                {
                    Thread.Sleep(500);
                    if (context != null)
                    {
                        context.Post(delegate (object dummy)
                        {
                            if (connectionTimer.ElapsedMilliseconds > 1000)
                            {
                                ConnectionLED.Fill = WhiteFill;
                            }
                        }, null);
                    }
                }
                catch (ThreadInterruptedException)
                {

                }

            }
        }

        public void activityMQ(int type)
        {
            connectionTimer.Restart();
            if (context != null)
            {
                context.Post(delegate (object dummy)
                {
                    ConnectionLED.Fill = GreenFill;
                }, null);
            }
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

                        Console.WriteLine("OSC Packet: " + pkt.ToString());
                        activity(1);
                        activityMQ(1);

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

                                ChannelMessageBuilder builder = new ChannelMessageBuilder();

                                builder.Command = ChannelCommand.NoteOn;
                                builder.Data1 = (execNum + 16);
                                builder.Data2 = buttons[buttonId] ? 1 : 0;
                                builder.Build();
                                outDevice.Send(builder.Result);
                                // Console.WriteLine("SetButton: " + (buttonId).ToString() + " " + buttons[buttonId]);
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

                                    //Console.WriteLine("Set Fader: " + (playback).ToString() + " " + value);
                                }

                            }

                        }
                        // fspot/start/<mouse1 spots list>#<mouse2 spots list>@<viewID>
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
                                            viewID = Int32.Parse(ids.Substring(viewpos + 1));
                                            ids = ids.Substring(0, viewpos);
                                        }
                                        else
                                        {
                                            viewpos = ids.Length;
                                        }

                                        ids = ids.Substring(ids.LastIndexOf('/') + 1);

                                        foreach (Follow_Spot spot in m_spots)
                                        {
                                            spot.MouseControlID = -1;
                                        }

                                        int mouse = 0;

                                        foreach (string s in ids.Split('+'))
                                        {
                                            foreach (string idspot in s.Split(','))
                                            {
                                                int headId = Int32.Parse(idspot);
                                                foreach (Follow_Spot spot in m_spots)
                                                {
                                                    if (spot.Head == headId)
                                                        spot.MouseControlID = mouse;
                                                }
                                            }

                                            mouse++;
                                        }

                                    }

                                    //context.Post(delegate (object dummy)
                                    //{
                                    //    if (m_threeDWindow == null)
                                    //    {
                                    //        m_threeDWindow = new ThreeD();
                                    //        m_threeDWindow.grab();
                                    //    }
                                    //    else
                                    //    {
                                    //        m_threeDWindow.Show();
                                    //    }

                                    //    if (viewID > 0)
                                    //    {
                                    //        m_threeDWindow.setCameraView(viewID - 1);
                                    //    }

                                    //    if (spotsOnMouseControl())
                                    //    {
                                    //        m_threeDWindow.Macro_moveSpot(0);
                                    //        m_threeDWindow.setActive(true);
                                    //    }
                                    //    else
                                    //    {
                                    //        m_threeDWindow.setActive(false);
                                    //    }
                                    //}, null);

                                }
                                else if (parts[2] == "stop")
                                {
                                    foreach (Follow_Spot spot in m_spots)
                                    {
                                        spot.MouseControlID = -1;
                                    }

                                    //if (m_threeDWindow != null)
                                    //{
                                    //    context.Post(delegate (object dummy)
                                    //    {
                                    //        m_threeDWindow.setActive(false);
                                    //    }, null);
                                    //}
                                }
                            }

                        }

                    }
                }
                catch (System.Exception)
                {

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

        public void Mover(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Console.WriteLine("Mouse position" + e.GetPosition(this));
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



                if (FSactivity_Thread == null)
                {
                    FSactivity_Thread = new Thread(new ThreadStart(FSActivityMonitor));
                    FSactivity_Thread.IsBackground = true;
                    FSactivity_Thread.Start();
                }

                if (activity_Thread == null)
                {
                    activity_Thread = new Thread(new ThreadStart(ActivityMonitor));
                    activity_Thread.IsBackground = true;
                    activity_Thread.Start();
                }

                if (ArtNetactivity_Thread == null)
                {
                    ArtNetactivity_Thread = new Thread(new ThreadStart(ArtNetActivityMonitor));
                    ArtNetactivity_Thread.IsBackground = true;
                    ArtNetactivity_Thread.Start();
                }

                if (mqConnection_Thread == null)
                {
                    mqConnection_Thread = new Thread(new ThreadStart(mqConnection_Monitor));
                    mqConnection_Thread.IsBackground = true;
                    mqConnection_Thread.Start();
                }

                inDevice.ChannelMessageReceived += HandleChannelMessageReceived;
                inDevice.SysCommonMessageReceived += HandleSysCommonMessageReceived;
                inDevice.SysExMessageReceived += HandleSysExMessageReceived;
                inDevice.SysRealtimeMessageReceived += HandleSysRealtimeMessageReceived;
                inDevice.Error += new EventHandler<ErrorEventArgs>(inDevice_Error);

                if (!MIDI_DEVICE_NAME.Contains("Loop"))
                    inDevice.StartRecording();

                ChannelMessageBuilder builder = new ChannelMessageBuilder();

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

                setupMQListener();
                m_Thread = new Thread(new ThreadStart(ListenLoop));
                m_Thread.IsBackground = true;
                m_Thread.Start();

                StartListening(MQ_IPAddress);

                foreach (dynamic v in AppResources.Lights)
                {
                    Follow_Spot spot = new Follow_Spot();
                    spot.Head = v.Head;
                    spot.Universe = v.Universe;
                    spot.Address = v.Address;
                    spot.MouseControlID = -1;

                    Point3D p;
                    switch ((int)v.Bar)
                    {
                        case 0: p = new Point3D((double)v.XOffset, 0.0, (double)AppResources.Bar0Height + 0.1); break;
                        case -1: p = new Point3D((double)v.XOffset, (double)AppResources.BarAudienceOffset, (double)AppResources.BarAudienceHeight + 0.1); break;
                        case 1: p = new Point3D((double)v.XOffset, (double)AppResources.Bar1Offset, (double)AppResources.Bar1Height + 0.1); break;
                        case 2: p = new Point3D((double)v.XOffset, (double)AppResources.Bar2Offset, (double)AppResources.Bar2Height + 0.1); break;
                        default: p = new Point3D((double)v.XOffset, (double)AppResources.Bar3Offset, (double)AppResources.Bar3Height + 0.1); break;
                    }
                    spot.Location = p;

                    m_spots.Add(spot);
                }
                FollwSpot_dataGrid.ItemsSource = m_spots;

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

        public bool spotsOnMouseControl()
        {
            int i = 0;
            for (i = 0; i < MainWindow.m_spots.Count; i++)
            {
                if (MainWindow.m_spots[i].MouseControlID >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        void ArtNet_NewPacket(object sender, NewPacketEventArgs<ArtNetPacket> e)
        {
            //Console.WriteLine($"Received ArtNet packet with OpCode: {e.Packet.OpCode} from {e.Source}");
            if (!spotsOnMouseControl())
            {
                ArtNetactivity(1);

                if (e.Packet.OpCode == Haukcode.ArtNet.ArtNetOpCodes.Dmx)
                {
                    ArtNetDmxPacket dmx = (ArtNetDmxPacket)e.Packet;
                    context.Post(delegate (object dummy)
                    {
                        foreach (Follow_Spot spot in m_spots)
                        {
                            if (dmx.Universe == spot.Universe)
                            {
                                double pan = Math.Round(((dmx.DmxData[spot.Address + (PAN_DMX_OFFSET - 1)] * 256) + dmx.DmxData[spot.Address + (PAN_DMX_OFFSET)]) / 65535.0 * PAN_RANGE - (PAN_RANGE / 2), 3);
                                double tilt = Math.Round(((dmx.DmxData[spot.Address + (TILT_DMX_OFFSET - 1)] * 256) + dmx.DmxData[spot.Address + (TILT_DMX_OFFSET)]) / 65535.0 * (TILT_RANGE) - (TILT_RANGE/2), 3);
                                if (PAN_INVERT)
                                    pan = -pan;

                                if (TILT_INVERT)
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

                        //Console.WriteLine("P: {0}, T:{1}", p, t);

                        //}



                        // Tell clients

                    }, null);

                    if ((spotsOnMouseControl()) && ((dmx.Universe != (short)ARTNET_TXUniverse)))
                        updateDMX(dmx.DmxData);
                }
            }

        }

        void informClients()
        {
            System.IO.MemoryStream stream = new System.IO.MemoryStream();
            BinaryFormatter serializer = new BinaryFormatter();
            serializer.Serialize(stream, m_spots);
            byte[] buffer = new byte[stream.Length + 3];
            buffer[0] = 2; // Position Update
            buffer[1] = (byte)(stream.Length / 256);
            buffer[2] = (byte)(stream.Length & 0xFF);
            stream.ToArray();

            Array.Copy(stream.ToArray(), 0, buffer, 3, stream.Length);

            for(int c = 0; c<clientHandlers.Length; c++)
            {
                ClientHandler ch = clientHandlers[c];

                if (ch != null)
                {
                    try
                    {
                        Socket client = ch.getClient();

                        if (client.Connected)
                            client.Send(buffer, (int)stream.Length + 3, SocketFlags.None);
                    }
                    catch (Exception w)
                    {
                        ch.shutdown();
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

            activity(0);
            Console.WriteLine("Channel Message: " + e.Message.Command.ToString() + ", " + e.Message.Data1 + ", " + e.Message.Data2);
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
                    //Console.WriteLine("Controller: " + "/rpc " + "\\07, " + (attribute) + ", " + (delta) + "," + ((encoderState[e.Message.Data1 - 10]) ? "1" : "0") + "H");
                }
                else
                {
                    delta = e.Message.Data2 - 64;
                    if (encoderState[e.Message.Data1 - 10])
                    {
                        delta *= 2;
                    }
                    sender.Send(new OscMessage("/rpc", "\\08," + (attribute) + "," + (delta) + "H"));
                    //Console.WriteLine("Controller: " +"/rpc " + "\\08, " + (attribute) + ", " + (delta) + "," +((encoderState[e.Message.Data1 - 10]) ?"1":"0")+ "H");
                }

                //Console.WriteLine("Controller: " + e.Message.Data1 + ":" + e.Message.Data2);

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
                //  Console.WriteLine("Send: /exec/" + (e.Message.Data1 - 15).ToString() + buttons[e.Message.Data1-16]);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOff) && (e.Message.Data1 >= 16) && (e.Message.Data1 <= 39) && (e.Message.Data2 == 0))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();

                builder.Command = ChannelCommand.NoteOn;
                builder.Data1 = (e.Message.Data1);
                builder.Data2 = buttons[e.Message.Data1 - 16] ? 1 : 0;
                builder.Build();
                outDevice.Send(builder.Result);
                // Console.WriteLine("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
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
                Console.WriteLine("selectedPlayback: " + selectedPlayback);
                builder.Command = ChannelCommand.NoteOn;
                for (int j = 0; j < 9; j++)
                {
                    builder.Data1 = j + 40;
                    builder.Data2 = buttons[j + 40 - 16] ? 2 : 0;
                    builder.Build();
                    outDevice.Send(builder.Result);
                }
                //  Console.WriteLine("Send: /exec/" + (e.Message.Data1 - 15).ToString() + buttons[e.Message.Data1-16]);
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
                //Console.WriteLine("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
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

                //Console.WriteLine("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
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
                m_Thread = new Thread(new ThreadStart(ListenLoop));
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

            updateDMX();
        }

        public void updateDMX(byte[] packet)
        {
            ArtNetSequence++;

            if (m_TXsocket.EnableBroadcast)
            {
                m_TXsocket.Send(new ArtNetDmxPacket
                {
                    Sequence = ArtNetSequence,
                    Physical = 1,
                    Universe = (short)ARTNET_TXUniverse,
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
                    Universe = (short)ARTNET_TXUniverse,
                    DmxData = packet
                }, address);
            }
            ArtNetactivity(2);
        }

        public void updateDMX()
        {
            byte[] packet = new byte[512];

            foreach (Follow_Spot spot in m_spots)
            {
                double pan = spot.Pan;
                double tilt = spot.Tilt;

                if (PAN_INVERT)
                    pan = -pan;

                if (TILT_INVERT)
                    tilt = -tilt;

                int PanDMX = (int)Math.Round((((pan + (PAN_RANGE/2)) / PAN_RANGE) * 65535.0), 0);
                int TiltDMX = (int)Math.Round((((tilt + (TILT_RANGE/2)) / TILT_RANGE) * 65535.0), 0);

                packet[spot.Address +(PAN_DMX_OFFSET - 1)] = (byte)(PanDMX / 256);
                packet[spot.Address +(PAN_DMX_OFFSET)] = (byte)(PanDMX % 256);
                packet[spot.Address + (TILT_DMX_OFFSET - 1)] = (byte)(TiltDMX / 256);
                packet[spot.Address + (TILT_DMX_OFFSET)] = (byte)(TiltDMX % 256);
            }

            updateDMX(packet);
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
                Console.WriteLine(e.ToString());
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
                    Console.WriteLine("Waiting for a connection...");
                    Socket client = listener.Accept();
                    Console.WriteLine("Connected ...");
                    ClientHandler ch = new ClientHandler(client, this);

                    ch.start();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

    }


    public class ClientHandler
    {
        Socket client;
        MainWindow mainWindow;
        Thread clientThread;
        int clientID = -1;

        public Socket getClient()
        {
            return client;
        }

        public ClientHandler(Socket p_client, MainWindow p_mainWindow)
        {
            client = p_client;
            mainWindow = p_mainWindow;

            try
            {
                if (clientThread == null)
                {
                    clientThread = new Thread(new ThreadStart(clientLoop));
                    clientThread.IsBackground = true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        public void start()
        {
            clientThread.Start();
        }

        public void shutdown()
        {
            try
            {
                Console.WriteLine($"Client {clientID} shutting down.");
                if (clientID >= 0)
                    mainWindow.clientHandlers[clientID] = null;

                client.Shutdown(SocketShutdown.Both);
                client.Close();
                Console.WriteLine($"Client {clientID} shut down.");
            }
            catch (Exception w)
            {
                Console.WriteLine($"Client {clientID} shut down error: {w}");
            }
        }

        void clientLoop()
        {
            byte[] buffer = new byte[1024];

            try
            {
                // Connect to the remote endpoint.  
                while (true)
                {
                    int count = client.Receive(buffer, 1, SocketFlags.None);
                    mainWindow.FSactivity(clientID);

                    switch (buffer[0])
                    {
                        case 0:
                            Console.WriteLine("Server Command: " + buffer[0]);
                            break;
                        case 1:
                            {
                                client.Receive(buffer, 1, 1, SocketFlags.None);
                                clientID = buffer[1] - 1;
                                if (clientID == -1)
                                {
                                    for (int i = 0; i < mainWindow.clientHandlers.Length; i++)
                                    {
                                        if (mainWindow.clientHandlers[i] == null)
                                        {
                                            clientID = i;
                                            break;
                                        }
                                    }
                                }

                                if (clientID == mainWindow.clientHandlers.Length || clientID==-1)
                                {
                                    client.Shutdown(SocketShutdown.Both);
                                    client.Close();
                                    return;
                                }

                                if (mainWindow.clientHandlers[clientID] != null)
                                {
                                    mainWindow.clientHandlers[clientID].shutdown();
                                }
                                mainWindow.clientHandlers[clientID] = this;

                                Console.WriteLine("Server Command: " + buffer[0]);
                                String resource = Newtonsoft.Json.JsonConvert.SerializeObject(mainWindow.getAppResource());

                                Encoding.Default.GetBytes(resource);

                                int length = Encoding.Default.GetByteCount(resource);
                                byte[] op_buffer = new byte[length + 4];
                                op_buffer[0] = 1; // Server Update
                                op_buffer[1] = (byte)(length / 256);
                                op_buffer[2] = (byte)(length & 0xFF);
                                op_buffer[3] = (byte)clientID;

                                Encoding.Default.GetBytes(resource, 0, resource.Length, op_buffer, 4);
                                try
                                {
                                    if (client.Connected)
                                        client.Send(op_buffer, length + 4, SocketFlags.None);
                                }
                                catch (Exception w)
                                {
                                    shutdown();
                                }
                            }
                            break;

                        case 2:
                            {
                                client.Receive(buffer, 1, 2, SocketFlags.None);
                                int res_length = buffer[1] * 256 + buffer[2];
                                byte[] rcv_buffer = new byte[res_length];
                                int recieved = 0;
                                while (recieved < res_length)
                                {
                                    recieved += client.Receive(rcv_buffer, recieved, res_length - recieved, SocketFlags.None);
                                }

                                BinaryFormatter deserializer = new BinaryFormatter();
                                deserializer.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;

                                dynamic newspots = deserializer.Deserialize(new System.IO.MemoryStream(rcv_buffer, false));
                                //Console.WriteLine("DMX Update:" + newspots[0]);
                                for (int i = 0; i < MainWindow.m_spots.Count; i++)
                                {
                                    if (MainWindow.m_spots[i].MouseControlID == clientID)
                                    {
                                        MainWindow.m_spots[i].Pan = newspots[i].Pan;
                                        MainWindow.m_spots[i].Tilt = newspots[i].Tilt;

                                        MainWindow.m_spots[i].Target = newspots[i].Target;
                                    }
                                }

                                mainWindow.updateDMX();

                            }
                            break;

                        default:
                            Console.WriteLine("Server Command: " + buffer[0]);
                            break;
                    }

                }
            }
            catch (Exception w)
            {
                shutdown();
            }

        }
    }
}
