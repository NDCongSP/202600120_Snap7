using Sharp7;
using System.Net;

namespace Snap7ClientLib;

/// <summary>
/// Wrapper chính quản lý kết nối PLC Snap7
/// </summary>
public class PlcClient : IDisposable
{
    // Snap7 client native
    private readonly S7Client _client = new();

    private readonly string _host;
    private readonly int _rack;
    private readonly int _slot;
    private bool _disposed;

    /// <summary>
    /// host: IP hoặc hostname (VD: phucthinhautomation.com)
    /// </summary>
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
            // Resolve hostname → IP
            string ip = ResolveHost(_host);

            // Snap7 Connect
            return _client.ConnectTo(ip, _rack, _slot) == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Kết nối PLC bất đồng bộ
    /// </summary>
    public async Task<bool> ConnectAsync()
        => await Task.Run(Connect);

    /// <summary>
    /// Ngắt kết nối PLC
    /// </summary>
    public void Disconnect()
    {
        if (_client.Connected)
            _client.Disconnect();
    }

    /// <summary>
    /// Đảm bảo PLC luôn connected (auto reconnect)
    /// </summary>
    public bool EnsureConnected()
        => _client.Connected || Connect();

    /// <summary>
    /// EnsureConnected bất đồng bộ
    /// </summary>
    public async Task<bool> EnsureConnectedAsync()
        => await Task.Run(EnsureConnected);

    /// <summary>
    /// Expose S7Client cho GroupReader / Writer
    /// </summary>
    internal S7Client Client => _client;

    /// <summary>
    /// Resolve hostname hoặc IP
    /// </summary>
    private static string ResolveHost(string host)
    {
        if (IPAddress.TryParse(host, out _))
            return host;

        return Dns.GetHostAddresses(host)[0].ToString();
    }

    /// <summary>
    /// Dispose: Snap7 KHÔNG có Dispose → chỉ Disconnect
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        Disconnect();
        _disposed = true;
    }
}
