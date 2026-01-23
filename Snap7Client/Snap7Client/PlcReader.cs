using Sharp7;
using System.Text;

namespace Snap7ClientLib;

public class PlcReader
{
    private readonly PlcClient _plc;

    public PlcReader(PlcClient plc)
    {
        _plc = plc;
    }

    public T Read<T>(
        string address,
        PlcDataType dataType,
        int stringMaxLength = 0)
    {
        if (!_plc.EnsureConnected())
            throw new Exception("PLC not connected");

        var addr = PlcAddressParser.Parse(address);
        var client = _plc.Client;
        byte[] buffer;

        switch (dataType)
        {
            // ===== BIT =====
            case PlcDataType.Bool:
                buffer = new byte[1];
                client.DBRead(addr.Db, addr.Offset, 1, buffer);
                return (T)(object)((buffer[0] & (1 << addr.Bit)) != 0);

            // ===== 8 BIT =====
            case PlcDataType.Byte:
            case PlcDataType.USInt:
                buffer = new byte[1];
                client.DBRead(addr.Db, addr.Offset, 1, buffer);
                return (T)(object)buffer[0];

            case PlcDataType.SInt:
                buffer = new byte[1];
                client.DBRead(addr.Db, addr.Offset, 1, buffer);
                return (T)(object)(sbyte)buffer[0];

            case PlcDataType.Char:
                buffer = new byte[1];
                client.DBRead(addr.Db, addr.Offset, 1, buffer);
                return (T)(object)(char)buffer[0];

            // ===== 16 BIT =====
            case PlcDataType.Word:
            case PlcDataType.UInt:
                buffer = new byte[2];
                client.DBRead(addr.Db, addr.Offset, 2, buffer);
                ushort w = (ushort)((buffer[0] << 8) | buffer[1]);
                return (T)(object)w;

            case PlcDataType.Int:
                buffer = new byte[2];
                client.DBRead(addr.Db, addr.Offset, 2, buffer);
                short i = (short)((buffer[0] << 8) | buffer[1]);
                return (T)(object)i;

            // ===== 32 BIT =====
            case PlcDataType.DWord:
            case PlcDataType.UDInt:
                buffer = new byte[4];
                client.DBRead(addr.Db, addr.Offset, 4, buffer);
                uint dw = S7.GetDWordAt(buffer, 0);
                return (T)(object)dw;

            case PlcDataType.DInt:
                buffer = new byte[4];
                client.DBRead(addr.Db, addr.Offset, 4, buffer);
                int di = S7.GetDIntAt(buffer, 0);
                return (T)(object)di;

            case PlcDataType.Real:
                buffer = new byte[4];
                client.DBRead(addr.Db, addr.Offset, 4, buffer);
                float r = S7.GetRealAt(buffer, 0);
                return (T)(object)r;

            // ===== 64 BIT =====
            case PlcDataType.LWord:
            case PlcDataType.ULInt:
                buffer = new byte[8];
                client.DBRead(addr.Db, addr.Offset, 8, buffer);
                ulong lw = S7.GetULIntAt(buffer, 0);
                return (T)(object)lw;

            case PlcDataType.LInt:
                buffer = new byte[8];
                client.DBRead(addr.Db, addr.Offset, 8, buffer);
                long li = S7.GetLIntAt(buffer, 0);
                return (T)(object)li;

            case PlcDataType.LReal:
                buffer = new byte[8];
                client.DBRead(addr.Db, addr.Offset, 8, buffer);
                double lr = S7.GetLRealAt(buffer, 0);
                return (T)(object)lr;

            // ===== STRING =====
            case PlcDataType.String:
                if (stringMaxLength <= 0)
                    throw new Exception("String requires max length");

                buffer = new byte[stringMaxLength + 2];
                client.DBRead(addr.Db, addr.Offset, buffer.Length, buffer);
                string s = Encoding.ASCII.GetString(buffer, 2, buffer[1]);
                return (T)(object)s;

            default:
                throw new NotSupportedException($"Unsupported data type {dataType}");
        }
    }
}