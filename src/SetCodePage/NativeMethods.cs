using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SetCodePage;

internal static unsafe partial class NativeMethods
{
    internal const uint LoadLibraryAsImageResource = 0x00000020;
    internal const uint LoadLibraryAsDataFileExclusive = 0x00000040;

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint LoadLibraryEx(string fileName, nint file, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "FreeLibrary", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FreeLibrary(nint module);

    [LibraryImport("kernel32.dll", EntryPoint = "EnumResourceLanguagesW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumResourceLanguages(
        nint module,
        nint type,
        nint name,
        delegate* unmanaged[Stdcall]<nint, nint, nint, ushort, nint, int> callback,
        nint parameter);

    [LibraryImport("kernel32.dll", EntryPoint = "FindResourceW", SetLastError = true)]
    internal static partial nint FindResource(nint module, nint name, nint type);

    [LibraryImport("kernel32.dll", EntryPoint = "FindResourceExW", SetLastError = true)]
    internal static partial nint FindResourceEx(nint module, nint type, nint name, ushort language);

    [LibraryImport("kernel32.dll", EntryPoint = "SizeofResource", SetLastError = true)]
    internal static partial uint SizeofResource(nint module, nint resourceInfo);

    [LibraryImport("kernel32.dll", EntryPoint = "LoadResource", SetLastError = true)]
    internal static partial nint LoadResource(nint module, nint resourceInfo);

    [LibraryImport("kernel32.dll", EntryPoint = "LockResource")]
    internal static partial nint LockResource(nint resourceData);

    [LibraryImport("kernel32.dll", EntryPoint = "BeginUpdateResourceW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint BeginUpdateResource(
        string fileName,
        [MarshalAs(UnmanagedType.Bool)] bool deleteExistingResources);

    [LibraryImport("kernel32.dll", EntryPoint = "UpdateResourceW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UpdateResource(
        nint update,
        nint type,
        nint name,
        ushort language,
        void* data,
        uint dataSize);

    [LibraryImport("kernel32.dll", EntryPoint = "EndUpdateResourceW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EndUpdateResource(
        nint update,
        [MarshalAs(UnmanagedType.Bool)] bool discard);

    [LibraryImport("kernel32.dll", EntryPoint = "IsValidLocaleName",
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsValidLocaleName(string localeName);
}
