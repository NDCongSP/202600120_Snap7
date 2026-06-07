namespace McProtocolClientLib.Tags;

/// <summary>
/// Enum biểu diễn toàn bộ kiểu dữ liệu PLC Mitsubishi MC Protocol.
/// Mapping tương ứng với Word device (D, W, R, ZR, SD, SW)
/// và Bit device (M, X, Y, B, F, L, S, SM, SB).
/// 1 Word device = 16 bit = 2 byte.
/// 32-bit / Real chiếm 2 word liên tiếp (ví dụ D100 = low word, D101 = high word).
/// </summary>
public enum PlcDataType
{
    // Bit – đọc 1 bit (Bool)
    Bool,

    // 1 byte – ít dùng trong Mitsubishi (1 word = 2 byte) nhưng hỗ trợ
    Byte,       // UInt8 (lower byte của word)
    SInt,       // Int8
    USInt,      // UInt8
    Char,       // ASCII char

    // 2 byte = 1 word
    Word,       // UInt16
    Int,        // Int16
    UInt,       // UInt16

    // 4 byte = 2 word
    DWord,      // UInt32
    DInt,       // Int32
    UDInt,      // UInt32
    Real,       // Float32 (IEEE 754)

    // 8 byte = 4 word
    LWord,      // UInt64
    LInt,       // Int64
    ULInt,      // UInt64
    LReal,      // Float64 (IEEE 754)

    // String ASCII Mitsubishi: 2 ký tự / 1 word, không có header MaxLen/CurLen
    String
}
