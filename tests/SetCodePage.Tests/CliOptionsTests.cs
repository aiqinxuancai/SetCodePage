namespace SetCodePage.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void Parse_uses_safe_defaults()
    {
        var options = CliOptions.Parse(["legacy.exe"]);

        Assert.Equal("legacy.exe", options.InputPath);
        Assert.Equal("zh-CN", options.CodePage);
        Assert.Null(options.OutputPath);
        Assert.False(options.NoBackup);
    }

    [Fact]
    public void Parse_supports_inline_values_and_flags()
    {
        var options = CliOptions.Parse(
            ["legacy.exe", "--code-page=ja-JP", "--output=patched.exe", "--dry-run", "--force"]);

        Assert.Equal("ja-JP", options.CodePage);
        Assert.Equal("patched.exe", options.OutputPath);
        Assert.True(options.DryRun);
        Assert.True(options.Force);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_rejects_empty_output_values(string value)
    {
        Assert.Throws<CliUsageException>(() =>
            CliOptions.Parse(["legacy.exe", "--output", value]));
    }
}
