using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON.PacketFormats;
using CoreRCON.Parsers;

namespace CoreRCON
{
    public class RCON : IDisposable
    {
        internal static string Identifier = "";
        private readonly object _lock = new object();

        // Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
        private TaskCompletionSource<bool> _authenticationTask;

        private bool _connected;

        public bool GetConnected() => _connected;
        public IPEndPoint GetIpEndPoint() => _endpoint;
        
        private readonly IPEndPoint _endpoint;

        // When generating the packet ID, use a never-been-used (for automatic packets) ID.
        private int _packetId = 1;

        private readonly string _password;
        private int _staleCounter = 0;
        private uint _reconnectDelay;
        private bool _disposed = false;

        /// <summary>
        ///     Initialize an RCON connection and automatically call ConnectAsync().
        /// </summary>
        public RCON(IPAddress host, ushort port, string password, uint reconnectDelay = 30000)
            : this(new IPEndPoint(host, port), password, reconnectDelay)
        {
        }

        /// <summary>
        ///     Initialize an RCON connection and automatically call ConnectAsync().
        /// </summary>
        public RCON(IPEndPoint endpoint, string password, uint reconnectDelay = 30000)
        {
            _endpoint = endpoint;
            _password = password;
            _reconnectDelay = reconnectDelay;
            
            try
            {
                //Setup some async code to timeout the connection
                Task.Run(async () => 
                {
                    var connect = ConnectAsync();

                    //If we don't connect in under 2 seconds, we can abort.
                    if (await Task.WhenAny(connect, Task.Delay(3000)) != connect)
                    {
                        //Client is hung connecting, Likely waiting for an authentication packet.
                        Dispose();
                    }
                }).Wait();
            }
            catch
            {
                Console.WriteLine($"RCON failed to connect to {_endpoint}");
            }
        }

        // Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
        private Dictionary<int, Action<string>> _pendingCommands { get; } = new Dictionary<int, Action<string>>();

        private Socket _tcp { get; set; }

        public void Dispose()
        {
            //Don't dispose if already disposed
            if (_disposed)
                return;

            if (OnDisconnected != null) OnDisconnected();

            _connected = false;
            try
            {
                _tcp.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                Console.WriteLine("Unable to properly shutdown TCP Socket. This is likely because of a network issue.");
            }

            try
            {
                _tcp.Dispose();
            }
            catch
            {
                Console.WriteLine("Unable to properly dispose TCP Socket. This is likely because of a network issue.");
            }

            _disposed = true;
        }

        public event Action OnDisconnected;

        public event LogEventHandler OnLog;

        public delegate void LogEventHandler(string message);

        private void Log(string message)
        {
            if (OnLog != null)
                OnLog(message);
        }

        /// <summary>
        ///     Connect to a server through RCON.  Automatically sends the authentication packet.
        /// </summary>
        /// <returns>Awaitable which will complete when a successful connection is made and authentication is successful.</returns>
        public async Task ConnectAsync()
        {
            _tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await _tcp.ConnectAsync(_endpoint);
            _connected = true;

            // Set up TCP listener
            var e = new SocketAsyncEventArgs();
            e.Completed += TCPPacketReceived;
            e.SetBuffer(new byte[Constants.MAX_PACKET_SIZE], 0, Constants.MAX_PACKET_SIZE);

            // Start listening for responses
            _tcp.ReceiveAsync(e);

            // Wait for successful authentication
            _authenticationTask = new TaskCompletionSource<bool>();
            await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _password));
            await _authenticationTask.Task;

            //Task.Run(() => WatchForDisconnection(_reconnectDelay)).Forget();
            Task.Run(CheckIfStale).Forget();
        }

        private async Task CheckIfStale()
        {
            while (true)
            {
                //If we somehow disconnected, break out.
                if (!_connected)
                    break;

                //Value is multiplied by 5
                if(_staleCounter > 120)
                {
                    Console.WriteLine($"RCON Client for {_endpoint} is stale - Disposing!");
                    Dispose();
                    break;
                }

                await Task.Delay(5000);
                _staleCounter++;
            }
        }

        /// <summary>
        ///     Send a command to the server, and wait for the response before proceeding.  Expect the result to be parseable into
        ///     T.
        /// </summary>
        /// <typeparam name="T">Type to parse the command as.</typeparam>
        /// <param name="command">Command to send to the server.</param>
        public async Task<T> SendCommandAsync<T>(string command)
            where T : class, IParseable, new()
        {
            Monitor.Enter(_lock);
            var source = new TaskCompletionSource<T>();
            var instance = ParserHelpers.CreateParser<T>();

            var container = new ParserContainer
            {
                IsMatch = line => instance.IsMatch(line),
                Parse = line => instance.Parse(line),
                Callback = parsed => source.SetResult((T) parsed)
            };

            _pendingCommands.Add(++_packetId, container.TryCallback);
            var packet = new RCONPacket(_packetId, PacketType.ExecCommand, command);
            Monitor.Exit(_lock);

            await SendPacketAsync(packet);
            return await source.Task;
        }

        /// <summary>
        ///     Send a command to the server, and wait for the response before proceeding.  R
        /// </summary>
        /// <param name="command">Command to send to the server.</param>
        public async Task<string> SendCommandAsync(string command)
        {
            Monitor.Enter(_lock);
            var source = new TaskCompletionSource<string>();
            _pendingCommands.Add(++_packetId, source.SetResult);
            var packet = new RCONPacket(_packetId, PacketType.ExecCommand, command);
            Monitor.Exit(_lock);

            _staleCounter = 0;
            await SendPacketAsync(packet);
            return await source.Task;
        }

        private void RCONPacketReceived(RCONPacket packet)
        {
            // Call pending result and remove from map
            Action<string> action;
            if (_pendingCommands.TryGetValue(packet.Id, out action))
            {
                action?.Invoke(packet.Body);
                _pendingCommands.Remove(packet.Id);
            }
        }

        /// <summary>
        ///     Send a packet to the server.
        /// </summary>
        /// <param name="packet">Packet to send, which will be serialized.</param>
        private async Task SendPacketAsync(RCONPacket packet)
        {
            if (!_connected) throw new InvalidOperationException("Connection is closed.");
            await _tcp.SendAsync(new ArraySegment<byte>(packet.ToBytes()), SocketFlags.None);
        }

        /// <summary>
        ///     Event called whenever raw data is received on the TCP socket.
        /// </summary>
        private void TCPPacketReceived(object sender, SocketAsyncEventArgs e)
        {
            // Parse out the actual RCON packet
            RCONPacket packet = null;
            try
            {
                packet = RCONPacket.FromBytes(e.Buffer);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Unable to read packet, this has been observed when trying to communicate" +
                                  " with a server that is currently booting.\n"+exception.Message);
                Dispose();
                return;
            }

            if (packet.Type == PacketType.AuthResponse)
            {
                // Failed auth responses return with an ID of -1
                if (packet.Id == -1)
                    throw new AuthenticationException($"Authentication failed for {_tcp.RemoteEndPoint}.");

                // Tell Connect that authentication succeeded
                _authenticationTask.SetResult(true);
            }

            // Forward to handler
            RCONPacketReceived(packet);

            // Continue listening
            if (!_connected) return;
            _tcp.ReceiveAsync(e);
        }

        /// <summary>
        ///     Polls the server to check if RCON is still authenticated.  Will still throw if the password was changed elsewhere.
        /// </summary>
        /// <param name="delay">Time in milliseconds to wait between polls.</param>
        private async void WatchForDisconnection(uint delay)
        {
            var checkedDelay = checked((int) delay);

            while (true)
            {
                try
                {
                    Identifier = Guid.NewGuid().ToString().Substring(0, 5);
                    await SendCommandAsync(Constants.CHECK_STR + Identifier);
                }
                catch
                {
                    Dispose();
                    return;
                }

                await Task.Delay(checkedDelay);
            }
        }
    }
}