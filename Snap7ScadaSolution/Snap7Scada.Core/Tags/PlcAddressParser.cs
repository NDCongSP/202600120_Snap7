using System.Text.RegularExpressions;

namespace Snap7ClientLib.Tags;

/// <summary>
/// Parse địa chỉ PLC dạng SCADA thành DB / Offset / Bit / Size
/// </summary>
internal static class PlcAddressParser
{
    /// <summary>
    /// Phân tích Address và gán vào PlcTag
    /// </summary>
    public static void Parse(PlcTag tag)
    {
        // Regex bắt:
        // DB1.DBX2.0
        // DB1.DBW0
        // DB1.DBD22
        var match = Regex.Match(
            tag.Address,
            @"DB(\d+)\.DB([BXWD])(\d+)(?:\.(\d))?"
        );

        if (!match.Success)
            throw new FormatException($"Invalid PLC address: {tag.Address}");

        // DB number
        tag.Db = int.Parse(match.Groups[1].Value);

        // Byte offset
        tag.Offset = int.Parse(match.Groups[3].Value);

        // Bit offset (nếu có)
        tag.Bit = match.Groups[4].Success
            ? int.Parse(match.Groups[4].Value)
            : 0;

        // Xác định số byte cần đọc dựa vào kiểu dữ liệu
        tag.Size = tag.DataType switch
        {
            PlcDataType.Bool => 1, // đọc 1 byte rồi mask bit

            PlcDataType.Byte or PlcDataType.SInt
                or PlcDataType.USInt or PlcDataType.Char => 1,

            PlcDataType.Word or PlcDataType.Int
                or PlcDataType.UInt => 2,

            PlcDataType.DWord or PlcDataType.DInt
                or PlcDataType.UDInt or PlcDataType.Real => 4,

            PlcDataType.LWord or PlcDataType.LInt
                or PlcDataType.ULInt or PlcDataType.LReal => 8,

            // String Siemens = MaxLen + CurLen + Data
            PlcDataType.String => tag.StringLength + 2,

            _ => throw new NotSupportedException()
        };
    }
}
