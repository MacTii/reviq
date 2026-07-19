using Reviq.Application.Features.Webhook;

namespace Reviq.Application.Tests.Features.Webhook;

public class PrFileLanguageDetectorTests
{
    [Theory]
    [InlineData("Service.cs", "C#")]
    [InlineData("component.tsx", "TypeScript")]
    [InlineData("index.ts", "TypeScript")]
    [InlineData("app.js", "JavaScript")]
    [InlineData("view.jsx", "JavaScript")]
    [InlineData("main.py", "Python")]
    [InlineData("Main.java", "Java")]
    [InlineData("main.go", "Go")]
    [InlineData("lib.rs", "Rust")]
    [InlineData("index.php", "PHP")]
    public void Detect_KnownExtension_ReturnsLanguage(string fileName, string expected) =>
        Assert.Equal(expected, PrFileLanguageDetector.Detect(fileName));

    [Fact]
    public void Detect_UnknownExtension_ReturnsUnknown() =>
        Assert.Equal("Unknown", PrFileLanguageDetector.Detect("data.bin"));

    [Theory]
    [InlineData("Service.cs", true)]
    [InlineData("data.bin", false)]
    [InlineData("README.md", false)]
    public void IsSupported_MatchesKnownExtensionsOnly(string fileName, bool expected) =>
        Assert.Equal(expected, PrFileLanguageDetector.IsSupported(fileName));
}
