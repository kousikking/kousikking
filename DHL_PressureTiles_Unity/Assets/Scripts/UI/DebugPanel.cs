using UnityEngine;
using System.Collections.Generic;
using DHL_PressureTiles.ArtNet;
using DHL_PressureTiles.Config;

namespace DHL_PressureTiles.UI
{
    /// <summary>
    /// In-game debug panel for testing ArtNet/serial commands.
    /// Features:
    /// - Edit and send custom commands to individual tiles
    /// - Send/receive raw UDP packets
    /// - Live DMX channel monitor
    /// - Tile grid test (click to toggle tiles)
    /// - Connection status and log viewer
    /// Toggle with F12 or a hidden 3-finger tap.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [Header("Config")]
        public TileConfig config;

        private bool isVisible = false;
        private Vector2 scrollPos;
        private Vector2 logScrollPos;

        // Connection settings (editable)
        private string targetIP = "192.168.1.100";
        private string targetPort = "6454";
        private string receivePort = "6455";
        private string universe = "0";

        // Command fields
        private string customCommand = "TILE:0:255:200:0";
        private string rawHexCommand = "41 72 74 2D 4E 65 74 00";
        private int selectedTile = 0;
        private int selectedColorR = 255;
        private int selectedColorG = 200;
        private int selectedColorB = 0;

        // Test grid
        private bool[] testGridState;

        // Tabs
        private int selectedTab = 0;
        private readonly string[] tabNames = { "Connection", "Send Commands", "Tile Grid Test", "Receive Log", "DMX Monitor" };

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle logStyle;
        private GUIStyle tileOnStyle;
        private GUIStyle tileOffStyle;
        private bool stylesInitialized = false;

        private void Start()
        {
            if (config != null)
            {
                targetIP = config.artNetTargetIP;
                targetPort = config.artNetPort.ToString();
                receivePort = config.receivePort.ToString();
                universe = config.artNetUniverse.ToString();
                testGridState = new bool[config.TotalTiles];
            }
        }

        private void Update()
        {
            // Toggle with F12
            if (Input.GetKeyDown(KeyCode.F12))
            {
                isVisible = !isVisible;
            }

            // Toggle with 3-finger tap (touch)
            if (Input.touchCount == 3)
            {
                bool allBegan = true;
                foreach (Touch t in Input.touches)
                {
                    if (t.phase != TouchPhase.Began) allBegan = false;
                }
                if (allBegan) isVisible = !isVisible;
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.8f, 0f) }
            };

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.12f, 0.95f)) }
            };

            logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.7f, 1f, 0.7f) },
                richText = true
            };

            tileOnStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = MakeTex(2, 2, new Color(1f, 0.8f, 0f, 0.9f)), textColor = Color.black },
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };

            tileOffStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 0.9f)), textColor = new Color(1f, 1f, 1f, 0.4f) },
                fontSize = 10
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!isVisible) return;
            InitStyles();

            float panelW = 800;
            float panelH = 700;
            float panelX = (Screen.width - panelW) / 2;
            float panelY = (Screen.height - panelH) / 2;

            // Background
            GUI.Box(new Rect(panelX - 5, panelY - 5, panelW + 10, panelH + 10), "", boxStyle);

            GUILayout.BeginArea(new Rect(panelX, panelY, panelW, panelH));

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("⚡ ArtNet Debug Panel", headerStyle);
            GUILayout.FlexibleSpace();

            var artNet = ArtNetController.Instance;
            if (artNet != null)
            {
                string status = artNet.IsConnected ? "● CONNECTED" : "○ DISCONNECTED";
                Color statusColor = artNet.IsConnected ? Color.green : Color.red;
                var statusStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = statusColor }, fontStyle = FontStyle.Bold };
                GUILayout.Label(status, statusStyle);
            }

            if (GUILayout.Button("✕", GUILayout.Width(30), GUILayout.Height(25)))
                isVisible = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Tabs
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            GUILayout.Space(10);

            scrollPos = GUILayout.BeginScrollView(scrollPos);

            switch (selectedTab)
            {
                case 0: DrawConnectionTab(); break;
                case 1: DrawSendCommandsTab(); break;
                case 2: DrawTileGridTestTab(); break;
                case 3: DrawReceiveLogTab(); break;
                case 4: DrawDMXMonitorTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ══════════════════════════════════════
        // TAB 0: CONNECTION
        // ══════════════════════════════════════

        private void DrawConnectionTab()
        {
            GUILayout.Label("ArtNet / UDP Connection Settings", GUI.skin.label);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Target IP:", GUILayout.Width(120));
            targetIP = GUILayout.TextField(targetIP, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("ArtNet Port:", GUILayout.Width(120));
            targetPort = GUILayout.TextField(targetPort, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Receive Port:", GUILayout.Width(120));
            receivePort = GUILayout.TextField(receivePort, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Universe:", GUILayout.Width(120));
            universe = GUILayout.TextField(universe, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply & Reconnect", GUILayout.Height(35)))
            {
                var artNet = ArtNetController.Instance;
                if (artNet != null)
                {
                    artNet.UpdateConfig(targetIP, int.Parse(targetPort), int.Parse(universe));
                    artNet.Connect();
                }
            }

            if (GUILayout.Button("Send ArtPoll (Discover)", GUILayout.Height(35)))
            {
                ArtNetController.Instance?.SendArtPoll();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // Stats
            var ctrl = ArtNetController.Instance;
            if (ctrl != null)
            {
                GUILayout.Label($"Packets Sent: {ctrl.PacketsSent}");
                GUILayout.Label($"Packets Received: {ctrl.PacketsReceived}");
                if (!string.IsNullOrEmpty(ctrl.LastError))
                {
                    var errStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } };
                    GUILayout.Label($"Last Error: {ctrl.LastError}", errStyle);
                }
            }
        }

        // ══════════════════════════════════════
        // TAB 1: SEND COMMANDS
        // ══════════════════════════════════════

        private void DrawSendCommandsTab()
        {
            // ── Single Tile Control ──
            GUILayout.Label("▸ Single Tile Control", headerStyle);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tile Index (0-71):", GUILayout.Width(140));
            int.TryParse(GUILayout.TextField(selectedTile.ToString(), GUILayout.Width(60)), out selectedTile);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("R:", GUILayout.Width(20));
            int.TryParse(GUILayout.TextField(selectedColorR.ToString(), GUILayout.Width(50)), out selectedColorR);
            GUILayout.Label("G:", GUILayout.Width(20));
            int.TryParse(GUILayout.TextField(selectedColorG.ToString(), GUILayout.Width(50)), out selectedColorG);
            GUILayout.Label("B:", GUILayout.Width(20));
            int.TryParse(GUILayout.TextField(selectedColorB.ToString(), GUILayout.Width(50)), out selectedColorB);

            if (GUILayout.Button("SEND", GUILayout.Width(70), GUILayout.Height(25)))
            {
                var artNet = ArtNetController.Instance;
                if (artNet != null)
                {
                    Color c = new Color(selectedColorR / 255f, selectedColorG / 255f, selectedColorB / 255f);
                    artNet.SetTileColor(selectedTile, c);
                    artNet.SendDMXFrame();
                }
            }
            GUILayout.EndHorizontal();

            // Quick color buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yellow", GUILayout.Height(30)))
                SendQuickColor(TileColorType.Yellow);
            if (GUILayout.Button("Red", GUILayout.Height(30)))
                SendQuickColor(TileColorType.Red);
            if (GUILayout.Button("White", GUILayout.Height(30)))
                SendQuickColor(TileColorType.White);
            if (GUILayout.Button("OFF", GUILayout.Height(30)))
                SendQuickColor(TileColorType.Off);
            if (GUILayout.Button("ALL OFF", GUILayout.Height(30)))
            {
                ArtNetController.Instance?.ClearAllTiles();
                ArtNetController.Instance?.SendDMXFrame();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // ── Custom Command ──
            GUILayout.Label("▸ Custom Command", headerStyle);
            GUILayout.Label("Format: TILE:index:R:G:B  or  plain text");
            GUILayout.Space(3);

            GUILayout.BeginHorizontal();
            customCommand = GUILayout.TextField(customCommand, GUILayout.Height(25));
            if (GUILayout.Button("SEND", GUILayout.Width(70), GUILayout.Height(25)))
            {
                ArtNetController.Instance?.SendCustomCommand(customCommand);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // ── Raw Hex ──
            GUILayout.Label("▸ Raw Hex UDP", headerStyle);
            GUILayout.Label("Space-separated hex bytes (e.g. 41 72 74 2D)");
            GUILayout.Space(3);

            GUILayout.BeginHorizontal();
            rawHexCommand = GUILayout.TextField(rawHexCommand, GUILayout.Height(25));
            if (GUILayout.Button("SEND", GUILayout.Width(70), GUILayout.Height(25)))
            {
                ArtNetController.Instance?.SendCustomCommand(rawHexCommand);
            }
            GUILayout.EndHorizontal();
        }

        private void SendQuickColor(TileColorType colorType)
        {
            var artNet = ArtNetController.Instance;
            if (artNet != null)
            {
                artNet.SetTileColor(selectedTile, colorType);
                artNet.SendDMXFrame();
            }
        }

        // ══════════════════════════════════════
        // TAB 2: TILE GRID TEST
        // ══════════════════════════════════════

        private void DrawTileGridTestTab()
        {
            GUILayout.Label("▸ Click tiles to toggle ON/OFF", headerStyle);
            GUILayout.Label("Yellow = ON, Dark = OFF. Changes sent to hardware in real time.");
            GUILayout.Space(10);

            if (testGridState == null || testGridState.Length != config.TotalTiles)
                testGridState = new bool[config.TotalTiles];

            float tileSize = 70;
            float gap = 4;

            for (int row = 0; row < config.rows; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < config.columns; col++)
                {
                    int idx = row * config.columns + col;
                    GUIStyle style = testGridState[idx] ? tileOnStyle : tileOffStyle;

                    if (GUILayout.Button(idx.ToString(), style, GUILayout.Width(tileSize), GUILayout.Height(tileSize)))
                    {
                        testGridState[idx] = !testGridState[idx];
                        var artNet = ArtNetController.Instance;
                        if (artNet != null)
                        {
                            if (testGridState[idx])
                                artNet.SetTileColor(idx, TileColorType.Yellow);
                            else
                                artNet.ClearTile(idx);
                            artNet.SendDMXFrame();
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ALL ON", GUILayout.Height(35)))
            {
                for (int i = 0; i < testGridState.Length; i++)
                {
                    testGridState[i] = true;
                    ArtNetController.Instance?.SetTileColor(i, TileColorType.Yellow);
                }
                ArtNetController.Instance?.SendDMXFrame();
            }
            if (GUILayout.Button("ALL OFF", GUILayout.Height(35)))
            {
                for (int i = 0; i < testGridState.Length; i++)
                    testGridState[i] = false;
                ArtNetController.Instance?.ClearAllTiles();
                ArtNetController.Instance?.SendDMXFrame();
            }
            if (GUILayout.Button("CHASE TEST", GUILayout.Height(35)))
            {
                StartCoroutine(ChaseTest());
            }
            if (GUILayout.Button("RANDOM TEST", GUILayout.Height(35)))
            {
                StartCoroutine(RandomTest());
            }
            GUILayout.EndHorizontal();
        }

        private System.Collections.IEnumerator ChaseTest()
        {
            var artNet = ArtNetController.Instance;
            if (artNet == null) yield break;

            for (int i = 0; i < config.TotalTiles; i++)
            {
                artNet.ClearAllTiles();
                artNet.SetTileColor(i, TileColorType.Yellow);
                artNet.SendDMXFrame();
                testGridState = new bool[config.TotalTiles];
                testGridState[i] = true;
                yield return new WaitForSeconds(0.05f);
            }
            artNet.ClearAllTiles();
            artNet.SendDMXFrame();
            testGridState = new bool[config.TotalTiles];
        }

        private System.Collections.IEnumerator RandomTest()
        {
            var artNet = ArtNetController.Instance;
            if (artNet == null) yield break;

            TileColorType[] colors = { TileColorType.Yellow, TileColorType.Red, TileColorType.White };

            for (int round = 0; round < 20; round++)
            {
                artNet.ClearAllTiles();
                testGridState = new bool[config.TotalTiles];

                int count = Random.Range(4, 10);
                for (int j = 0; j < count; j++)
                {
                    int idx = Random.Range(0, config.TotalTiles);
                    var col = colors[Random.Range(0, colors.Length)];
                    artNet.SetTileColor(idx, col);
                    testGridState[idx] = true;
                }
                artNet.SendDMXFrame();
                yield return new WaitForSeconds(0.3f);
            }

            artNet.ClearAllTiles();
            artNet.SendDMXFrame();
            testGridState = new bool[config.TotalTiles];
        }

        // ══════════════════════════════════════
        // TAB 3: RECEIVE LOG
        // ══════════════════════════════════════

        private void DrawReceiveLogTab()
        {
            GUILayout.Label("▸ Incoming Data Log", headerStyle);

            var artNet = ArtNetController.Instance;
            if (artNet == null)
            {
                GUILayout.Label("ArtNet controller not found.");
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Log", GUILayout.Width(100)))
                artNet.ClearLog();
            GUILayout.Label($"  Entries: {artNet.LogEntries.Count}");
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            logScrollPos = GUILayout.BeginScrollView(logScrollPos, GUILayout.Height(500));
            foreach (string entry in artNet.LogEntries)
            {
                GUILayout.Label(entry, logStyle);
            }
            GUILayout.EndScrollView();
        }

        // ══════════════════════════════════════
        // TAB 4: DMX MONITOR
        // ══════════════════════════════════════

        private void DrawDMXMonitorTab()
        {
            GUILayout.Label("▸ Live DMX Universe State", headerStyle);

            var artNet = ArtNetController.Instance;
            if (artNet == null)
            {
                GUILayout.Label("ArtNet controller not found.");
                return;
            }

            byte[] dmx = artNet.CurrentDMXState;

            GUILayout.Label($"Showing channels {config.dmxStartAddress} to {config.dmxStartAddress + (config.TotalTiles * config.channelsPerTile) - 1}");
            GUILayout.Space(5);

            // Show tile-by-tile DMX values
            for (int i = 0; i < Mathf.Min(config.TotalTiles, 72); i++)
            {
                int offset = (config.dmxStartAddress - 1) + (i * config.channelsPerTile);
                if (offset + 2 >= 512) break;

                byte r = dmx[offset];
                byte g = dmx[offset + 1];
                byte b = dmx[offset + 2];

                bool active = r > 0 || g > 0 || b > 0;
                Color labelColor = active ? new Color(1f, 0.8f, 0f) : new Color(0.5f, 0.5f, 0.5f);

                if (i % 9 == 0 && i > 0)
                    GUILayout.Space(3);

                GUILayout.BeginHorizontal();
                var tileStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = labelColor }, fontSize = 11 };
                GUILayout.Label($"Tile {i:D2}  CH{offset + 1}-{offset + 3}:  R={r:D3}  G={g:D3}  B={b:D3}", tileStyle);

                // Color preview swatch
                var swatchTex = MakeTex(20, 14, new Color(r / 255f, g / 255f, b / 255f));
                GUILayout.Label(new GUIContent(swatchTex), GUILayout.Width(20), GUILayout.Height(14));
                GUILayout.EndHorizontal();
            }
        }

        // ══════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════

        private Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Show/hide the debug panel from code.
        /// </summary>
        public void Toggle() => isVisible = !isVisible;
        public bool IsVisible => isVisible;
    }
}
