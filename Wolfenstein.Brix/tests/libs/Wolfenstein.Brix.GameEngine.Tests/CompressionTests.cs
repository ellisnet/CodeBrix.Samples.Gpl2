using System;
using SilverAssertions;
using Wolfenstein.Brix.GameEngine.Assets;
using Xunit;

namespace Wolfenstein.Brix.GameEngine.Tests;

// Golden tests for the three data-file decompression schemes, using
// small synthetic streams whose expansions were worked out by hand
// from the format definitions.
public class CompressionTests
{
    [Fact]
    public void Carmack_literal_words_pass_through()
    {
        //Arrange - size 4, then two plain words
        var source = new byte[] { 0x04, 0x00, 0x01, 0x00, 0x02, 0x00 };

        //Act
        var output = Compression.CarmackDecode(source);

        //Assert
        output.AsSpan().SequenceEqual(new byte[] { 0x01, 0x00, 0x02, 0x00 }).Should().BeTrue();
    }

    [Fact]
    public void Carmack_near_pointer_copies_recent_words()
    {
        //Arrange - words 1,2 then a near pointer copying both again
        var source = new byte[]
        {
            0x08, 0x00,
            0x01, 0x00,
            0x02, 0x00,
            0x02, 0xA7, 0x02,
        };

        //Act
        var output = Compression.CarmackDecode(source);

        //Assert
        output.AsSpan().SequenceEqual(new byte[] { 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x02, 0x00 }).Should().BeTrue();
    }

    [Fact]
    public void Carmack_far_pointer_copies_from_the_start_of_the_output()
    {
        //Arrange - words 3,4 then a far pointer back to word 0
        var source = new byte[]
        {
            0x08, 0x00,
            0x03, 0x00,
            0x04, 0x00,
            0x02, 0xA8, 0x00, 0x00,
        };

        //Act
        var output = Compression.CarmackDecode(source);

        //Assert
        output.AsSpan().SequenceEqual(new byte[] { 0x03, 0x00, 0x04, 0x00, 0x03, 0x00, 0x04, 0x00 }).Should().BeTrue();
    }

    [Fact]
    public void Carmack_escaped_literal_reconstructs_a_marker_valued_word()
    {
        //Arrange - an escaped word whose high byte is the near marker
        var source = new byte[] { 0x02, 0x00, 0x00, 0xA7, 0x34 };

        //Act
        var output = Compression.CarmackDecode(source);

        //Assert
        output.AsSpan().SequenceEqual(new byte[] { 0x34, 0xA7 }).Should().BeTrue();
    }

    [Fact]
    public void Rlew_expands_tagged_runs_and_passes_literals()
    {
        //Arrange - tag 0xABCD: one literal word then a run of three
        var source = new byte[]
        {
            0x08, 0x00,
            0x77, 0x66,
            0xCD, 0xAB, 0x03, 0x00, 0x11, 0x22,
        };

        //Act
        var output = Compression.RlewDecode(source, 0xABCD);

        //Assert
        output.AsSpan().SequenceEqual(new byte[] { 0x77, 0x66, 0x11, 0x22, 0x11, 0x22, 0x11, 0x22 }).Should().BeTrue();
    }

    [Fact]
    public void Huffman_walks_the_dictionary_from_the_head_node()
    {
        //Arrange - head node 254: bit0 emits 'A', bit1 walks to node 0;
        // node 0: bit0 emits 'B', bit1 emits 'C'. The stream encodes
        // "ABC" as bits 0, 10, 11 (LSB first) = 0b00011010.
        var dictionary = new ushort[510];
        dictionary[254 * 2] = 0x41;
        dictionary[254 * 2 + 1] = 256;
        dictionary[0] = 0x42;
        dictionary[1] = 0x43;
        var source = new byte[] { 0x1A };

        //Act
        var output = Compression.HuffmanExpand(source, 3, dictionary);

        //Assert
        output.AsSpan().SequenceEqual(new byte[] { 0x41, 0x42, 0x43 }).Should().BeTrue();
    }

    [Fact]
    public void Huffman_throws_when_the_stream_is_too_short()
    {
        //Arrange - the single byte cannot produce five output bytes
        var dictionary = new ushort[510];
        dictionary[254 * 2] = 0x41;
        dictionary[254 * 2 + 1] = 0x42;
        var source = new byte[] { 0x00 };

        //Act + Assert
        Assert.Throws<InvalidWolfDataException>(() => Compression.HuffmanExpand(source, 10, dictionary));
    }
}
