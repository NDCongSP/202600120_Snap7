using HslCommunication;
using McProtocolClientLib.Core;
using McProtocolClientLib.Diagnostics;
using McProtocolClientLib.Tags;
using System.Text;

namespace McProtocolClientLib.IO;

/// <summary>
/// Ghi nhiều tag PLC Mitsubishi trong 1 batch (tối ưu tốc độ – chuẩn SCADA).
/// - Word device: Read-Modify-Write một range word (tránh ghi đè bit khác).
/// - Bit device : ghi trực tiếp Bool / mảng Bool.
///
/// Tự động chia nhỏ block khi gap lớn hoặc range vượt giới hạn MC Protocol (~480 word/req).
/// </summary>
public class PlcGroupWriter
{
    /// <summary>Giới hạn an toàn số word/1 request (QnA-3E binary max 480).</summary>
    public int MaxWordsPerRequest { get; set; } = 450;

    /// <summary>Nếu khoảng trống giữa 2 tag liên tiếp lớn hơn ngưỡng này (word) thì tách block.</summary>
    public int MaxWordGap { get; set; } = 32;

    private readonly PlcClient _plc;
    private readonly Dictionary<string, object?> _lastWritten = new();
    public PlcDiagnostics Diagnostics { get; } = new();

    /// <summary>Sự kiện báo lỗi ghi – không throw exception ra ngoài (nếu dùng *Safe*).</summary>
    public event Action<string>? OnWriteError;

    public PlcGroupWriter(PlcClient plc)
    {
        _plc = plc;
    }

    public Task WriteGroupAsync(IEnumerable<PlcTag> tags)
        => Task.Run(() => WriteGroup(tags));

    /// <summary>Chỉ ghi tag thay đổi giá trị so với lần ghi trước.</summary>
    public void WriteGroupOnlyChanged(IEnumerable<PlcTag> tags)
    {
        var changed = new List<PlcTag>();

        foreach (var tag in tags)
        {
            if (!_lastWritten.TryGetValue(tag.Name, out var old) ||
                !Equals(old, tag.NewValue))
            {
                _lastWritten[tag.Name] = tag.NewValue;
                changed.Add(tag);
            }
        }

        if (changed.Count > 0)
            WriteGroup(changed);
    }

    /// <summary>Ghi group tag đồng bộ. Throw exception khi lỗi (gọi từ button có try/catch).</summary>
    public void WriteGroup(IEnumerable<PlcTag> tags)
    {
        Diagnostics.MeasureWrite(() =>
        {
            if (!_plc.EnsureConnected())
                throw new Exception("PLC not connected");

            var list = tags.ToList();
            foreach (var tag in list)
                PlcAddressParser.Parse(tag);

            var wordTags = list.Where(t => t.DeviceKind == PlcDeviceKind.WordDevice).ToList();
            var bitTags  = list.Where(t => t.DeviceKind == PlcDeviceKind.BitDevice).ToList();

            WriteWordDevices(wordTags);
            WriteBitDevices(bitTags);
        });
    }

    /// <summary>
    /// Phiên bản ghi an toàn: KHÔNG throw – nuốt lỗi và bắn OnWriteError.
    /// Dùng cho thread polling/auto-write.
    /// </summary>
    public void WriteGroupSafe(IEnumerable<PlcTag> tags)
    {
        try { WriteGroup(tags); }
        catch (Exception ex) { OnWriteError?.Invoke(ex.Message); }
    }

    public Task WriteGroupSafeAsync(IEnumerable<PlcTag> tags)
        => Task.Run(() => WriteGroupSafe(tags));

    // ===========================================================================
    // WORD DEVICES – Read-Modify-Write
    // ===========================================================================
    private void WriteWordDevices(List<PlcTag> tags)
    {
        if (tags.Count == 0) return;

        foreach (var deviceGroup in tags.GroupBy(t => t.DeviceCode))
        {
            // Chia thành block hợp lý
            var blocks = PlcGroupReader.SplitIntoBlocks(
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

                lock (_plc.SyncLock)
                {
                    // ⚠ RMW – đọc trước để tránh phá bit/byte khác (đặc biệt cho Bit-in-word)
                    var read = (OperateResult<byte[]>)_plc.Client.Read(startAddr, wordLength);
                    if (!read.IsSuccess)
                        throw new Exception($"MC Read {startAddr}[{wordLength}] failed: {read.Message}");
                    byte[] buffer = read.Content;

                    foreach (var tag in block)
                    {
                        int byteIndex = (tag.Offset - minOffset) * 2;
                        EncodeWordValue(buffer, byteIndex, tag);
                    }

                    var write = (OperateResult)_plc.Client.Write(startAddr, buffer);
                    if (!write.IsSuccess)
                        throw new Exception($"MC Write {startAddr}[{wordLength}] failed: {write.Message}");
                }
            }
        }
    }

    /// <summary>Ghi 1 tag vào buffer (đơn vị byte).</summary>
    private void EncodeWordValue(byte[] buffer, int index, PlcTag tag)
    {
        if (tag.NewValue == null) return;

        var bt = (HslCommunication.Core.IByteTransform)_plc.Client.ByteTransform;

        switch (tag.DataType)
        {
            case PlcDataType.Bool:
                {
                    bool b = Convert.ToBoolean(tag.NewValue);
                    int bit = tag.Bit;
                    int byteOff = bit / 8;
                    int bitOff  = bit % 8;
                    if (b) buffer[index + byteOff] |= (byte)(1 << bitOff);
                    else   buffer[index + byteOff] &= (byte)~(1 << bitOff);
                    break;
                }

            case PlcDataType.Byte:
            case PlcDataType.USInt:
                buffer[index] = Convert.ToByte(tag.NewValue);
                break;

            case PlcDataType.SInt:
                buffer[index] = (byte)Convert.ToSByte(tag.NewValue);
                break;

            case PlcDataType.Char:
                buffer[index] = (byte)Convert.ToChar(tag.NewValue);
                break;

            case PlcDataType.Word:
            case PlcDataType.UInt:
                CopyTo(buffer, index, bt.TransByte(Convert.ToUInt16(tag.NewValue)));
                break;

            case PlcDataType.Int:
                CopyTo(buffer, index, bt.TransByte(Convert.ToInt16(tag.NewValue)));
                break;

            case PlcDataType.DWord:
            case PlcDataType.UDInt:
                CopyTo(buffer, index, bt.TransByte(Convert.ToUInt32(tag.NewValue)));
                break;

            case PlcDataType.DInt:
                CopyTo(buffer, index, bt.TransByte(Convert.ToInt32(tag.NewValue)));
                break;

            case PlcDataType.Real:
                CopyTo(buffer, index, bt.TransByte(Convert.ToSingle(tag.NewValue)));
                break;

            case PlcDataType.LWord:
            case PlcDataType.ULInt:
                CopyTo(buffer, index, bt.TransByte(Convert.ToUInt64(tag.NewValue)));
                break;

            case PlcDataType.LInt:
                CopyTo(buffer, index, bt.TransByte(Convert.ToInt64(tag.NewValue)));
                break;

            case PlcDataType.LReal:
                CopyTo(buffer, index, bt.TransByte(Convert.ToDouble(tag.NewValue)));
                break;

            case PlcDataType.String:
                WriteString(buffer, index, tag);
                break;
        }
    }

    /// <summary>Ghi string ASCII Mitsubishi (2 ký tự / 1 word, padding bằng 0).</summary>
    private static void WriteString(byte[] buffer, int index, PlcTag tag)
    {
        string text = tag.NewValue?.ToString() ?? "";
        if (text.Length > tag.StringLength)
            text = text.Substring(0, tag.StringLength);

        int byteCount = tag.WordSize * 2;
        byte[] strBytes = new byte[byteCount];
        var ascii = Encoding.ASCII.GetBytes(text);
        Array.Copy(ascii, 0, strBytes, 0, Math.Min(ascii.Length, byteCount));
        // Mitsubishi QnA3E binary: low_byte = char[2k] → write in-order, no swap needed.
        Array.Copy(strBytes, 0, buffer, index, byteCount);
    }

    // ===========================================================================
    // BIT DEVICES – ghi trực tiếp
    // ===========================================================================
    private void WriteBitDevices(List<PlcTag> tags)
    {
        if (tags.Count == 0) return;

        foreach (var group in tags.GroupBy(t => t.DeviceCode))
        {
            foreach (var tag in group)
            {
                if (tag.NewValue == null) continue;

                string addr = FormatAddress(group.Key, tag.Offset, tag.OffsetRadix);
                bool value = Convert.ToBoolean(tag.NewValue);

                lock (_plc.SyncLock)
                {
                    var write = (OperateResult)_plc.Client.Write(addr, value);
                    if (!write.IsSuccess)
                        throw new Exception($"MC WriteBit {addr} failed: {write.Message}");
                }
            }
        }
    }

    // ===========================================================================
    // Helpers
    // ===========================================================================
    private static void CopyTo(byte[] dst, int index, byte[] src)
    {
        Array.Copy(src, 0, dst, index, src.Length);
    }

    private static string FormatAddress(string deviceCode, int offset, int radix)
    {
        return radix == 16
            ? $"{deviceCode}{offset:X}"
            : $"{deviceCode}{offset}";
    }
}
