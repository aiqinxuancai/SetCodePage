namespace SetCodePage.Tests;

public sealed class PeCompatibilityAnalyzerTests
{
    [Fact]
    public void FindRelevantAnsiGdiTextImports_keeps_only_risky_unique_imports()
    {
        var imports = new[]
        {
            "CreateWindowExA",
            "DrawTextA",
            "DrawTextW",
            "ExtTextOutA",
            "DrawTextA",
            "SetWindowTextA",
            "TextOutA"
        };

        var result = PeCompatibilityAnalyzer.FindRelevantAnsiGdiTextImports(imports);

        Assert.Equal(["DrawTextA", "ExtTextOutA", "TextOutA"], result);
    }

    [Fact]
    public void FindAnsiGdiTextImports_accepts_a_managed_pe_without_throwing()
    {
        var result = PeCompatibilityAnalyzer.FindAnsiGdiTextImports(
            typeof(PeCompatibilityAnalyzerTests).Assembly.Location);

        Assert.NotNull(result);
    }
}
