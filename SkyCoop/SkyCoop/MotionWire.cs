using System;
namespace SkyCoop;
public static class MotionWire
{
    public static bool IsMotion(byte[] wire, bool fromHost) =>
        TryGetPayload(wire, fromHost, out int offset, out _) &&
        BitConverter.ToInt32(wire, offset) == (fromHost ? 194 : 196);

    internal static bool TryGetPayload(byte[] wire, bool fromHost, out int offset, out int length)
    {
        offset = length = 0;
        if (wire == null || wire.Length < (fromHost ? 8 : 16)) return false;
        int lengthOffset = 0;
        if (!fromHost)
        {
            int guidLength = BitConverter.ToInt32(wire, 4);
            if (guidLength < 0 || guidLength > wire.Length - 16) return false;
            lengthOffset = 8 + guidLength;
        }
        offset = lengthOffset + 4;
        length = BitConverter.ToInt32(wire, lengthOffset);
        return length >= 4 && length <= wire.Length - offset;
    }
}
