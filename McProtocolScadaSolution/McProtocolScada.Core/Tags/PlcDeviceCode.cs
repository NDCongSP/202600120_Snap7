namespace McProtocolClientLib.Tags;

/// <summary>
/// Phân loại device code Mitsubishi:
/// - WordDevice: D, W, R, ZR, SD, SW (mỗi đơn vị = 1 word = 16 bit)
/// - BitDevice : M, X, Y, B, F, L, S, SM, SB (mỗi đơn vị = 1 bit)
/// </summary>
public enum PlcDeviceKind
{
    Unknown,
    WordDevice,
    BitDevice
}

/// <summary>
/// Bảng meta-data các device Mitsubishi phổ biến.
/// Dùng cho parser & validator.
/// </summary>
internal static class PlcDeviceCode
{
    /// <summary>
    /// Trả về loại device + radix của offset.
    /// Mitsubishi quy ước: X, Y, B, SB, DX, DY là HEX, còn lại là DECIMAL.
    /// </summary>
    public static (PlcDeviceKind kind, int radix) Resolve(string deviceCode)
    {
        switch (deviceCode.ToUpperInvariant())
        {
            // Word devices (decimal offset)
            case "D":   // Data register
            case "W":   // Link register (offset hex theo chuẩn nhưng HslCommunication parse đúng)
            case "R":   // File register
            case "ZR":  // Extended file register
            case "SD":  // Special data register
            case "SW":  // Special link register
                return (PlcDeviceKind.WordDevice, 10);

            // Bit devices (decimal offset)
            case "M":   // Internal relay
            case "L":   // Latch relay
            case "F":   // Annunciator
            case "S":   // Step relay
            case "SM":  // Special internal relay
                return (PlcDeviceKind.BitDevice, 10);

            // Bit devices (HEX offset trong PLC Mitsubishi Q/L/iQ-R)
            case "X":   // Input
            case "Y":   // Output
            case "B":   // Link relay
            case "SB":  // Special link relay
                return (PlcDeviceKind.BitDevice, 16);

            default:
                return (PlcDeviceKind.Unknown, 10);
        }
    }
}
