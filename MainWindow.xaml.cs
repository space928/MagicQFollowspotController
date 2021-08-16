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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using Haukcode.ArtNet.Packets;
using Haukcode.ArtNet.Sockets;
using Haukcode.Sockets;
using Rug.Osc;
using Sanford.Multimedia;
using Sanford.Multimedia.Midi;


namespace MidiApp
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisUI.Controls.AdonisWindow
    {


        //static string MIDI_DEVICE_NAME = "LoopBe";
        static string MIDI_DEVICE_NAME = "X-TOUCH";
        public OscSender sender;
        public OscReceiver receiver;

        public const int SysExBufferSize = 128;

        public InputDevice inDevice = null;
        public OutputDevice outDevice = null;

        public Thread m_Thread = null;
        public Thread activity_Thread = null;
        public Thread ArtNetactivity_Thread = null;
        public ArtNetSocket m_socket = null;
        public ArtNetSocket m_TXsocket = null;
        public static List<Follow_Spot> m_spots = new List<Follow_Spot>();

        public Thread mqConnection_Thread = null;
        public String MQhostname = "not connected";
        public String MQshowfile = "not connected";

        public string resourceFileName = @"resources.json";
        public static dynamic AppResources;
        public Thread m_ResourceLoader_Thread = null;

        public ThreeD m_threeDWindow = null;

        Dictionary<string, int> attributes = new Dictionary<string, int>();

        //string[] attributeNames = {"Intensity", "Intensity Mode", "Shutter", "Iris", "Pan", "Tilt", "Col1", "Col2",
        //                            "Gobo1", "Gobo2", "Rotate1", "Rotate2", "Focus", "Zoom", "FX1", "FX2", "Cyan",
        //                            "Magenta", "Yellow", "Colmix", "Cont1", "Cont2", "Macro1", "Macro2", "Undefined1",
        //                            "Undefined2", "Col3", "Col4", "Gobo3", "Gobo4", "Rotate3", "Rotate4", "Frost1",
        //                            "Frost2", "FX3", "FX4", "FX5", "FX6", "FX7", "FX8", "Cont3", "Cont4", "Cont5",
        //                            "Cont6", "Cont7", "Cont8", "Pos1", "Pos2", "Pos3", "Pos4", "Pos5", "Pos6"
        //};

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
                MessageBox.Show("Cannot find resource file\n"+ resourceFileName, "File Not Found", MessageBoxButton.OK, MessageBoxImage.Stop);
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
                        context.Post(delegate (object dummy)
                        {
                            m_threeDWindow?.UpdateModel();
                        }, null);

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
            if (context != null) {
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
                        else if (parts[1].Equals("fspot"))
                        {
                            if (parts.Length >= 2)
                            {
                                if (parts[2] == "start")
                                {
                                    string ids = msg.ToString();
                                    int viewID = -1;

                                    if (!ids.EndsWith("/"))
                                    {
                                        ids = ids.Substring(ids.LastIndexOf('/') + 1);
                                        string[] idspot = ids.Split(',');

                                        int headId = Int32.Parse(idspot[0]);

                                        foreach (Follow_Spot spot in m_spots)
                                        {
                                            spot.IsLeadSpot = spot.Head == headId;
                                        }
                                        if (idspot.Length > 1)
                                        {
                                            viewID = Int32.Parse(idspot[1]);
                                        }
                                    }

                                    context.Post(delegate (object dummy)
                                    {
                                        if (m_threeDWindow == null)
                                        {
                                            m_threeDWindow = new ThreeD();
                                            m_threeDWindow.grab();
                                        }
                                        else
                                        {
                                            m_threeDWindow.Show();
                                        }

                                        if (viewID > 0)
                                        {
                                            m_threeDWindow.setCameraView(viewID - 1);
                                        }
                                    }, null);

                                }
                                else if (parts[2] == "stop")
                                {
                                    foreach (Follow_Spot spot in m_spots)
                                    {
                                        spot.IsLeadSpot = false;
                                    }
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

                for (var i = 0; i < 127; i++) {
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

                foreach (dynamic v in AppResources.Lights)
                {
                    Follow_Spot spot = new Follow_Spot();
                    spot.Head = v.Head;
                    spot.Universe = v.Universe;
                    spot.Address = v.Address;
                    spot.IsLeadSpot = false;

                    Point3D p;
                    switch ((int)v.Bar)
                    {
                        case 0: p = new Point3D((double)v.XOffset, 0.0, (double)AppResources.Bar0Height + 0.1); break;
                        case -1: p = new Point3D((double)v.XOffset, (double)AppResources.BarAudienceOffset, (double)AppResources.BarAudienceHeight + 0.1); break;
                        case 1: p = new Point3D((double)v.XOffset, (double)AppResources.Bar1Offset, (double)AppResources.Bar1Height + 0.1); break;
                        default: p = new Point3D((double)v.XOffset, (double)AppResources.Bar2Offset, (double)AppResources.Bar2Height + 0.1); break;
                    }
                    spot.Location = p;

                    m_spots.Add(spot);
                }
                FollwSpot_dataGrid.ItemsSource = m_spots;

                ArtNetListner();

                m_threeDWindow = new ThreeD();
                m_threeDWindow.grab();

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

        public int leadSpot()
        {
            int i = 0;
            for (i = 0; i < MainWindow.m_spots.Count; i++)
            {
                if (MainWindow.m_spots[i].IsLeadSpot)
                {
                    return i;
                }
            }

            return -1;
        }

        void ArtNet_NewPacket(object sender, NewPacketEventArgs<ArtNetPacket> e)
        {
            //Console.WriteLine($"Received ArtNet packet with OpCode: {e.Packet.OpCode} from {e.Source}");
            if (leadSpot() < 0)
            {
                ArtNetactivity(1);

                context.Post(delegate (object dummy)
                {
                    if (e.Packet.OpCode == Haukcode.ArtNet.ArtNetOpCodes.Dmx)
                    {
                        ArtNetDmxPacket dmx = (ArtNetDmxPacket)e.Packet;

                        foreach (Follow_Spot spot in m_spots)
                        {
                            if (dmx.Universe == spot.Universe - 1)
                            {
                                spot.Pan = Math.Round(((dmx.DmxData[spot.Address - 1] * 256) + dmx.DmxData[spot.Address]) / 65535.0 * 540.0 - 270.0, 3);
                                spot.Tilt = Math.Round(((dmx.DmxData[spot.Address + 1] * 256) + dmx.DmxData[spot.Address + 2]) / 65535.0 * 270.0 - 135.0, 3);
                            }
                        }

                        //if (dmx.Universe == m_spots[0].Universe - 1)
                        //{
                        //                        Follow_Spot spot = m_spots[0];

                        //                        int p = (dmx.DmxData[spot.Address - 1] * 256) + dmx.DmxData[spot.Address];
                        //                        int t = (dmx.DmxData[spot.Address + 1] * 256) + dmx.DmxData[spot.Address + 2];

                        //Console.WriteLine("P: {0}, T:{1}", p, t);
                        //updateDMX(dmx.DmxData);
                        //}

                        if (m_threeDWindow != null)
                        {
                            m_threeDWindow.DMX_moveSpot(leadSpot());
                        }

                    }
                }, null);
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

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            if (m_threeDWindow == null)
            {
                m_threeDWindow = new ThreeD();
                m_threeDWindow.grab();
            }
            else
            {
                m_threeDWindow.Show();
            }
        }

        private void inDevice_Error(object source, ErrorEventArgs e)
        {
            MessageBox.Show(e.Error.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        private void HandleChannelMessageReceived(object source, ChannelMessageEventArgs e)
        {

            activity(0);

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
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 40) && (e.Message.Data1 <= 48) && (e.Message.Data2 == 127))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();
                for (int j = 0; j < 9; j++)
                {
                    buttons[j + 40 - 16] = false;
                }
                buttons[e.Message.Data1 - 16] = true;
                selectedPlayback = e.Message.Data1 - 39;

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
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 49) && (e.Message.Data1 <= 54) && (e.Message.Data2 == 127))
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

        private void FollwSpot_dataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        private byte ArtNetSequence = 0;
        public void PointSpots()
        {
            foreach (Follow_Spot spot in m_spots)
            {
                Vector3D p = (spot.Target - spot.Location);
                Point3D direction = Spherical.ToSpherical(-p.Y, p.X, p.Z);
                direction.Y += 90;

                direction = Spherical.MinSphericalMove(new Point3D(1, spot.Tilt, spot.Pan), direction);
                spot.Tilt = direction.Y;
                spot.Pan = direction.Z;
            }

            updateDMX();
        }

        public void updateDMX(byte[] packet)
        {
            ArtNetSequence++;

            m_TXsocket.Send(new ArtNetDmxPacket
            {
                Sequence = ArtNetSequence,
                Physical = 1,
                Universe = (short)ARTNET_TXUniverse,
                DmxData = packet
            });
            ArtNetactivity(2);
        }

        public void updateDMX()
        {
            byte[] packet = new byte[512];

            foreach (Follow_Spot spot in m_spots)
            {
                int PanDMX = (int)Math.Round((((spot.Pan + 270.0) / 540.0) * 65535.0),0);
                int TiltDMX = (int)Math.Round((((spot.Tilt + 135.0) / 270.0) * 65535.0),0);

                packet[spot.Address - 1] = (byte)(PanDMX / 256);
                packet[spot.Address] = (byte)(PanDMX % 256);
                packet[spot.Address + 1] = (byte)(TiltDMX / 256);
                packet[spot.Address + 2] = (byte)(TiltDMX % 256);
            }

            updateDMX(packet);
        }
    }

    public class Follow_Spot : INotifyPropertyChanged
    {
        private double pan;
        private double tilt;
        private bool isActive;
        private Point3D target;

        public event PropertyChangedEventHandler PropertyChanged;

        public Point3D Location { get; set; }
        public int Head { get; set; }
        public int Universe { get; set; }
        public int Address { get; set; }

        public string DMX_Base
        {
            get => Universe.ToString() + "-" + Address.ToString();
        }

        public Point3D Target { get => target; set { target = value; OnPropertyChanged(); } }
        public double Pan { get => pan; set { pan = value; OnPropertyChanged(); } }
        public double Tilt { get => tilt; set { tilt = value; OnPropertyChanged(); } }
        public bool IsLeadSpot { get => isActive; set { isActive = value; OnPropertyChanged(); } }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }


}
