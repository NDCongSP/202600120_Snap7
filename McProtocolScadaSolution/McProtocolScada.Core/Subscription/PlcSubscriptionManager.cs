using System.Collections.Concurrent;
using McProtocolClientLib.Core;
using McProtocolClientLib.IO;
using McProtocolClientLib.Tags;

namespace McProtocolClientLib.Subscription
{
    /// <summary>
    /// Quản lý subscription tag (polling + event OnValueChanged) – không overlap, hỗ trợ deadband.
    /// Tương thích .NET Standard 2.0 (không dùng PeriodicTimer).
    /// </summary>
    public sealed class PlcSubscriptionManager : IDisposable
    {
        private readonly PlcGroupReader _reader;

        // Cache giá trị trước đó theo tên tag
        private readonly ConcurrentDictionary<string, object?> _cache = new();

        // Vòng đời polling
        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        // Cờ chống re-entrancy khi handler có write-back
        private int _reentrancyGuard = 0;

        /// <summary>Sự kiện khi GIÁ TRỊ tag THỰC SỰ thay đổi (sau so sánh kiểu mạnh + deadband).</summary>
        public event Action<PlcTag>? OnValueChanged;

        public PlcSubscriptionManager(PlcGroupReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            _reader = reader;
        }

        /// <summary>
        /// Bắt đầu subscribe các tag theo chu kỳ (ms). Nếu đang chạy sẽ dừng phiên cũ và chạy lại.
        /// </summary>
        public void Subscribe(IEnumerable<PlcTag> tags, int intervalMs = 200)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs));

            Stop();

            var tagList = tags.Where(t => t != null).Distinct().ToList();
            if (tagList.Count == 0) return;

            _cache.Clear();

            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoopAsync(tagList, intervalMs, _cts.Token));
        }

        private async Task RunLoopAsync(IReadOnlyList<PlcTag> tags, int intervalMs, CancellationToken ct)
        {
            // Lần đầu: seed cache KHÔNG raise
            await SafeReadAndProcessAsync(tags, raiseChanges: false, ct).ConfigureAwait(false);

            while (!ct.IsCancellationRequested)
            {
                await SafeReadAndProcessAsync(tags, raiseChanges: true, ct).ConfigureAwait(false);

                try
                {
                    await Task.Delay(intervalMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // bình thường khi Stop()
                }
            }
        }

        /// <summary>Đọc nhóm & xử lý thay đổi; có thể chọn raise hay chỉ seed.</summary>
        private async Task SafeReadAndProcessAsync(IReadOnlyList<PlcTag> tags, bool raiseChanges, CancellationToken ct)
        {
            try
            {
                await _reader.ReadGroupAsync(tags).ConfigureAwait(false);
                var readOk = true;

                foreach (var tag in tags)
                {
                    tag.Status = readOk ? PlcConnectionState.Connected : PlcConnectionState.Disconnected;

                    var newVal = tag.NewValue;

                    // Seed cache lần đầu
                    if (!_cache.TryGetValue(tag.Name, out var oldVal))
                    {
                        _cache[tag.Name] = newVal;
                        tag.LastValue = newVal;
                        continue;
                    }

                    if (IsValueChanged(oldVal, newVal, tag))
                    {
                        tag.LastValue = oldVal;
                        _cache[tag.Name] = newVal;

                        if (raiseChanges)
                        {
                            if (Interlocked.CompareExchange(ref _reentrancyGuard, 1, 0) == 0)
                            {
                                try
                                {
                                    tag.RaiseValueChanged();
                                    OnValueChanged?.Invoke(tag);
                                }
                                finally
                                {
                                    Interlocked.Exchange(ref _reentrancyGuard, 0);
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // bình thường khi Stop()
            }
            catch (Exception)
            {
                foreach (var tag in tags)
                    tag.Status = PlcConnectionState.Disconnected;
            }
        }

        /// <summary>So sánh giá trị cũ/mới, có deadband cho số thực.</summary>
        private static bool IsValueChanged(object? oldVal, object? newVal, PlcTag tag)
        {
            if (oldVal == null || newVal == null) return true;

            switch (tag.DataType)
            {
                case PlcDataType.Bool:
                    return !Equals(Convert.ToBoolean(oldVal), Convert.ToBoolean(newVal));

                case PlcDataType.Int:
                case PlcDataType.DInt:
                case PlcDataType.LInt:
                case PlcDataType.UInt:
                case PlcDataType.UDInt:
                case PlcDataType.ULInt:
                case PlcDataType.Byte:
                case PlcDataType.Word:
                case PlcDataType.DWord:
                case PlcDataType.LWord:
                    return !Equals(Convert.ToInt64(oldVal), Convert.ToInt64(newVal));

                case PlcDataType.Real:
                case PlcDataType.LReal:
                    {
                        double defaultBand = tag.DataType == PlcDataType.Real ? 1e-4 : 1e-6;
                        double band = (tag.Deadband > 0) ? tag.Deadband : defaultBand;

                        double o = Convert.ToDouble(oldVal);
                        double n = Convert.ToDouble(newVal);
                        return Math.Abs(o - n) > band;
                    }

                default:
                    return !Equals(oldVal, newVal);
            }
        }

        /// <summary>Dừng subscription hiện tại.</summary>
        public void Stop()
        {
            _cts?.Cancel();

            try { _loopTask?.Wait(); } catch { /* ignore */ }
            _loopTask = null;

            _cts?.Dispose();
            _cts = null;

            _cache.Clear();
        }

        public void Dispose() => Stop();
    }
}
