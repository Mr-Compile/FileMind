using FileMind.Platform.Windows;
using Xunit;

namespace FileMind.Application.Tests;

public class RelativeToTests
{
    private static readonly WindowsPathNormalizer Paths = new();

    [Fact]
    public void Nested_ReturnsRelativeSubpath()
        => Assert.Equal(@"receipts\jan", Paths.RelativeTo(@"C:\dl\receipts\jan", @"C:\dl"));

    [Fact]
    public void RootEqual_ReturnsEmpty()
        => Assert.Equal("", Paths.RelativeTo(@"C:\dl", @"C:\dl"));

    [Fact]
    public void NotUnder_ReturnsNull()
        => Assert.Null(Paths.RelativeTo(@"C:\other\x", @"C:\dl"));

    [Fact]
    public void CaseMixed_StillResolves()
        => Assert.Equal(@"receipts", Paths.RelativeTo(@"c:\DL\receipts", @"C:\dl"));

    [Fact]
    public void TrailingSlashRoot_StillResolves()
        => Assert.Equal("x", Paths.RelativeTo(@"C:\dl\x", @"C:\dl\"));

    [Fact]
    public void ForwardSlashes_Normalize()
        => Assert.Equal(@"receipts", Paths.RelativeTo("C:/dl/receipts", @"C:\dl"));

    [Fact]
    public void SiblingWithSharedPrefix_IsNotUnder()
        => Assert.Null(Paths.RelativeTo(@"C:\dl2\x", @"C:\dl"));
}
