using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

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
                Console.WriteLine(e.ToString());
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
                            Console.WriteLine("Server Command: " + buffer[0]);
                            break;
                        case MessageType.ConfigureClient:
                            ConfigureClient(buffer);
                            break;

                        case MessageType.SpotUpdate:
                            SpotUpdate(buffer);
                            break;

                        default:
                            Console.WriteLine("Server Command: " + buffer[0]);
                            break;
                    }

                }
            }
            catch
            {
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

            BsonDataReader bsonDataReader = new(new System.IO.MemoryStream(rcv_buffer, false));

            FollowSpot[] newspots = JsonSerializer.Create().Deserialize<FollowSpot[]>(bsonDataReader);
            //Console.WriteLine("DMX Update:" + newspots[0]);
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

            mainWindow.updateDMX();
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

            Console.WriteLine("Server Command: " + buffer[0]);
            string resource = JsonConvert.SerializeObject(mainWindow.GetAppResource());

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
            catch
            {
                Shutdown();
            }
        }
    }
}
