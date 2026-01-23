using Sharp7;
using System.Text;

namespace Snap7ClientLib;

public class PlcWriter
{
    private readonly PlcClient _plc;

    public PlcWriter(PlcClient plc)
    {
        _plc = plc;
    }

    public void Write<T>(
        string address,
        PlcDataType dataType,
        T value,
        int stringMaxLength = 0)
    {
        if (!_plc.EnsureConnected())
            throw new Exception("PLC not connected");

        var addr = PlcAddressParser.Parse(address);
        var client = _plc.Client;
        byte[] buffer;

        switch (dataType)
        {
            case PlcDataType.Bool:
                buffer = new byte[1];
                client.DBRead(addr.Db, addr.Offset, 1, buffer);
                if ((bool)(object)value!)
                    buffer[0] |= (byte)(1 << addr.Bit);
                else
                    buffer[0] &= (byte)~(1 << addr.Bit);
                client.DBWrite(addr.Db, addr.Offset, 1, buffer);
                break;

            case PlcDataType.Byte:
            case PlcDataType.USInt:
                client.DBWrite(addr.Db, addr.Offset, 1, new[] { Convert.ToByte(value) });
                break;

            case PlcDataType.SInt:
                client.DBWrite(addr.Db, addr.Offset, 1, new[] { (byte)Convert.ToSByte(value) });
                break;

            case PlcDataType.Char:
                client.DBWrite(addr.Db, addr.Offset, 1, new[] { (byte)(char)(object)value! });
                break;

            case PlcDataType.Word:
            case PlcDataType.UInt:
                buffer = new byte[2];
                ushort w = Convert.ToUInt16(value);
                buffer[0] = (byte)(w >> 8);
                buffer[1] = (byte)w;
                client.DBWrite(addr.Db, addr.Offset, 2, buffer);
                break;

            case PlcDataType.Int:
                buffer = new byte[2];
                short i = Convert.ToInt16(value);
                buffer[0] = (byte)(i >> 8);
                buffer[1] = (byte)i;
                client.DBWrite(addr.Db, addr.Offset, 2, buffer);
                break;

            case PlcDataType.DWord:
            case PlcDataType.UDInt:
                buffer = new byte[4];
                S7.SetDWordAt(buffer, 0, Convert.ToUInt32(value));
                client.DBWrite(addr.Db, addr.Offset, 4, buffer);
                break;

            case PlcDataType.DInt:
                buffer = new byte[4];
                S7.SetDIntAt(buffer, 0, Convert.ToInt32(value));
                client.DBWrite(addr.Db, addr.Offset, 4, buffer);
                break;

            case PlcDataType.Real:
                buffer = new byte[4];
                S7.SetRealAt(buffer, 0, Convert.ToSingle(value));
                client.DBWrite(addr.Db, addr.Offset, 4, buffer);
                break;

            case PlcDataType.LWord:
            case PlcDataType.ULInt:
                buffer = new byte[8];
                S7.SetULintAt(buffer, 0, Convert.ToUInt64(value));
                client.DBWrite(addr.Db, addr.Offset, 8, buffer);
                break;

            case PlcDataType.LInt:
                buffer = new byte[8];
                S7.SetLIntAt(buffer, 0, Convert.ToInt64(value));
                client.DBWrite(addr.Db, addr.Offset, 8, buffer);
                break;

            case PlcDataType.LReal:
                buffer = new byte[8];
                S7.SetLRealAt(buffer, 0, Convert.ToDouble(value));
                client.DBWrite(addr.Db, addr.Offset, 8, buffer);
                break;

            case PlcDataType.String:
                if (stringMaxLength <= 0)
                    throw new Exception("String requires max length");

                string s = value!.ToString()!;
                buffer = new byte[stringMaxLength + 2];
                buffer[0] = (byte)stringMaxLength;
                buffer[1] = (byte)Math.Min(s.Length, stringMaxLength);
                Encoding.ASCII.GetBytes(s, 0, buffer[1], buffer, 2);
                client.DBWrite(addr.Db, addr.Offset, buffer.Length, buffer);
                break;
        }
    }
}