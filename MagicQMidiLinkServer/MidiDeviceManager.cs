using Rug.Osc;
using Sanford.Multimedia;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MidiApp;

internal class MidiDeviceManager : IDisposable
{
    private InputDevice midiInDevice;
    private OutputDevice midiOutDevice;
    private MainWindow mainWindow;

    private ChannelMessageBuilder builder;

    private const int SysExBufferSize = 128;

    private readonly bool[] buttonState;
    private readonly float[] faders;
    private readonly bool[] encoderState;

    private Stopwatch FWatch = Stopwatch.StartNew();
    private double FLastMillis;
    private double FDiff;
    private double FDiffTimeStamp;
    private int counter;
    private int FLastTimestamp;

    public bool IsConnected => midiInDevice != null && midiOutDevice != null;

    public MidiDeviceManager(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow;
        builder = new();

        buttonState = new bool[256];
        faders = new float[256];
        encoderState = new bool[256];
    }

    public void LookForXTouch()
    {
        XTouchSearcher.DoWorkWithModal(mainWindow, progress =>
        {
            while (midiInDevice == null)
            {
                if (midiInDevice == null)
                {
                    progress.Report("Searching.");
                    for (var d = 0; d < InputDevice.DeviceCount; d++)
                    {
                        Logger.Log($"Midi Input Device: {InputDevice.GetDeviceCapabilities(d).name}");

                        if (InputDevice.GetDeviceCapabilities(d).name.Contains(MainWindow.AppResources.midiControllerSettings.midiDeviceName))
                        {
                            midiInDevice = new InputDevice(d);
                            break;
                        }
                    }
                    Thread.Sleep(500);
                }

                if (midiOutDevice == null)
                {
                    progress.Report("Searching...");
                    for (var d = 0; d < OutputDevice.DeviceCount; d++)
                    {
                        Logger.Log($"Midi Output Device: {OutputDevice.GetDeviceCapabilities(d).name}");
                        if (OutputDevice.GetDeviceCapabilities(d).name.Contains(MainWindow.AppResources.midiControllerSettings.midiDeviceName))
                        {
                            midiOutDevice = new OutputDevice(d);
                            break;
                        }
                    }
                    Thread.Sleep(500);
                }
            }

            mainWindow.SetStatusMessage($"Connected to MIDI device!");
        });
    }

    public void Dispose()
    {
        midiInDevice?.Dispose();
        midiOutDevice?.Dispose();
    }

    public void SetupMidiDevice()
    {
        try
        {
            midiInDevice.ChannelMessageReceived += HandleChannelMessageReceived;
            midiInDevice.SysCommonMessageReceived += HandleSysCommonMessageReceived;
            midiInDevice.SysExMessageReceived += HandleSysExMessageReceived;
            midiInDevice.SysRealtimeMessageReceived += HandleSysRealtimeMessageReceived;
            midiInDevice.Error += new EventHandler<ErrorEventArgs>(MidiIn_Error);

            if (!MainWindow.AppResources.midiControllerSettings.midiDeviceName.Contains("Loop"))
                midiInDevice.StartRecording();

            builder.Command = ChannelCommand.Controller;
            builder.MidiChannel = 0;
            builder.Data1 = 127;
            builder.Data2 = 0;
            builder.Build();
            midiOutDevice.Send(builder.Result);

            builder.Command = ChannelCommand.ProgramChange;
            builder.MidiChannel = 1;
            builder.Data1 = 0;
            builder.Data2 = 0;
            builder.Build();
            midiOutDevice.Send(builder.Result);

            builder.Command = ChannelCommand.Controller;
            builder.MidiChannel = 0;

            for (var i = 0; i < 127; i++)
            {
                builder.Data1 = i;
                builder.Data2 = 0;
                builder.Build();
                midiOutDevice.Send(builder.Result);
            }

            builder.Command = ChannelCommand.NoteOn;
            builder.MidiChannel = 0;
            builder.Data1 = 0;
            builder.Data2 = 1;
            builder.Build();
            midiOutDevice.Send(builder.Result);

            for (var i = 0; i < 39; i++)
            {
                builder.Data1 = i;
                builder.Data2 = (i == 32) ? 2 : 0;
                builder.Build();
                midiOutDevice.Send(builder.Result);
            }

            mainWindow.SelectedPlayback = 9;

            builder.Command = ChannelCommand.NoteOn;
            builder.MidiChannel = 0;
            builder.Data1 = 38;
            builder.Data2 = 2;
            builder.Build();
            midiOutDevice.Send(builder.Result);

            mainWindow.SetStatusMessage($"Initialised MIDI device!");
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to initialise MIDI device: \n{ex}", Severity.ERROR);
        }
    }

    private void MidiIn_Error(object source, ErrorEventArgs e)
    {
        //MessageBox.Show(e.Error.Message, "Midi Error!", MessageBoxButton.OK, MessageBoxImage.Stop);
        Logger.Log(e.Error, Severity.ERROR);
    }

    private void HandleChannelMessageReceived(object source, ChannelMessageEventArgs e)
    {
        mainWindow.Activity(0);
        try
        {
            //Logger.Log("Channel Message: " + e.Message.Command.ToString() + ", " + e.Message.Data1 + ", " + e.Message.Data2);
            if ((e.Message.Command == ChannelCommand.Controller) && (e.Message.Data1 < 10))
            {
                // Fader message
                mainWindow.SendOSCMessage(new OscMessage($"/pb/{e.Message.Data1}", e.Message.Data2 / 127.0f));
                faders[e.Message.Data1] = e.Message.Data2 / 127.0f;
            }
            else if ((e.Message.Command == ChannelCommand.Controller) && (e.Message.Data1 >= 10))
            {
                // Encoder message
                int attribute = e.Message.Data1 - 10;
                int delta = e.Message.Data2;

                if (e.Message.Data1 == 24)
                {
                    // Set selected attribute
                    delta = (e.Message.Data2 < 64) ? 1 : -1;

                    mainWindow.SelectedAttribute = (mainWindow.SelectedAttribute + delta) % (mainWindow.attributeNames.Length);
                }
                else
                {
                    // Set selected attribute value
                    if (e.Message.Data1 == 25)
                        attribute = mainWindow.SelectedAttribute;

                    char category;
                    if (e.Message.Data2 < 64)
                    {
                        category = '7';
                    }
                    else
                    {
                        delta = e.Message.Data2 - 64;
                        category = '8';
                    }

                    if (encoderState[e.Message.Data1 - 10])
                        delta *= 2;

                    mainWindow.SendOSCMessage(new OscMessage("/rpc", $"\\0{category},{attribute},{delta}H"));
                }

                //Logger.Log("Controller: " + e.Message.Data1 + ":" + e.Message.Data2);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 < 16) && (e.Message.Data2 == 127))
            {
                // Encoder push feedback
                encoderState[e.Message.Data1] = !encoderState[e.Message.Data1];
                builder.Command = ChannelCommand.Controller;
                builder.MidiChannel = 0;
                int encoderId = e.Message.Data1 + 10;

                builder.MidiChannel = 1;
                builder.Data1 = encoderId;
                builder.Data2 = encoderState[e.Message.Data1] ? 0 : 4;
                builder.Build();
                midiOutDevice.Send(builder.Result);

                builder.MidiChannel = 0;
                builder.Data1 = encoderId;
                builder.Data2 = encoderState[e.Message.Data1] ? 0 : 64;
                builder.Build();
                midiOutDevice.Send(builder.Result);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 16) && (e.Message.Data1 <= 39) && (e.Message.Data2 == 127))
            {
                // Exec buttons
                int buttonInd = e.Message.Data1 - 16;
                bool newState = !buttonState[buttonInd];
                buttonState[buttonInd] = newState;

                mainWindow.SendOSCMessage(new OscMessage($"/exec/{buttonInd + 1}", newState ? 1 : 0));
                //  Logger.Log("Send: /exec/" + (e.Message.Data1 - 15).ToString() + buttons[e.Message.Data1-16]);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOff) && (e.Message.Data1 >= 16) && (e.Message.Data1 <= 39) && (e.Message.Data2 == 0))
            {
                // Button feedback
                builder.Command = ChannelCommand.NoteOn;
                builder.MidiChannel = 0;
                builder.Data1 = (e.Message.Data1);
                builder.Data2 = buttonState[e.Message.Data1 - 16] ? 1 : 0;
                builder.Build();
                midiOutDevice.Send(builder.Result);
                // Logger.Log("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
                //                sender.Send(new OscMessage("/feedback/exec"));
            }
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 40) && (e.Message.Data1 <= 48) && (e.Message.Data2 >= 50))
            {
                // Playback SEL buttons
                for (int j = 0; j < 9; j++)
                    buttonState[j + 40 - 16] = false;
                buttonState[e.Message.Data1 - 16] = true;

                mainWindow.SelectedPlayback = e.Message.Data1 - 39;
                Logger.Log($"SelectedPlayback: {mainWindow.SelectedPlayback}");

                builder.Command = ChannelCommand.NoteOn;
                builder.MidiChannel = 0;
                for (int j = 0; j < 9; j++)
                {
                    builder.Data1 = j + 40;
                    builder.Data2 = buttonState[j + 40 - 16] ? 2 : 0;
                    builder.Build();
                    midiOutDevice.Send(builder.Result);
                }
                //  Logger.Log("Send: /exec/" + (e.Message.Data1 - 15).ToString() + buttons[e.Message.Data1-16]);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOff) && (e.Message.Data1 >= 40) && (e.Message.Data1 <= 48) && (e.Message.Data2 == 0))
            {
                // Playback SEL buttons LED feedback
                builder.Command = ChannelCommand.NoteOn;
                builder.MidiChannel = 0;
                for (int j = 0; j < 9; j++)
                {
                    builder.Data1 = j + 40;
                    builder.Data2 = buttonState[j + 40 - 16] ? 2 : 0;
                    builder.Build();
                    midiOutDevice.Send(builder.Result);
                }
                //Logger.Log("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
            }
            else if ((e.Message.Command == ChannelCommand.NoteOn) && (e.Message.Data1 >= 49) && (e.Message.Data1 <= 54) && (e.Message.Data2 >= 50))
            {
                // Special buttons
                if (mainWindow.SelectedPlayback > 0)
                {
                    int selectedPlayback = mainWindow.SelectedPlayback;
                    switch (e.Message.Data1)
                    {
                        case 49:
                            mainWindow.SendOSCMessage(new OscMessage("/rpc", $"{selectedPlayback}B"));
                            break;
                        case 50:
                            mainWindow.SendOSCMessage(new OscMessage("/rpc", $"{selectedPlayback}F"));
                            break;
                        case 51:
                            mainWindow.SendOSCMessage(new OscMessage("/rpc", "\\31H"));
                            break;
                        case 52:
                            mainWindow.SendOSCMessage(new OscMessage("/rpc", "\\30H"));
                            break;
                        case 53:
                            mainWindow.SendOSCMessage(new OscMessage("/rpc", $"{selectedPlayback}S"));
                            mainWindow.SendOSCMessage(new OscMessage("/rpc", $"{selectedPlayback}R"));
                            break;
                        case 54:
                            mainWindow.SendOSCMessage(new OscMessage("/rpc", $"{selectedPlayback}G"));
                            break;
                    }
                }

                //Logger.Log("SetButton: " + e.Message.Data1.ToString() + " " + buttons[e.Message.Data1]);
                //                sender.Send(new OscMessage("/feedback/exec"));

            }
            else if ((e.Message.Command == ChannelCommand.NoteOff) && (e.Message.Data1 == 54))
            {
                // ???
                builder.Command = ChannelCommand.NoteOn;
                builder.MidiChannel = 1;

                builder.Data1 = 38;
                builder.Data2 = 2;
                builder.Build();
                midiOutDevice.Send(builder.Result);
            }
        } catch (Exception ex)
        {
            Logger.Log($"Exception encountered while parsing MIDI message. \n" +
                $"{{Command={e.Message.Command}; Channel={e.Message.MidiChannel}; " +
                $"Data1={e.Message.Data1}; Data2={e.Message.Data2}; Status={e.Message.Status}}}\n{ex}", Severity.ERROR);
        }
    }

    private void HandleSysExMessageReceived(object source, SysExMessageEventArgs e)
    {
        /*context.Post(delegate (object dummy)
        {
            string result = "\n\n"; ;

            foreach (byte b in e.Message)
            {
                result += string.Format("{0:X2} ", b);
            }

            //                sysExRichTextBox.AppendText(result);
        }, null);*/
    }

    private void HandleSysCommonMessageReceived(object source, SysCommonMessageEventArgs e) { }

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

    public void SendSelMessage(int selectedPB)
    {
        for (int j = 0; j < 9; j++)
            buttonState[j + 40 - 16] = false;

        selectedPB = Math.Clamp(selectedPB, 0, 20);

        buttonState[selectedPB + 39 - 16] = true;
        builder.Command = ChannelCommand.NoteOn;
        builder.MidiChannel = 0;
        for (int j = 0; j < 9; j++)
        {
            builder.Data1 = j + 40;
            builder.Data2 = buttonState[j + 40 - 16] ? 2 : 0;
            builder.Build();
            midiOutDevice.Send(builder.Result);
        }
    }

    public void SendFaderMessage(OscMessage msg, int playback)
    {
        float value = (float)msg[0];
        byte valueI = (byte)(127.0f * value);
        if ((byte)(127.0f * faders[playback]) != valueI)
        {
            faders[playback] = value;

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
                builder.Data2 = valueI;
                builder.Build();
                midiOutDevice.Send(builder.Result);
            }
            //Logger.Log("Set Fader: " + (playback).ToString() + " " + value);
        }
    }

    public void SendExecMessage(OscMessage msg, int page, int execNum)
    {
        bool value = ((float)(msg[0]) > 0.5f);
        int buttonId = 64 * page + execNum;
        if (buttonId > buttonState.Length)
            return;

        if (buttonState[buttonId] != value)
        {
            buttonState[buttonId] = value;

            builder.Command = ChannelCommand.NoteOn;
            builder.MidiChannel = 0;
            builder.Data1 = (execNum + 16);
            builder.Data2 = buttonState[buttonId] ? 1 : 0;
            builder.Build();
            midiOutDevice.Send(builder.Result);
            // Logger.Log("SetButton: " + (buttonId).ToString() + " " + buttons[buttonId]);
        }
    }
}
