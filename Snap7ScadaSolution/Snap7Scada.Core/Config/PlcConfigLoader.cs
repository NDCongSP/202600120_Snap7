using Snap7ClientLib.Tags;
using System.Text.Json;

namespace Snap7ClientLib.Config;

public static class PlcTagConfigLoader
{
    public static List<PlcConfig> Load(string file)
    {
        string json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<List<PlcConfig>>(
            JsonDocument.Parse(json).RootElement.GetProperty("plcs").GetRawText()
        )!;
    }
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
    public PlcDataType Type { get; set; }
    public int Length { get; set; }
    public double Deadband { get; set; } = 0;
}