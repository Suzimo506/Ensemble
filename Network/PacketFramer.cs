using System;
using System.Collections.Generic;
using System.Text.Json;
using MDEN.Protocol;
using MDEN.Protocol.Envelopes;

namespace MDEN.Network
{
    // 处理 TCP 粘包和半包
    public class PacketFramer
    {
        private const int MaxPayloadSize = 1048576;
        private readonly List<byte> _buffer = new List<byte>();

        public void AppendData(byte[] data, int offset, int length)
        {
            for (var i = 0; i < length; i++)
            {
                _buffer.Add(data[offset + i]);
            }
        }

        public bool TryDecode(out ServerEnvelope envelope)
        {
            envelope = null;
            if (_buffer.Count < 4) return false;

            int bodyLength = _buffer[0] |
                             (_buffer[1] << 8) |
                             (_buffer[2] << 16) |
                             (_buffer[3] << 24);
            if (bodyLength <= 0 || bodyLength > MaxPayloadSize)
            {
                throw new InvalidOperationException($"Invalid packet length: {bodyLength}");
            }

            if (_buffer.Count < 4 + bodyLength) return false;

            byte[] bodyBytes = new byte[bodyLength];
            _buffer.CopyTo(4, bodyBytes, 0, bodyLength);
            _buffer.RemoveRange(0, 4 + bodyLength);

            envelope = JsonSerializer.Deserialize<ServerEnvelope>(bodyBytes, ProtocolJson.Options);
            return true;
        }

        public byte[] Encode(ClientEnvelope envelope)
        {
            byte[] bodyBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, ProtocolJson.Options);
            byte[] headerBytes = BitConverter.GetBytes(bodyBytes.Length);
            byte[] packet = new byte[4 + bodyBytes.Length];
            Array.Copy(headerBytes, 0, packet, 0, 4);
            Array.Copy(bodyBytes, 0, packet, 4, bodyBytes.Length);
            return packet;
        }
    }
}
