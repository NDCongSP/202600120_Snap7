using System;
using System.Text;

namespace McProtocolClientLib.Core
{
    /// <summary>
    /// Chuyển đổi byte[] (đọc từ PLC) sang giá trị số và ngược lại.
    /// Thay thế HslCommunication.Core.IByteTransform/RegularByteTransform (DEC-013: bỏ hoàn toàn
    /// dependency HslCommunication).
    /// </summary>
    /// <remarks>
    /// Đã xác nhận bằng thực nghiệm (chạy RegularByteTransform thật của HslCommunication 11.6.4)
    /// rằng DataFormat.DCBA — giá trị MelsecMcNet dùng làm default và project này đang dùng —
    /// tương đương little-endian thuần, KHÔNG hoán đổi byte/word nào cả (TransUInt32({01,02,03,04})
    /// = 0x04030201, đúng như BitConverter.ToUInt32 trên buffer gốc). Vì project chỉ dùng đúng 1
    /// format này, class này chỉ implement hành vi DCBA — không thêm enum DataFormat cho 3 giá trị
    /// (ABCD/BADC/CDAB) chưa từng và sẽ không được dùng.
    /// </remarks>
    public interface IByteTransform
    {
        short TransInt16(byte[] buffer, int index);
        ushort TransUInt16(byte[] buffer, int index);
        int TransInt32(byte[] buffer, int index);
        uint TransUInt32(byte[] buffer, int index);
        long TransInt64(byte[] buffer, int index);
        ulong TransUInt64(byte[] buffer, int index);
        float TransSingle(byte[] buffer, int index);
        double TransDouble(byte[] buffer, int index);
        string TransString(byte[] buffer, int index, int length, Encoding encoding);

        byte[] TransByte(short value);
        byte[] TransByte(ushort value);
        byte[] TransByte(int value);
        byte[] TransByte(uint value);
        byte[] TransByte(long value);
        byte[] TransByte(ulong value);
        byte[] TransByte(float value);
        byte[] TransByte(double value);
    }

    public class RegularByteTransform : IByteTransform
    {
        public short TransInt16(byte[] buffer, int index) => BitConverter.ToInt16(buffer, index);
        public ushort TransUInt16(byte[] buffer, int index) => BitConverter.ToUInt16(buffer, index);
        public int TransInt32(byte[] buffer, int index) => BitConverter.ToInt32(buffer, index);
        public uint TransUInt32(byte[] buffer, int index) => BitConverter.ToUInt32(buffer, index);
        public long TransInt64(byte[] buffer, int index) => BitConverter.ToInt64(buffer, index);
        public ulong TransUInt64(byte[] buffer, int index) => BitConverter.ToUInt64(buffer, index);
        public float TransSingle(byte[] buffer, int index) => BitConverter.ToSingle(buffer, index);
        public double TransDouble(byte[] buffer, int index) => BitConverter.ToDouble(buffer, index);
        public string TransString(byte[] buffer, int index, int length, Encoding encoding) => encoding.GetString(buffer, index, length);

        public byte[] TransByte(short value) => BitConverter.GetBytes(value);
        public byte[] TransByte(ushort value) => BitConverter.GetBytes(value);
        public byte[] TransByte(int value) => BitConverter.GetBytes(value);
        public byte[] TransByte(uint value) => BitConverter.GetBytes(value);
        public byte[] TransByte(long value) => BitConverter.GetBytes(value);
        public byte[] TransByte(ulong value) => BitConverter.GetBytes(value);
        public byte[] TransByte(float value) => BitConverter.GetBytes(value);
        public byte[] TransByte(double value) => BitConverter.GetBytes(value);
    }
}
