using HslCommunication;
using McProtocolClientLib.Core;
using McProtocolClientLib.Diagnostics;
using McProtocolClientLib.Tags;
using System.Text;

namespace McProtocolClientLib.IO;

/// <summary>
/// Đọc nhiều tag PLC Mitsubishi trong 1 request batch (tối ưu tốc độ).
/// - Tag thuộc Word device (D, W, R, ZR, SD, SW): gom theo device, đọc 1 lần dạng byte[]
///   rồi giải mã từng tag (Word/Int/DInt/Real/LReal/String/Bit-in-word).
/// - Tag thuộc Bit device  (M, X, Y, B, F, L, S, SM, SB): gom theo device, đọc 1 lần dạng bool[].
///
/// LƯU Ý: MC Protocol QnA-3E binary tối đa ~480 word/request và ~7168 point cho bit.
/// Nếu range tag quá lớn hoặc có khoảng trống lớn, lớp này sẽ tự động chia nhỏ.
/// </summary>
public class PlcGroupReader
{
    /// <summary>Giới hạn an toàn số word/1 request (QnA-3E binary max 480).</summary>
    public int MaxWordsPerRequest { get; set; } = 450;

    /// <summary>Nếu khoảng trống giữa 2 tag liên tiếp lớn hơn ngưỡng này (word) thì tách block.</summary>
    public int MaxWordGap { get; set; } = 32;

    /// <summary>Giới hạn an toàn số bit/1 request (QnA-3E binary max 7168).</summary>
    public int MaxBitsPerRequest { get; set; } = 3584;

    /// <summary>Nếu khoảng trống giữa 2 bit-tag liên tiếp lớn hơn ngưỡng này thì tách block.</summary>
    public int MaxBitGap { get; set; } = 64;

    private readonly PlcClient _plc;
    public PlcDiagnostics Diagnostics { get; } = new();

    /// <summary>
    /// Sự kiện báo lỗi đọc – không throw exception ra ngoài.
    /// Bind để hiển thị MessageBox/log mà KHÔNG làm crash form.
    /// </summary>
    public event Action<string>? OnReadError;

    public PlcGroupReader(PlcClient plc)
    {
        _plc = plc;
    }

    /// <summary>Đọc group tag đồng bộ (không throw – nuốt lỗi và bắn OnReadError).</summary>
    public void ReadGroup(IEnumerable<PlcTag> tags)
    {
        Diagnostics.MeasureRead(() =>
        {
            try
            {
                // Không gọi EnsureConnected() ở đây để tránh concurrent Connect() flood.
                // Watchdog là sole reconnect mechanism. Nếu chưa Connected, bỏ qua polling cycle này.
                if (_plc.State != PlcConnectionState.Connected)
                {
                    OnReadError?.Invoke("PLC not connected");
                    return;
                }

                var list = tags.ToList();

                // Parse địa chỉ từng tag
                foreach (var tag in list)
                    PlcAddressParser.Parse(tag);

                // Tách 2 nhóm: word-device (gồm cả bit-in-word) và bit-device
                var wordTags = list.Where(t => t.DeviceKind == PlcDeviceKind.WordDevice).ToList();
                var bitTags  = list.Where(t => t.DeviceKind == PlcDeviceKind.BitDevice).ToList();

                ReadWordDevices(wordTags);
                ReadBitDevices(bitTags);
            }
            catch (Exception ex)
            {
                foreach (var tag in tags)
                    tag.Status = PlcConnectionState.Disconnected;

                // Log exact HslCommunication error để chẩn đoán nguyên nhân mất kết nối
                PlcLogger.Warn($"[{_plc.Host}:{_plc.Port}] ReadGroup FAIL: {ex.Message}");

                // Báo PlcClient biết để watchdog kích hoạt reconnect ngay
                _plc.NotifyError();
                OnReadError?.Invoke(ex.Message);
            }
        });
    }

    /// <summary>Đọc group bất đồng bộ.</summary>
    public Task ReadGroupAsync(IEnumerable<PlcTag> tags)
        => Task.Run(() => ReadGroup(tags));

    // ===========================================================================
    // WORD DEVICES (D, W, R, ZR, SD, SW)
    // ===========================================================================
    private void ReadWordDevices(List<PlcTag> tags)
    {
        if (tags.Count == 0) return;

        // Group theo device code (D, W, R, ZR, SD, SW)
        foreach (var deviceGroup in tags.GroupBy(t => t.DeviceCode))
        {
            // Chia thành các block hợp lý theo gap & max length
            var blocks = SplitIntoBlocks(
                deviceGroup.OrderBy(t => t.Offset).ToList(),
                MaxWordGap,
                MaxWordsPerRequest);

            foreach (var block in blocks)
            {
                int minOffset = block.Min(t => t.Offset);
                int maxOffset = block.Max(t => t.Offset + t.WordSize);
                ushort wordLength = (ushort)(maxOffset - minOffset);
                if (wordLength == 0) continue;

                string startAddr = FormatAddress(deviceGroup.Key, minOffset, block[0].OffsetRadix);

                byte[] buffer;
                lock (_plc.SyncLock)
                {
                    var res = (OperateResult<byte[]>)_plc.Client.Read(startAddr, wordLength);
                    if (!res.IsSuccess)
                        throw new Exception($"MC Read {startAddr}[{wordLength}] failed: {res.Message}");
                    buffer = res.Content;
                }

                // Giải mã từng tag từ buffer (mỗi word = 2 byte)
                foreach (var tag in block)
                {
                    int byteIndex = (tag.Offset - minOffset) * 2;
                    object raw = DecodeWordValue(buffer, byteIndex, tag);
                    tag.ApplyScaling(raw);
                }
            }
        }
    }

    /// <summary>
    /// Giải mã 1 tag từ buffer byte[] (đã read theo word).
    /// </summary>
    private object DecodeWordValue(byte[] b, int i, PlcTag t)
    {
        var bt = (HslCommunication.Core.IByteTransform)_plc.Client.ByteTransform;

        switch (t.DataType)
        {
            case PlcDataType.Bool:
                {
                    // Bit-in-word: D100.5 → bit thứ 5 trong word D100
                    int bit = t.Bit;
                    int byteOff = bit / 8;
                    int bitOff  = bit % 8;
                    return (b[i + byteOff] & (1 << bitOff)) != 0;
                }

            case PlcDataType.Byte:
            case PlcDataType.USInt:
                return b[i];

            case PlcDataType.SInt:
                return (sbyte)b[i];

            case PlcDataType.Char:
                return (char)b[i];

            case PlcDataType.Word:
            case PlcDataType.UInt:
                return bt.TransUInt16(b, i);

            case PlcDataType.Int:
                return bt.TransInt16(b, i);

            case PlcDataType.DWord:
            case PlcDataType.UDInt:
                return bt.TransUInt32(b, i);

            case PlcDataType.DInt:
                return bt.TransInt32(b, i);

            case PlcDataType.Real:
                return bt.TransSingle(b, i);

            case PlcDataType.LWord:
            case PlcDataType.ULInt:
                return bt.TransUInt64(b, i);

            case PlcDataType.LInt:
                return bt.TransInt64(b, i);

            case PlcDataType.LReal:
                return bt.TransDouble(b, i);

            case PlcDataType.String:
                {
                    int byteCount = t.WordSize * 2;
                    if (i + byteCount > b.Length) byteCount = b.Length - i;
                    // Mitsubishi QnA3E binary: low_byte = char[2k], high_byte = char[2k+1] per word.
                    // Bytes arrive in order [low, high] → ASCII decode in-order is correct, no swap needed.
                    string s = bt.TransString(b, i, byteCount, Encoding.ASCII);
                    int nullIdx = s.IndexOf('\0');
                    if (nullIdx >= 0) s = s.Substring(0, nullIdx);
                    return s;
                }

            default:
                return null!;
        }
    }

    // ===========================================================================
    // BIT DEVICES (M, X, Y, B, F, L, S, SM, SB)
    // ===========================================================================
    private void ReadBitDevices(List<PlcTag> tags)
    {
        if (tags.Count == 0) return;

        foreach (var deviceGroup in tags.GroupBy(t => t.DeviceCode))
        {
            var blocks = SplitIntoBlocks(
                deviceGroup.OrderBy(t => t.Offset).ToList(),
                MaxBitGap,
                MaxBitsPerRequest);

            foreach (var block in blocks)
            {
                int minOffset = block.Min(t => t.Offset);
                int maxOffset = block.Max(t => t.Offset + 1); // mỗi bit-tag = 1 bit
                ushort length = (ushort)(maxOffset - minOffset);
                if (length == 0) continue;

                string startAddr = FormatAddress(deviceGroup.Key, minOffset, block[0].OffsetRadix);

                bool[] bits;
                lock (_plc.SyncLock)
                {
                    var res = (OperateResult<bool[]>)_plc.Client.ReadBool(startAddr, length);
                    if (!res.IsSuccess)
                        throw new Exception($"MC ReadBool {startAddr}[{length}] failed: {res.Message}");
                    bits = res.Content;
                }

                foreach (var tag in block)
                {
                    int idx = tag.Offset - minOffset;
                    object raw = bits[idx];
                    tag.ApplyScaling(raw);
                }
            }
        }
    }

    // ===========================================================================
    // BLOCK SPLITTER – cắt range thành các block hợp lệ
    // ===========================================================================
    /// <summary>
    /// Cắt danh sách tag (cùng device) đã sort theo Offset thành các block sao cho:
    /// - Khoảng trống giữa 2 tag liên tiếp ≤ maxGap
    /// - Tổng độ dài 1 block ≤ maxLength
    /// </summary>
    internal static List<List<PlcTag>> SplitIntoBlocks(List<PlcTag> sortedTags, int maxGap, int maxLength)
    {
        var blocks = new List<List<PlcTag>>();
        if (sortedTags.Count == 0) return blocks;

        // WordSize của tag bit-device không có ý nghĩa "size", coi = 1 (1 bit = 1 đơn vị)
        int UnitSize(PlcTag t) =>
            t.DeviceKind == PlcDeviceKind.WordDevice ? Math.Max(1, t.WordSize) : 1;

        var current = new List<PlcTag> { sortedTags[0] };
        int currentStart = sortedTags[0].Offset;
        int currentEnd   = sortedTags[0].Offset + UnitSize(sortedTags[0]);

        for (int i = 1; i < sortedTags.Count; i++)
        {
            var t = sortedTags[i];
            int tStart = t.Offset;
            int tEnd   = t.Offset + UnitSize(t);

            int gap = tStart - currentEnd;          // khoảng trống tới phần tử mới
            int newLen = tEnd - currentStart;       // độ dài block nếu thêm vào

            if (gap > maxGap || newLen > maxLength)
            {
                blocks.Add(current);
                current = new List<PlcTag> { t };
                currentStart = tStart;
                currentEnd = tEnd;
            }
            else
            {
                current.Add(t);
                if (tEnd > currentEnd) currentEnd = tEnd;
            }
        }

        blocks.Add(current);
        return blocks;
    }

    // ===========================================================================
    // Helpers
    // ===========================================================================
    private static string FormatAddress(string deviceCode, int offset, int radix)
    {
        return radix == 16
            ? $"{deviceCode}{offset:X}"
            : $"{deviceCode}{offset}";
    }
}
