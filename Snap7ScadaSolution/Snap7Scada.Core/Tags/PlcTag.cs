using Snap7ClientLib.Core;

namespace Snap7ClientLib.Tags;

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

    // 1. Thêm LastValue để so sánh hoặc hiển thị lịch sử gần nhất 🔄
    public object? LastValue { get; set; }

    // ... các thuộc tính cơ bản
    public double OffsetValue { get; set; } = 0;
    public double GainRate { get; set; } = 1;
    public int NumDecimal { get; set; } = 3;

    /// <summary>
    /// Giá trị thô từ PLC
    /// </summary>
    public object? RawValue { get; set; }

    public void ApplyScaling(object raw)
    {
        // Cất giá trị thô để debug nếu cần
        this.RawValue = raw;

        //if (raw is float || raw is double || raw is int || raw is short || raw is ushort)
        if (raw is not string && raw is not bool)
        {
            try
            {
                double rawDouble = Convert.ToDouble(raw);

                // Công thức: (Giá trị thô * Gain) + Offset
                double scaledValue = (rawDouble * GainRate) + OffsetValue;

                // Làm tròn theo số chữ số thập phân cấu hình
                this.Value = Math.Round(scaledValue, (int)NumDecimal);
            }
            catch { this.Value = raw; } // Nếu lỗi thì trả về giá trị thô
        }
        else
        {
            // Đối với String hoặc Bool, không áp dụng scaling
            this.Value = raw;
        }

        // Đảm bảo Status luôn cập nhật là Connected khi có dữ liệu
        this.Status = PlcConnectionState.Connected;
    }

    // 2. Thêm Status để biết tag có đang kết nối tốt không ✅
    public PlcConnectionState Status { get; set; } = PlcConnectionState.Disconnected;

    /// <summary>
    /// Sự kiện riêng cho từng tag
    /// </summary>
    public event Action<PlcTag>? ValueChanged;

    /// <summary>
    /// Phương thức hỗ trợ để kích hoạt sự kiện
    /// </summary>
    public void RaiseValueChanged()
    {
        ValueChanged?.Invoke(this);
    }
}
