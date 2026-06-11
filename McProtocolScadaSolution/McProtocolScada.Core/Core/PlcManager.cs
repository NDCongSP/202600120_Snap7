using McProtocolClientLib.Config;
using McProtocolClientLib.IO;
using McProtocolClientLib.Tags;

namespace McProtocolClientLib.Core;

/// <summary>
/// Quản lý nhiều PLC Mitsubishi cùng lúc (SCADA Core).
/// </summary>
public class PlcManager
{
    private readonly Dictionary<string, PlcRuntime> _plcs = new();

    public void LoadFromConfig(string jsonFile)
    {
        var configs = PlcTagConfigLoader.Load(jsonFile);

        foreach (var cfg in configs)
        {
            var plc = new PlcClient(cfg.Host, cfg.Port, cfg.FrameType, cfg.Network, cfg.Station);
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
                    StringLength = t.Length,
                    OffsetValue = t.OffsetValue,
                    GainRate = t.GainRate,
                    NumDecimal = t.NumDecimal,
                    Deadband = t.Deadband
                }).ToList()
            };
        }
    }

    public PlcRuntime GetPlc(string name) => _plcs[name];

    public IEnumerable<string> GetAllPlcNames() => _plcs.Keys;
}

public class PlcRuntime
{
    public PlcClient Client { get; set; } = null!;
    public PlcGroupReader Reader { get; set; } = null!;
    public PlcGroupWriter Writer { get; set; } = null!;
    public List<PlcTag> Tags { get; set; } = new();
}
