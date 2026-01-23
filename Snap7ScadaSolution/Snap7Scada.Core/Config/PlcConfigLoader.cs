using Newtonsoft.Json;
using Snap7ClientLib.Tags;

namespace Snap7ClientLib.Config;

public static class PlcTagConfigLoader
{
    public static List<PlcConfig> Load(string file)
    {

        string json = File.ReadAllText(file);

        var list = JsonConvert.DeserializeObject<List<PlcConfig>>(json);
        return list ?? new List<PlcConfig>();
    }
}


public class PlcConfigRoot
{
    public List<PlcConfig> Plcs { get; set; } = new();
}

public class PlcConfig
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Rack { get; set; }
    public int Slot { get; set; }
    public List<TagConfig> Tags { get; set; } = new();
}

public class TagConfig
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public PlcDataType DataType { get; set; }
    public int Length { get; set; }
    public double Deadband { get; set; } = 0;

    /// <summary>
    /// Cộng vào giá trị của tag rồi trả về.
    /// </summary>
    public double OffsetValue { get; set; } = 0;

    /// <summary>
    /// Nhân vào giá trị của tag rồi trả về.
    /// </summary>
    public double GainRate { get; set;} = 1;

    /// <summary>
    /// Hiển bao nhiêu số lẻ sau dấu chấm.
    /// </summary>
    public int NumDecimal { get; set; } = 3;
}