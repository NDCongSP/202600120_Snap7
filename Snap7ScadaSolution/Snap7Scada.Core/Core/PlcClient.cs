using Sharp7;
using System.Net;

namespace Snap7ClientLib.Core;

/// <summary>
/// Client kết nối PLC Siemens S7 (Snap7)
/// Hỗ trợ sync/async + auto reconnect
/// </summary>
public class PlcClient : IDisposable
{
    private readonly S7Client _client = new();
    private readonly string _host;
    private readonly int _rack;
    private readonly int _slot;
    private Timer? _watchdog;

    public PlcConnectionState State { get; private set; } =
        PlcConnectionState.Disconnected;

    public event Action<PlcConnectionState>? StateChanged;

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

    /// <summary>
    /// Watchdog PLC sống
    /// </summary>
    public void StartWatchdog(int intervalMs = 2000)
    {
        _watchdog = new Timer(_ =>
        {
            if (!_client.Connected)
                EnsureConnected();
        }, null, 0, intervalMs);
    }

    public void StopWatchdog()
    {
        _watchdog?.Dispose();
        _watchdog = null;
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
