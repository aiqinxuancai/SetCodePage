using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace SetCodePage;

internal static class PeCompatibilityAnalyzer
{
    private const int ImportDescriptorSize = 20;
    private const int ImportByNameHintSize = 2;
    private const int MaximumImportNameLength = 256;

    private static readonly HashSet<string> AnsiGdiTextFunctions =
        new(StringComparer.Ordinal)
        {
            "DrawTextA",
            "DrawTextExA",
            "ExtTextOutA",
            "GrayStringA",
            "PolyTextOutA",
            "TabbedTextOutA",
            "TextOutA"
        };

    public static IReadOnlyList<string> FindAnsiGdiTextImports(string path)
    {
        try
        {
            var image = File.ReadAllBytes(path);
            using var stream = new MemoryStream(image, writable: false);
            using var peReader = new PEReader(stream);

            var headers = peReader.PEHeaders;
            var peHeader = headers.PEHeader;
            if (peHeader is null || peHeader.ImportTableDirectory.Size == 0 ||
                !TryRvaToFileOffset(headers, peHeader.ImportTableDirectory.RelativeVirtualAddress, out var descriptorOffset))
            {
                return [];
            }

            var importedNames = new List<string>();
            var isPe32Plus = peHeader.Magic == PEMagic.PE32Plus;
            var entrySize = isPe32Plus ? sizeof(ulong) : sizeof(uint);
            var ordinalFlag = isPe32Plus ? 1UL << 63 : 1UL << 31;

            while (IsRangeValid(image, descriptorOffset, ImportDescriptorSize))
            {
                var originalFirstThunk = ReadUInt32(image, descriptorOffset);
                var name = ReadUInt32(image, descriptorOffset + 12);
                var firstThunk = ReadUInt32(image, descriptorOffset + 16);
                if (originalFirstThunk == 0 && name == 0 && firstThunk == 0)
                {
                    break;
                }

                var thunkRva = originalFirstThunk != 0 ? originalFirstThunk : firstThunk;
                ReadImportNames(
                    image,
                    headers,
                    thunkRva,
                    entrySize,
                    ordinalFlag,
                    importedNames);

                descriptorOffset += ImportDescriptorSize;
            }

            return FindRelevantAnsiGdiTextImports(importedNames);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or IOException or OverflowException)
        {
            // Compatibility analysis is advisory and must not prevent manifest patching.
            return [];
        }
    }

    internal static IReadOnlyList<string> FindRelevantAnsiGdiTextImports(
        IEnumerable<string> importedNames) =>
        importedNames
            .Where(AnsiGdiTextFunctions.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void ReadImportNames(
        byte[] image,
        PEHeaders headers,
        uint thunkRva,
        int entrySize,
        ulong ordinalFlag,
        ICollection<string> importedNames)
    {
        if (thunkRva > int.MaxValue ||
            !TryRvaToFileOffset(headers, (int)thunkRva, out var thunkOffset))
        {
            return;
        }

        while (IsRangeValid(image, thunkOffset, entrySize))
        {
            var thunk = entrySize == sizeof(ulong)
                ? BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(thunkOffset, entrySize))
                : BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(thunkOffset, entrySize));

            if (thunk == 0)
            {
                break;
            }

            if ((thunk & ordinalFlag) == 0 && thunk <= int.MaxValue &&
                TryRvaToFileOffset(headers, (int)thunk, out var importByNameOffset) &&
                TryReadNullTerminatedAscii(
                    image,
                    importByNameOffset + ImportByNameHintSize,
                    out var importedName))
            {
                importedNames.Add(importedName);
            }

            thunkOffset += entrySize;
        }
    }

    private static bool TryRvaToFileOffset(PEHeaders headers, int rva, out int offset)
    {
        foreach (var section in headers.SectionHeaders)
        {
            var sectionSize = Math.Max(section.VirtualSize, section.SizeOfRawData);
            var relativeOffset = (long)rva - section.VirtualAddress;
            if (relativeOffset < 0 || relativeOffset >= sectionSize)
            {
                continue;
            }

            var fileOffset = section.PointerToRawData + relativeOffset;
            if (fileOffset is >= 0 and <= int.MaxValue)
            {
                offset = (int)fileOffset;
                return true;
            }
        }

        offset = 0;
        return false;
    }

    private static bool TryReadNullTerminatedAscii(byte[] image, int offset, out string value)
    {
        if (offset < 0 || offset >= image.Length)
        {
            value = string.Empty;
            return false;
        }

        var maximumLength = Math.Min(MaximumImportNameLength, image.Length - offset);
        var length = Array.IndexOf(image, (byte)0, offset, maximumLength) - offset;
        if (length < 0)
        {
            value = string.Empty;
            return false;
        }

        value = Encoding.ASCII.GetString(image, offset, length);
        return true;
    }

    private static uint ReadUInt32(byte[] image, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset, sizeof(uint)));

    private static bool IsRangeValid(byte[] image, int offset, int length) =>
        offset >= 0 && length >= 0 && offset <= image.Length - length;
}
