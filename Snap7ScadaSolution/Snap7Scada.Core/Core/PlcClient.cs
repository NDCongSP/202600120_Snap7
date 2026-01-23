using Sharp7;
using Snap7Scada.Lib.Core;
using System.Net;

namespace Snap7ClientLib.Core;

/// <summary>
/// Lớp quản lý kết nối PLC Snap7
/// - IP hoặc hostname
/// - Auto reconnect
/// - Sync / Async
/// </summary>
public class PlcClient : IDisposable
{
    private readonly S7Client _client = new();
    private readonly string _host;
    private readonly int _rack;
    private readonly int _slot;
    private bool _disposed;

    public PlcConnectionState State { get; private set; }
        = PlcConnectionState.Disconnected;

    /// <summary>
    /// Event thay đổi trạng thái kết nối PLC
    /// </summary>
    public event Action<PlcConnectionState>? ConnectionStateChanged;

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
            UpdateState(PlcConnectionState.Connecting);

            string ip = ResolveHost(_host);
            int res = _client.ConnectTo(ip, _rack, _slot);

            UpdateState(res == 0
                ? PlcConnectionState.Connected
                : PlcConnectionState.Error);

            return res == 0;
        }
        catch
        {
            UpdateState(PlcConnectionState.Error);
            return false;
        }
    }

    /// <summary>
    /// Kết nối PLC (async)
    /// </summary>
    public Task<bool> ConnectAsync()
        => Task.Run(Connect);

    /// <summary>
    /// Đảm bảo PLC luôn connected
    /// </summary>
    public bool EnsureConnected()
        => _client.Connected || Connect();

    public Task<bool> EnsureConnectedAsync()
        => Task.Run(EnsureConnected);

    public void Disconnect()
    {
        if (_client.Connected)
            _client.Disconnect();

        UpdateState(PlcConnectionState.Disconnected);
    }

    internal S7Client Client => _client;

    private void UpdateState(PlcConnectionState state)
    {
        if (State == state) return;
        State = state;
        ConnectionStateChanged?.Invoke(state);
    }

    private static string ResolveHost(string host)
    {
        if (IPAddress.TryParse(host, out _))
            return host;

        return Dns.GetHostAddresses(host)[0].ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Disconnect();
        _disposed = true;
    }
}
