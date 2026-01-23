using Sharp7;
using Snap7ClientLib.Core;
using Snap7ClientLib.Diagnostics;

namespace Snap7ClientLib.Tags;

/// <summary>
/// Đọc nhiều tag PLC trong 1 lần DBRead (tối ưu tốc độ)
/// </summary>
public class PlcGroupReader
{
    private readonly PlcClient _plc;
    public PlcDiagnostics Diagnostics { get; } = new();

    public PlcGroupReader(PlcClient plc)
    {
        _plc = plc;
    }

    /// <summary>
    /// Đọc group tag đồng bộ
    /// </summary>
    public void ReadGroup(IEnumerable<PlcTag> tags)
    {
        Diagnostics.MeasureRead(() =>
        {
            // Đảm bảo PLC đang connected
            _plc.EnsureConnected();

            var list = tags.ToList();

            // Parse địa chỉ từng tag
            foreach (var tag in list)
                PlcAddressParser.Parse(tag);

            // Group theo DB number
            foreach (var group in list.GroupBy(t => t.Db))
            {
                // Tính vùng nhớ nhỏ nhất cần đọc
                int min = group.Min(t => t.Offset);
                int max = group.Max(t => t.Offset + t.Size);
                int length = max - min;

                byte[] buffer = new byte[length];

                // Đọc DB chỉ 1 lần
                _plc.Client.DBRead(group.Key, min, length, buffer);

                // Giải mã từng tag
                foreach (var tag in group)
                {
                    int index = tag.Offset - min;
                    //tag.Value = ReadValue(buffer, index, tag);

                    //xử lý lại giá trị của tag trước khi trả về
                    object rawData = ReadValue(buffer, index, tag);
                    // Gọi hàm xử lý để tính toán Gain/Offset
                    tag.ApplyScaling(rawData);

                    // Kích hoạt sự kiện để WinForm cập nhật UI
                    tag.RaiseValueChanged();
                }
            }
        });
    }

    /// <summary>
    /// Đọc group bất đồng bộ
    /// </summary>
    public async Task ReadGroupAsync(IEnumerable<PlcTag> tags)
        => await Task.Run(() => ReadGroup(tags));

    /// <summary>
    /// Giải mã dữ liệu từ buffer sang kiểu C#
    /// </summary>
    private static object ReadValue(byte[] b, int i, PlcTag t) => t.DataType switch
    {
        PlcDataType.Bool => (b[i] & (1 << t.Bit)) != 0,
        PlcDataType.Byte or PlcDataType.USInt => b[i],
        PlcDataType.SInt => (sbyte)b[i],
        PlcDataType.Char => (char)b[i],
        PlcDataType.Word or PlcDataType.UInt => S7.GetWordAt(b, i),
        PlcDataType.Int => S7.GetIntAt(b, i),
        PlcDataType.DWord or PlcDataType.UDInt => S7.GetDWordAt(b, i),
        PlcDataType.DInt => S7.GetDIntAt(b, i),
        PlcDataType.Real => S7.GetRealAt(b, i), // Trả về float
        PlcDataType.LWord or PlcDataType.ULInt => S7.GetULIntAt(b, i),
        PlcDataType.LInt => S7.GetLIntAt(b, i),
        PlcDataType.LReal => S7.GetLRealAt(b, i), // Trả về double
        PlcDataType.String => S7.GetStringAt(b, i),
        _ => null!
    };
}
