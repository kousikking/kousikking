using UnityEngine;
using System;

namespace DHL_PressureTiles.Config
{
    /// <summary>
    /// Configuration for tile grid and ArtNet communication.
    /// Editable from Unity Inspector and the in-game debug panel.
    /// </summary>
    [CreateAssetMenu(fileName = "TileConfig", menuName = "DHL/Tile Config")]
    public class TileConfig : ScriptableObject
    {
        [Header("Grid Layout")]
        public int columns = 9;
        public int rows = 8;

        [Header("ArtNet / UDP Settings")]
        [Tooltip("IP address of the Teensy 4.1 ArtNet node")]
        public string artNetTargetIP = "192.168.1.100";
        [Tooltip("ArtNet UDP port (default 6454)")]
        public int artNetPort = 6454;
        [Tooltip("ArtNet Universe (0-32767)")]
        public int artNetUniverse = 0;
        [Tooltip("ArtNet Subnet (0-15)")]
        public int artNetSubnet = 0;
        [Tooltip("ArtNet Net (0-127)")]
        public int artNetNet = 0;
        [Tooltip("UDP port to listen for incoming tile press data")]
        public int receivePort = 6455;

        [Header("Tile DMX Mapping")]
        [Tooltip("Number of DMX channels per tile (typically 3 for RGB)")]
        public int channelsPerTile = 3;
        [Tooltip("DMX start address (1-512)")]
        public int dmxStartAddress = 1;

        [Header("Tile Colors (RGB)")]
        public Color yellowTile = new Color(1f, 0.8f, 0f);
        public Color redTile = new Color(0.91f, 0.09f, 0.3f);
        public Color whiteTile = new Color(1f, 1f, 1f);
        public Color offTile = new Color(0f, 0f, 0f);

        [Header("Game Settings")]
        public float gameDuration = 60f;
        public int minLitTiles = 4;
        public int maxLitTilesStart = 6;
        public int maxLitTilesEnd = 10;
        public float tileLifetime = 2.5f;

        [Header("Scoring")]
        public int yellowPoints = 100;
        public int redPoints = 150;
        public int whitePoints = 200;
        public int wrongTilePenalty = -50;

        [Header("Speed Multipliers")]
        public float fastThresholdMs = 500f;
        public float fastMultiplier = 2.0f;
        public float mediumThresholdMs = 1000f;
        public float mediumMultiplier = 1.5f;
        public float slowMultiplier = 1.0f;

        [Header("Streak Bonuses")]
        public int streak3Threshold = 3;
        public float streak3Multiplier = 1.2f;
        public int streak5Threshold = 5;
        public float streak5Multiplier = 1.5f;
        public int streak10Threshold = 10;
        public float streak10Multiplier = 2.0f;

        public int TotalTiles => columns * rows;

        /// <summary>
        /// Get DMX start channel for a specific tile index (0-based).
        /// </summary>
        public int GetTileDMXChannel(int tileIndex)
        {
            return dmxStartAddress + (tileIndex * channelsPerTile);
        }

        /// <summary>
        /// Convert tile Color to DMX byte values (R, G, B).
        /// </summary>
        public byte[] ColorToDMX(Color color)
        {
            return new byte[]
            {
                (byte)(color.r * 255),
                (byte)(color.g * 255),
                (byte)(color.b * 255)
            };
        }

        /// <summary>
        /// Get score points for a tile color type.
        /// </summary>
        public int GetPointsForColor(TileColorType colorType)
        {
            switch (colorType)
            {
                case TileColorType.Yellow: return yellowPoints;
                case TileColorType.Red: return redPoints;
                case TileColorType.White: return whitePoints;
                default: return 0;
            }
        }

        /// <summary>
        /// Get speed multiplier based on reaction time in milliseconds.
        /// </summary>
        public float GetSpeedMultiplier(float reactionTimeMs)
        {
            if (reactionTimeMs <= fastThresholdMs) return fastMultiplier;
            if (reactionTimeMs <= mediumThresholdMs) return mediumMultiplier;
            return slowMultiplier;
        }

        /// <summary>
        /// Get streak multiplier based on current streak count.
        /// </summary>
        public float GetStreakMultiplier(int streak)
        {
            if (streak >= streak10Threshold) return streak10Multiplier;
            if (streak >= streak5Threshold) return streak5Multiplier;
            if (streak >= streak3Threshold) return streak3Multiplier;
            return 1.0f;
        }
    }

    public enum TileColorType
    {
        Off,
        Yellow,
        Red,
        White
    }
}
