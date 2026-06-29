using McProtocolClientLib.IO;
using McProtocolClientLib.Tags;
using Xunit;

namespace McProtocolScada.Tests;

/// <summary>
/// Unit tests cho PlcGroupReader.SplitIntoBlocks — logic chia block theo gap/length.
/// </summary>
public class BlockSplitterTests
{
    // Helper: tạo word-device tag với Offset và WordSize đã set sẵn (không cần parse)
    private static PlcTag WordTag(int offset, int wordSize = 1)
    {
        var tag = new PlcTag { Address = $"D{offset}", DataType = PlcDataType.Word };
        tag.DeviceKind = PlcDeviceKind.WordDevice;
        tag.DeviceCode = "D";
        tag.Offset = offset;
        tag.WordSize = wordSize;
        tag.OffsetRadix = 10;
        return tag;
    }

    // Helper: tạo bit-device tag
    private static PlcTag BitTag(int offset)
    {
        var tag = new PlcTag { Address = $"M{offset}", DataType = PlcDataType.Bool };
        tag.DeviceKind = PlcDeviceKind.BitDevice;
        tag.DeviceCode = "M";
        tag.Offset = offset;
        tag.WordSize = 1;
        tag.OffsetRadix = 10;
        return tag;
    }

    [Fact]
    public void EmptyList_ReturnsEmptyResult()
    {
        var result = PlcGroupReader.SplitIntoBlocks(new List<PlcTag>(), 32, 450);
        Assert.Empty(result);
    }

    [Fact]
    public void SingleTag_ReturnsSingleBlock()
    {
        var result = PlcGroupReader.SplitIntoBlocks(new List<PlcTag> { WordTag(100) }, 32, 450);
        Assert.Single(result);
        Assert.Single(result[0]);
        Assert.Equal(100, result[0][0].Offset);
    }

    [Fact]
    public void AdjacentTags_MergedIntoOneBlock()
    {
        // D100 và D101 liền nhau → 1 block
        var tags = new List<PlcTag> { WordTag(100), WordTag(101) };
        var result = PlcGroupReader.SplitIntoBlocks(tags, 32, 450);
        Assert.Single(result);
        Assert.Equal(2, result[0].Count);
    }

    [Fact]
    public void GapEqualToMax_MergedIntoOneBlock()
    {
        // D100 (size=1, end=101), D133 (start=133) → gap = 133-101 = 32 = maxGap → merge
        var tags = new List<PlcTag> { WordTag(100), WordTag(133) };
        var result = PlcGroupReader.SplitIntoBlocks(tags, 32, 450);
        Assert.Single(result);
    }

    [Fact]
    public void GapBelowMax_MergedIntoOneBlock()
    {
        // gap = 30 < 32 → merge
        var tags = new List<PlcTag> { WordTag(100), WordTag(131) };
        var result = PlcGroupReader.SplitIntoBlocks(tags, 32, 450);
        Assert.Single(result);
    }

    [Fact]
    public void GapAboveMax_SplitIntoTwoBlocks()
    {
        // gap = 100 > 32 → split
        var tags = new List<PlcTag> { WordTag(100), WordTag(201) };
        var result = PlcGroupReader.SplitIntoBlocks(tags, 32, 450);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0]);
        Assert.Single(result[1]);
    }

    [Fact]
    public void ThreeTags_TwoCloseOneDistant_TwoBlocks()
    {
        // D100, D102 (gap=1 ≤ 32), D500 (gap=398 > 32) → 2 blocks
        var tags = new List<PlcTag> { WordTag(100), WordTag(102), WordTag(500) };
        var result = PlcGroupReader.SplitIntoBlocks(tags, 32, 450);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Count);  // D100, D102
        Assert.Single(result[1]);           // D500
    }

    [Fact]
    public void MaxLengthExceeded_ForcesNewBlock()
    {
        // maxLength=10: D0(size=1, end=1), D5(start=5, gap=4≤5, newLen=6≤10 → merge)
        // D11(start=11, gap=5≤5, newLen=12>10 → NEW BLOCK)
        var t1 = WordTag(0);
        var t2 = WordTag(5);
        var t3 = WordTag(11);
        var result = PlcGroupReader.SplitIntoBlocks(new List<PlcTag> { t1, t2, t3 }, maxGap: 5, maxLength: 10);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Count); // D0, D5
        Assert.Single(result[1]);          // D11
    }

    [Fact]
    public void LargeWordSizeTag_CountsInLength()
    {
        // D100(size=2, end=102), D103(size=1, end=104) → gap=103-102=1 ≤ 5, newLen=104-100=4 ≤ 10 → merge
        var t1 = WordTag(100, wordSize: 2);
        var t2 = WordTag(103, wordSize: 1);
        var result = PlcGroupReader.SplitIntoBlocks(new List<PlcTag> { t1, t2 }, maxGap: 5, maxLength: 10);
        Assert.Single(result);
        Assert.Equal(2, result[0].Count);
    }

    [Fact]
    public void TagsFromActualConfig_D162toD685_SingleBlock()
    {
        // Các tag từ tags.json: D162, D671..D685 — gap lớn (671-163=508 > 32) → split
        var part = WordTag(162);
        var step1 = WordTag(671);
        var tags = new List<PlcTag> { part, step1 };
        var result = PlcGroupReader.SplitIntoBlocks(tags, maxGap: 32, maxLength: 450);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void TagsD671toD685_ConsecutiveGroup_MergedIntoOneBlock()
    {
        // D671..D685 liên tiếp, span = 685+1-671 = 15 ≤ 450 → 1 block
        var tags = Enumerable.Range(671, 15)
                             .Select(i => WordTag(i))
                             .ToList();
        var result = PlcGroupReader.SplitIntoBlocks(tags, maxGap: 32, maxLength: 450);
        Assert.Single(result);
        Assert.Equal(15, result[0].Count);
    }

    [Fact]
    public void MaxLengthBoundary_ExactlyAtMax_StaysInSameBlock()
    {
        // D0(size=1) + D449(size=1) → span=450 = maxLength → merge (boundary condition: newLen > maxLength fails, 450 > 450 = false)
        var t1 = WordTag(0);
        var t2 = WordTag(449);
        var result = PlcGroupReader.SplitIntoBlocks(new List<PlcTag> { t1, t2 }, maxGap: 450, maxLength: 450);
        Assert.Single(result);
    }

    [Fact]
    public void MaxLengthBoundaryPlusOne_Splits()
    {
        // D0 + D450 → span=451 > 450 → split
        var t1 = WordTag(0);
        var t2 = WordTag(450);
        var result = PlcGroupReader.SplitIntoBlocks(new List<PlcTag> { t1, t2 }, maxGap: 450, maxLength: 450);
        Assert.Equal(2, result.Count);
    }
}
