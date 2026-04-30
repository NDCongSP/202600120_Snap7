using McProtocolClientLib.Core;

namespace McProtocolClientLib.Tags;

/// <summary>
/// Đại diện cho 1 tag PLC giống SCADA (Kepware, GX-Works, MX OPC...) –
/// dùng cho Mitsubishi MC Protocol.
/// </summary>
public class PlcTag
{
    /// <summary>
    /// Tên tag (dùng cho UI / Subscription)
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Địa chỉ PLC dạng Mitsubishi: D100, D100.5, M50, X1A, Y20, ZR1000, W30, B1F0, R100...
    /// </summary>
    public string Address { get; set; } = "";

    /// <summary>
    /// Kiểu dữ liệu PLC.
    /// </summary>
    public PlcDataType DataType { get; set; }

    /// <summary>
    /// Chiều dài string (chỉ dùng cho PlcDataType.String) – tính theo ký tự ASCII.
    /// </summary>
    public int StringLength { get; set; } = 0;

    // ===== Thông tin sau khi parse address =====

    internal string DeviceCode = "";        // D, M, X, Y, W, B, R, ZR, SD, SW, SM, SB, L, F, S
    internal PlcDeviceKind DeviceKind;      // WordDevice / BitDevice
    internal int OffsetRadix = 10;          // 10 (DEC) hoặc 16 (HEX)
    internal int Offset;                    // Offset trong device (đã chuyển về thập phân)
    internal int Bit;                       // Bit offset (D100.5 hoặc bool device)
    internal bool IsBitInWord;              // true nếu là D100.5 / W10.3 ...
    internal int WordSize;                  // Số word cần đọc/ghi (đơn vị word)

    /// <summary>
    /// Trả về địa chỉ HslCommunication (đã chuyển về thập phân để truyền vào driver).
    /// HslCommunication tự xử lý radix chuẩn cho từng device, nên tốt nhất ta giữ nguyên
    /// chuỗi gốc, chỉ uppercase. Giúp tránh sai số khi convert ngược.
    /// </summary>
    internal string HslAddress => Address.Trim().ToUpperInvariant();

    /// <summary>
    /// Sự kiện riêng cho từng tag.
    /// </summary>
    public event Action<PlcTag>? ValueChanged;

    /// <summary>
    /// Kích hoạt sự kiện ValueChanged.
    /// </summary>
    public void RaiseValueChanged()
    {
        ValueChanged?.Invoke(this);
    }

    private object? _newValue;

    /// <summary>
    /// Giá trị đọc được / sẽ ghi xuống PLC.
    /// </summary>
    public object? NewValue
    {
        get => _newValue;
        set
        {
            LastValue = _newValue;
            _newValue = value;
        }
    }

    /// <summary>
    /// Giá trị ngay trước đó (để so sánh / hiển thị lịch sử gần nhất).
    /// </summary>
    public object? LastValue { get; set; }

    /// <summary>Cộng vào giá trị thô trước khi trả về.</summary>
    public double OffsetValue { get; set; } = 0;

    /// <summary>Nhân vào giá trị thô trước khi trả về.</summary>
    public double GainRate { get; set; } = 1;

    /// <summary>Số chữ số sau dấu chấm khi hiển thị / lưu.</summary>
    public int NumDecimal { get; set; } = 3;

    /// <summary>
    /// Với tag kiểu số (ví dụ ScaleValue), đọc từ PLC có thể dao động vài đơn vị rất nhỏ
    /// dù thực tế "không đổi". Hãy áp deadband (ngưỡng tối thiểu) trước khi coi là "đổi".
    /// </summary>
    public double Deadband { get; set; } = 0;

    /// <summary>
    /// Giá trị thô từ PLC (chưa qua scaling).
    /// </summary>
    public object? RawValue { get; set; }

    /// <summary>
    /// Áp công thức (raw * Gain) + Offset, làm tròn theo NumDecimal.
    /// </summary>
    public void ApplyScaling(object raw)
    {
        this.RawValue = raw;

        if (raw is not string && raw is not bool)
        {
            try
            {
                double rawDouble = Convert.ToDouble(raw);
                double scaledValue = (rawDouble * GainRate) + OffsetValue;
                this.NewValue = Math.Round(scaledValue, (int)NumDecimal);
            }
            catch
            {
                this.NewValue = raw;
            }
        }
        else
        {
            this.NewValue = raw;
        }

        this.Status = PlcConnectionState.Connected;
    }

    /// <summary>
    /// Trạng thái kết nối hiện tại của tag.
    /// </summary>
    public PlcConnectionState Status { get; set; } = PlcConnectionState.Disconnected;

    private static bool NearlyEqual(double a, double b, double deadband) =>
        Math.Abs(a - b) <= deadband;
}
