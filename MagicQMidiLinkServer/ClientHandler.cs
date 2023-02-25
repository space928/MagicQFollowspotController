using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

namespace MidiApp
{
    enum MessageType : byte
    {
        Initialize = 0,
        ConfigureClient = 1,
        SpotUpdate = 2,         // Spot position update
        Message = 3            //Message box
    }

    public class ClientHandler
    {
        private readonly Socket client;
        private readonly MainWindow mainWindow;
        private readonly Thread clientThread;

        private int clientID = -1;

        public Socket Client => client;

        public ClientHandler(Socket p_client, MainWindow p_mainWindow)
        {
            client = p_client;
            mainWindow = p_mainWindow;

            try
            {
                if (clientThread == null)
                {
                    clientThread = new(new ThreadStart(ClientLoop));
                    clientThread.IsBackground = true;
                }
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString());
            }

        }

        public void Start()
        {
            clientThread.Start();
        }

        public void Shutdown()
        {
            try
            {
                Logger.Log($"Client {clientID} shutting down.");
                if (clientID >= 0)
                    mainWindow.clientHandlers[clientID] = null;

                client.Shutdown(SocketShutdown.Both);
                client.Close();
                Logger.Log($"Client {clientID} shut down.");
            }
            catch (Exception w)
            {
                Logger.Log($"Client {clientID} shut down error: {w}", Severity.FATAL);
            }
        }

        public void ReconfigureClient()
        {
            try
            {
                SendAppResources();
            } catch (Exception e)
            {
                Logger.Log($"Couldn't reconfigure client {clientID} error: {e}", Severity.ERROR);
            }
        }

        void ClientLoop()
        {
            Span<byte> buffer = new byte[1024];

            try
            {
                // Connect to the remote endpoint.  
                while (true)
                {
                    int count = client.Receive(buffer[0..1], SocketFlags.None);
                    mainWindow.FSactivity(clientID);

                    switch ((MessageType)buffer[0])
                    {
                        case MessageType.Initialize:
                            Logger.Log("Server Command: " + buffer[0]);
                            break;
                        case MessageType.ConfigureClient:
                            ConfigureClient(buffer);
                            break;

                        case MessageType.SpotUpdate:
                            SpotUpdate(buffer);
                            break;

                        default:
                            Logger.Log("Server Command: " + buffer[0]);
                            break;
                    }

                }
            }
            catch (Exception e)
            {
                Logger.Log(e, Severity.FATAL);
                Shutdown();
            }

        }

        private void SpotUpdate(Span<byte> buffer)
        {
            client.Receive(buffer[1..3], SocketFlags.None);
            int res_length = buffer[1] * 256 + buffer[2];
            byte[] rcv_buffer = new byte[res_length];
            int recieved = 0;
            while (recieved < res_length)
            {
                recieved += client.Receive(rcv_buffer, recieved, res_length - recieved, SocketFlags.None);
            }

            FollowSpot[] newspots = JsonSerializer.Deserialize<FollowSpot[]>(rcv_buffer, AppResourcesData.JsonSerializerOptions);
            if(newspots.Length != MainWindow.FollowSpots.Count)
            {
                Logger.Log($"Spot update contained {newspots.Length} followspots, expected {MainWindow.FollowSpots.Count}!", Severity.ERROR);
                return;
            }
            //Logger.Log("DMX Update:" + newspots[0]);
            for (int i = 0; i < MainWindow.FollowSpots.Count; i++)
            {
                if (MainWindow.FollowSpots[i].MouseControlID == clientID)
                {
                    MainWindow.FollowSpots[i].Pan = newspots[i].Pan;
                    MainWindow.FollowSpots[i].Tilt = newspots[i].Tilt;

                    MainWindow.FollowSpots[i].Target = newspots[i].Target;

                    MainWindow.FollowSpots[i].Zoom = newspots[i].Zoom;

                    MainWindow.FollowSpots[i].HeightOffset = newspots[i].HeightOffset;
                }
            }

            mainWindow.UpdateDMX();
        }

        private void ConfigureClient(Span<byte> buffer)
        {
            client.Receive(buffer[1..2], SocketFlags.None);
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

            if (clientID == mainWindow.clientHandlers.Length || clientID == -1)
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
                return;
            }

            mainWindow.clientHandlers[clientID]?.Shutdown();
            mainWindow.clientHandlers[clientID] = this;

            Logger.Log("Server Command: " + buffer[0]);
            SendAppResources();
        }

        private void SendAppResources()
        {
            mainWindow.ReloadAppResources();
            byte[] resource = JsonSerializer.SerializeToUtf8Bytes(MainWindow.AppResources, AppResourcesData.JsonSerializerOptions);

            byte[] op_buffer = new byte[resource.Length + 4];
            op_buffer[0] = 1; // Server Update
            op_buffer[1] = (byte)((resource.Length >> 8) & 0xff);
            op_buffer[2] = (byte)(resource.Length & 0xff);
            op_buffer[3] = (byte)clientID;

            Array.Copy(resource, 0, op_buffer, 4, resource.Length);
            try
            {
                if (client.Connected)
                    client.Send(op_buffer, op_buffer.Length, SocketFlags.None);
            }
            catch
            {
                Logger.Log("Client socket exception!", Severity.FATAL);
                Shutdown();
            }
        }
    }
}
