using System.Text;
using System.Xml.Linq;

namespace SetCodePage.Tests;

public sealed class ManifestTransformerTests
{
    private static readonly XNamespace AssemblyV1 = "urn:schemas-microsoft-com:asm.v1";
    private static readonly XNamespace WindowsSettings2019 =
        "http://schemas.microsoft.com/SMI/2019/WindowsSettings";

    [Fact]
    public void Create_builds_valid_application_manifest()
    {
        var result = ManifestTransformer.Create("旧程序.exe", "zh-CN");
        var document = Parse(result.Data);

        Assert.Equal(ManifestChange.Created, result.Change);
        Assert.Equal(AssemblyV1 + "assembly", document.Root!.Name);
        Assert.Equal(
            "zh-CN",
            document.Descendants(WindowsSettings2019 + "activeCodePage").Single().Value);
        Assert.Equal(
            "SetCodePage.Application",
            document.Descendants(AssemblyV1 + "assemblyIdentity").Single().Attribute("name")!.Value);
    }

    [Fact]
    public void Transform_adds_code_page_and_preserves_existing_settings()
    {
        var source = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0"
                      xmlns:asmv3="urn:schemas-microsoft-com:asm.v3">
              <assemblyIdentity type="win32" name="Legacy.App" version="1.2.3.4" />
              <description>Existing description</description>
              <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
                <security>
                  <requestedPrivileges>
                    <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
                  </requestedPrivileges>
                </security>
              </trustInfo>
            </assembly>
            """;

        var result = ManifestTransformer.Transform(Encoding.UTF8.GetBytes(source), "zh-CN");
        var document = Parse(result.Data);

        Assert.Equal(ManifestChange.Added, result.Change);
        Assert.Equal("Existing description", document.Descendants(AssemblyV1 + "description").Single().Value);
        Assert.Equal(
            "requireAdministrator",
            document.Descendants().Single(element => element.Name.LocalName == "requestedExecutionLevel")
                .Attribute("level")!.Value);
        Assert.Equal(
            "zh-CN",
            document.Descendants(WindowsSettings2019 + "activeCodePage").Single().Value);
    }

    [Fact]
    public void Transform_updates_existing_code_page_and_removes_duplicates()
    {
        var source = """
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0"
                      xmlns:asmv3="urn:schemas-microsoft-com:asm.v3">
              <asmv3:application>
                <asmv3:windowsSettings xmlns:ws="http://schemas.microsoft.com/SMI/2019/WindowsSettings">
                  <ws:activeCodePage>UTF-8</ws:activeCodePage>
                  <ws:activeCodePage>ja-JP</ws:activeCodePage>
                </asmv3:windowsSettings>
              </asmv3:application>
            </assembly>
            """;

        var result = ManifestTransformer.Transform(Encoding.UTF8.GetBytes(source), "zh-CN");
        var elements = Parse(result.Data).Descendants(WindowsSettings2019 + "activeCodePage").ToList();

        Assert.Equal(ManifestChange.Updated, result.Change);
        Assert.Single(elements);
        Assert.Equal("zh-CN", elements[0].Value);
    }

    [Fact]
    public void Transform_reuses_assembly_v1_application_container()
    {
        var source = """
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
              <application>
                <windowsSettings>
                  <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
                </windowsSettings>
              </application>
            </assembly>
            """;

        var result = ManifestTransformer.Transform(Encoding.UTF8.GetBytes(source), "zh-CN");
        var document = Parse(result.Data);

        Assert.Equal(ManifestChange.Added, result.Change);
        Assert.Single(document.Root!.Elements(AssemblyV1 + "application"));
        Assert.DoesNotContain(document.Root.Elements(), element =>
            element.Name.LocalName == "application" && element.Name.Namespace != AssemblyV1);
        Assert.Equal(
            "zh-CN",
            document.Descendants(WindowsSettings2019 + "activeCodePage").Single().Value);
    }

    [Fact]
    public void Transform_creates_assembly_v1_windows_settings_without_changing_its_namespace()
    {
        var source = """
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
              <application />
            </assembly>
            """;

        var result = ManifestTransformer.Transform(Encoding.UTF8.GetBytes(source), "zh-CN");
        var document = Parse(result.Data);

        var application = document.Root!.Element(AssemblyV1 + "application");
        var windowsSettings = application!.Element(AssemblyV1 + "windowsSettings");
        Assert.NotNull(windowsSettings);
        Assert.Equal(
            "zh-CN",
            windowsSettings.Element(WindowsSettings2019 + "activeCodePage")!.Value);
    }

    [Fact]
    public void Transform_returns_original_data_when_value_is_unchanged()
    {
        var source = """
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0"
                      xmlns:asmv3="urn:schemas-microsoft-com:asm.v3">
              <asmv3:application>
                <asmv3:windowsSettings>
                  <activeCodePage xmlns="http://schemas.microsoft.com/SMI/2019/WindowsSettings">zh-CN</activeCodePage>
                </asmv3:windowsSettings>
              </asmv3:application>
            </assembly>
            """;
        var data = Encoding.UTF8.GetBytes(source);

        var result = ManifestTransformer.Transform(data, "ZH-cn");

        Assert.Equal(ManifestChange.Unchanged, result.Change);
        Assert.Same(data, result.Data);
    }

    [Fact]
    public void Transform_accepts_utf16_manifest_with_trailing_null()
    {
        var source = """
            <?xml version="1.0" encoding="UTF-16"?>
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0" />
            """;
        var bytes = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes(source))
            .Concat([(byte)0, (byte)0])
            .ToArray();

        var result = ManifestTransformer.Transform(bytes, "zh-CN");

        Assert.Equal(ManifestChange.Added, result.Change);
        Assert.Equal(
            "zh-CN",
            Parse(result.Data).Descendants(WindowsSettings2019 + "activeCodePage").Single().Value);
    }

    private static XDocument Parse(byte[] data) =>
        XDocument.Parse(Encoding.UTF8.GetString(data));
}
