
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Snap7ClientLib.Core;

namespace Snap7ClientLib.Tags
{
    /// <summary>
    /// Quản lý subscription tag (polling + event OnValueChanged) – không overlap, hỗ trợ deadband.
    /// Tương thích .NET Framework/.NET Standard (không dùng PeriodicTimer/ThrowIfNull).
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

        /// <summary>Bắt đầu subscribe các tag theo chu kỳ (ms). Nếu đang chạy sẽ dừng phiên cũ và chạy lại.</summary>
        public void Subscribe(IEnumerable<PlcTag> tags, int intervalMs = 200)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs));

            // Dừng phiên cũ (nếu có)
            Stop();

            // "Đóng băng" danh sách để tránh bị sửa bên ngoài / enumerate nhiều lần
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
                // PlcGroupReader hiện tại của anh không nhận CancellationToken
                await _reader.ReadGroupAsync(tags).ConfigureAwait(false);
                var readOk = true;

                foreach (var tag in tags)
                {
                    // Trạng thái cơ bản
                    tag.Status = readOk ? PlcConnectionState.Connected : PlcConnectionState.Disconnected;

                    var newVal = tag.NewValue;

                    // Seed cache lần đầu
                    if (!_cache.TryGetValue(tag.Name, out var oldVal))
                    {
                        _cache[tag.Name] = newVal;
                        tag.LastValue = newVal;
                        continue;
                    }

                    //bool changed = IsValueChanged(oldVal, newVal, tag);
                    //System.Diagnostics.Debug.WriteLine(
                    //    $"[{tag.Name}] old={(oldVal ?? "null")} new={(newVal ?? "null")} type={tag.DataType} changed={changed}");

                    // So sánh kiểu mạnh + deadband
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
                                    tag.RaiseValueChanged();     // event riêng của Tag (nếu có)
                                    OnValueChanged?.Invoke(tag); // event Manager
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
                // Lỗi đọc: đánh dấu Disconnected; có thể log nếu cần
                foreach (var tag in tags)
                    tag.Status = PlcConnectionState.Disconnected;
                // TODO: Logger.LogError(ex, "Read/process failed");
            }
        }

        /// <summary>So sánh giá trị cũ/mới, có deadband cho số thực. Hỗ trợ deadband per-tag nếu có thuộc tính.</summary>
        private static bool IsValueChanged(object? oldVal, object? newVal, PlcTag tag)
        {
            // Một trong hai null → xem như thay đổi
            if (oldVal == null || newVal == null) return true;

            switch (tag.DataType)
            {
                case PlcDataType.Bool:
                    return !Equals(Convert.ToBoolean(oldVal), Convert.ToBoolean(newVal));

                case PlcDataType.Int:     // Int16
                case PlcDataType.DInt:    // Int32
                case PlcDataType.LInt:    // Int64
                case PlcDataType.UInt:
                case PlcDataType.UDInt:
                case PlcDataType.ULInt:
                case PlcDataType.Byte:
                case PlcDataType.Word:
                case PlcDataType.DWord:
                case PlcDataType.LWord:
                    return !Equals(Convert.ToInt64(oldVal), Convert.ToInt64(newVal));

                case PlcDataType.Real:    // float
                case PlcDataType.LReal:   // double
                    {
                        // Nếu PlcTag có property Deadband (>0) thì dùng; không thì mặc định.
                        double defaultBand = tag.DataType == PlcDataType.Real ? 1e-4 : 1e-6;
                        double band = (tag.Deadband > 0) ? tag.Deadband : defaultBand;

                        double o = Convert.ToDouble(oldVal);
                        double n = Convert.ToDouble(newVal);
                        return Math.Abs(o - n) > band;
                    }

                default:
                    // Fallback cho string/khác
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