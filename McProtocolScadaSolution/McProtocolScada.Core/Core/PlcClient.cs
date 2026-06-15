using HslCommunication.Profinet.Melsec;
using McProtocolClientLib.Diagnostics;
using McProtocolClientLib.Tags;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace McProtocolClientLib.Core
{
    /// <summary>
    /// Loại MC Protocol: Mitsubishi hỗ trợ nhiều khung (frame) + định dạng.
    /// </summary>
    public enum McFrameType
    {
        /// <summary>QnA-3E Binary (Q/L/iQ-R series, mặc định, port 6000-6019)</summary>
        QnA3E_Binary,
        /// <summary>QnA-3E ASCII (port 6001 mặc định)</summary>
        QnA3E_Ascii,
        /// <summary>A1E Binary (A series cũ)</summary>
        A1E_Binary,
        /// <summary>A1E ASCII</summary>
        A1E_Ascii,
        /// <summary>iQ-R Binary (frame 4E)</summary>
        iQR_Binary,
    }

    /// <summary>
    /// Client kết nối PLC Mitsubishi qua MC Protocol (HslCommunication).
    /// Hỗ trợ sync/async + auto reconnect + watchdog.
    /// </summary>
    /// <remarks>
    /// File: McProtocolScada.Core/Core/PlcClient.cs
    /// Created: 2026-06-01 | Modified: 2026-06-15
    /// </remarks>
    public class PlcClient : IDisposable
    {
        /// <summary>Khóa dùng chung cho Read/Write — tránh concurrent HslComm calls.</summary>
        public readonly object SyncLock = new object();

        // Mutable — recreate mỗi lần Connect() để xóa HslComm internal error state
        // (HslCommunication "System authorization failed" không tự recover trên instance cũ)
        private object _client;

        private readonly string _host;
        private readonly int _port;
        private readonly McFrameType _frameType;
        private readonly byte _network;
        private readonly byte _station;
        private readonly string _id; // dùng cho log: "192.168.11.1:8000"

        private CancellationTokenSource? _watchdogCts;

        // Ngăn concurrent Connect() — chỉ 1 attempt tại 1 thời điểm
        private readonly SemaphoreSlim _connectGuard = new SemaphoreSlim(1, 1);

        // Tín hiệu để watchdog thức dậy ngay khi NotifyError() được gọi
        private readonly SemaphoreSlim _reconnectSignal = new SemaphoreSlim(0, 1);

        public PlcConnectionState State { get; private set; } = PlcConnectionState.Disconnected;
        public event Action<PlcConnectionState>? StateChanged;

        /// <summary>Host/IP của PLC (nguyên bản, không qua DNS).</summary>
        public string Host => _host;

        /// <summary>Port MC Protocol.</summary>
        public int Port => _port;

        public PlcClient(string host, int port = 6000,
                         McFrameType frameType = McFrameType.QnA3E_Binary,
                         byte network = 0, byte station = 0xFF)
        {
            _host = host;
            _port = port;
            _frameType = frameType;
            _network = network;
            _station = station;
            _id = $"{host}:{port}";
            _client = CreateClient();

            PlcLogger.Info($"[{_id}] PlcClient created (frame={frameType}, net={network}, sta={station})");
        }

        // =========================================================================
        // CONNECT
        // =========================================================================

        /// <summary>
        /// Kết nối PLC (sync). Mỗi lần tạo lại instance HslCommunication mới
        /// để xóa "System authorization failed" và bất kỳ TCP socket state tích lũy.
        /// </summary>
        public bool Connect()
        {
            // Non-blocking: nếu đang có Connect() khác chạy thì bỏ qua
            if (!_connectGuard.Wait(0))
            {
                PlcLogger.Info($"[{_id}] Connect() skipped — another attempt in progress");
                return State == PlcConnectionState.Connected;
            }

            try
            {
                SetState(PlcConnectionState.Connecting);
                PlcLogger.Info($"[{_id}] Connect() → recreating HslComm instance + Read(D0) test...");

                bool success;
                string resultMsg;

                lock (SyncLock)
                {
                    // CRITICAL: tạo lại HslComm instance để xóa "System authorization failed"
                    // và TCP socket state từ connection cũ
                    try { ((dynamic)_client).ConnectClose(); } catch { }
                    _client = CreateClient();

                    var res = ((dynamic)_client).Read("D0", (ushort)1);
                    success = res.IsSuccess;
                    resultMsg = success ? "OK" : ((string)res.Message);
                }

                if (success)
                {
                    PlcLogger.Info($"[{_id}] Connect() SUCCESS");
                    SetState(PlcConnectionState.Connected);
                }
                else
                {
                    PlcLogger.Warn($"[{_id}] Connect() FAILED: {resultMsg}");
                    SetState(PlcConnectionState.Error);
                }
                return success;
            }
            catch (Exception ex)
            {
                PlcLogger.Error($"[{_id}] Connect() EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                SetState(PlcConnectionState.Error);
                return false;
            }
            finally
            {
                _connectGuard.Release();
            }
        }

        /// <summary>Kết nối PLC (async).</summary>
        public Task<bool> ConnectAsync() => Task.Run(() => Connect());

        public void Disconnect()
        {
            lock (SyncLock)
            {
                try { ((dynamic)_client).ConnectClose(); } catch { }
            }
            PlcLogger.Info($"[{_id}] Disconnected");
            SetState(PlcConnectionState.Disconnected);
        }

        /// <summary>
        /// Dùng cho Write path (user-triggered): thử kết nối nếu chưa connected.
        /// Non-blocking nếu có connect đang chạy.
        /// </summary>
        public bool EnsureConnected()
        {
            if (State == PlcConnectionState.Connected) return true;
            return Connect();
        }

        public Task<bool> EnsureConnectedAsync() => Task.Run(() => EnsureConnected());

        // =========================================================================
        // WATCHDOG — sole reconnect mechanism
        // =========================================================================

        /// <summary>
        /// Watchdog tự động reconnect khi mất kết nối.
        /// Thức dậy NGAY khi NotifyError() được gọi (không đợi hết interval).
        /// Mặc định 3s/lần khi không có sự kiện lỗi.
        /// </summary>
        public void StartWatchdog(int intervalMs = 3000)
        {
            StopWatchdog();
            PlcLogger.Info($"[{_id}] Watchdog started (interval={intervalMs}ms)");

            _watchdogCts = new CancellationTokenSource();
            var token = _watchdogCts.Token;

            _ = Task.Run(async () =>
            {
                int consecutiveFailures = 0;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (State != PlcConnectionState.Connected)
                        {
                            PlcLogger.Info($"[{_id}] Watchdog: State={State}, attempt reconnect (fail#{consecutiveFailures + 1})");
                            SetState(PlcConnectionState.Reconnecting);

                            bool ok = await Task.Run(() => Connect(), token);
                            if (ok)
                            {
                                consecutiveFailures = 0;
                                PlcLogger.Info($"[{_id}] Watchdog: reconnect SUCCESS");
                            }
                            else
                            {
                                consecutiveFailures++;
                                PlcLogger.Warn($"[{_id}] Watchdog: reconnect FAILED (total={consecutiveFailures})");
                            }
                        }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        // Không để exception thoát ra ngoài — giữ vòng lặp sống
                        consecutiveFailures++;
                        PlcLogger.Error($"[{_id}] Watchdog EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                        SetState(PlcConnectionState.Error);
                    }

                    // Chờ: thức dậy sớm nếu NotifyError() báo hiệu, hoặc timeout sau intervalMs
                    try
                    {
                        await _reconnectSignal.WaitAsync(intervalMs, token);
                        // Trả về true = NotifyError() đã báo hiệu → lặp ngay
                        // Trả về false = timeout bình thường → lặp để check state
                    }
                    catch (OperationCanceledException) { return; }
                }

                PlcLogger.Info($"[{_id}] Watchdog stopped");
            }, token);
        }

        public void StopWatchdog()
        {
            _watchdogCts?.Cancel();
            _watchdogCts?.Dispose();
            _watchdogCts = null;
        }

        // =========================================================================
        // ERROR NOTIFICATION
        // =========================================================================

        /// <summary>
        /// Gọi bởi Reader/Writer khi giao tiếp thất bại.
        /// Đánh dấu State=Error và đánh thức watchdog ngay lập tức (không đợi interval).
        /// </summary>
        public void NotifyError()
        {
            if (State == PlcConnectionState.Connected)
            {
                PlcLogger.Warn($"[{_id}] NotifyError() → State→Error, wake watchdog");
                SetState(PlcConnectionState.Error);
                // Wake watchdog immediately (ignore SemaphoreFullException nếu signal đang pending)
                try { _reconnectSignal.Release(); } catch { }
            }
        }

        // =========================================================================
        // PING — ICMP only, không tạo TCP vào port MC Protocol
        // =========================================================================

        /// <summary>
        /// Kiểm tra host còn sống bằng ICMP ping.
        /// Không tạo TCP connection vào port MC Protocol để tránh làm đầy connection table Q series (~8 slots).
        /// </summary>
        public bool Pingable()
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send(_host, 1000);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch { return false; }
        }

        // =========================================================================
        // INTERNAL ACCESS
        // =========================================================================

        /// <summary>Cho phép Reader/Writer truy cập HslCommunication driver.</summary>
        internal dynamic Client => (dynamic)_client;

        private void SetState(PlcConnectionState state)
        {
            State = state;
            try { StateChanged?.Invoke(state); }
            catch { }
        }

        private object CreateClient()
        {
            string ip = ResolveHost(_host);
            switch (_frameType)
            {
                case McFrameType.QnA3E_Ascii:
                    return new MelsecMcAsciiNet { IpAddress = ip, Port = _port, NetworkNumber = _network, NetworkStationNumber = _station, ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
                case McFrameType.A1E_Binary:
                    return new MelsecA1ENet { IpAddress = ip, Port = _port, ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
                case McFrameType.A1E_Ascii:
                    return new MelsecA1EAsciiNet { IpAddress = ip, Port = _port, ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
                case McFrameType.iQR_Binary:
                    return new MelsecMcRNet { IpAddress = ip, Port = _port, NetworkNumber = _network, NetworkStationNumber = _station, ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
                default: // QnA3E_Binary
                    return new MelsecMcNet { IpAddress = ip, Port = _port, NetworkNumber = _network, NetworkStationNumber = _station, ConnectTimeOut = 3000, ReceiveTimeOut = 3000 };
            }
        }

        private static string ResolveHost(string host)
        {
            if (IPAddress.TryParse(host, out _)) return host;
            return Dns.GetHostAddresses(host)[0].ToString();
        }

        public void Dispose()
        {
            StopWatchdog();
            Disconnect();
            lock (SyncLock)
            {
                (_client as IDisposable)?.Dispose();
            }
            _connectGuard.Dispose();
            try { _reconnectSignal.Dispose(); } catch { }
        }

        // =========================================================================
        // DEBUG UTILITY
        // =========================================================================

        /// <summary>Raw socket test bypass HslCommunication — dùng để debug frame.</summary>
        public static async Task<string> TestRawAsync(string host, int port)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                    return "Raw FAIL: Connect timeout (3s)";
                await connectTask;
                var stream = tcp.GetStream();
                stream.ReadTimeout = 5000;
                byte[] req = { 0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x0C, 0x00, 0x10, 0x00, 0x01, 0x04, 0x00, 0x00, 0xA2, 0x00, 0x00, 0xA8, 0x01, 0x00 };
                await stream.WriteAsync(req, 0, req.Length);
                var buf = new byte[64];
                int n = await stream.ReadAsync(buf, 0, 64);
                var hex = string.Join(" ", System.Linq.Enumerable.Take(buf, n).Select(b => $"{b:X2}"));
                if (n >= 13 && buf[0] == 0xD0 && buf[9] == 0x00 && buf[10] == 0x00)
                {
                    ushort val = (ushort)(buf[11] | (buf[12] << 8));
                    return $"Raw OK: D162={val}\n{hex}";
                }
                return $"Raw {n} bytes:\n{hex}";
            }
            catch (Exception ex) { return $"Raw FAIL: {ex.Message}"; }
        }
    }
}
