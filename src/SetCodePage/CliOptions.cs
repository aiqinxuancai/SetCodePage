namespace SetCodePage;

internal sealed record CliOptions(
    string? InputPath,
    string? OutputPath,
    string CodePage,
    bool DryRun,
    bool NoBackup,
    bool Force,
    bool ShowHelp,
    bool ShowVersion)
{
    public static CliOptions Parse(string[] args)
    {
        string? inputPath = null;
        string? outputPath = null;
        var codePage = "zh-CN";
        var dryRun = false;
        var noBackup = false;
        var force = false;
        var showHelp = false;
        var showVersion = false;
        var optionsEnded = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (!optionsEnded && argument == "--")
            {
                optionsEnded = true;
                continue;
            }

            if (!optionsEnded && TrySplitOption(argument, out var option, out var inlineValue))
            {
                switch (option)
                {
                    case "-h":
                    case "--help":
                        EnsureNoInlineValue(option, inlineValue);
                        showHelp = true;
                        break;
                    case "--version":
                        EnsureNoInlineValue(option, inlineValue);
                        showVersion = true;
                        break;
                    case "-o":
                    case "--output":
                        outputPath = ReadValue(args, ref index, option, inlineValue);
                        break;
                    case "-c":
                    case "--code-page":
                        codePage = ReadValue(args, ref index, option, inlineValue);
                        break;
                    case "--dry-run":
                        EnsureNoInlineValue(option, inlineValue);
                        dryRun = true;
                        break;
                    case "--no-backup":
                        EnsureNoInlineValue(option, inlineValue);
                        noBackup = true;
                        break;
                    case "-f":
                    case "--force":
                        EnsureNoInlineValue(option, inlineValue);
                        force = true;
                        break;
                    default:
                        throw new CliUsageException($"未知选项：{option}");
                }

                continue;
            }

            if (inputPath is not null)
            {
                throw new CliUsageException("一次只能处理一个输入文件。");
            }

            inputPath = argument;
        }

        if (!showHelp && !showVersion && string.IsNullOrWhiteSpace(inputPath))
        {
            throw new CliUsageException("缺少要处理的 EXE 或 DLL 路径。");
        }

        if (string.IsNullOrWhiteSpace(codePage))
        {
            throw new CliUsageException("代码页名称不能为空。");
        }

        return new CliOptions(
            inputPath,
            outputPath,
            codePage,
            dryRun,
            noBackup,
            force,
            showHelp,
            showVersion);
    }

    private static bool TrySplitOption(string argument, out string option, out string? value)
    {
        option = argument;
        value = null;

        if (argument.Length == 0 || argument[0] != '-' || argument == "-")
        {
            return false;
        }

        var separatorIndex = argument.IndexOf('=');
        if (separatorIndex >= 0)
        {
            option = argument[..separatorIndex];
            value = argument[(separatorIndex + 1)..];
        }

        return true;
    }

    private static string ReadValue(string[] args, ref int index, string option, string? inlineValue)
    {
        if (inlineValue is not null)
        {
            if (string.IsNullOrWhiteSpace(inlineValue))
            {
                throw new CliUsageException($"选项 {option} 缺少值。");
            }

            return inlineValue;
        }

        if (++index >= args.Length)
        {
            throw new CliUsageException($"选项 {option} 缺少值。");
        }

        if (string.IsNullOrWhiteSpace(args[index]))
        {
            throw new CliUsageException($"选项 {option} 缺少值。");
        }

        return args[index];
    }

    private static void EnsureNoInlineValue(string option, string? inlineValue)
    {
        if (inlineValue is not null)
        {
            throw new CliUsageException($"选项 {option} 不接受值。");
        }
    }
}

internal sealed class CliUsageException(string message) : Exception(message);
