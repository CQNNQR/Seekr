using Seekr.Models;
using Seekr.Services;
using Xunit;

namespace Seekr.Core.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void Settings_DefaultValues_AreCorrect()
    {
        // Act
        var settings = new AppSettings();

        // Assert
        Assert.Equal("Light", settings.Theme);
        Assert.Equal("Auto", settings.SizeUnit);
        Assert.Equal("Tree", settings.DefaultViewMode);
        Assert.Equal("Pie", settings.DefaultGraph);
        Assert.Equal(10, settings.MaxPieSlices);
        Assert.Equal(15, settings.MaxBarItems);
        Assert.Equal(100, settings.MaxTopFiles);
        Assert.Equal(2.0, settings.MinSlicePercentage);
        Assert.True(settings.RememberLastPath);
        Assert.True(settings.ConfirmBeforeDelete);
        Assert.True(settings.ShowHiddenFiles);
        Assert.True(settings.ShowSystemFiles);
    }

    [Fact]
    public void AppSettings_CanSerializeAndDeserialize()
    {
        // Arrange
        var settings = new AppSettings
        {
            Theme = "Dark",
            SizeUnit = "GB",
            ShowHiddenFiles = false,
            MaxPieSlices = 15
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal("GB", loaded.SizeUnit);
        Assert.False(loaded.ShowHiddenFiles);
        Assert.Equal(15, loaded.MaxPieSlices);
    }

    [Fact]
    public void ScanOptions_DefaultValues()
    {
        // Act
        var options = ScanOptions.Default;

        // Assert
        Assert.True(options.ScanHiddenFiles);
        Assert.True(options.ScanSystemFiles);
        Assert.Equal(-1, options.MaxDepth);
        Assert.False(options.FollowSymlinks);
        Assert.True(options.RetryOnDriveNotReady);
        Assert.Equal(5, options.RetryCount);
        Assert.Equal(1000, options.RetryDelayMs);
        Assert.Empty(options.ExcludedExtensions);
        Assert.Empty(options.ExcludedFolders);
    }
}
