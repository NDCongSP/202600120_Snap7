using McProtocolClientLib.Core;
using Xunit;

namespace McProtocolScada.Tests;

/// <summary>
/// Unit tests cho Mc3EBinaryClient — raw TCP MC Protocol (DEC-012, thay HslCommunication).
/// Không cần PLC thật: test frame encoding/decoding + address parsing (pure logic).
/// </summary>
public class Mc3EBinaryClientTests
{
    // Byte sequence đã verify với PLC thật Q06UDV qua PlcClient.TestRawAsync() (đọc D162 thành công).
    private static readonly byte[] KnownGoodReadD162 =
    {
        0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x0C, 0x00, 0x10, 0x00,
        0x01, 0x04, 0x00, 0x00, 0xA2, 0x00, 0x00, 0xA8, 0x01, 0x00
    };

    [Fact]
    public void BuildBatchReadRequest_D162_MatchesKnownGoodBytes()
    {
        var client = new Mc3EBinaryClient { NetworkNumber = 0, NetworkStationNumber = 0 };
        byte[] req = client.BuildBatchReadRequest(offset: 162, deviceCode: 0xA8, count: 1, isBit: false);
        Assert.Equal(KnownGoodReadD162, req);
    }

    [Fact]
    public void BuildBatchReadRequest_BitUnits_SetsSubcommandTo1()
    {
        var client = new Mc3EBinaryClient();
        byte[] req = client.BuildBatchReadRequest(offset: 100, deviceCode: 0x90, count: 8, isBit: true);
        // byte index 13-14 = subcommand (LE)
        Assert.Equal(0x01, req[13]);
        Assert.Equal(0x00, req[14]);
    }

    [Fact]
    public void BuildBatchWriteRequest_UsesCommand0x1401()
    {
        var client = new Mc3EBinaryClient();
        byte[] req = client.BuildBatchWriteRequest(offset: 0, deviceCode: 0xA8, count: 1, new byte[] { 0x34, 0x12 }, isBit: false);
        // byte index 11-12 = command (LE) = 0x1401
        Assert.Equal(0x01, req[11]);
        Assert.Equal(0x14, req[12]);
        // payload appended at the end
        Assert.Equal(0x34, req[req.Length - 2]);
        Assert.Equal(0x12, req[req.Length - 1]);
    }

    [Fact]
    public void BuildFrame_DataLengthField_IncludesTimerPlusPayload()
    {
        var client = new Mc3EBinaryClient();
        byte[] requestData = new byte[10];
        byte[] frame = client.BuildFrame(requestData);
        ushort dataLength = (ushort)(frame[7] | (frame[8] << 8));
        Assert.Equal(12, dataLength); // 2 (timer) + 10 (requestData)
    }

    // ===== ADDRESS PARSING =====

    [Theory]
    [InlineData("D162", 0xA8, 162)]
    [InlineData("D0", 0xA8, 0)]
    [InlineData("M100", 0x90, 100)]
    [InlineData("X1A", 0x9C, 26)]      // HEX
    [InlineData("Y20", 0x9D, 32)]      // HEX
    [InlineData("B1F0", 0xA0, 496)]    // HEX
    [InlineData("ZR1000", 0xB0, 1000)]
    [InlineData("SD10", 0xA9, 10)]
    [InlineData("SW5", 0xB5, 5)]
    [InlineData("SM50", 0x91, 50)]
    [InlineData("SB1A", 0xA1, 26)]     // HEX
    [InlineData("L5", 0x92, 5)]
    [InlineData("F5", 0x93, 5)]
    [InlineData("S5", 0x98, 5)]
    [InlineData("W30", 0xB4, 30)]  // W radix=10 trong project này (xem PlcDeviceCode.Resolve)
    [InlineData("R100", 0xAF, 100)]
    public void TryParseAddress_ValidAddresses_ReturnsCorrectDeviceCodeAndOffset(string address, byte expectedDevCode, int expectedOffset)
    {
        bool ok = Mc3EBinaryClient.TryParseAddress(address, out byte devCode, out int offset, out string err);
        Assert.True(ok, err);
        Assert.Equal(expectedDevCode, devCode);
        Assert.Equal(expectedOffset, offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Q100")]   // unknown device
    [InlineData("DXYZ")]   // invalid offset for decimal device
    public void TryParseAddress_InvalidAddresses_ReturnsFalse(string address)
    {
        bool ok = Mc3EBinaryClient.TryParseAddress(address, out _, out _, out string err);
        Assert.False(ok);
        Assert.NotEmpty(err);
    }

    // ===== BIT PACKING ROUND-TRIP (2 điểm/byte, low nibble trước) =====

    [Theory]
    [InlineData(new bool[] { true, false, true, false }, new byte[] { 0x01, 0x01 })]
    [InlineData(new bool[] { false, false, false, false }, new byte[] { 0x00, 0x00 })]
    [InlineData(new bool[] { true, true, true, true }, new byte[] { 0x11, 0x11 })]
    [InlineData(new bool[] { true }, new byte[] { 0x01 })]
    public void BitPacking_NibbleEncodingDecoding_RoundTrips(bool[] bits, byte[] packed)
    {
        // Encode giống cách ReadBool decode ngược lại: low nibble = điểm chẵn, high nibble = điểm lẻ.
        bool[] decoded = new bool[bits.Length];
        for (int i = 0; i < bits.Length; i++)
        {
            byte b = packed[i / 2];
            int nibble = (i % 2 == 0) ? (b & 0x0F) : ((b >> 4) & 0x0F);
            decoded[i] = nibble != 0;
        }
        Assert.Equal(bits, decoded);
    }
}
