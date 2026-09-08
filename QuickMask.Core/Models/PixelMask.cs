using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SkiaSharp;

namespace QuickMask.Core.Models;

/// <summary>Bit-packed pixel mask backed by 64-bit words.</summary>
public sealed class PixelMask
{
    private readonly ulong[] _words;

    public PixelMask(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        Length = length;
        _words = new ulong[(length + 63) >> 6];
    }

    public int Length { get; }

    public Span<ulong> Words => _words;

    public bool this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_words[index >> 6] & (1UL << index)) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ref var word = ref _words[index >> 6];
            var bit = 1UL << index;

            if (value) word |= bit;
            else word &= ~bit;
        }
    }

    public void Clear() => _words.AsSpan().Clear();

    /// <summary>this |= other</summary>
    public void Or(PixelMask other)
    {
        ValidateSameLength(this, other);
        Combine(other._words, or: true);
    }

    /// <summary>this &amp;= ~other</summary>
    public void AndNot(PixelMask other)
    {
        ValidateSameLength(this, other);
        Combine(other._words, or: false);
    }

    private void Combine(ulong[] other, bool or)
    {
        var words = _words.AsSpan();
        var otherWords = other.AsSpan();
        var index = 0;

        if (Vector256.IsHardwareAccelerated)
        {
            var left = MemoryMarshal.Cast<ulong, Vector256<byte>>(words);
            var right = MemoryMarshal.Cast<ulong, Vector256<byte>>(otherWords);
            var vectors = left.Length;

            for (; index < vectors; index++)
                left[index] = or ? Vector256.BitwiseOr(left[index], right[index]) : Vector256.AndNot(left[index], right[index]);

            index = vectors * (Vector256<byte>.Count / sizeof(ulong));
        }

        for (; index < words.Length; index++)
            words[index] = or ? words[index] | otherWords[index] : words[index] & ~otherWords[index];
    }

    public int PopCount()
    {
        var count = 0;

        foreach (var word in _words) count += BitOperations.PopCount(word);

        return count;
    }

    public int CountSetBits(int startIndex, int length)
    {
        if (startIndex < 0 || length < 0 || startIndex + length > Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex));

        var words = _words.AsSpan();
        var wordIndex = startIndex >> 6;
        var offset = startIndex & 63;
        var remaining = length;
        var count = 0;

        while (remaining > 0)
        {
            if (offset == 0 && remaining >= 64)
            {
                count += BitOperations.PopCount(words[wordIndex++]);
                remaining -= 64;
                continue;
            }

            var take = Math.Min(64 - offset, remaining);
            var mask = take == 64 ? ulong.MaxValue : (1UL << take) - 1;

            count += BitOperations.PopCount((words[wordIndex++] >> offset) & mask);
            remaining -= take;
            offset = 0;
        }

        return count;
    }

    public void WriteColors(Span<SKColor> destination, int startIndex, SKColor onColor, SKColor offColor)
    {
        if (startIndex < 0 || destination.Length > Length - startIndex)
            throw new ArgumentOutOfRangeException(nameof(destination));

        var wordIndex = startIndex >> 6;
        var bit = startIndex & 63;
        var written = 0;

        while (written < destination.Length)
        {
            var word = _words[wordIndex++] >> bit;
            var count = Math.Min(64 - bit, destination.Length - written);

            for (var i = 0; i < count; i++)
            {
                destination[written++] = (word & 1UL) != 0 ? onColor : offColor;
                word >>= 1;
            }

            bit = 0;
        }
    }

    private static void ValidateSameLength(PixelMask first, PixelMask second)
    {
        if (first.Length != second.Length)
            throw new ArgumentException("Pixel masks must have the same length.", nameof(second));
    }
}
