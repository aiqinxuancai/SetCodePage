using System.Reflection;
using Spectre.Console;

namespace SetCodePage;

internal static class ApplicationRunner
{
    public static int Run(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                WriteHelp();
                return 0;
            }

            if (options.ShowVersion)
            {
                WriteVersion();
                return 0;
            }

            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("该工具只能在 Windows 上运行。");
            }

            if (!IsValidCodePage(options.CodePage))
            {
                throw new CliUsageException(
                    $"无效的 Windows 区域名称或代码页值：{options.CodePage}");
            }

            var plan = PatchService.Analyze(options.InputPath!, options.CodePage);
            WritePlan(plan, options);

            if (options.DryRun)
            {
                AnsiConsole.MarkupLine("[yellow]仅检查：未修改任何文件。[/]");
                return 0;
            }

            var outcome = PatchService.Apply(
                plan,
                options.OutputPath,
                options.NoBackup,
                options.Force);

            WriteOutcome(plan, outcome);
            return 0;
        }
        catch (CliUsageException exception)
        {
            WriteError(exception.Message);
            AnsiConsole.MarkupLine("运行 [cyan]SetCodePage --help[/] 查看用法。");
            return 2;
        }
        catch (Exception exception)
        {
            WriteError(exception.Message);
            return 1;
        }
    }

    private static bool IsValidCodePage(string codePage) =>
        codePage.Equals("UTF-8", StringComparison.OrdinalIgnoreCase) ||
        codePage.Equals("Legacy", StringComparison.OrdinalIgnoreCase) ||
        NativeMethods.IsValidLocaleName(codePage);

    private static void WritePlan(PatchPlan plan, CliOptions options)
    {
        var action = GetActionSummary(plan);
        var destination = options.OutputPath is null
            ? plan.InputPath
            : Path.GetFullPath(options.OutputPath);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[grey]项目[/]").NoWrap())
            .AddColumn("[grey]内容[/]");

        table.AddRow("输入", Escape(plan.InputPath));
        table.AddRow("输出", Escape(destination));
        table.AddRow("代码页", $"[cyan]{Escape(plan.CodePage)}[/]");
        table.AddRow("Manifest", Escape(action));
        table.AddRow("语言资源", plan.Resources.Count.ToString());

        AnsiConsole.Write(new Rule("[bold deepskyblue1]SetCodePage[/]").LeftJustified());
        AnsiConsole.Write(table);

        if (!plan.CodePage.Equals("UTF-8", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine(
                "[yellow]注意：按进程指定非 UTF-8 区域代码页需要 Windows 11 或 Windows Server 2022 及以上版本。[/]");
        }
    }

    private static void WriteOutcome(PatchPlan plan, PatchOutcome outcome)
    {
        if (!plan.HasChanges && !outcome.FileWritten)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Manifest 已是目标代码页，无需修改。");
            return;
        }

        AnsiConsole.MarkupLine(
            $"[green]✓[/] 已写入：[link={EscapeLink(outcome.DestinationPath)}]{Escape(outcome.DestinationPath)}[/]");

        if (outcome.BackupPath is not null)
        {
            AnsiConsole.MarkupLine(
                $"[grey]  备份：[/][link={EscapeLink(outcome.BackupPath)}]{Escape(outcome.BackupPath)}[/]");
        }

        AnsiConsole.MarkupLine(
            "[yellow]资源更新会使原有 Authenticode 数字签名失效；如有签名，请重新签署输出文件。[/]");
    }

    private static string GetActionSummary(PatchPlan plan)
    {
        if (!plan.HadManifest)
        {
            return "不存在，将创建";
        }

        if (!plan.HasChanges)
        {
            return "已存在，无需修改";
        }

        return plan.Resources.Any(resource => resource.Change == ManifestChange.Updated)
            ? "已存在，将更新 activeCodePage"
            : "已存在，将添加 activeCodePage";
    }

    private static void WriteHelp()
    {
        AnsiConsole.Write(new Rule("[bold deepskyblue1]SetCodePage[/]").LeftJustified());
        AnsiConsole.MarkupLine("更新 PE 文件内嵌 Manifest 的 [cyan]activeCodePage[/]。\n");
        AnsiConsole.MarkupLine("[bold]用法[/]");
        AnsiConsole.MarkupLine("  SetCodePage <文件> [选项]\n");

        var options = new Table()
            .HideHeaders()
            .Border(TableBorder.None)
            .AddColumn(new TableColumn(string.Empty).NoWrap())
            .AddColumn(string.Empty);

        options.AddRow("[cyan]-c, --code-page <值>[/]", "区域名称或 UTF-8/Legacy，默认 zh-CN");
        options.AddRow("[cyan]-o, --output <文件>[/]", "写入新文件；省略时原地更新");
        options.AddRow("[cyan]--dry-run[/]", "只分析，不写文件");
        options.AddRow("[cyan]--no-backup[/]", "原地更新时不创建 .bak 备份");
        options.AddRow("[cyan]-f, --force[/]", "覆盖已有输出文件或备份");
        options.AddRow("[cyan]-h, --help[/]", "显示帮助");
        options.AddRow("[cyan]--version[/]", "显示版本");
        AnsiConsole.Write(options);

        AnsiConsole.MarkupLine("\n[bold]示例[/]");
        AnsiConsole.MarkupLine("  [grey]SetCodePage legacy.exe[/]");
        AnsiConsole.MarkupLine("  [grey]SetCodePage legacy.exe -o patched.exe[/]");
        AnsiConsole.MarkupLine("  [grey]SetCodePage legacy.exe -c ja-JP --dry-run[/]");
    }

    private static void WriteVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        AnsiConsole.MarkupLine($"SetCodePage [cyan]{Escape(version)}[/]");
    }

    private static void WriteError(string message) =>
        AnsiConsole.MarkupLine($"[red]错误：[/]{Escape(message)}");

    private static string Escape(string value) => Markup.Escape(value);

    private static string EscapeLink(string value) =>
        Markup.Escape(new Uri(value).AbsoluteUri);
}
