using System.Collections;

namespace QuickMask.Core.Utils;

public static class BitArrayUtils
{
    public static int GetCount(BitArray bitArray, bool value)
    {
        var count = 0;
        for (var i = 0; i < bitArray.Length; i++)
            if (bitArray[i] == value) count++;
        return count;
    }

    public static bool Equals(BitArray first, BitArray second)
    {
        if (first.Length != second.Length) return false;
        for (var i = 0; i < first.Length; i++)
            if (first[i] != second[i]) return false;
        return true;
    }

    public static void Merge(ref BitArray target, BitArray source, bool remove = false)
    {
        if (target.Length != source.Length)
            throw new ArgumentException("Bit arrays must have the same length.", nameof(source));

        if (!remove)
        {
            target.Or(source);
            return;
        }

        var inverse = (BitArray)source.Clone();
        inverse.Not();
        target.And(inverse);
    }
}
