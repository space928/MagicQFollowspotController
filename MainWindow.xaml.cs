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


        static string MIDI_DEVICE_NAME = "LoopBe";
        //static string MIDI_DEVICE_NAME = "X-TOUCH";
        public OscSender sender;
        public OscReceiver receiver;

        public const int SysExBufferSize = 128;

        public InputDevice inDevice = null;
        public OutputDevice outDevice = null;

        public Thread m_Thread = null;
        public Thread activity_Thread = null;
        public ArtNetSocket m_socket = null;
        public List<Follow_Spot> m_spots = new List<Follow_Spot>();

        public Thread mqConnection_Thread = null;
        public String MQhostname = "not connected";
        public String MQshowfile = "not connected";

        bool logging = false;

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

        //        IPAddress address = null;

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

            String strHostName = Dns.GetHostName();
            IPHostEntry iphostentry = Dns.GetHostEntry(strHostName);

            foreach (IPAddress ipaddress in iphostentry.AddressList)
            {
                if (ipaddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ipInput.Items.Add(ipaddress.ToString());
            }
            context = SynchronizationContext.Current;

            for (int i = 0; i < attributeNames.Length; i++)
            {
                attributes.Add(attributeNames[i], i);
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
            if (context != null) {
                context.Post(delegate (object dummy)
                {
                    ActivityLED.Fill = GreenFill;
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

                        if (logging)
                            context.Post(delegate (object dummy)
                            {
                                if (parts.Length > 3)
                                {
                                    channelListBox.Items.Add(
                                        parts[1] + '\t' + parts[2] + '\t' +
                                        parts[3] + '\t' + msg[0].ToString());
                                }
                                else
                                if (parts.Length > 2)
                                {
                                    channelListBox.Items.Add(
                                        parts[1] + '\t' + parts[2] + '\t' +
                                        '\t' + msg[0].ToString());
                                }
                                else
                                {
                                    channelListBox.Items.Add(
                                        parts[1] + '\t' + '\t' + '\t' +
                                        '\t' + msg.ToString());
                                }

                                channelListBox.SelectedIndex = channelListBox.Items.Count - 1;

                            }, null);
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

                //inDevice.StartRecording();

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

                ipInput_SelectionChanged(null, null);
                m_Thread = new Thread(new ThreadStart(ListenLoop));
                m_Thread.IsBackground = true;
                m_Thread.Start();

                Follow_Spot fs = new Follow_Spot();
                fs.Head = 301;
                fs.Universe = 1;
                fs.Address = 120;
                fs.IsActive = false;
                m_spots.Add(fs);

                fs = new Follow_Spot();
                fs.Head = 302;
                fs.Universe = 1;
                fs.Address = 144;
                fs.IsActive = false;
                m_spots.Add(fs);

                fs = new Follow_Spot();
                fs.Head = 305;
                fs.Universe = 1;
                fs.Address = 415;
                fs.IsActive = false;
                m_spots.Add(fs);

                fs = new Follow_Spot();
                fs.Head = 306;
                fs.Universe = 1;
                fs.Address = 439;
                fs.IsActive = false;
                m_spots.Add(fs);
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

        void ArtNet_NewPacket(object sender, NewPacketEventArgs<ArtNetPacket> e)
        {
            //Console.WriteLine($"Received ArtNet packet with OpCode: {e.Packet.OpCode} from {e.Source}");

            context.Post(delegate (object dummy)
            {
                if (e.Packet.OpCode == Haukcode.ArtNet.ArtNetOpCodes.Dmx)
                {
                    ArtNetDmxPacket dmx = (ArtNetDmxPacket)e.Packet;

                    foreach (Follow_Spot spot in m_spots)
                    {
                        if (dmx.Universe == spot.Universe-1)
                        {
                            spot.Pan = (float)Math.Round(((dmx.DmxData[spot.Address - 1] * 256) + dmx.DmxData[spot.Address]) / 65535.0 * 540.0 - 270.0, 1);
                            spot.Tilt = (float)Math.Round(((dmx.DmxData[spot.Address+1] * 256) + dmx.DmxData[spot.Address + 2]) / 65535.0 * 270.0 - 135.0, 1);
                        }
                    }
                }
            }, null);


        }
        void ArtNetListner()
        {
            m_socket = new ArtNetSocket();

            m_socket.NewPacket += ArtNet_NewPacket;

            var addresses = GetAddressesFromInterfaceType();
            var addr = addresses.ToArray()[2];

            m_socket.Open(addr.Address, addr.NetMask);
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

            if (m_Thread!=null)
                m_Thread.Interrupt();
            Application.Current.Shutdown();

        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            channelListBox.Items.Clear();
            AdonisUI.ResourceLocator.SetColorScheme(Application.Current.Resources, AdonisUI.ResourceLocator.DarkColorScheme);

            logging = true;

            try
            {
                inDevice.StartRecording();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            logging = false;
        }


        private void inDevice_Error(object source, ErrorEventArgs e)
        {
            MessageBox.Show(e.Error.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        private void HandleChannelMessageReceived(object source, ChannelMessageEventArgs e)
        {

            activity(0);

            if ((e.Message.Command== ChannelCommand.Controller) && (e.Message.Data1 <10))
            {
                sender.Send(new OscMessage("/pb/"+ e.Message.Data1, e.Message.Data2/127.0f));
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

                    sender.Send(new OscMessage("/pb/10/" + (selected_Attribute+1), 1.00f));

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

                    sender.Send(new OscMessage("/rpc", "\\07,"+ (attribute) + "," + (delta) + "H"));
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
            buttons[e.Message.Data1-16] = !buttons[e.Message.Data1 - 16];

            sender.Send(new OscMessage("/exec/" + (e.Message.Data1 -15), buttons[e.Message.Data1 - 16] ? 1 : 0));
            //  Console.WriteLine("Send: /exec/" + (e.Message.Data1 - 15).ToString() + buttons[e.Message.Data1-16]);
            } 
            else if ((e.Message.Command == ChannelCommand.NoteOff) && (e.Message.Data1 >= 16) && (e.Message.Data1 <= 39) && (e.Message.Data2 == 0))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();

                builder.Command = ChannelCommand.NoteOn;
                builder.Data1 = (e.Message.Data1);
                builder.Data2 = buttons[e.Message.Data1 - 16] ?1:0;
                builder.Build();
                outDevice.Send(builder.Result);
               // Console.WriteLine("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
//                sender.Send(new OscMessage("/feedback/exec"));
            } 
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 40) && (e.Message.Data1 <= 48) && (e.Message.Data2 == 127))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();
                for (int j = 0; j<9; j++)
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
                //                sender.Send(new OscMessage("/feedback/exec"));
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
                            break;
                        case 52:
                            sender.Send(new OscMessage("/rpc", selectedPlayback + "F"));
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


            if (logging)
                context.Post(delegate (object dummy)
                {
                    channelListBox.Items.Add(
                        e.Message.Command.ToString() + '\t' + '\t' +
                        e.Message.MidiChannel.ToString() + '\t' +
                        e.Message.Data1.ToString() + '\t' +
                        e.Message.Data2.ToString());

                    channelListBox.SelectedIndex = channelListBox.Items.Count - 1;
                }, null);
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
            if (logging)
                context.Post(delegate (object dummy)
                {
                    channelListBox.Items.Add(
                        e.Message.SysCommonType.ToString() + '\t' + '\t' +
                        e.Message.Data1.ToString() + '\t' +
                        e.Message.Data2.ToString());

                    channelListBox.SelectedIndex = channelListBox.Items.Count - 1;
                }, null);
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

            if (logging)
                context.Post(delegate (object dummy)
                {
                    channelListBox.Items.Add(
                        e.Message.SysRealtimeType.ToString());

                    channelListBox.Items.Add("BPM from stopwatch: " + FDiff.ToString("F4"));
                    channelListBox.Items.Add("BPM from driver timestamp: " + FDiffTimeStamp.ToString("F4"));

                    channelListBox.SelectedIndex = channelListBox.Items.Count - 1;
                }, null);
        }

        IPAddress MQ_IPAddress = null;
        private void ipInput_SelectionChanged(object m_sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (ipInput.SelectedItem != null)
                {
                    MQ_IPAddress = IPAddress.Parse(ipInput.SelectedItem.ToString());
                    ipInput.Background = this.Background;

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
            } catch (FormatException)
            {
                ipInput.Background = new SolidColorBrush(Colors.Red);
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

        private void FollwSpot_dataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

    }

    public class Follow_Spot : INotifyPropertyChanged
    {
        private float pan;
        private float tilt;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Head { get; set; }
        public int Universe { get; set; }
        public int Address { get; set; }

        public string DMX_Base
        {
            get => Universe.ToString() + "-" + Address.ToString();
        }

        public float Pan { get => pan; set { pan = value; OnPropertyChanged(); } }
        public float Tilt { get => tilt; set { tilt = value; OnPropertyChanged(); } }
        public bool IsActive { get; set; }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }


}
