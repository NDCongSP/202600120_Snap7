namespace Snap7ClientLib
{
    internal static class PlcDataTypeHelper
    {
        /// <summary>
        /// HÀM TÍNH SIZE THEO KIỂU DỮ LIỆU.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="stringLength"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static int GetSize(PlcDataType type, int stringLength = 0)
        {
            return type switch
            {
                PlcDataType.Bool => 1,
                PlcDataType.Byte or PlcDataType.SInt or PlcDataType.USInt or PlcDataType.Char => 1,

                PlcDataType.Word or PlcDataType.Int or PlcDataType.UInt => 2,

                PlcDataType.DWord or PlcDataType.DInt or PlcDataType.UDInt or PlcDataType.Real => 4,

                PlcDataType.LWord or PlcDataType.LInt or PlcDataType.ULInt or PlcDataType.LReal => 8,

                PlcDataType.String => stringLength + 2,

                _ => throw new NotSupportedException()
            };
        }

        /// <summary>
        /// PREPARE TAG (PARSE + CALC SIZE).
        /// </summary>
        /// <param name="tag"></param>
        public static void PrepareTag(PlcTag tag)
        {
            var addr = PlcAddressParser.Parse(tag.Address);

            tag.Db = addr.Db;
            tag.Offset = addr.Offset;
            tag.Bit = addr.Bit;
            tag.Size = PlcDataTypeHelper.GetSize(tag.DataType, tag.StringLength);
        }
    }
}
