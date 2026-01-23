using Snap7ClientLib.Config;
using Snap7ClientLib.Tags;

namespace Snap7ClientLib.Core;

/// <summary>
/// Quản lý nhiều PLC cùng lúc (SCADA Core)
/// </summary>
public class PlcManager
{
    private readonly Dictionary<string, PlcRuntime> _plcs = new();

    public void LoadFromConfig(string jsonFile)
    {
        var configs = PlcTagConfigLoader.Load(jsonFile);

        foreach (var cfg in configs)
        {
            var plc = new PlcClient(cfg.Host, cfg.Rack, cfg.Slot);
            var reader = new PlcGroupReader(plc);
            var writer = new PlcGroupWriter(plc);

            _plcs[cfg.Name] = new PlcRuntime
            {
                Client = plc,
                Reader = reader,
                Writer = writer,
                Tags = cfg.Tags.Select(t => new PlcTag
                {
                    Name = t.Name,
                    Address = t.Address,
                    DataType = t.DataType,
                    StringLength = t.Length
                }).ToList()
            };
        }
    }

    public PlcRuntime GetPlc(string name) => _plcs[name];
}

public class PlcRuntime
{
    public PlcClient Client { get; set; } = null!;
    public PlcGroupReader Reader { get; set; } = null!;
    public PlcGroupWriter Writer { get; set; } = null!;
    public List<PlcTag> Tags { get; set; } = new();
}