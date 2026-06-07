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
        /// Kết nối PLC (sync). HslCommunication dùng "long-connection" qua ConnectServer().
        /// </summary>
        public bool Connect()
        {
            try
            {
                SetState(PlcConnectionState.Connecting);

                // Cập nhật IP đã resolve (nếu host là tên miền)
                ApplyIp(ResolveHost(_host));

                // Bật long-connection
                var ok = (dynamic)_client;
                var res = ok.ConnectServer();

                bool success = res.IsSuccess;
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
        /// Tự reconnect khi mất kết nối.
        /// </summary>
        public bool EnsureConnected()
        {
            if (Pingable())
                return true;

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
        /// Kiểm tra "sống" bằng cách đọc 1 word đơn giản (D0). Nhẹ và đủ chính xác.
        /// </summary>
        public bool Pingable()
        {
            try
            {
                var ok = (dynamic)_client;
                var res = ok.Read("D0", (ushort)1);
                return res.IsSuccess;
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
    }
}
