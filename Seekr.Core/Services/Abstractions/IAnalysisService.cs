using Seekr.Models;

namespace Seekr.Core.Services.Abstractions;

/// <summary>
/// Interface for file system analysis operations
/// </summary>
public interface IAnalysisService
{
    Dictionary<string, long> GetFileTypeDistribution(FileSystemNode rootNode);
    List<FileSystemNode> GetLargestItems(FileSystemNode rootNode, int count = 100);
    List<FileSystemNode> GetTopFiles(FileSystemNode rootNode, int count = 100);
    List<FileSystemNode> GetTopDirectories(FileSystemNode rootNode, int count = 100);
    Dictionary<string, long> GetCategorizedDistribution(FileSystemNode rootNode);
}