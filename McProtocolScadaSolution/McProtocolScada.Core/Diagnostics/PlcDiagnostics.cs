namespace McProtocolClientLib.Diagnostics;

/// <summary>
/// Thống kê trạng thái PLC (Mitsubishi MC Protocol)
/// </summary>
public class PlcDiagnostics
{
    public int ReadCount { get; private set; }
    public int WriteCount { get; private set; }
    public int ErrorCount { get; private set; }

    public double LastReadTimeMs { get; private set; }
    public double LastWriteTimeMs { get; private set; }

    internal void MeasureRead(Action action)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            action();
            ReadCount++;
        }
        catch
        {
            ErrorCount++;
            throw;
        }
        finally
        {
            sw.Stop();
            LastReadTimeMs = sw.Elapsed.TotalMilliseconds;
        }
    }

    internal void MeasureWrite(Action action)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            action();
            WriteCount++;
        }
        catch
        {
            ErrorCount++;
            throw;
        }
        finally
        {
            sw.Stop();
            LastWriteTimeMs = sw.Elapsed.TotalMilliseconds;
        }
    }
}
