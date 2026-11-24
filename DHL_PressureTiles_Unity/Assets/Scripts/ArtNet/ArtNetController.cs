using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DHL_PressureTiles.Config;

namespace DHL_PressureTiles.ArtNet
{
    /// <summary>
    /// Main ArtNet controller: sends DMX data to Teensy 4.1 via UDP,
    /// receives tile press events back via UDP.
    /// Attach to a GameObject in the scene.
    /// </summary>
    public class ArtNetController : MonoBehaviour
    {
        [Header("Configuration")]
        public TileConfig config;

        [Header("Status")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private int packetsSent = 0;
        [SerializeField] private int packetsReceived = 0;
        [SerializeField] private string lastError = "";

        // UDP clients
        private UdpClient sendClient;
        private UdpClient receiveClient;
        private Thread receiveThread;
        private bool isRunning = false;

        // DMX state: full 512-channel universe
        private byte[] dmxUniverse = new byte[512];
        private bool dmxDirty = false;
        private byte sequenceNumber = 1;

        // Regex to parse incoming tile press: <tileId>
        private static readonly Regex PACKET_ID_PATTERN = new Regex(@"<(\d+)>");

        // Thread-safe queue for received tile events
        private readonly Queue<TilePressEvent> receivedEvents = new Queue<TilePressEvent>();
        private readonly object eventLock = new object();

        // Events
        public event Action<TilePressEvent> OnTilePressed;
        public event Action<string> OnError;
        public event Action<byte[]> OnRawDataReceived;

        // Singleton
        public static ArtNetController Instance { get; private set; }

        // ── Logging ──
        private readonly List<string> logEntries = new List<string>();
        private const int MAX_LOG_ENTRIES = 200;
        public IReadOnlyList<string> LogEntries => logEntries;
        public event Action<string> OnLogEntry;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Connect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void Update()
        {
            // Process received events on main thread
            lock (eventLock)
            {
                while (receivedEvents.Count > 0)
                {
                    var evt = receivedEvents.Dequeue();
                    OnTilePressed?.Invoke(evt);
                }
            }

            // Send DMX if dirty
            if (dmxDirty && isConnected)
            {
                SendDMXFrame();
                dmxDirty = false;
            }
        }

        /// <summary>
        /// Connect to ArtNet node. Call this to (re)establish connection.
        /// </summary>
        public void Connect()
        {
            Disconnect();

            try
            {
                // Send client
                sendClient = new UdpClient();
                sendClient.EnableBroadcast = true;

                // Receive client
                receiveClient = new UdpClient(config.receivePort);
                receiveClient.Client.ReceiveTimeout = 1000;

                // Start receive thread
                isRunning = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "ArtNet-Receive"
                };
                receiveThread.Start();

                isConnected = true;
                lastError = "";
                Log($"[ArtNet] Connected. Sending to {config.artNetTargetIP}:{config.artNetPort}, receiving on port {config.receivePort}");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                isConnected = false;
                Log($"[ArtNet] Connection error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Disconnect and clean up.
        /// </summary>
        public void Disconnect()
        {
            isRunning = false;
            isConnected = false;

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(2000);
                receiveThread = null;
            }

            sendClient?.Close();
            sendClient = null;

            receiveClient?.Close();
            receiveClient = null;

            Log("[ArtNet] Disconnected");
        }

        // ══════════════════════════════════════
        // SENDING
        // ══════════════════════════════════════

        /// <summary>
        /// Set a tile's color by grid index (0-62).
        /// Looks up the hardware ID from the serpentine mapping, then sets DMX channels.
        /// Changes are batched and sent on next frame.
        /// </summary>
        public void SetTileColor(int gridIndex, Color color)
        {
            if (gridIndex < 0 || gridIndex >= config.TotalTiles) return;

            int hardwareId = config.GridIndexToHardwareId(gridIndex);
            SetTileColorByHardwareId(hardwareId, color);
        }

        /// <summary>
        /// Set a tile's color by hardware ID (1-63) directly.
        /// </summary>
        public void SetTileColorByHardwareId(int hardwareId, Color color)
        {
            if (hardwareId < 1 || hardwareId > 63) return;

            int dmxChannel = config.GetTileDMXChannel(hardwareId);
            int offset = dmxChannel - 1; // DMX is 1-based, array is 0-based
            if (offset < 0 || offset + 2 >= 512) return;

            dmxUniverse[offset] = (byte)(color.r * 255);
            dmxUniverse[offset + 1] = (byte)(color.g * 255);
            dmxUniverse[offset + 2] = (byte)(color.b * 255);
            dmxDirty = true;
        }

        /// <summary>
        /// Set a tile's color by TileColorType enum.
        /// </summary>
        public void SetTileColor(int tileIndex, TileColorType colorType)
        {
            Color color;
            switch (colorType)
            {
                case TileColorType.Yellow: color = config.yellowTile; break;
                case TileColorType.Red: color = config.redTile; break;
                case TileColorType.White: color = config.whiteTile; break;
                default: color = config.offTile; break;
            }
            SetTileColor(tileIndex, color);
        }

        /// <summary>
        /// Turn off a specific tile.
        /// </summary>
        public void ClearTile(int tileIndex)
        {
            SetTileColor(tileIndex, TileColorType.Off);
        }

        /// <summary>
        /// Turn off all tiles.
        /// </summary>
        public void ClearAllTiles()
        {
            Array.Clear(dmxUniverse, 0, 512);
            dmxDirty = true;
        }

        /// <summary>
        /// Set raw DMX channel value. For advanced/custom commands.
        /// </summary>
        public void SetDMXChannel(int channel, byte value)
        {
            if (channel >= 0 && channel < 512)
            {
                dmxUniverse[channel] = value;
                dmxDirty = true;
            }
        }

        /// <summary>
        /// Send raw bytes as a UDP packet (for custom protocols).
        /// </summary>
        public void SendRawUDP(byte[] data, string targetIP = null, int targetPort = -1)
        {
            if (sendClient == null) return;

            try
            {
                string ip = targetIP ?? config.artNetTargetIP;
                int port = targetPort > 0 ? targetPort : config.artNetPort;
                var endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
                sendClient.Send(data, data.Length, endpoint);
                packetsSent++;
                Log($"[ArtNet] Sent raw UDP: {data.Length} bytes to {ip}:{port}");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Log($"[ArtNet] Send error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Send a custom ArtNet command string (for testing).
        /// Format: "TILE:index:R:G:B" or raw hex.
        /// </summary>
        public void SendCustomCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return;

            try
            {
                // Parse tile command: "TILE:0:255:200:0"
                if (command.StartsWith("TILE:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = command.Split(':');
                    if (parts.Length >= 5)
                    {
                        int idx = int.Parse(parts[1]);
                        byte r = byte.Parse(parts[2]);
                        byte g = byte.Parse(parts[3]);
                        byte b = byte.Parse(parts[4]);
                        SetTileColor(idx, new Color(r / 255f, g / 255f, b / 255f));
                        SendDMXFrame();
                        Log($"[CMD] Set tile {idx} to RGB({r},{g},{b})");
                        return;
                    }
                }

                // Parse hex command: "41 72 74 2D ..."
                if (command.Contains(" "))
                {
                    var hexParts = command.Split(' ');
                    byte[] data = new byte[hexParts.Length];
                    for (int i = 0; i < hexParts.Length; i++)
                    {
                        data[i] = Convert.ToByte(hexParts[i], 16);
                    }
                    SendRawUDP(data);
                    return;
                }

                // Plain text command
                byte[] textBytes = System.Text.Encoding.ASCII.GetBytes(command);
                SendRawUDP(textBytes);
                Log($"[CMD] Sent text command: {command}");
            }
            catch (Exception ex)
            {
                Log($"[CMD] Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Force-send the current DMX universe state now.
        /// </summary>
        public void SendDMXFrame()
        {
            if (sendClient == null || !isConnected) return;

            try
            {
                byte[] packet = ArtNetPacket.BuildArtDmxPacket(
                    dmxUniverse,
                    config.artNetUniverse,
                    config.artNetSubnet,
                    config.artNetNet,
                    sequenceNumber
                );

                var endpoint = new IPEndPoint(IPAddress.Parse(config.artNetTargetIP), config.artNetPort);
                sendClient.Send(packet, packet.Length, endpoint);
                packetsSent++;

                sequenceNumber++;
                if (sequenceNumber == 0) sequenceNumber = 1; // Skip 0
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Log($"[ArtNet] Send DMX error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Send an ArtPoll discovery packet.
        /// </summary>
        public void SendArtPoll()
        {
            if (sendClient == null) return;

            try
            {
                byte[] packet = ArtNetPacket.BuildArtPollPacket();
                var endpoint = new IPEndPoint(IPAddress.Broadcast, config.artNetPort);
                sendClient.Send(packet, packet.Length, endpoint);
                packetsSent++;
                Log("[ArtNet] Sent ArtPoll (discovery)");
            }
            catch (Exception ex)
            {
                Log($"[ArtNet] ArtPoll error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════
        // RECEIVING
        // ══════════════════════════════════════

        private void ReceiveLoop()
        {
            while (isRunning)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = receiveClient.Receive(ref remoteEP);

                    if (data == null || data.Length == 0) continue;

                    packetsReceived++;
                    OnRawDataReceived?.Invoke(data);

                    // Try parse as ArtNet
                    if (ArtNetPacket.TryParseArtDmx(data, out int universe, out byte[] dmxData))
                    {
                        ProcessArtNetDMX(dmxData);
                        continue;
                    }

                    // Try parse as custom tile press protocol
                    // Expected format: "PRESS:tileIndex" or "RELEASE:tileIndex"
                    string message = System.Text.Encoding.ASCII.GetString(data).Trim();
                    ProcessCustomMessage(message, remoteEP.Address.ToString());
                }
                catch (SocketException ex)
                {
                    // Timeout is expected
                    if (ex.SocketErrorCode != SocketError.TimedOut)
                    {
                        Log($"[ArtNet] Receive error: {ex.Message}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Socket closed, exit loop
                    break;
                }
                catch (Exception ex)
                {
                    Log($"[ArtNet] Unexpected receive error: {ex.Message}");
                }
            }
        }

        private void ProcessArtNetDMX(byte[] dmxData)
        {
            // Parse DMX data to detect tile presses
            // Convention: Teensy sends back DMX values > 0 on tile's channel when pressed
            for (int i = 0; i < config.TotalTiles; i++)
            {
                int offset = (config.dmxStartAddress - 1) + (i * config.channelsPerTile);
                if (offset < dmxData.Length && dmxData[offset] > 128)
                {
                    var evt = new TilePressEvent
                    {
                        tileIndex = i,
                        pressed = true,
                        timestamp = Time.time,
                        pressure = dmxData[offset] / 255f
                    };

                    lock (eventLock)
                    {
                        receivedEvents.Enqueue(evt);
                    }
                }
            }
        }

        private void ProcessCustomMessage(string message, string senderIP)
        {
            Log($"[RCV] From {senderIP}: {message}");

            // PRIMARY FORMAT: <tileId> — from Teensy pressure tiles
            // The hardware sends the hardware tile ID (1-63) wrapped in angle brackets
            var match = PACKET_ID_PATTERN.Match(message);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int hardwareId))
            {
                // Convert hardware ID to grid index for the game
                int gridIndex = config.HardwareIdToGridIndex(hardwareId);
                if (gridIndex >= 0)
                {
                    var evt = new TilePressEvent
                    {
                        tileIndex = gridIndex,
                        hardwareId = hardwareId,
                        pressed = true,
                        timestamp = Time.time,
                        pressure = 1f
                    };

                    lock (eventLock)
                    {
                        receivedEvents.Enqueue(evt);
                    }

                    Log($"[RCV] Tile press: hardware ID {hardwareId} → grid index {gridIndex}");
                    return;
                }
                else
                {
                    Log($"[RCV] Unknown hardware ID: {hardwareId}");
                }
            }

            // FALLBACK: "PRESS:tileIndex" or "RELEASE:tileIndex"
            var parts = message.Split(':');
            if (parts.Length >= 2)
            {
                bool isPress = parts[0].Equals("PRESS", StringComparison.OrdinalIgnoreCase);
                bool isRelease = parts[0].Equals("RELEASE", StringComparison.OrdinalIgnoreCase);

                if ((isPress || isRelease) && int.TryParse(parts[1], out int tileIndex))
                {
                    float pressure = 1f;
                    if (parts.Length >= 3 && float.TryParse(parts[2], out float p))
                        pressure = p;

                    var evt = new TilePressEvent
                    {
                        tileIndex = tileIndex,
                        hardwareId = -1,
                        pressed = isPress,
                        timestamp = Time.time,
                        pressure = pressure
                    };

                    lock (eventLock)
                    {
                        receivedEvents.Enqueue(evt);
                    }
                }
            }
        }

        // ══════════════════════════════════════
        // LOGGING
        // ══════════════════════════════════════

        private void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

            lock (logEntries)
            {
                logEntries.Add(entry);
                if (logEntries.Count > MAX_LOG_ENTRIES)
                    logEntries.RemoveAt(0);
            }

            OnLogEntry?.Invoke(entry);
            Debug.Log(entry);
        }

        public void ClearLog()
        {
            lock (logEntries)
            {
                logEntries.Clear();
            }
        }

        // ══════════════════════════════════════
        // STATUS
        // ══════════════════════════════════════

        public bool IsConnected => isConnected;
        public int PacketsSent => packetsSent;
        public int PacketsReceived => packetsReceived;
        public string LastError => lastError;
        public byte[] CurrentDMXState => (byte[])dmxUniverse.Clone();

        /// <summary>
        /// Update config at runtime (from debug panel).
        /// </summary>
        public void UpdateConfig(string targetIP, int port, int universe)
        {
            config.artNetTargetIP = targetIP;
            config.artNetPort = port;
            config.artNetUniverse = universe;
            Log($"[ArtNet] Config updated: {targetIP}:{port} universe {universe}");
        }
    }

    /// <summary>
    /// Data for a tile press/release event received from hardware.
    /// </summary>
    [Serializable]
    public struct TilePressEvent
    {
        public int tileIndex;     // Grid index (0-62, row-major)
        public int hardwareId;    // Hardware tile ID (1-63, serpentine)
        public bool pressed;
        public float timestamp;
        public float pressure;    // 0-1 normalized
    }
}
