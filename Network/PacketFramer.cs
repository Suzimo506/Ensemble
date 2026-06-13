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
        private readonly List<byte> _buffer = new List<byte>();

        public void AppendData(byte[] data, int offset, int length)
        {
            byte[] slice = new byte[length];
            Array.Copy(data, offset, slice, 0, length);
            _buffer.AddRange(slice);
        }

        public bool TryDecode(out ServerEnvelope envelope)
        {
            envelope = null;
            if (_buffer.Count < 4) return false;

            int bodyLength = BitConverter.ToInt32(_buffer.ToArray(), 0);
            if (_buffer.Count < 4 + bodyLength) return false;

            byte[] bodyBytes = _buffer.GetRange(4, bodyLength).ToArray();
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
