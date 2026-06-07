namespace McProtocolClientLib.Core;

/// <summary>
/// Trạng thái kết nối PLC (Mitsubishi MC Protocol)
/// </summary>
public enum PlcConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}
