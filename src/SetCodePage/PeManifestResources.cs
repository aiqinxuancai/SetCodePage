using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SetCodePage;

internal sealed record ManifestResource(ushort Language, byte[] Data);

internal static unsafe class PeManifestResources
{
    private const int ManifestResourceType = 24;
    private const int ApplicationManifestId = 1;

    private static readonly nint ResourceType = (nint)ManifestResourceType;
    private static readonly nint ResourceName = (nint)ApplicationManifestId;

    public static IReadOnlyList<ManifestResource> Read(string path)
    {
        var module = NativeMethods.LoadLibraryEx(
            path,
            0,
            NativeMethods.LoadLibraryAsImageResource |
            NativeMethods.LoadLibraryAsDataFileExclusive);

        if (module == 0)
        {
            throw CreateWin32Exception($"无法以资源文件方式打开 {path}");
        }

        try
        {
            if (NativeMethods.FindResource(module, ResourceName, ResourceType) == 0)
            {
                return [];
            }

            var languages = EnumerateLanguages(module);
            var resources = new List<ManifestResource>(languages.Count);
            foreach (var language in languages)
            {
                resources.Add(new ManifestResource(language, ReadResource(module, language)));
            }

            return resources;
        }
        finally
        {
            NativeMethods.FreeLibrary(module);
        }
    }

    public static void Write(string path, IReadOnlyCollection<ManifestResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.Count == 0)
        {
            return;
        }

        var update = NativeMethods.BeginUpdateResource(path, deleteExistingResources: false);
        if (update == 0)
        {
            throw CreateWin32Exception($"无法开始更新 {path} 的资源");
        }

        try
        {
            foreach (var resource in resources)
            {
                fixed (byte* data = resource.Data)
                {
                    if (!NativeMethods.UpdateResource(
                            update,
                            ResourceType,
                            ResourceName,
                            resource.Language,
                            data,
                            checked((uint)resource.Data.Length)))
                    {
                        throw CreateWin32Exception(
                            $"无法写入语言 ID 0x{resource.Language:X4} 的 Manifest 资源");
                    }
                }
            }

            var handleToCommit = update;
            update = 0;
            if (!NativeMethods.EndUpdateResource(handleToCommit, discard: false))
            {
                throw CreateWin32Exception($"无法提交 {path} 的资源更新");
            }
        }
        finally
        {
            if (update != 0)
            {
                NativeMethods.EndUpdateResource(update, discard: true);
            }
        }
    }

    private static List<ushort> EnumerateLanguages(nint module)
    {
        var languages = new List<ushort>();
        var handle = GCHandle.Alloc(languages);

        try
        {
            var succeeded = NativeMethods.EnumResourceLanguages(
                module,
                ResourceType,
                ResourceName,
                &CollectLanguage,
                GCHandle.ToIntPtr(handle));

            if (!succeeded)
            {
                throw CreateWin32Exception("无法枚举 Manifest 资源语言");
            }

            return languages;
        }
        finally
        {
            handle.Free();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CollectLanguage(
        nint module,
        nint type,
        nint name,
        ushort language,
        nint parameter)
    {
        _ = module;
        _ = type;
        _ = name;

        var languages = (List<ushort>?)GCHandle.FromIntPtr(parameter).Target;
        languages?.Add(language);
        return 1;
    }

    private static byte[] ReadResource(nint module, ushort language)
    {
        var info = NativeMethods.FindResourceEx(module, ResourceType, ResourceName, language);
        if (info == 0)
        {
            throw CreateWin32Exception(
                $"无法查找语言 ID 0x{language:X4} 的 Manifest 资源");
        }

        var size = NativeMethods.SizeofResource(module, info);
        if (size == 0)
        {
            throw CreateWin32Exception(
                $"无法读取语言 ID 0x{language:X4} 的 Manifest 资源大小");
        }

        var loaded = NativeMethods.LoadResource(module, info);
        var pointer = loaded == 0 ? 0 : NativeMethods.LockResource(loaded);
        if (pointer == 0)
        {
            throw new InvalidDataException(
                $"语言 ID 0x{language:X4} 的 Manifest 资源数据无效。");
        }

        var data = new byte[size];
        Marshal.Copy(pointer, data, 0, checked((int)size));
        return data;
    }

    private static Win32Exception CreateWin32Exception(string operation)
    {
        var error = Marshal.GetLastPInvokeError();
        return new Win32Exception(error, $"{operation}（Win32 错误 {error}）");
    }
}
