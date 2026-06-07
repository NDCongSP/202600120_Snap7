using System.Text.RegularExpressions;

namespace McProtocolClientLib.Tags;

/// <summary>
/// Parse địa chỉ PLC Mitsubishi (MC Protocol) sang DeviceCode / Offset / Bit / Size.
///
/// Hỗ trợ:
///   D100         – word device, decimal offset
///   D100.5       – bit thứ 5 trong word D100 (chỉ dùng cho Bool)
///   M50          – bit device, decimal offset
///   X1A / Y20    – bit device, HEX offset (Q/L/iQ-R)
///   ZR1000       – extended file register
///   W30 / R100   – link/file register
///   SD0 / SW10   – special register
///   B1F0 / SB100 – link bit (HEX)
/// </summary>
internal static class PlcAddressParser
{
    /// <summary>
    /// Phân tích Address và gán vào PlcTag.
    /// </summary>
    public static void Parse(PlcTag tag)
    {
        if (string.IsNullOrWhiteSpace(tag.Address))
            throw new FormatException("Empty PLC address");

        // Regex: device code (1-2 chữ cái) + offset (hex/dec) + bit option (.x)
        var match = Regex.Match(
            tag.Address.Trim().ToUpperInvariant(),
            @"^([A-Z]{1,2})([0-9A-F]+)(?:\.([0-9A-F]+))?$");

        if (!match.Success)
            throw new FormatException($"Invalid Mitsubishi address: {tag.Address}");

        string  deviceCode = match.Groups[1].Value;
        string  offsetText = match.Groups[2].Value;
        string? bitText    = match.Groups[3].Success ? match.Groups[3].Value : null;

        var (kind, radix) = PlcDeviceCode.Resolve(deviceCode);
        if (kind == PlcDeviceKind.Unknown)
            throw new FormatException($"Unsupported Mitsubishi device: {deviceCode}");

        tag.DeviceCode = deviceCode;
        tag.DeviceKind = kind;
        tag.OffsetRadix = radix;
        tag.Offset = Convert.ToInt32(offsetText, radix);

        // Bit option (D100.5)
        if (bitText != null)
        {
            // Bit phải nằm trên word device + DataType=Bool
            if (kind != PlcDeviceKind.WordDevice)
                throw new FormatException(
                    $"Bit suffix only allowed on word devices: {tag.Address}");

            tag.Bit = Convert.ToInt32(bitText, radix);
            if (tag.Bit < 0 || tag.Bit > 15)
                throw new FormatException(
                    $"Bit index out of range (0..15): {tag.Address}");

            tag.IsBitInWord = true;
        }
        else
        {
            tag.Bit = 0;
            tag.IsBitInWord = false;
        }

        // Tính số WORD cần đọc/ghi (đơn vị word, không phải byte)
        // (Bool trên bit device chỉ cần 1 bit, ta vẫn quy đổi sang 1 đơn vị)
        tag.WordSize = ResolveWordSize(tag);
    }

    /// <summary>
    /// Quy đổi DataType sang số word cần đọc trên Mitsubishi.
    /// </summary>
    private static int ResolveWordSize(PlcTag tag)
    {
        return tag.DataType switch
        {
            // Bit device hoặc bit-in-word: tính như 1 word
            PlcDataType.Bool => 1,

            // 1 byte – Mitsubishi vẫn đọc nguyên 1 word (lower byte)
            PlcDataType.Byte or PlcDataType.SInt
                or PlcDataType.USInt or PlcDataType.Char => 1,

            // 16-bit
            PlcDataType.Word or PlcDataType.Int
                or PlcDataType.UInt => 1,

            // 32-bit (2 word)
            PlcDataType.DWord or PlcDataType.DInt
                or PlcDataType.UDInt or PlcDataType.Real => 2,

            // 64-bit (4 word)
            PlcDataType.LWord or PlcDataType.LInt
                or PlcDataType.ULInt or PlcDataType.LReal => 4,

            // String Mitsubishi: 2 ký tự / 1 word, không có header → ceil(len/2)
            PlcDataType.String => (tag.StringLength + 1) / 2,

            _ => throw new NotSupportedException()
        };
    }
}
