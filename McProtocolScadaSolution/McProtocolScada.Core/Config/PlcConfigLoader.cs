using McProtocolClientLib.Core;
using McProtocolClientLib.Tags;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace McProtocolClientLib.Config;

/// <summary>
/// Đọc danh sách PLC + Tag từ file JSON.
/// </summary>
public static class PlcTagConfigLoader
{
    public static List<PlcConfig> Load(string file)
    {
        string json = File.ReadAllText(file);

        var settings = new JsonSerializerSettings();
        // Cho phép đọc enum dạng chuỗi (DataType: "Real", FrameType: "QnA3E_Binary")
        settings.Converters.Add(new StringEnumConverter());

        var list = JsonConvert.DeserializeObject<List<PlcConfig>>(json, settings);
        return list ?? new List<PlcConfig>();
    }
}

/// <summary>
/// Cấu hình 1 PLC Mitsubishi.
/// </summary>
public class PlcConfig
{
    /// <summary>Tên định danh (ví dụ "PLC_1", "Line1_Q03UDV").</summary>
    public string Name { get; set; } = "";

    /// <summary>IP hoặc hostname.</summary>
    public string Host { get; set; } = "";

    /// <summary>Port MC Protocol – mặc định 6000 (QnA-3E binary).</summary>
    public int Port { get; set; } = 6000;

    /// <summary>Loại frame: QnA3E_Binary / QnA3E_Ascii / A1E_Binary / A1E_Ascii / iQR_Binary.</summary>
    public McFrameType FrameType { get; set; } = McFrameType.QnA3E_Binary;

    /// <summary>Network number (mặc định 0).</summary>
    public byte Network { get; set; } = 0;

    /// <summary>PC station number (mặc định 0xFF = local PLC).</summary>
    public byte Station { get; set; } = 0xFF;

    public List<TagConfig> Tags { get; set; } = new();
}

/// <summary>
/// Cấu hình 1 tag.
/// </summary>
public class TagConfig
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public PlcDataType DataType { get; set; }

    /// <summary>Số ký tự (chỉ dùng cho String).</summary>
    public int Length { get; set; }

    /// <summary>
    /// Với tag số (ví dụ ScaleValue), giá trị có thể dao động vài đơn vị nhỏ dù thực tế "không đổi".
    /// Hãy áp deadband (ngưỡng tối thiểu) trước khi coi là "đổi".
    /// </summary>
    public double Deadband { get; set; } = 0;

    /// <summary>Cộng thêm vào raw value.</summary>
    public double OffsetValue { get; set; } = 0;

    /// <summary>Nhân raw value (Gain).</summary>
    public double GainRate { get; set; } = 1;

    /// <summary>Số lẻ sau dấu chấm khi hiển thị.</summary>
    public int NumDecimal { get; set; } = 3;
}
