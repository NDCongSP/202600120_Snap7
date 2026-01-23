namespace Snap7ClientLib.Core;

/// <summary>
/// Trạng thái kết nối PLC
/// </summary>
public enum PlcConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}
