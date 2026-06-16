using McProtocolClientLib.Core;
using System.Reflection;
using Xunit;

namespace McProtocolScada.Tests;

/// <summary>
/// Unit tests cho PlcClient state machine — không cần PLC thật.
/// Test: state transitions, NotifyError, watchdog lifecycle.
/// </summary>
public class PlcClientStateTests
{
    // 127.0.0.1:9999 — không có ai lắng nghe → Connect() sẽ fail (timeout), không đóng băng
    private const string TestHost = "127.0.0.1";
    private const int TestPort = 9999;

    /// <summary>
    /// Đặt State trực tiếp qua backing field (bypass private setter) để setup test.
    /// </summary>
    private static void ForceState(PlcClient client, PlcConnectionState state)
    {
        var field = typeof(PlcClient).GetField("<State>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(client, state);
    }

    // ===== STATE KHỞI TẠO =====

    [Fact]
    public void InitialState_IsDisconnected()
    {
        using var client = new PlcClient(TestHost, TestPort);
        Assert.Equal(PlcConnectionState.Disconnected, client.State);
    }

    [Fact]
    public void HostAndPort_MatchConstructorArgs()
    {
        using var client = new PlcClient("192.168.1.1", 8000);
        Assert.Equal("192.168.1.1", client.Host);
        Assert.Equal(8000, client.Port);
    }

    // ===== NOTIFY ERROR =====

    [Fact]
    public void NotifyError_WhenConnected_ChangesStateToError()
    {
        using var client = new PlcClient(TestHost, TestPort);
        ForceState(client, PlcConnectionState.Connected);
        Assert.Equal(PlcConnectionState.Connected, client.State);

        client.NotifyError();

        Assert.Equal(PlcConnectionState.Error, client.State);
    }

    [Fact]
    public void NotifyError_WhenDisconnected_NoStateChange()
    {
        using var client = new PlcClient(TestHost, TestPort);
        // Initial = Disconnected
        client.NotifyError();
        Assert.Equal(PlcConnectionState.Disconnected, client.State);
    }

    [Fact]
    public void NotifyError_WhenAlreadyError_NoStateChange()
    {
        using var client = new PlcClient(TestHost, TestPort);
        ForceState(client, PlcConnectionState.Error);

        client.NotifyError(); // gọi 2 lần — phải idempotent
        Assert.Equal(PlcConnectionState.Error, client.State);
    }

    [Fact]
    public void NotifyError_WhenConnecting_NoStateChange()
    {
        using var client = new PlcClient(TestHost, TestPort);
        ForceState(client, PlcConnectionState.Connecting);

        client.NotifyError();
        Assert.Equal(PlcConnectionState.Connecting, client.State);
    }

    // ===== STATE CHANGED EVENT =====

    [Fact]
    public void NotifyError_FiresStateChangedEvent()
    {
        using var client = new PlcClient(TestHost, TestPort);
        ForceState(client, PlcConnectionState.Connected);

        PlcConnectionState? observed = null;
        client.StateChanged += s => observed = s;

        client.NotifyError();

        Assert.Equal(PlcConnectionState.Error, observed);
    }

    [Fact]
    public void NotifyError_WhenNotConnected_DoesNotFireStateChanged()
    {
        using var client = new PlcClient(TestHost, TestPort);
        bool firedInappropriately = false;
        client.StateChanged += _ => firedInappropriately = true;

        client.NotifyError(); // Disconnected → không thay đổi
        Assert.False(firedInappropriately);
    }

    [Fact]
    public void NotifyError_MultipleSubscribers_AllReceiveEvent()
    {
        using var client = new PlcClient(TestHost, TestPort);
        ForceState(client, PlcConnectionState.Connected);

        int callCount = 0;
        client.StateChanged += _ => callCount++;
        client.StateChanged += _ => callCount++;

        client.NotifyError();

        Assert.Equal(2, callCount);
    }

    // ===== WATCHDOG LIFECYCLE =====

    [Fact]
    public void StartWatchdog_DoesNotThrow()
    {
        using var client = new PlcClient(TestHost, TestPort);
        var ex = Record.Exception(() => client.StartWatchdog(100));
        Assert.Null(ex);
        client.StopWatchdog();
    }

    [Fact]
    public void StopWatchdog_WithoutStart_DoesNotThrow()
    {
        using var client = new PlcClient(TestHost, TestPort);
        var ex = Record.Exception(() => client.StopWatchdog());
        Assert.Null(ex);
    }

    [Fact]
    public void StartWatchdog_TwiceWithoutStop_DoesNotThrow()
    {
        // Thứ hai gọi StartWatchdog phải dừng cái cũ trước
        using var client = new PlcClient(TestHost, TestPort);
        var ex = Record.Exception(() =>
        {
            client.StartWatchdog(200);
            client.StartWatchdog(300); // phải stop + restart
        });
        Assert.Null(ex);
        client.StopWatchdog();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var client = new PlcClient(TestHost, TestPort);
        var ex = Record.Exception(() => client.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_WithActiveWatchdog_DoesNotThrow()
    {
        var client = new PlcClient(TestHost, TestPort);
        client.StartWatchdog(100);
        var ex = Record.Exception(() => client.Dispose());
        Assert.Null(ex);
    }

    // ===== RECONNECT SIGNAL — watchdog wakes up ngay khi NotifyError =====

    [Fact]
    public async Task NotifyError_WakesWatchdog_WithinShortTime()
    {
        // Kiểm tra watchdog thức dậy trong vòng 500ms sau khi NotifyError()
        // (interval = 60000ms → không thể wake by timeout, chỉ wake bằng signal)
        using var client = new PlcClient(TestHost, TestPort);
        ForceState(client, PlcConnectionState.Connected);

        var reconnectingObserved = new TaskCompletionSource<bool>();
        client.StateChanged += s =>
        {
            if (s == PlcConnectionState.Reconnecting || s == PlcConnectionState.Connecting)
                reconnectingObserved.TrySetResult(true);
        };

        client.StartWatchdog(60000); // interval rất dài, không tự tick

        await Task.Delay(50); // cho watchdog khởi động

        var sw = System.Diagnostics.Stopwatch.StartNew();
        client.NotifyError(); // → State=Error → wake watchdog

        // Watchdog phải thức dậy và bắt đầu connect trong 1000ms
        bool woke = await Task.WhenAny(reconnectingObserved.Task, Task.Delay(1000)) == reconnectingObserved.Task;
        sw.Stop();

        Assert.True(woke, $"Watchdog did not wake within 1000ms (elapsed={sw.ElapsedMilliseconds}ms)");
    }

    // ===== CONNECT KHI KHÔNG CÓ PLC (nhanh, timeout ≤ 3s) =====

    [Fact]
    public void Connect_ToNonExistentHost_ReturnsFalse()
    {
        // 127.0.0.1:9999 — không có server → connect phải fail nhanh
        using var client = new PlcClient(TestHost, TestPort);
        bool result = client.Connect();
        Assert.False(result);
        Assert.Equal(PlcConnectionState.Error, client.State);
    }

    [Fact]
    public void Connect_ToNonExistentHost_StateBecomesError()
    {
        using var client = new PlcClient(TestHost, TestPort);
        client.Connect();
        // State phải là Error (không phải Connecting hay Disconnected)
        Assert.Equal(PlcConnectionState.Error, client.State);
    }

    [Fact]
    public void Connect_ToNonExistentHost_IsNotLicenseLimited()
    {
        // Lỗi timeout mạng KHÔNG phải lỗi license — phải phân biệt rõ 2 loại lỗi
        using var client = new PlcClient(TestHost, TestPort);
        client.Connect();
        Assert.False(client.IsLicenseLimited);
    }

    // ===== LICENSE LIMIT DETECTION (DEC-011) =====
    // HslCommunication free-tier trả về "System authorization failed..." khi hết quota —
    // đây là lỗi process-level của library, KHÔNG phải lỗi PLC/mạng. Watchdog phải nhận
    // diện đúng để backoff dài hơn thay vì retry vô nghĩa mỗi vài giây.

    [Theory]
    [InlineData("System authorization failed, need to use activation code authorization, thank you for your support. Active device number: 12687", true)]
    [InlineData("system authorization failed", true)] // case-insensitive
    [InlineData("SYSTEM AUTHORIZATION FAILED", true)]
    [InlineData("OK", false)]
    [InlineData("Connect timeout", false)]
    [InlineData("Socket Exception", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAuthorizationError_DetectsLicenseMessageCorrectly(string? message, bool expected)
    {
        Assert.Equal(expected, PlcClient.IsAuthorizationError(message));
    }
}
