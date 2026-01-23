using Sharp7;
using Snap7ClientLib.Core;
using Snap7ClientLib.Diagnostics;
using System.Text;

namespace Snap7ClientLib.Tags;

/// <summary>
/// Ghi nhiều tag PLC trong 1 lần DBWrite (tối ưu tốc độ – chuẩn SCADA)
/// </summary>
public class PlcGroupWriter
{
    private readonly PlcClient _plc;
    private readonly Dictionary<string, object?> _lastWritten = new();
    public PlcDiagnostics Diagnostics { get; } = new();

    public PlcGroupWriter(PlcClient plc)
    {
        _plc = plc;
    }

    public Task WriteGroupAsync(IEnumerable<PlcTag> tags)
    => Task.Run(() => WriteGroup(tags));


    public void WriteGroupOnlyChanged(IEnumerable<PlcTag> tags)
    {
        var changed = new List<PlcTag>();

        foreach (var tag in tags)
        {
            if (!_lastWritten.TryGetValue(tag.Name, out var old) ||
                !Equals(old, tag.Value))
            {
                _lastWritten[tag.Name] = tag.Value;
                changed.Add(tag);
            }
        }

        if (changed.Count > 0)
            WriteGroup(changed);
    }


    /// <summary>
    /// Ghi group tag đồng bộ
    /// </summary>
    public void WriteGroup(IEnumerable<PlcTag> tags)
    {
        // Đảm bảo PLC luôn connected
        if (!_plc.EnsureConnected())
            throw new Exception("PLC not connected");

        var list = tags.ToList();

        // Parse địa chỉ PLC cho từng tag
        foreach (var tag in list)
            PlcAddressParser.Parse(tag);

        // Group theo DB number
        foreach (var group in list.GroupBy(t => t.Db))
        {
            // Xác định vùng nhớ cần ghi nhỏ nhất
            int minOffset = group.Min(t => t.Offset);
            int maxOffset = group.Max(t => t.Offset + t.Size);
            int length = maxOffset - minOffset;

            byte[] buffer = new byte[length];

            // ⚠️ RẤT QUAN TRỌNG:
            // Phải DBRead trước để tránh ghi đè bit/byte khác
            _plc.Client.DBRead(group.Key, minOffset, length, buffer);

            // Ghi từng tag vào buffer
            foreach (var tag in group)
            {
                int index = tag.Offset - minOffset;
                WriteValue(buffer, index, tag);
            }

            // Ghi DB chỉ 1 lần
            _plc.Client.DBWrite(group.Key, minOffset, length, buffer);
        }
    }

    /// <summary>
    /// Ghi giá trị từng tag vào buffer
    /// </summary>
    private static void WriteValue(byte[] buffer, int index, PlcTag tag)
    {
        if (tag.Value == null) return;

        switch (tag.DataType)
        {
            case PlcDataType.Bool:
                // Ghi bit bằng mask
                bool b = Convert.ToBoolean(tag.Value);
                if (b)
                    buffer[index] |= (byte)(1 << tag.Bit);
                else
                    buffer[index] &= (byte)~(1 << tag.Bit);
                break;

            case PlcDataType.Byte:
            case PlcDataType.USInt:
                buffer[index] = Convert.ToByte(tag.Value);
                break;

            case PlcDataType.SInt:
                buffer[index] = (byte)Convert.ToSByte(tag.Value);
                break;

            case PlcDataType.Char:
                buffer[index] = (byte)Convert.ToChar(tag.Value);
                break;

            case PlcDataType.Word:
            case PlcDataType.UInt:
                S7.SetWordAt(buffer, index, Convert.ToUInt16(tag.Value));
                break;

            case PlcDataType.Int:
                S7.SetIntAt(buffer, index, Convert.ToInt16(tag.Value));
                break;

            case PlcDataType.DWord:
            case PlcDataType.UDInt:
                S7.SetDWordAt(buffer, index, Convert.ToUInt32(tag.Value));
                break;

            case PlcDataType.DInt:
                S7.SetDIntAt(buffer, index, Convert.ToInt32(tag.Value));
                break;

            case PlcDataType.Real:
                S7.SetRealAt(buffer, index, Convert.ToSingle(tag.Value));
                break;

            case PlcDataType.LWord:
            case PlcDataType.ULInt:
                S7.SetULintAt(buffer, index, Convert.ToUInt64(tag.Value));
                break;

            case PlcDataType.LInt:
                S7.SetLIntAt(buffer, index, Convert.ToInt64(tag.Value));
                break;

            case PlcDataType.LReal:
                S7.SetLRealAt(buffer, index, Convert.ToDouble(tag.Value));
                break;

            case PlcDataType.String:
                WriteString(buffer, index, tag);
                break;
        }
    }

    /// <summary>
    /// Ghi string Siemens (MaxLen + CurLen + Data)
    /// </summary>
    private static void WriteString(byte[] buffer, int index, PlcTag tag)
    {
        string text = tag.Value?.ToString() ?? "";

        buffer[index] = (byte)tag.StringLength;
        buffer[index + 1] = (byte)Math.Min(text.Length, tag.StringLength);

        Encoding.ASCII.GetBytes(
            text, 0, buffer[index + 1],
            buffer, index + 2);
    }
}
