using Sharp7;
using System.Net;

namespace Snap7ClientLib.Core
{

    /// <summary>
    /// Client kết nối PLC Siemens S7 (Snap7)
    /// Hỗ trợ sync/async + auto reconnect
    /// </summary>
    public class PlcClient : IDisposable
    {
        /// <summary>
        /// Biến dùng để ngăn chặn hiện tượng "độc chiếm tài nguyên" (Resource Contention).
        /// Để tránh việc Đọc và Ghi "đâm" vào nhau gây ra trễ tích lũy, bạn nên thêm một cơ chế khóa đơn giản trong lớp PlcClient hoặc các lớp Reader/Writer.
        /// Khóa dùng chung cho cả Read và Write.
        /// </summary>
        public readonly object SyncLock = new object(); // Khóa dùng chung cho cả Read và Write

        private readonly S7Client _client = new();
        private  string _host;
        private readonly int _rack;
        private readonly int _slot;

        private CancellationTokenSource? _watchdogCts;
        private readonly SemaphoreSlim _connectionLock = new(1, 1); // Đảm bảo không kết nối chồng chéo

        public PlcConnectionState State { get; private set; } =
            PlcConnectionState.Disconnected;

        public event Action<PlcConnectionState>? StateChanged;

        public string Host
        {
            get=>_host;
            set
            {
                if (State == PlcConnectionState.Connected)
                    throw new InvalidOperationException("Không thể thay đổi Host khi đang kết nối.");
                _host = value;
            }
        }


        public PlcClient(string host, int rack = 0, int slot = 1)
        {
            _host = host;
            _rack = rack;
            _slot = slot;
        }

        /// <summary>
        /// Kết nối PLC (sync)
        /// </summary>
        public bool Connect()
        {
            try
            {
                SetState(PlcConnectionState.Connecting);

                string ip = ResolveHost(_host);
                int res = _client.ConnectTo(ip, _rack, _slot);

                SetState(res == 0
                    ? PlcConnectionState.Connected
                    : PlcConnectionState.Error);

                return res == 0;
            }
            catch
            {
                SetState(PlcConnectionState.Error);
                return false;
            }
        }

        /// <summary>
        /// Kết nối PLC (async)
        /// </summary>
        public Task<bool> ConnectAsync()
            => Task.Run(Connect);

        public void Disconnect()
        {
            _client.Disconnect();
            SetState(PlcConnectionState.Disconnected);
        }

        /// <summary>
        /// Tự reconnect khi mất kết nối
        /// </summary>
        public bool EnsureConnected()
        {
            if (_client.Connected)
                return true;

            SetState(PlcConnectionState.Reconnecting);
            return Connect();
        }

        public Task<bool> EnsureConnectedAsync()
            => Task.Run(EnsureConnected);

        public void StartWatchdog(int intervalMs = 2000)
        {
            // Dừng watchdog cũ nếu đang chạy
            StopWatchdog();

            _watchdogCts = new CancellationTokenSource();
            var token = _watchdogCts.Token;

            // Chạy một Task ngầm không chặn luồng chính
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (!_client.Connected)
                        {
                            // Sử dụng Lock để tránh nhiều luồng cùng gọi Connect một lúc
                            await _connectionLock.WaitAsync(token);
                            try
                            {
                                // Kiểm tra lại sau khi lấy được lock
                                if (!_client.Connected)
                                {
                                    SetState(PlcConnectionState.Reconnecting);
                                    await ConnectAsync();
                                }
                            }
                            finally
                            {
                                _connectionLock.Release();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        SetState(PlcConnectionState.Error);
                    }

                    // Nghỉ đúng khoảng thời gian rồi mới kiểm tra tiếp
                    await Task.Delay(intervalMs, token);
                }
            }, token);
        }

        public void StopWatchdog()
        {
            _watchdogCts?.Cancel();
            _watchdogCts?.Dispose();
            _watchdogCts = null;
        }

        internal S7Client Client => _client;

        private void SetState(PlcConnectionState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private static string ResolveHost(string host)
        {
            if (IPAddress.TryParse(host, out _))
                return host;

            return Dns.GetHostAddresses(host)[0].ToString();
        }

        public void Dispose()
        {
            StopWatchdog();
            Disconnect();
        }
    }
}
