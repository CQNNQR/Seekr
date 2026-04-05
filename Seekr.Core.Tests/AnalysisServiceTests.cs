using Seekr.Models;
using Seekr.Services;
using Xunit;

namespace Seekr.Core.Tests;

public class AnalysisServiceTests
{
    private readonly AnalysisService _analysisService;

    public AnalysisServiceTests()
    {
        _analysisService = new AnalysisService();
    }

    private FileSystemNode CreateTestTree()
    {
        // Create a tree like:
        // Root
        // ├── documents
        // │   ├── doc1.txt (100 bytes)
        // │   └── doc2.pdf (200 bytes)
        // ├── images
        // │   ├── photo.jpg (500 bytes)
        // │   └── icon.png (300 bytes)
        // └── video.mp4 (1000 bytes)

        var root = new FileSystemNode
        {
            Name = "Root",
            FullPath = "/test",
            IsDirectory = true
        };

        var documents = new FileSystemNode
        {
            Name = "documents",
            FullPath = "/test/documents",
            IsDirectory = true,
            Parent = root
        };

        documents.Children.Add(new FileSystemNode
        {
            Name = "doc1.txt",
            FullPath = "/test/documents/doc1.txt",
            Size = 100,
            IsDirectory = false,
            Parent = documents
        });

        documents.Children.Add(new FileSystemNode
        {
            Name = "doc2.pdf",
            FullPath = "/test/documents/doc2.pdf",
            Size = 200,
            IsDirectory = false,
            Parent = documents
        });

        var images = new FileSystemNode
        {
            Name = "images",
            FullPath = "/test/images",
            IsDirectory = true,
            Parent = root
        };

        images.Children.Add(new FileSystemNode
        {
            Name = "photo.jpg",
            FullPath = "/test/images/photo.jpg",
            Size = 500,
            IsDirectory = false,
            Parent = images
        });

        images.Children.Add(new FileSystemNode
        {
            Name = "icon.png",
            FullPath = "/test/images/icon.png",
            Size = 300,
            IsDirectory = false,
            Parent = images
        });

        root.Children.Add(documents);
        root.Children.Add(images);
        root.Children.Add(new FileSystemNode
        {
            Name = "video.mp4",
            FullPath = "/test/video.mp4",
            Size = 1000,
            IsDirectory = false,
            Parent = root
        });

        return root;
    }

    [Fact]
    public void GetLargestItems_ReturnsOrderedBySize()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var largestItems = _analysisService.GetLargestItems(root, 10);

        // Assert
        Assert.NotEmpty(largestItems);
        Assert.Equal("video.mp4", largestItems[0].Name);
        Assert.Equal(1000, largestItems[0].Size);
    }

    [Fact]
    public void GetLargestItems_ReturnsLimitedCount()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var largestItems = _analysisService.GetLargestItems(root, 2);

        // Assert
        Assert.Equal(2, largestItems.Count);
    }

    [Fact]
    public void GetTopFiles_ReturnsAllFiles()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var topFiles = _analysisService.GetTopFiles(root, 10);

        // Assert - 5 files: doc1.txt, doc2.pdf, photo.jpg, icon.png, video.mp4
        Assert.Equal(5, topFiles.Count);
    }

    [Fact]
    public void GetTopFiles_ReturnsOrderedBySize()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var topFiles = _analysisService.GetTopFiles(root, 10);

        // Assert
        Assert.Equal("video.mp4", topFiles[0].Name);
        Assert.Equal(1000, topFiles[0].Size);
    }

    [Fact]
    public void GetTopFiles_ReturnsLimitedCount()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var topFiles = _analysisService.GetTopFiles(root, 2);

        // Assert
        Assert.Equal(2, topFiles.Count);
        Assert.Equal("video.mp4", topFiles[0].Name);
    }

    [Fact]
    public void GetFileTypeDistribution_GroupsByExtension()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var distribution = _analysisService.GetFileTypeDistribution(root);

        // Assert
        Assert.True(distribution.Count > 0);
        Assert.Equal(100, distribution[".txt"]);
        Assert.Equal(200, distribution[".pdf"]);
        Assert.Equal(500, distribution[".jpg"]);
        Assert.Equal(300, distribution[".png"]);
        Assert.Equal(1000, distribution[".mp4"]);
    }

    [Fact]
    public void GetFileTypeDistribution_NoExtension()
    {
        // Arrange
        var root = new FileSystemNode
        {
            Name = "Root",
            FullPath = "/test",
            IsDirectory = true
        };

        root.Children.Add(new FileSystemNode
        {
            Name = "README",
            FullPath = "/test/README",
            Size = 50,
            IsDirectory = false,
            Parent = root
        });

        // Act
        var distribution = _analysisService.GetFileTypeDistribution(root);

        // Assert
        Assert.Equal(50, distribution["(no extension)"]);
    }

    [Fact]
    public void GetCategorizedDistribution_GroupsByCategory()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var categories = _analysisService.GetCategorizedDistribution(root);

        // Assert
        Assert.True(categories.Count > 0);
        Assert.Equal(800, categories["Images"]); // .jpg (500) + .png (300)
        Assert.Equal(1000, categories["Videos"]); // .mp4
        Assert.Equal(300, categories["Documents"]); // .txt (100) + .pdf (200)
    }

    [Fact]
    public void GetTopDirectories_ReturnsDirectoriesOnly()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var topDirs = _analysisService.GetTopDirectories(root, 10);

        // Assert
        Assert.NotEmpty(topDirs);
        Assert.All(topDirs, d => Assert.True(d.IsDirectory));
    }

    [Fact]
    public void GetScanSummary_ComputesCorrectTotals()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var summary = _analysisService.GetScanSummary(root);

        // Assert
        Assert.Equal(5, summary.TotalFiles); // doc1.txt, doc2.pdf, photo.jpg, icon.png, video.mp4
        Assert.Equal(3, summary.TotalDirectories); // documents, images, and root
        Assert.True(summary.TotalSize > 0);
        Assert.NotNull(summary.LargestFile);
        Assert.True(summary.AverageFileSize >= 0);
    }

    [Fact]
    public void GetScanSummary_AverageFileSize()
    {
        // Arrange
        var root = CreateTestTree();

        // Act
        var summary = _analysisService.GetScanSummary(root);

        // Assert
        // Total = 100 + 200 + 500 + 300 + 1000 = 2100
        // Average = 2100 / 5 = 420
        Assert.Equal(420, summary.AverageFileSize);
    }
}
