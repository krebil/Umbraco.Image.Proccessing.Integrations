using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Umbraco.Tests;

/// <summary>
/// 3.0 renamed two settings this sample reads. A 2.x value left in place would otherwise be ignored,
/// and the sample would quietly run on the defaults; it refuses to start instead and names the new key.
/// Settings go in as host settings so <c>Program.cs</c> sees them where it reads them, before <c>Build()</c>.
/// </summary>
public sealed class RenamedSettingsTests
{
    [Theory]
    [InlineData("ImageProcessing:Storage:Mode", "AzureBlob", "MediaStorage:Mode")]
    [InlineData("ImageProcessing:RoutePrefix", "/media", "ImageProcessing:Sources")]
    public void LeftoverSettingFrom2x_FailsAtStartup_NamingTheReplacement(string key, string value, string replacement)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting(key, value));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(key, ex.Message, StringComparison.Ordinal);
        Assert.Contains(replacement, ex.Message, StringComparison.Ordinal);
    }
}
