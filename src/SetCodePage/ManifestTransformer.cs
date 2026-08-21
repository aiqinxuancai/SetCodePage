using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SetCodePage;

internal enum ManifestChange
{
    Unchanged,
    Created,
    Added,
    Updated
}

internal sealed record ManifestTransformResult(byte[] Data, ManifestChange Change);

internal static class ManifestTransformer
{
    private static readonly XNamespace AssemblyV1 = "urn:schemas-microsoft-com:asm.v1";
    private static readonly XNamespace AssemblyV3 = "urn:schemas-microsoft-com:asm.v3";
    private static readonly XNamespace WindowsSettings2019 =
        "http://schemas.microsoft.com/SMI/2019/WindowsSettings";

    public static ManifestTransformResult Create(string executableName, string codePage)
    {
        var identityName = CreateIdentityName(executableName);
        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                AssemblyV1 + "assembly",
                new XAttribute("manifestVersion", "1.0"),
                new XAttribute(XNamespace.Xmlns + "asmv3", AssemblyV3.NamespaceName),
                new XElement(
                    AssemblyV1 + "assemblyIdentity",
                    new XAttribute("type", "win32"),
                    new XAttribute("name", identityName),
                    new XAttribute("version", "1.0.0.0")),
                CreateApplicationElement(codePage)));

        return new ManifestTransformResult(Serialize(document), ManifestChange.Created);
    }

    public static ManifestTransformResult Transform(byte[] manifestData, string codePage)
    {
        ArgumentNullException.ThrowIfNull(manifestData);

        var document = Parse(manifestData);
        var root = document.Root
            ?? throw new InvalidDataException("Manifest XML 没有根元素。");

        if (root.Name != AssemblyV1 + "assembly")
        {
            throw new InvalidDataException(
                $"Manifest 根元素必须是 {{{AssemblyV1}}}assembly，实际为 {root.Name}。");
        }

        var existingElements = document
            .Descendants(WindowsSettings2019 + "activeCodePage")
            .ToList();

        if (existingElements.Count > 0)
        {
            var changed = existingElements.Count != 1 ||
                !string.Equals(existingElements[0].Value.Trim(), codePage, StringComparison.OrdinalIgnoreCase);

            existingElements[0].Value = codePage;
            foreach (var duplicate in existingElements.Skip(1))
            {
                duplicate.Remove();
            }

            return new ManifestTransformResult(
                changed ? Serialize(document) : manifestData,
                changed ? ManifestChange.Updated : ManifestChange.Unchanged);
        }

        EnsureNamespacePrefix(root, AssemblyV3, "asmv3");

        var application = root.Elements().FirstOrDefault(IsApplicationElement);
        if (application is null)
        {
            root.Add(CreateApplicationElement(codePage));
        }
        else
        {
            var windowsSettings = application
                .Elements()
                .Where(IsWindowsSettingsElement)
                .FirstOrDefault();

            if (windowsSettings is null)
            {
                application.Add(CreateWindowsSettingsElement(codePage, application.Name.Namespace));
            }
            else
            {
                windowsSettings.Add(
                    new XElement(WindowsSettings2019 + "activeCodePage", codePage));
            }
        }

        return new ManifestTransformResult(Serialize(document), ManifestChange.Added);
    }

    private static XDocument Parse(byte[] data)
    {
        var contentLength = GetContentLength(data);
        using var stream = new MemoryStream(data, 0, contentLength, writable: false);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false
            });

        try
        {
            return XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"无法解析现有 Manifest XML：{exception.Message}", exception);
        }
    }

    private static int GetContentLength(byte[] data)
    {
        var length = data.Length;
        var isUtf16 = length >= 2 &&
            ((data[0] == 0xFF && data[1] == 0xFE) ||
             (data[0] == 0xFE && data[1] == 0xFF));

        if (isUtf16)
        {
            while (length >= 2 && data[length - 1] == 0 && data[length - 2] == 0)
            {
                length -= 2;
            }
        }
        else
        {
            while (length > 0 && data[length - 1] == 0)
            {
                length--;
            }
        }

        return length;
    }

    private static XElement CreateApplicationElement(string codePage) =>
        new(
            AssemblyV3 + "application",
            CreateWindowsSettingsElement(codePage, AssemblyV3));

    private static XElement CreateWindowsSettingsElement(
        string codePage,
        XNamespace containerNamespace)
    {
        var windowsSettings = new XElement(containerNamespace + "windowsSettings");
        if (containerNamespace == AssemblyV3)
        {
            windowsSettings.Add(new XAttribute("xmlns", WindowsSettings2019.NamespaceName));
        }

        windowsSettings.Add(new XElement(WindowsSettings2019 + "activeCodePage", codePage));
        return windowsSettings;
    }

    private static bool IsApplicationElement(XElement element) =>
        element.Name.LocalName == "application" &&
        (element.Name.Namespace == AssemblyV1 || element.Name.Namespace == AssemblyV3);

    private static bool IsWindowsSettingsElement(XElement element) =>
        element.Name.LocalName == "windowsSettings" &&
        (element.Name.Namespace == AssemblyV1 || element.Name.Namespace == AssemblyV3);

    private static void EnsureNamespacePrefix(XElement root, XNamespace xmlNamespace, string preferredPrefix)
    {
        if (root.GetPrefixOfNamespace(xmlNamespace) is not null)
        {
            return;
        }

        var prefix = preferredPrefix;
        var suffix = 2;
        while (root.GetNamespaceOfPrefix(prefix) is not null)
        {
            prefix = preferredPrefix + suffix++;
        }

        root.Add(new XAttribute(XNamespace.Xmlns + prefix, xmlNamespace.NamespaceName));
    }

    private static string CreateIdentityName(string executableName)
    {
        var fileName = Path.GetFileNameWithoutExtension(executableName);
        var builder = new StringBuilder(fileName.Length);

        foreach (var character in fileName)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '_');
        }

        var safeName = builder.ToString().Trim('.', '_', '-');
        return $"SetCodePage.{(safeName.Length == 0 ? "Application" : safeName)}";
    }

    private static byte[] Serialize(XDocument document)
    {
        var declaration = document.Declaration;
        document.Declaration = new XDeclaration(
            declaration?.Version ?? "1.0",
            "UTF-8",
            declaration?.Standalone);

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(
                   stream,
                   new XmlWriterSettings
                   {
                       Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       Indent = true,
                       NewLineChars = "\r\n",
                       NewLineHandling = NewLineHandling.Replace,
                       OmitXmlDeclaration = false
                   }))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }
}
