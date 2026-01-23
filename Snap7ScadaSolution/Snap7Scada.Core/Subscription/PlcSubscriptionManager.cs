namespace Snap7ClientLib.Tags;

/// <summary>
/// Quản lý subscription tag (polling + event OnValueChanged)
/// </summary>
public class PlcSubscriptionManager
{
    private readonly PlcGroupReader _reader;
    private readonly Dictionary<string, object?> _cache = new();
    private Timer? _timer;

    /// <summary>
    /// Event phát sinh khi giá trị tag thay đổi
    /// </summary>
    public event Action<PlcTag>? OnValueChanged;

    public PlcSubscriptionManager(PlcGroupReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Bắt đầu subscribe tag
    /// </summary>
    /// <param name="intervalMs">Chu kỳ polling (ms)</param>
    public void Subscribe(IEnumerable<PlcTag> tags, int intervalMs = 200)
    {
        _timer = new Timer(async _ =>
        {
            await _reader.ReadGroupAsync(tags);

            foreach (var tag in tags)
            {
                if (!_cache.TryGetValue(tag.Name, out var old))
                {
                    _cache[tag.Name] = tag.Value;
                    continue;
                }

                // DEADBAND cho Real / LReal
                if (IsValueChanged(old, tag.Value, tag.DataType))
                {
                    _cache[tag.Name] = tag.Value;
                    OnValueChanged?.Invoke(tag);
                }
            }

        }, null, 0, intervalMs);
    }

    /// <summary>
    /// So sánh giá trị cũ/mới (có deadband cho float)
    /// </summary>
    private static bool IsValueChanged(object? oldVal, object? newVal, PlcDataType type)
    {
        if (oldVal == null || newVal == null)
            return true;

        return type switch
        {
            PlcDataType.Real =>
                Math.Abs((float)oldVal - (float)newVal) > 0.001f,

            PlcDataType.LReal =>
                Math.Abs((double)oldVal - (double)newVal) > 0.0001,

            _ => !Equals(oldVal, newVal)
        };
    }

    /// <summary>
    /// Dừng subscription
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _cache.Clear();
    }
}
