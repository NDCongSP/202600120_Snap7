namespace Snap7ClientLib.Tags;

/// <summary>
/// Enum biểu diễn toàn bộ kiểu dữ liệu PLC Siemens S7
/// Mapping tương ứng với DBB / DBW / DBD / DBX
/// </summary>
public enum PlcDataType
{
    // Bit
    Bool,

    // 1 byte
    Byte,       // UInt8
    SInt,       // Int8
    USInt,      // UInt8
    Char,       // ASCII char

    // 2 byte
    Word,       // UInt16
    Int,        // Int16
    UInt,       // UInt16

    // 4 byte
    DWord,      // UInt32
    DInt,       // Int32
    UDInt,      // UInt32
    Real,       // Float32

    // 8 byte
    LWord,      // UInt64
    LInt,       // Int64
    ULInt,      // UInt64
    LReal,      // Float64

    // String Siemens: MaxLen + CurLen + Data
    String
}