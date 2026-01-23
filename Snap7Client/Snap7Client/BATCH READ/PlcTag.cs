namespace Snap7ClientLib;

/// <summary>
/// Đại diện cho 1 tag PLC giống SCADA (Kepware, WinCC...)
/// </summary>
public class PlcTag
{
    /// <summary>
    /// Tên tag (dùng cho UI / Subscription)
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Địa chỉ PLC dạng: DB1.DBW0, DB1.DBD22, DB1.DBX2.0
    /// </summary>
    public string Address { get; set; } = "";

    /// <summary>
    /// Kiểu dữ liệu PLC
    /// </summary>
    public PlcDataType DataType { get; set; }

    /// <summary>
    /// Chiều dài string (chỉ dùng cho PlcDataType.String)
    /// </summary>
    public int StringLength { get; set; } = 0;

    // ===== Thông tin sau khi parse address =====

    internal int Db;       // DB number
    internal int Offset;   // Byte offset trong DB
    internal int Bit;      // Bit offset (chỉ cho Bool)
    internal int Size;     // Số byte cần đọc/ghi

    /// <summary>
    /// Giá trị đọc được / sẽ ghi xuống PLC
    /// </summary>
    public object? Value { get; set; }
}