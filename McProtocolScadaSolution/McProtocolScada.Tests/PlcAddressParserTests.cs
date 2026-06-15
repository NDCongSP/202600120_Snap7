using McProtocolClientLib.Tags;
using Xunit;

namespace McProtocolScada.Tests;

/// <summary>
/// Unit tests cho PlcAddressParser — pure logic, không cần PLC thật.
/// </summary>
public class PlcAddressParserTests
{
    // Helper: tạo tag + parse
    private static PlcTag ParseTag(string address, PlcDataType dataType = PlcDataType.Word, int strLen = 0)
    {
        var tag = new PlcTag { Address = address, DataType = dataType, StringLength = strLen };
        PlcAddressParser.Parse(tag);
        return tag;
    }

    // ===== WORD DEVICES (decimal) =====

    [Fact]
    public void D100_Word_ParsesDeviceAndOffset()
    {
        var t = ParseTag("D100", PlcDataType.Word);
        Assert.Equal("D", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.WordDevice, t.DeviceKind);
        Assert.Equal(10, t.OffsetRadix);
        Assert.Equal(100, t.Offset);
        Assert.Equal(0, t.Bit);
        Assert.False(t.IsBitInWord);
        Assert.Equal(1, t.WordSize);
    }

    [Fact]
    public void D_LowercaseAddress_Normalized()
    {
        // Parse phải ToUpperInvariant
        var t = ParseTag("d100", PlcDataType.Word);
        Assert.Equal("D", t.DeviceCode);
        Assert.Equal(100, t.Offset);
    }

    [Fact]
    public void ZR1000_ExtendedFileRegister()
    {
        var t = ParseTag("ZR1000", PlcDataType.Word);
        Assert.Equal("ZR", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.WordDevice, t.DeviceKind);
        Assert.Equal(10, t.OffsetRadix);
        Assert.Equal(1000, t.Offset);
        Assert.Equal(1, t.WordSize);
    }

    [Fact]
    public void W30_LinkRegister()
    {
        var t = ParseTag("W30", PlcDataType.Word);
        Assert.Equal("W", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.WordDevice, t.DeviceKind);
        Assert.Equal(30, t.Offset);
    }

    [Fact]
    public void R100_FileRegister()
    {
        var t = ParseTag("R100", PlcDataType.Word);
        Assert.Equal("R", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.WordDevice, t.DeviceKind);
        Assert.Equal(100, t.Offset);
    }

    [Fact]
    public void SD10_SpecialDataRegister()
    {
        var t = ParseTag("SD10", PlcDataType.Word);
        Assert.Equal("SD", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.WordDevice, t.DeviceKind);
        Assert.Equal(10, t.Offset);
    }

    // ===== BIT-IN-WORD =====

    [Fact]
    public void D100_5_BitInWord_ParsesBitAndOffset()
    {
        var t = ParseTag("D100.5", PlcDataType.Bool);
        Assert.Equal("D", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.WordDevice, t.DeviceKind);
        Assert.Equal(100, t.Offset);
        Assert.Equal(5, t.Bit);
        Assert.True(t.IsBitInWord);
        Assert.Equal(1, t.WordSize);
    }

    [Fact]
    public void D100_0_BitInWord_Bit0()
    {
        var t = ParseTag("D100.0", PlcDataType.Bool);
        Assert.Equal(0, t.Bit);
        Assert.True(t.IsBitInWord);
    }

    [Fact]
    public void D100_15_BitInWord_Bit15_MaxValid()
    {
        var t = ParseTag("D100.15", PlcDataType.Bool);
        Assert.Equal(15, t.Bit);
        Assert.True(t.IsBitInWord);
    }

    [Fact]
    public void D100_16_BitInWord_OutOfRange_Throws()
    {
        var tag = new PlcTag { Address = "D100.16", DataType = PlcDataType.Bool };
        Assert.Throws<FormatException>(() => PlcAddressParser.Parse(tag));
    }

    // ===== BIT DEVICES decimal =====

    [Fact]
    public void M50_InternalRelay()
    {
        var t = ParseTag("M50", PlcDataType.Bool);
        Assert.Equal("M", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.BitDevice, t.DeviceKind);
        Assert.Equal(10, t.OffsetRadix);
        Assert.Equal(50, t.Offset);
        Assert.False(t.IsBitInWord);
        Assert.Equal(1, t.WordSize);
    }

    [Fact]
    public void SM200_SpecialInternalRelay()
    {
        var t = ParseTag("SM200", PlcDataType.Bool);
        Assert.Equal("SM", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.BitDevice, t.DeviceKind);
        Assert.Equal(10, t.OffsetRadix);
        Assert.Equal(200, t.Offset);
    }

    // ===== BIT DEVICES HEX =====

    [Fact]
    public void X1A_HexOffset_26Decimal()
    {
        var t = ParseTag("X1A", PlcDataType.Bool);
        Assert.Equal("X", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.BitDevice, t.DeviceKind);
        Assert.Equal(16, t.OffsetRadix);
        Assert.Equal(0x1A, t.Offset); // 26 decimal
    }

    [Fact]
    public void Y20_HexOffset_32Decimal()
    {
        var t = ParseTag("Y20", PlcDataType.Bool);
        Assert.Equal("Y", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.BitDevice, t.DeviceKind);
        Assert.Equal(16, t.OffsetRadix);
        Assert.Equal(0x20, t.Offset); // 32 decimal
    }

    [Fact]
    public void B1F0_LinkRelay_HexOffset()
    {
        var t = ParseTag("B1F0", PlcDataType.Bool);
        Assert.Equal("B", t.DeviceCode);
        Assert.Equal(PlcDeviceKind.BitDevice, t.DeviceKind);
        Assert.Equal(16, t.OffsetRadix);
        Assert.Equal(0x1F0, t.Offset); // 496 decimal
    }

    // ===== WORD SIZE =====

    [Fact]
    public void D_DInt_WordSize2()
    {
        var t = ParseTag("D200", PlcDataType.DInt);
        Assert.Equal(2, t.WordSize);
    }

    [Fact]
    public void D_Real_WordSize2()
    {
        var t = ParseTag("D200", PlcDataType.Real);
        Assert.Equal(2, t.WordSize);
    }

    [Fact]
    public void D_LReal_WordSize4()
    {
        var t = ParseTag("D300", PlcDataType.LReal);
        Assert.Equal(4, t.WordSize);
    }

    [Fact]
    public void D_LInt_WordSize4()
    {
        var t = ParseTag("D300", PlcDataType.LInt);
        Assert.Equal(4, t.WordSize);
    }

    [Fact]
    public void D_String_Length16_WordSize8()
    {
        // (16 + 1) / 2 = 8 words
        var t = ParseTag("D8020", PlcDataType.String, strLen: 16);
        Assert.Equal(8, t.WordSize);
    }

    [Fact]
    public void D_String_Length17_WordSize9()
    {
        // (17 + 1) / 2 = 9 words (odd length rounds up)
        var t = ParseTag("D8020", PlcDataType.String, strLen: 17);
        Assert.Equal(9, t.WordSize);
    }

    [Fact]
    public void D_String_Length1_WordSize1()
    {
        // (1 + 1) / 2 = 1 word
        var t = ParseTag("D100", PlcDataType.String, strLen: 1);
        Assert.Equal(1, t.WordSize);
    }

    // ===== THỰC TẾ TỪ TAGS.JSON =====

    [Fact]
    public void D162_Part_Word()
    {
        var t = ParseTag("D162", PlcDataType.Word);
        Assert.Equal(162, t.Offset);
        Assert.Equal("D", t.DeviceCode);
        Assert.Equal(1, t.WordSize);
    }

    [Fact]
    public void D8020_PartName_String16()
    {
        var t = ParseTag("D8020", PlcDataType.String, strLen: 16);
        Assert.Equal(8020, t.Offset);
        Assert.Equal(8, t.WordSize); // 16 chars = 8 words
    }

    [Fact]
    public void D1953_StepRun_Int()
    {
        var t = ParseTag("D1953", PlcDataType.Int);
        Assert.Equal(1953, t.Offset);
        Assert.Equal(1, t.WordSize);
    }

    // ===== LỖI =====

    [Fact]
    public void EmptyAddress_ThrowsFormatException()
    {
        var tag = new PlcTag { Address = "", DataType = PlcDataType.Word };
        Assert.Throws<FormatException>(() => PlcAddressParser.Parse(tag));
    }

    [Fact]
    public void WhitespaceAddress_ThrowsFormatException()
    {
        var tag = new PlcTag { Address = "   ", DataType = PlcDataType.Word };
        Assert.Throws<FormatException>(() => PlcAddressParser.Parse(tag));
    }

    [Fact]
    public void NumericOnlyAddress_ThrowsFormatException()
    {
        var tag = new PlcTag { Address = "123", DataType = PlcDataType.Word };
        Assert.Throws<FormatException>(() => PlcAddressParser.Parse(tag));
    }

    [Fact]
    public void UnknownDevice_Z_ThrowsFormatException()
    {
        var tag = new PlcTag { Address = "Z100", DataType = PlcDataType.Word };
        Assert.Throws<FormatException>(() => PlcAddressParser.Parse(tag));
    }

    [Fact]
    public void BitSuffixOnBitDevice_ThrowsFormatException()
    {
        // Bit suffix chỉ hợp lệ trên word device
        var tag = new PlcTag { Address = "M50.3", DataType = PlcDataType.Bool };
        Assert.Throws<FormatException>(() => PlcAddressParser.Parse(tag));
    }

    [Fact]
    public void NullAddress_ThrowsFormatException()
    {
        var tag = new PlcTag { Address = null!, DataType = PlcDataType.Word };
        Assert.Throws<FormatException>(() => PlcAddressParser.Parse(tag));
    }
}
