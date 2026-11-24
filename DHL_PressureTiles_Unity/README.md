# DHL Express × F1 — Dance with DHL Express (Unity)

Pressure tiles game for physical 9×8 tile grid controlled via ArtNet/UDP to Teensy 4.1.

## Quick Start

1. Open Unity (2021.3 LTS or later)
2. Create new **3D** project
3. Copy the `Assets/` folder into your project
4. Create a TileConfig asset: **Assets → Create → DHL Pressure Tiles → Tile Config**
5. Create an empty scene, add an empty GameObject, attach **SceneSetup** script
6. Assign the TileConfig asset to the SceneSetup component
7. Set your Teensy IP in the TileConfig inspector (default: 192.168.1.100, port 6454)
8. Press **Play** — the entire UI builds itself at runtime

## Architecture

```
Scripts/
├── Config/
│   └── TileConfig.cs          # ScriptableObject — all settings in one place
├── ArtNet/
│   ├── ArtNetPacket.cs        # ArtNet DMX512 packet builder/parser
│   └── ArtNetController.cs    # UDP send/receive singleton
├── Game/
│   └── GameManager.cs         # State machine + scoring engine
└── UI/
    ├── GameUIController.cs    # Wires all 4 screens to GameManager events
    ├── SceneSetup.cs          # Auto-builds entire scene hierarchy at runtime
    └── DebugPanel.cs          # F12 debug panel (5 tabs)
```

## Debug Panel (F12)

| Tab | Purpose |
|-----|---------|
| Connection | Edit IP/port, reconnect, ArtPoll discovery |
| Send Commands | Control individual tiles, quick color buttons, raw UDP/hex |
| Tile Grid Test | Clickable 9×8 grid, ALL ON/OFF, Chase/Random tests |
| Receive Log | Scrollable log of all incoming messages |
| DMX Monitor | Live per-tile DMX channel values |

## ArtNet / Hardware

- **Protocol**: ArtNet (Art-Net 4, DMX512 over UDP)
- **Port**: 6454 (standard ArtNet)
- **Hardware**: Teensy 4.1 as ArtNet node
- **DMX Mapping**: 3 channels per tile (R, G, B), starting at channel 1
- **Input**: Teensy sends `PRESS:tileIndex` / `RELEASE:tileIndex` via UDP

## Command Format

Send custom commands via the Debug Panel:
- `TILE:0:255:204:0` — Set tile 0 to yellow (R:255 G:204 B:0)
- Raw hex bytes for direct protocol testing
- ArtPoll for device discovery

## Game Flow

1. **Start Screen** → Tap to start
2. **Registration** → Name + email
3. **Countdown** → 3, 2, 1, GO!
4. **Gameplay** → 60 seconds, tiles light up in yellow/red/white
5. **Final Score** → Points, correct steps, duration

## Scoring

| Color  | Points | Hex     |
|--------|--------|---------|
| Yellow | 100    | #FFCC00 |
| Red    | 150    | #E8164D |
| White  | 200    | #FFFFFF |

- Speed multiplier: faster taps = higher multiplier (up to 2.0×)
- Streak multiplier: consecutive hits boost score (up to 2.5× at 10+ streak)
- Wrong tile penalty: −50 points
