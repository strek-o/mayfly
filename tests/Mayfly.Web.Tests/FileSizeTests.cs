using Mayfly.Web.Files;

namespace Mayfly.Web.Tests;

public class FileSizeTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(100L * 1024 * 1024, "100 MB")]
    [InlineData(3L * 1024 * 1024 * 1024 * 1024, "3072 GB")]
    public void Format_UsesLargestFittingUnit(long bytes, string expected)
    {
        Assert.Equal(expected, FileSize.Format(bytes));
    }
}
