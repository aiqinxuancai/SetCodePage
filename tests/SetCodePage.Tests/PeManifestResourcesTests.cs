namespace SetCodePage.Tests;

public sealed class PeManifestResourcesTests
{
    [Fact]
    public void Write_can_create_and_then_update_manifest_resource()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var sourceAssembly = typeof(PeManifestResourcesTests).Assembly.Location;
        var temporaryFile = Path.Combine(
            Path.GetTempPath(),
            $"SetCodePage-{Guid.NewGuid():N}.dll");

        File.Copy(sourceAssembly, temporaryFile);

        try
        {
            var initial = ManifestTransformer.Create("fixture.dll", "zh-CN");
            PeManifestResources.Write(
                temporaryFile,
                [new ManifestResource(0, initial.Data)]);

            var createdResources = PeManifestResources.Read(temporaryFile);
            Assert.Single(createdResources);

            var updated = ManifestTransformer.Transform(createdResources[0].Data, "ja-JP");
            PeManifestResources.Write(
                temporaryFile,
                [new ManifestResource(createdResources[0].Language, updated.Data)]);

            var finalResources = PeManifestResources.Read(temporaryFile);
            var finalManifest = System.Text.Encoding.UTF8.GetString(finalResources.Single().Data);
            Assert.Contains(">ja-JP</", finalManifest, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }
}
