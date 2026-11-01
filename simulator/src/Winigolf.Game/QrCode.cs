using System.Text;

namespace Winigolf.Game;

internal sealed record QrCodeSymbol(bool[,] Modules, bool[,] FunctionModules);

// Small byte-mode QR encoder for the short LAN URLs shown by the lobby.
// Version 4-L holds up to 78 UTF-8 bytes and needs no additional package.
internal static class QrCode
{
    private const int Version = 4;
    private const int Size = Version * 4 + 17;
    private const int DataCodewords = 80;
    private const int ErrorCodewords = 20;

    public static QrCodeSymbol Encode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > 78) throw new ArgumentException("The lobby URL is too long for the QR code.", nameof(value));

        var data = EncodeData(bytes);
        var codewords = data.Concat(ErrorCorrection(data, ErrorCodewords)).ToArray();
        var modules = new bool[Size, Size];
        var function = new bool[Size, Size];

        DrawFinder(modules, function, 3, 3);
        DrawFinder(modules, function, Size - 4, 3);
        DrawFinder(modules, function, 3, Size - 4);
        DrawAlignment(modules, function, 26, 26);
        for (var i = 8; i < Size - 8; i++)
        {
            SetFunction(modules, function, i, 6, i % 2 == 0);
            SetFunction(modules, function, 6, i, i % 2 == 0);
        }
        ReserveFormat(modules, function);
        DrawCodewords(modules, function, codewords);
        DrawFormat(modules, function, 0);
        return new QrCodeSymbol(modules, function);
    }

    private static byte[] EncodeData(byte[] bytes)
    {
        var bits = new List<bool>(DataCodewords * 8);
        AppendBits(bits, 0b0100, 4); // byte mode
        AppendBits(bits, bytes.Length, 8);
        foreach (var value in bytes) AppendBits(bits, value, 8);
        for (var i = 0; i < Math.Min(4, DataCodewords * 8 - bits.Count); i++) bits.Add(false);
        while (bits.Count % 8 != 0) bits.Add(false);

        var result = new List<byte>(DataCodewords);
        for (var i = 0; i < bits.Count; i += 8)
        {
            byte value = 0;
            for (var j = 0; j < 8; j++) value = (byte)((value << 1) | (bits[i + j] ? 1 : 0));
            result.Add(value);
        }
        for (var pad = 0xEC; result.Count < DataCodewords; pad ^= 0xEC ^ 0x11) result.Add((byte)pad);
        return result.ToArray();
    }

    private static void AppendBits(List<bool> bits, int value, int count)
    {
        for (var i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
    }

    private static byte[] ErrorCorrection(byte[] data, int degree)
    {
        var generator = new byte[] { 1 };
        byte root = 1;
        for (var i = 0; i < degree; i++)
        {
            var next = new byte[generator.Length + 1];
            for (var j = 0; j < generator.Length; j++)
            {
                next[j] ^= generator[j];
                next[j + 1] ^= Multiply(generator[j], root);
            }
            generator = next;
            root = Multiply(root, 2);
        }

        var work = new byte[data.Length + degree];
        data.CopyTo(work, 0);
        for (var i = 0; i < data.Length; i++)
        {
            var factor = work[i];
            for (var j = 0; j < degree; j++) work[i + j + 1] ^= Multiply(generator[j + 1], factor);
        }
        return work[^degree..];
    }

    private static byte Multiply(byte x, byte y)
    {
        byte result = 0;
        for (var i = 0; i < 8; i++)
        {
            if ((y & 1) != 0) result ^= x;
            var carry = (x & 0x80) != 0;
            x <<= 1;
            if (carry) x ^= 0x1D;
            y >>= 1;
        }
        return result;
    }

    private static void DrawFinder(bool[,] modules, bool[,] function, int centerX, int centerY)
    {
        for (var dy = -4; dy <= 4; dy++)
        for (var dx = -4; dx <= 4; dx++)
        {
            var x = centerX + dx; var y = centerY + dy;
            if ((uint)x >= Size || (uint)y >= Size) continue;
            var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
            SetFunction(modules, function, x, y, distance != 2 && distance <= 3);
        }
    }

    private static void DrawAlignment(bool[,] modules, bool[,] function, int centerX, int centerY)
    {
        for (var dy = -2; dy <= 2; dy++)
        for (var dx = -2; dx <= 2; dx++)
        {
            var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
            SetFunction(modules, function, centerX + dx, centerY + dy, distance == 2 || distance == 0);
        }
    }

    private static void ReserveFormat(bool[,] modules, bool[,] function)
    {
        for (var i = 0; i <= 5; i++) SetFunction(modules, function, 8, i, false);
        SetFunction(modules, function, 8, 7, false);
        SetFunction(modules, function, 8, 8, false);
        SetFunction(modules, function, 7, 8, false);
        for (var i = 9; i < 15; i++) SetFunction(modules, function, 14 - i, 8, false);
        for (var i = 0; i < 8; i++) SetFunction(modules, function, Size - 1 - i, 8, false);
        for (var i = 8; i < 15; i++) SetFunction(modules, function, 8, Size - 15 + i, false);
        SetFunction(modules, function, 8, Size - 8, true);
    }

    private static void DrawFormat(bool[,] modules, bool[,] function, int mask)
    {
        var data = (1 << 3) | mask; // error correction level L
        var remainder = data;
        for (var i = 0; i < 10; i++) remainder = (remainder << 1) ^ (((remainder >> 9) & 1) * 0x537);
        var bits = ((data << 10) | remainder) ^ 0x5412;
        for (var i = 0; i <= 5; i++) SetFunction(modules, function, 8, i, GetBit(bits, i));
        SetFunction(modules, function, 8, 7, GetBit(bits, 6));
        SetFunction(modules, function, 8, 8, GetBit(bits, 7));
        SetFunction(modules, function, 7, 8, GetBit(bits, 8));
        for (var i = 9; i < 15; i++) SetFunction(modules, function, 14 - i, 8, GetBit(bits, i));
        for (var i = 0; i < 8; i++) SetFunction(modules, function, Size - 1 - i, 8, GetBit(bits, i));
        for (var i = 8; i < 15; i++) SetFunction(modules, function, 8, Size - 15 + i, GetBit(bits, i));
        SetFunction(modules, function, 8, Size - 8, true);
    }

    private static bool GetBit(int value, int index) => ((value >> index) & 1) != 0;

    private static void DrawCodewords(bool[,] modules, bool[,] function, byte[] codewords)
    {
        var bitIndex = 0;
        var upward = true;
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (var vert = 0; vert < Size; vert++)
            for (var offset = 0; offset < 2; offset++)
            {
                var x = right - offset;
                var y = upward ? Size - 1 - vert : vert;
                if (function[y, x]) continue;
                var value = bitIndex < codewords.Length * 8 &&
                    ((codewords[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                modules[y, x] = value ^ ((x + y) % 2 == 0); // mask pattern 0
                bitIndex++;
            }
            upward = !upward;
        }
    }

    private static void SetFunction(bool[,] modules, bool[,] function, int x, int y, bool dark)
    {
        modules[y, x] = dark;
        function[y, x] = true;
    }
}
