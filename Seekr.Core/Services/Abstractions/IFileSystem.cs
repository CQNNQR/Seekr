using System;
using System.Collections.Generic;
using System.IO;

namespace Seekr.Core.Services.Abstractions;

/// <summary>
/// Abstraction for file system operations to enable testing and platform-specific implementations
/// </summary>
public interface IFileSystem
{
    // Directory operations
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFileSystemEntries(string path);
    DirectoryInfo GetDirectoryInfo(string path);
    string GetPathRoot(string path);

    // File operations
    bool FileExists(string path);
    FileInfo GetFileInfo(string path);

    // Drive operations
    DriveInfo GetDriveInfo(string path);
}

/// <summary>
/// Default implementation using System.IO
/// </summary>
public class SystemFileSystem : IFileSystem
{
    public virtual bool DirectoryExists(string path) => Directory.Exists(path);

    public virtual IEnumerable<string> EnumerateFileSystemEntries(string path)
    {
        return Directory.EnumerateFileSystemEntries(path);
    }

    public virtual DirectoryInfo GetDirectoryInfo(string path) => new DirectoryInfo(path);

    public virtual string GetPathRoot(string path) => Path.GetPathRoot(path) ?? string.Empty;

    public virtual bool FileExists(string path) => File.Exists(path);

    public virtual FileInfo GetFileInfo(string path) => new FileInfo(path);

    public virtual DriveInfo GetDriveInfo(string path) => new DriveInfo(path);
}
