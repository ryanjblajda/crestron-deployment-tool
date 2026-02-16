using CrestronDeploymentTool.Model.Networking;
using CrestronDeploymentTool.Model.TargetDevices;
using CrestronDeploymentTool.UserInterface;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Serilog;

namespace CrestronDeploymentTool.Discovery
{
    /// <summary>
    /// a static class to facilitate the discovery of crestron devices connected to a NIC
    /// </summary>
    public static class CrestronDeviceDiscovery
    {
        private const string prefix = "Device Discovery |";
        public static bool IsDiscoveryActive { get; private set; }

        public static CancellationTokenSource? CancellationToken;

        private const int discoveryPort = 41794;
        private const int discoveryInitialTime = 8000;
        private const int discoveryAdditionalTime = 2000;
        private const int discoveryPacketPauseTime = 500;
        private const int discoveryPacketBroadcasts = 3;
        private const string discoveryResponsePattern = @"\x15[\x00-\x1F]*(?<hostname>[a-zA-Z0-9-]*)[\s\x00]*(?<model>[a-zA-Z0-9-]+)[\s\x00\w]*?\[(?<firmware>[^,#\]]+?(?:\([^)]*\))?)(?:,\s*#(?<serial>[^\]]+))?\]\s*@?(?<devid>[\w-]+)?";
        private static readonly byte[] discoveryRequestHeader = new byte[] { 0x14, 0x00, 0x00, 0x00, 0x01, 0x04, 0x00, 0x03, 0x00, 0x00 };
        private static readonly byte[] discoveryResponse = new byte[] { 0x15, 0x00, 0x00, 0x00 };

        /// <summary>
        /// begins discovering devices on the provided list of network interfaces
        /// </summary>
        /// <param name="networkInterfaces">the valid network interfaces to attempt to search for devices on</param>
        /// <returns>the count of discovered devices</returns>
        public static int DiscoverDevices(List<AvailableNetworkInterface> networkInterfaces)
        {
            List<CrestronDevice> discovered = new List<CrestronDevice>();
            
            if (!IsDiscoveryActive) {
                
                IsDiscoveryActive = true;

                CancellationToken = new CancellationTokenSource();

                networkInterfaces.ForEach(async i =>
                {
                    await DiscoverDevicesViaInterface(i.Interface, CancellationToken);
                });

                IsDiscoveryActive = false;
            }

            return discovered.Count;
        }

        /// <summary>
        /// generates the discovery packet that crestron devices listen
        /// </summary>
        /// <returns>a byte array containing the discovery request packet</returns>
        private static byte[] GenerateDiscoveryPacket()
        {
            List<byte> packet = discoveryRequestHeader.ToList();

            packet = packet.Concat(Encoding.ASCII.GetBytes(Dns.GetHostName())).ToList();

            packet = packet.Concat(new byte[266 - packet.Count]).ToList();
            
            return packet.ToArray();
        }

        /// <summary>
        /// discovers devices on provided network interface
        /// </summary>
        /// <param name="intf">the ineterface to use</param>
        /// <param name="cancellationToken">a cancellation token source for asynchronous function calls</param>
        /// <returns></returns>
        private static async Task DiscoverDevicesViaInterface(NetworkInterface? intf, CancellationTokenSource? cancellationToken)
        {
            if (intf != null)
            {
                if (intf.OperationalStatus == OperationalStatus.Up)
                {
                    try
                    {
                        IPAddress? address = intf.GetIPProperties()?.UnicastAddresses.First(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Address;
                        
                        if (address != null)
                        {
                            IPEndPoint local = new IPEndPoint(address, discoveryPort);
                            UdpClient client = new UdpClient(local);
                            client.EnableBroadcast = true;
                            //listen for responses in a separate thread
                            _ = Task.Run(async () => { await Listen(client, cancellationToken); });

                            byte[] discover = GenerateDiscoveryPacket();

                            for (int i = 0; i < discoveryPacketBroadcasts; i++)
                            {
                                IPEndPoint broadcast = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
                                int sent = await client.SendAsync(discover, discover.Count(), broadcast);
                                Log.Information($"{prefix} UDP Broadcast Sent: {sent} bytes from {client.Client.LocalEndPoint} -> {broadcast}");
                                Thread.Sleep(discoveryPacketPauseTime);
                            }
                            client.Dispose();
                        }
                    }
                    catch (SocketException ex) {
                        Log.Fatal($"{prefix} {ex.Message}");
                        Application.Current.Dispatcher.Invoke(() => { ConfirmationDialog.Show($"Exception Encountered:\r\r{ex.Message}", "Discovery -> Socket Exception", MessageBoxButton.OK); }); 
                    } 
                }
                else { Log.Warning($"{prefix} Interface {intf.Name} is {intf.OperationalStatus}, not broadcasting discovery packet..."); }
            }
        }

        /// <summary>
        /// parses the incoming device response and adds new devices to the discovered device list
        /// </summary>
        /// <param name="bytes">the incoming data from udp</param>
        /// <param name="ipaddr">the ip address of the sender</param>
        /// <param name="timer">a reference to the timer object to extend the discovery time when a new device is added</param>
        private static void HandleIncomingDeviceResponse(byte[] bytes, string ipaddr, Timer timer)
        {
            if (bytes.Length > 0)
            {
                //Log.Debug("Valid Response Length");

                if (bytes.Take(discoveryResponse.Length).SequenceEqual(discoveryResponse))
                {
                    //Log.Debug("Valid Discovery Header Found");

                    string data = Encoding.ASCII.GetString(bytes);

                    Log.Debug($"{prefix} Received: {Utilities.TextHelpers.CleanString(data)} from device @ {ipaddr}");

                    Match match = Regex.Match(data, discoveryResponsePattern);
                    
                    if (match.Groups.Count != 0)
                    {
                        string name = match.Groups["hostname"].Value;
                        string model = match.Groups["model"].Value;
                        string firmware = match.Groups["firmware"].Value;
                        string serial = match.Groups["serial"].Value;

                        Log.Debug($"{prefix} NAME: {name} MODEL: {model} FIRMWARE: {firmware} SERIAL: {serial}");

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            lock (DiscoveredDevices.AvailableDiscoveredDevices)
                            {
                                if (!DiscoveredDevices.AvailableDiscoveredDevices.ToList().Any(d => d.Serial == serial))
                                {
                                    timer.Change(discoveryAdditionalTime, Timeout.Infinite);
                                    Log.Debug($"{prefix} Device Found, Discovery Time Extended by {discoveryAdditionalTime / 1000}s");
                                    DiscoveredDevices.AddDevice(new CrestronDevice(name, model, serial, firmware, ipaddr), DiscoveredDevices.AvailableDiscoveredDevices);
                                }
                                else
                                {
                                    Log.Debug($"{prefix} Existing Device Response, Updating Device Details...");
                                    CrestronDevice? match = DiscoveredDevices.AvailableDiscoveredDevices.ToList().Find(d => d.Serial == serial);
                                    //if we find a matching serial number, update the details if needed
                                    if (match != null)
                                    {
                                        if (match.Model != model) { match.Model = model; }
                                        if (match.NetworkConfiguration.Hostname != name) { match.NetworkConfiguration.Hostname = name; }
                                        if (match.FirmwareVersion != firmware) match.FirmwareVersion = firmware;
                                    }
                                }
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// the callback for when the discovery listening timer has expired, causing the cancellation token source to become cancelled, ending the listening task
        /// </summary>
        /// <param name="userobj">the cancellation token, as a generic object</param>
        private static void OnTimerExpired(object? userobj)
        {
            //Log.Debug(userobj?.GetType());
            if (userobj?.GetType() == typeof(CancellationTokenSource)) {
                Log.Debug($"{prefix} User Object Is CancellationTokenSource");
                CancellationTokenSource token = (CancellationTokenSource)userobj;
                token.Cancel();
                Log.Debug($"{prefix} Timer Expired...Cancelling Token");
            }
        }

        /// <summary>
        /// a loop to listen for crestron udp responses after the initial broadcast on a NIC
        /// </summary>
        /// <param name="client">the udp client</param>
        /// <param name="token">the cancellation token source to monitor so the task knows when to end</param>
        /// <returns></returns>
        private static async Task Listen(UdpClient client, CancellationTokenSource? token)
        {
            Timer timer = new Timer(OnTimerExpired, token, discoveryInitialTime, Timeout.Infinite);

            if (token != null)
            {
                Log.Debug($"{prefix} Starting Listen @ {client.Client.LocalEndPoint}");

                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult rx = await client.ReceiveAsync();
                    Log.Debug($"{prefix} {rx.RemoteEndPoint.Address.ToString()}:{rx.RemoteEndPoint.Port} => Sent {rx.Buffer.Length} Bytes");
                    HandleIncomingDeviceResponse(rx.Buffer, rx.RemoteEndPoint.Address.ToString().ToLowerInvariant(), timer);
                }

                Log.Debug($"{prefix} Stopping Listen @ {client.Client.LocalEndPoint}");
            }
        }
    }
}
