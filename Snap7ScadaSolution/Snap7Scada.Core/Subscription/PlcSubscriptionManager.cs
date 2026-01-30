using Snap7ClientLib.Core;

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
        // Trong phương thức Subscribe của PlcSubscriptionManager
        _timer = new Timer(async _ =>
        {
            // Đọc dữ liệu từ PLC cho toàn bộ nhóm tag
            await _reader.ReadGroupAsync(tags);

            foreach (var tag in tags)
            {
                // 1. Cập nhật trạng thái kết nối dựa trên kết quả đọc
                // Giả sử nếu ReadGroup thành công thì Status là Connected
                tag.Status = PlcConnectionState.Connected;

                // 2. Kiểm tra giá trị cũ trong cache
                if (!_cache.TryGetValue(tag.Name, out var oldVal))
                {
                    // Nếu lần đầu đọc, lưu vào cache và gán LastValue
                    _cache[tag.Name] = tag.NewValue;
                    tag.LastValue = tag.NewValue;
                    continue;
                }

                // 3. So sánh giá trị mới và cũ
                if (IsValueChanged(oldVal, tag.NewValue, tag.DataType))
                {
                    // Lưu giá trị cũ vào LastValue trước khi cập nhật mới
                    tag.LastValue = oldVal;

                    // Cập nhật giá trị mới vào cache
                    _cache[tag.Name] = tag.NewValue;

                    // 4. Kích hoạt sự kiện riêng của chính Tag đó
                    tag.RaiseValueChanged();

                    // Vẫn kích hoạt sự kiện chung của Manager nếu cần
                    OnValueChanged?.Invoke(tag);
                }
            }
        }, null, 0, intervalMs);
    }

    public async Task SubscribeAsync(IEnumerable<PlcTag> tags, int intervalMs = 200, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            // 1. Chờ việc đọc hoàn tất
            await _reader.ReadGroupAsync(tags);

            foreach (var tag in tags)
            {
                // 1. Cập nhật trạng thái kết nối dựa trên kết quả đọc
                // Giả sử nếu ReadGroup thành công thì Status là Connected
                tag.Status = PlcConnectionState.Connected;

                // 2. Kiểm tra giá trị cũ trong cache
                if (!_cache.TryGetValue(tag.Name, out var oldVal))
                {
                    // Nếu lần đầu đọc, lưu vào cache và gán LastValue
                    _cache[tag.Name] = tag.NewValue;
                    tag.LastValue = tag.NewValue;
                    continue;
                }

                // 3. So sánh giá trị mới và cũ
                if (IsValueChanged(oldVal, tag.NewValue, tag.DataType))
                {
                    // Lưu giá trị cũ vào LastValue trước khi cập nhật mới
                    tag.LastValue = oldVal;

                    // Cập nhật giá trị mới vào cache
                    _cache[tag.Name] = tag.NewValue;

                    // 4. Kích hoạt sự kiện riêng của chính Tag đó
                    tag.RaiseValueChanged();

                    // Vẫn kích hoạt sự kiện chung của Manager nếu cần
                    OnValueChanged?.Invoke(tag);
                }
            }

            // 2. Nghỉ đúng khoảng interval rồi mới lặp lại
            await Task.Delay(intervalMs, ct);
        }
    }

    /// <summary>
    /// So sánh giá trị cũ/mới (có deadband cho float)
    /// </summary>
    private static bool IsValueChanged(object? oldVal, object? newVal, PlcDataType type)
    {
        if (oldVal == null || newVal == null) return true;

        return type switch
        {
            // Sử dụng Convert.ToDouble để an toàn cho cả float và double
            PlcDataType.Real or PlcDataType.LReal =>
                Math.Abs(Convert.ToDouble(oldVal) - Convert.ToDouble(newVal)) > 0.0001,

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
