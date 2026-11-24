using System;

namespace DHL_PressureTiles.ArtNet
{
    /// <summary>
    /// Builds and parses ArtNet DMX512 packets (Art-Net protocol).
    /// ArtNet uses UDP port 6454 to send DMX data to lighting fixtures.
    /// </summary>
    public static class ArtNetPacket
    {
        // ArtNet header "Art-Net\0"
        private static readonly byte[] ART_NET_HEADER = { 0x41, 0x72, 0x74, 0x2D, 0x4E, 0x65, 0x74, 0x00 };

        // OpCode for ArtDmx (little-endian: 0x5000)
        private const byte OPCODE_LOW = 0x00;
        private const byte OPCODE_HIGH = 0x50;

        // Protocol version 14
        private const byte PROTOCOL_VERSION_HIGH = 0x00;
        private const byte PROTOCOL_VERSION_LOW = 14;

        /// <summary>
        /// Build an ArtDmx packet with the given DMX channel data.
        /// </summary>
        /// <param name="dmxData">Array of DMX channel values (up to 512 bytes)</param>
        /// <param name="universe">ArtNet Universe (0-15)</param>
        /// <param name="subnet">ArtNet Subnet (0-15)</param>
        /// <param name="net">ArtNet Net (0-127)</param>
        /// <param name="sequence">Sequence number (0 = disabled, 1-255 = sequential)</param>
        /// <returns>Complete ArtNet UDP packet bytes</returns>
        public static byte[] BuildArtDmxPacket(byte[] dmxData, int universe = 0, int subnet = 0, int net = 0, byte sequence = 0)
        {
            // Ensure DMX data length is even and within 512
            int dataLength = Math.Min(dmxData.Length, 512);
            if (dataLength % 2 != 0) dataLength++;

            // ArtDmx packet: 18 bytes header + DMX data
            byte[] packet = new byte[18 + dataLength];

            // Header: "Art-Net\0"
            Array.Copy(ART_NET_HEADER, 0, packet, 0, 8);

            // OpCode (little-endian)
            packet[8] = OPCODE_LOW;
            packet[9] = OPCODE_HIGH;

            // Protocol version (big-endian)
            packet[10] = PROTOCOL_VERSION_HIGH;
            packet[11] = PROTOCOL_VERSION_LOW;

            // Sequence
            packet[12] = sequence;

            // Physical port
            packet[13] = 0;

            // Universe (SubUni + Net)
            // SubUni = (subnet << 4) | universe
            packet[14] = (byte)((subnet << 4) | (universe & 0x0F));
            packet[15] = (byte)(net & 0x7F);

            // Length (big-endian)
            packet[16] = (byte)((dataLength >> 8) & 0xFF);
            packet[17] = (byte)(dataLength & 0xFF);

            // DMX data
            Array.Copy(dmxData, 0, packet, 18, Math.Min(dmxData.Length, dataLength));

            return packet;
        }

        /// <summary>
        /// Build a DMX data array for controlling RGB tiles.
        /// </summary>
        /// <param name="totalChannels">Total DMX channels needed</param>
        /// <param name="tileIndex">0-based tile index</param>
        /// <param name="channelsPerTile">Channels per tile (3 for RGB)</param>
        /// <param name="startAddress">DMX start address (1-based)</param>
        /// <param name="r">Red value 0-255</param>
        /// <param name="g">Green value 0-255</param>
        /// <param name="b">Blue value 0-255</param>
        /// <returns>DMX data array</returns>
        public static byte[] BuildTileDMX(int totalChannels, int tileIndex, int channelsPerTile, int startAddress, byte r, byte g, byte b)
        {
            byte[] dmxData = new byte[totalChannels];
            int offset = (startAddress - 1) + (tileIndex * channelsPerTile);

            if (offset + 2 < totalChannels)
            {
                dmxData[offset] = r;
                dmxData[offset + 1] = g;
                dmxData[offset + 2] = b;
            }

            return dmxData;
        }

        /// <summary>
        /// Parse an incoming ArtNet packet to extract DMX data.
        /// </summary>
        /// <param name="packetData">Raw UDP packet bytes</param>
        /// <param name="universe">Parsed universe number</param>
        /// <param name="dmxData">Extracted DMX channel data</param>
        /// <returns>True if valid ArtDmx packet</returns>
        public static bool TryParseArtDmx(byte[] packetData, out int universe, out byte[] dmxData)
        {
            universe = 0;
            dmxData = null;

            if (packetData == null || packetData.Length < 18)
                return false;

            // Verify header
            for (int i = 0; i < 8; i++)
            {
                if (packetData[i] != ART_NET_HEADER[i])
                    return false;
            }

            // Verify OpCode
            if (packetData[8] != OPCODE_LOW || packetData[9] != OPCODE_HIGH)
                return false;

            // Parse universe
            universe = packetData[14] | (packetData[15] << 8);

            // Parse data length
            int dataLength = (packetData[16] << 8) | packetData[17];
            dataLength = Math.Min(dataLength, packetData.Length - 18);

            // Extract DMX data
            dmxData = new byte[dataLength];
            Array.Copy(packetData, 18, dmxData, 0, dataLength);

            return true;
        }

        /// <summary>
        /// Build an ArtPoll packet (device discovery).
        /// </summary>
        public static byte[] BuildArtPollPacket()
        {
            byte[] packet = new byte[14];
            Array.Copy(ART_NET_HEADER, 0, packet, 0, 8);

            // OpCode for ArtPoll (0x2000, little-endian)
            packet[8] = 0x00;
            packet[9] = 0x20;

            // Protocol version
            packet[10] = PROTOCOL_VERSION_HIGH;
            packet[11] = PROTOCOL_VERSION_LOW;

            // Flags
            packet[12] = 0x00;
            packet[13] = 0x00;

            return packet;
        }
    }
}
