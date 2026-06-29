using McProtocolClientLib.Tags;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace McProtocolClientLib.Core
{
    /// <summary>
    /// Raw TCP client tự cài đặt MC Protocol QnA-3E Binary frame (Mitsubishi Q/L/iQ-R series),
    /// thay thế HslCommunication.MelsecMcNet để loại bỏ giới hạn license process-level
    /// ("System authorization failed...") — xem DEC-012. DEC-013: bỏ hoàn toàn dependency
    /// HslCommunication khỏi project (OperateResult/IByteTransform tự viết — xem OperateResult.cs,
    /// ByteTransform.cs).
    /// </summary>
    /// <remarks>
    /// File: McProtocolScada.Core/Core/Mc3EBinaryClient.cs
    /// Created: 2026-06-16
    ///
    /// API (Read/ReadBool/Write/ConnectClose/ByteTransform) khớp với MelsecMcNet để
    /// PlcGroupReader/PlcGroupWriter gọi qua dynamic mà KHÔNG cần sửa.
    ///
    /// Frame đã verify với PLC thật (Q06UDV) qua PlcClient.TestRawAsync(): đọc D162 thành công.
    /// Bit-device packing (2 điểm/byte, low nibble trước) lấy từ tài liệu MC Protocol chuẩn,
    /// CHƯA verify trực tiếp trên PLC thật — nên test READ trước khi tin tưởng WRITE production.
    /// </remarks>
    public class Mc3EBinaryClient
    {
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 6000;
        public byte NetworkNumber { get; set; } = 0;
        public byte NetworkStationNumber { get; set; } = 0;
        public int ConnectTimeOut { get; set; } = 3000;
        public int ReceiveTimeOut { get; set; } = 3000;

        /// <summary>
        /// RegularByteTransform tự viết (xem ByteTransform.cs) — hành vi đã xác nhận khớp byte-for-byte
        /// với HslCommunication.RegularByteTransform(DataFormat.DCBA) qua thực nghiệm (DEC-013), nên
        /// decode DWord/Real/LReal vẫn đúng như cũ.
        /// </summary>
        public IByteTransform ByteTransform { get; set; } = new RegularByteTransform();

        // Device code nhị phân theo chuẩn MC Protocol. Chỉ D=0xA8 đã verify thực tế (TestRawAsync);
        // còn lại lấy từ tài liệu chuẩn Mitsubishi (đồng nhất với bảng dùng trong HslCommunication).
        private static readonly Dictionary<string, byte> DeviceCodeMap = new Dictionary<string, byte>
        {
            ["D"] = 0xA8, ["W"] = 0xB4, ["R"] = 0xAF, ["ZR"] = 0xB0, ["SD"] = 0xA9, ["SW"] = 0xB5,
            ["M"] = 0x90, ["L"] = 0x92, ["F"] = 0x93, ["S"] = 0x98, ["SM"] = 0x91,
            ["X"] = 0x9C, ["Y"] = 0x9D, ["B"] = 0xA0, ["SB"] = 0xA1,
        };

        // Thử match 2-ký-tự trước (ZR/SD/SW/SM/SB) để tránh nhầm với 1-ký-tự (Z, S, S, S, S).
        private static readonly string[] TwoLetterDevices = { "ZR", "SD", "SW", "SM", "SB" };

        /// <summary>No-op — client dùng short-connection (mở/đóng TCP mỗi request), không có session để đóng.</summary>
        public void ConnectClose() { }

        public OperateResult<byte[]> Read(string address, ushort length)
        {
            if (!TryParseAddress(address, out byte devCode, out int offset, out string err))
                return new OperateResult<byte[]>(err);

            byte[] req = BuildBatchReadRequest(offset, devCode, length, isBit: false);
            var (ok, data, msg) = SendReceive(req);
            return ok ? OperateResult.CreateSuccessResult(data) : new OperateResult<byte[]>(msg);
        }

        public OperateResult<bool[]> ReadBool(string address, ushort length)
        {
            if (!TryParseAddress(address, out byte devCode, out int offset, out string err))
                return new OperateResult<bool[]>(err);

            byte[] req = BuildBatchReadRequest(offset, devCode, length, isBit: true);
            var (ok, data, msg) = SendReceive(req);
            if (!ok) return new OperateResult<bool[]>(msg);

            // 2 điểm/byte: low nibble = điểm chẵn, high nibble = điểm lẻ (chuẩn MC Protocol binary bit-units).
            bool[] result = new bool[length];
            for (int i = 0; i < length; i++)
            {
                byte b = data[i / 2];
                int nibble = (i % 2 == 0) ? (b & 0x0F) : ((b >> 4) & 0x0F);
                result[i] = nibble != 0;
            }
            return OperateResult.CreateSuccessResult(result);
        }

        public OperateResult Write(string address, byte[] data)
        {
            if (!TryParseAddress(address, out byte devCode, out int offset, out string err))
                return new OperateResult(err);

            ushort wordCount = (ushort)(data.Length / 2);
            byte[] req = BuildBatchWriteRequest(offset, devCode, wordCount, data, isBit: false);
            var (ok, _, msg) = SendReceive(req);
            return ok ? OperateResult.CreateSuccessResult() : new OperateResult(msg);
        }

        public OperateResult Write(string address, bool value)
        {
            if (!TryParseAddress(address, out byte devCode, out int offset, out string err))
                return new OperateResult(err);

            byte[] payload = { (byte)(value ? 0x01 : 0x00) };
            byte[] req = BuildBatchWriteRequest(offset, devCode, 1, payload, isBit: true);
            var (ok, _, msg) = SendReceive(req);
            return ok ? OperateResult.CreateSuccessResult() : new OperateResult(msg);
        }

        // =========================================================================
        // ADDRESS PARSING
        // =========================================================================

        internal static bool TryParseAddress(string address, out byte deviceCode, out int offset, out string error)
        {
            deviceCode = 0; offset = 0; error = "";
            if (string.IsNullOrEmpty(address))
            {
                error = "Address is null/empty";
                return false;
            }

            string addr = address.Trim().ToUpperInvariant();

            string? devicePart = null;
            foreach (var two in TwoLetterDevices)
            {
                if (addr.StartsWith(two, StringComparison.Ordinal)) { devicePart = two; break; }
            }
            if (devicePart == null)
                devicePart = addr.Substring(0, 1);

            if (!DeviceCodeMap.TryGetValue(devicePart, out deviceCode))
            {
                error = $"Unknown device code in address '{address}'";
                return false;
            }

            string numPart = addr.Substring(devicePart.Length);
            var (_, radix) = PlcDeviceCode.Resolve(devicePart);
            try
            {
                offset = Convert.ToInt32(numPart, radix);
                return true;
            }
            catch
            {
                error = $"Invalid offset in address '{address}'";
                return false;
            }
        }

        // =========================================================================
        // FRAME BUILD — QnA 3E Binary
        // (đã verify byte-for-byte với PLC thật cho batch read word: xem PlcClient.TestRawAsync)
        // =========================================================================

        internal byte[] BuildBatchReadRequest(int offset, byte deviceCode, ushort count, bool isBit)
        {
            var data = new List<byte>(10)
            {
                0x01, 0x04,                            // command 0x0401 = batch read (LE)
                (byte)(isBit ? 0x01 : 0x00), 0x00,      // subcommand: 0=word units, 1=bit units
                (byte)(offset & 0xFF),
                (byte)((offset >> 8) & 0xFF),
                (byte)((offset >> 16) & 0xFF),
                deviceCode,
                (byte)(count & 0xFF),
                (byte)((count >> 8) & 0xFF),
            };
            return BuildFrame(data.ToArray());
        }

        internal byte[] BuildBatchWriteRequest(int offset, byte deviceCode, ushort count, byte[] payload, bool isBit)
        {
            var data = new List<byte>(10 + payload.Length)
            {
                0x01, 0x14,                            // command 0x1401 = batch write (LE)
                (byte)(isBit ? 0x01 : 0x00), 0x00,
                (byte)(offset & 0xFF),
                (byte)((offset >> 8) & 0xFF),
                (byte)((offset >> 16) & 0xFF),
                deviceCode,
                (byte)(count & 0xFF),
                (byte)((count >> 8) & 0xFF),
            };
            data.AddRange(payload);
            return BuildFrame(data.ToArray());
        }

        internal byte[] BuildFrame(byte[] requestData)
        {
            const ushort cpuMonitorTimer = 0x0010; // 16 * 250ms = 4s — giống TestRawAsync đã verify OK
            ushort dataLength = (ushort)(2 + requestData.Length); // +2 cho field timer phía sau

            var frame = new List<byte>(11 + requestData.Length)
            {
                0x50, 0x00,                            // subheader (request)
                NetworkNumber,
                0xFF,                                  // PC number
                0xFF, 0x03,                             // I/O dest module (own station)
                NetworkStationNumber,
                (byte)(dataLength & 0xFF),
                (byte)((dataLength >> 8) & 0xFF),
                (byte)(cpuMonitorTimer & 0xFF),
                (byte)((cpuMonitorTimer >> 8) & 0xFF),
            };
            frame.AddRange(requestData);
            return frame.ToArray();
        }

        // =========================================================================
        // TRANSPORT — short connection: mở TCP mới mỗi request, đóng ngay sau khi xong
        // (giữ nguyên convention từ Session 3 — tránh làm đầy connection table Q series ~8 slots).
        // =========================================================================

        private (bool ok, byte[] data, string error) SendReceive(byte[] request)
        {
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(IpAddress, Port);
                if (!connectTask.Wait(ConnectTimeOut))
                    return (false, Array.Empty<byte>(), "Connect timeout");

                using var stream = tcp.GetStream();
                stream.WriteTimeout = ConnectTimeOut;
                stream.ReadTimeout = ReceiveTimeOut;

                stream.Write(request, 0, request.Length);

                byte[] header = ReadExact(stream, 9);
                ushort bodyLength = (ushort)(header[7] | (header[8] << 8));
                byte[] body = ReadExact(stream, bodyLength);

                ushort endCode = (ushort)(body[0] | (body[1] << 8));
                if (endCode != 0)
                    return (false, Array.Empty<byte>(), $"PLC end code 0x{endCode:X4}");

                byte[] data = new byte[body.Length - 2];
                Array.Copy(body, 2, data, 0, data.Length);
                return (true, data, "OK");
            }
            catch (Exception ex)
            {
                return (false, Array.Empty<byte>(), ex.Message);
            }
        }

        private static byte[] ReadExact(NetworkStream stream, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buf, read, count - read);
                if (n <= 0) throw new IOException("Connection closed while reading response");
                read += n;
            }
            return buf;
        }
    }
}
