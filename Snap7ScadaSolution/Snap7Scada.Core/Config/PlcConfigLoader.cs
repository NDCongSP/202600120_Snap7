using Snap7ClientLib.Tags;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snap7ClientLib.Config;

public static class PlcTagConfigLoader
{
    public static List<PlcConfig> Load(string file)
    {

        string json = File.ReadAllText(file);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var list = JsonSerializer.Deserialize<List<PlcConfig>>(json, options);
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
}