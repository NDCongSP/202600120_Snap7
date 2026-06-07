using HslCommunication.Profinet.Melsec;
using McProtocolClientLib.Tags;
using System.Net;

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
    public class PlcClient : IDisposable
    {
        /// <summary>
        /// Khóa dùng chung cho cả Read và Write – tránh "đâm" gây trễ tích lũy.
        /// </summary>
        public readonly object SyncLock = new object();

        private readonly object _client; // MelsecMcNet / MelsecMcAsciiNet / MelsecA1ENet / MelsecA1EAsciiNet / MelsecMcRNet
        private readonly string _host;
        private readonly int _port;
        private readonly McFrameType _frameType;
        private readonly byte _network;
        private readonly byte _station;

        private CancellationTokenSource? _watchdogCts;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        public PlcConnectionState State { get; private set; } =
            PlcConnectionState.Disconnected;

        public event Action<PlcConnectionState>? StateChanged;

        public PlcClient(string host, int port = 6000,
                         McFrameType frameType = McFrameType.QnA3E_Binary,
                         byte network = 0, byte station = 0xFF)
        {
            _host = host;
            _port = port;
            _frameType = frameType;
            _network = network;
            _station = station;

            _client = CreateClient();
        }

        /// <summary>
        /// Tạo instance driver tương ứng frame.
        /// </summary>
        private object CreateClient()
        {
            switch (_frameType)
            {
                case McFrameType.QnA3E_Ascii:
                    return new MelsecMcAsciiNet
                    {
                        IpAddress = ResolveHost(_host),
                        Port = _port,
                        NetworkNumber = _network,
                        NetworkStationNumber = _station,
                        ConnectTimeOut = 3000,
                        ReceiveTimeOut = 3000,
                    };

                case McFrameType.A1E_Binary:
                    return new MelsecA1ENet
                    {
                        IpAddress = ResolveHost(_host),
                        Port = _port,
                        ConnectTimeOut = 3000,
                        ReceiveTimeOut = 3000,
                    };

                case McFrameType.A1E_Ascii:
                    return new MelsecA1EAsciiNet
                    {
                        IpAddress = ResolveHost(_host),
                        Port = _port,
                        ConnectTimeOut = 3000,
                        ReceiveTimeOut = 3000,
                    };

                case McFrameType.iQR_Binary:
                    return new MelsecMcRNet
                    {
                        IpAddress = ResolveHost(_host),
                        Port = _port,
                        NetworkNumber = _network,
                        NetworkStationNumber = _station,
                        ConnectTimeOut = 3000,
                        ReceiveTimeOut = 3000,
                    };

                case McFrameType.QnA3E_Binary:
                default:
                    return new MelsecMcNet
                    {
                        IpAddress = ResolveHost(_host),
                        Port = _port,
                        NetworkNumber = _network,
                        NetworkStationNumber = _station,
                        ConnectTimeOut = 3000,
                        ReceiveTimeOut = 3000,
                    };
            }
        }

        /// <summary>
        /// Kết nối PLC (sync). Dùng short-connection (1 TCP/request) thay ConnectServer()
        /// vì Q series không respond đúng khi giữ persistent socket.
        /// </summary>
        public bool Connect()
        {
            try
            {
                SetState(PlcConnectionState.Connecting);
                ApplyIp(ResolveHost(_host));

                // Verify bằng 1 short-connection read thực tế (không dùng ConnectServer)
                bool success;
                lock (SyncLock)
                {
                    var ok = (dynamic)_client;
                    var res = ok.Read("D0", (ushort)1);
                    success = res.IsSuccess;
                }

                SetState(success ? PlcConnectionState.Connected : PlcConnectionState.Error);
                return success;
            }
            catch
            {
                SetState(PlcConnectionState.Error);
                return false;
            }
        }

        /// <summary>Kết nối PLC (async).</summary>
        public Task<bool> ConnectAsync() => Task.Run(Connect);

        public void Disconnect()
        {
            try
            {
                var ok = (dynamic)_client;
                ok.ConnectClose();
            }
            catch { /* ignore */ }
            SetState(PlcConnectionState.Disconnected);
        }

        /// <summary>
        /// Tự reconnect khi mất kết nối hoặc sau khi PLC khởi động lại.
        /// </summary>
        public bool EnsureConnected()
        {
            if (Pingable())
            {
                if (State == PlcConnectionState.Connected)
                    return true;
                // Host sống nhưng state chưa Connected (Error/Disconnected) → reconnect
                return Connect();
            }

            SetState(PlcConnectionState.Reconnecting);
            return Connect();
        }

        public Task<bool> EnsureConnectedAsync() => Task.Run(EnsureConnected);

        /// <summary>
        /// Watchdog kiểm tra kết nối định kỳ; tự reconnect nếu rớt mạng.
        /// </summary>
        public void StartWatchdog(int intervalMs = 2000)
        {
            StopWatchdog();

            _watchdogCts = new CancellationTokenSource();
            var token = _watchdogCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (!Pingable())
                        {
                            await _connectionLock.WaitAsync(token);
                            try
                            {
                                if (!Pingable())
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
                    catch
                    {
                        SetState(PlcConnectionState.Error);
                    }

                    try { await Task.Delay(intervalMs, token); }
                    catch (OperationCanceledException) { break; }
                }
            }, token);
        }

        public void StopWatchdog()
        {
            _watchdogCts?.Cancel();
            _watchdogCts?.Dispose();
            _watchdogCts = null;
        }

        /// <summary>
        /// Kiểm tra host còn sống bằng ICMP ping — KHÔNG tạo TCP connection vào port MC Protocol,
        /// tránh làm đầy connection table của PLC (Q series giới hạn ~8 kết nối đồng thời).
        /// </summary>
        public bool Pingable()
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send(_host, 1000);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cho phép Reader/Writer truy cập driver thực để gọi Read/Write.
        /// </summary>
        internal dynamic Client => (dynamic)_client;

        private void SetState(PlcConnectionState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void ApplyIp(string ip)
        {
            try
            {
                var ok = (dynamic)_client;
                ok.IpAddress = ip;
            }
            catch { /* ignore */ }
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
            (_client as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Test raw socket MC Protocol (bypass HslCommunication) — dùng để debug.
        /// Gửi đúng frame đã chứng minh hoạt động bằng PowerShell.
        /// </summary>
        public static async Task<string> TestRawAsync(string host, int port)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                    return "Raw FAIL: Connect timeout (3s)";
                await connectTask; // re-throw nếu có exception
                var stream = tcp.GetStream();
                stream.ReadTimeout = 5000;

                // 3E Binary: Read D162 (0xA2), 1 word — same frame as working PowerShell test
                byte[] req = {
                    0x50,0x00,0x00,0xFF,0xFF,0x03,0x00,
                    0x0C,0x00,0x10,0x00,0x01,0x04,0x00,0x00,
                    0xA2,0x00,0x00,0xA8,0x01,0x00
                };
                await stream.WriteAsync(req, 0, req.Length);

                var buf = new byte[64];
                int n = await stream.ReadAsync(buf, 0, 64);
                var hex = string.Join(" ", buf.Take(n).Select(b => $"{b:X2}"));

                if (n >= 13 && buf[0] == 0xD0 && buf[9] == 0x00 && buf[10] == 0x00)
                {
                    ushort val = (ushort)(buf[11] | (buf[12] << 8));
                    return $"Raw OK: D162={val}\n{hex}";
                }
                return $"Raw {n} bytes (unexpected):\n{hex}";
            }
            catch (Exception ex)
            {
                return $"Raw FAIL: {ex.Message}";
            }
        }
    }
}
