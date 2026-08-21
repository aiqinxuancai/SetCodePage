namespace SetCodePage;

internal sealed record ResourcePatch(ushort Language, byte[] Data, ManifestChange Change);

internal sealed record PatchPlan(
    string InputPath,
    string CodePage,
    bool HadManifest,
    IReadOnlyList<ResourcePatch> Resources)
{
    public bool HasChanges => Resources.Any(resource => resource.Change != ManifestChange.Unchanged);
}

internal sealed record PatchOutcome(string DestinationPath, string? BackupPath, bool FileWritten);

internal static class PatchService
{
    public static PatchPlan Analyze(string inputPath, string codePage)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException("找不到输入文件。", fullInputPath);
        }

        var resources = PeManifestResources.Read(fullInputPath);
        if (resources.Count == 0)
        {
            var created = ManifestTransformer.Create(Path.GetFileName(fullInputPath), codePage);
            return new PatchPlan(
                fullInputPath,
                codePage,
                HadManifest: false,
                [new ResourcePatch(0, created.Data, created.Change)]);
        }

        var patches = new List<ResourcePatch>(resources.Count);
        foreach (var resource in resources)
        {
            var transformed = ManifestTransformer.Transform(resource.Data, codePage);
            patches.Add(new ResourcePatch(resource.Language, transformed.Data, transformed.Change));
        }

        return new PatchPlan(fullInputPath, codePage, HadManifest: true, patches);
    }

    public static PatchOutcome Apply(
        PatchPlan plan,
        string? outputPath,
        bool noBackup,
        bool force)
    {
        var destinationPath = outputPath is null
            ? plan.InputPath
            : Path.GetFullPath(outputPath);
        var isInPlace = string.Equals(
            plan.InputPath,
            destinationPath,
            StringComparison.OrdinalIgnoreCase);

        if (!isInPlace && File.Exists(destinationPath) && !force)
        {
            throw new IOException(
                $"输出文件已存在：{destinationPath}。使用 --force 明确覆盖。");
        }

        string? backupPath = null;
        if (isInPlace && plan.HasChanges && !noBackup)
        {
            backupPath = plan.InputPath + ".bak";
            if (File.Exists(backupPath) && !force)
            {
                throw new IOException(
                    $"备份文件已存在：{backupPath}。请先移走它，或使用 --force 明确覆盖。");
            }

            File.Copy(plan.InputPath, backupPath, overwrite: force);
        }

        if (!isInPlace)
        {
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(plan.InputPath, destinationPath, overwrite: force);
        }

        if (!plan.HasChanges)
        {
            return new PatchOutcome(destinationPath, backupPath, FileWritten: !isInPlace);
        }

        var changedResources = plan.Resources
            .Where(resource => resource.Change != ManifestChange.Unchanged)
            .Select(resource => new ManifestResource(resource.Language, resource.Data))
            .ToArray();

        PeManifestResources.Write(destinationPath, changedResources);
        return new PatchOutcome(destinationPath, backupPath, FileWritten: true);
    }
}
