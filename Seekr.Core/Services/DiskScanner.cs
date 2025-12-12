using Seekr.Models;
using Serilog;
using System.Collections.Concurrent;

namespace Seekr.Services;

/// <summary>
/// High-performance disk scanner with multithreading and progress reporting
/// </summary>
public class DiskScanner
{
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ScanOptions _options;
    private long _scannedBytes;
    private int _scannedItems;
    
    // Throttling for progress reporting
    private long _lastReportTime;
    private const long ReportIntervalTicks = 100 * 10000; // 100ms in ticks

    public DiskScanner(ScanOptions? options = null)
    {
        _options = options ?? ScanOptions.Default;
    }

    /// <summary>
    /// Event fired when scan progress is updated
    /// </summary>
    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Event fired when current directory being scanned changes
    /// </summary>
    public event EventHandler<string>? CurrentDirectoryChanged;

    /// <summary>
    /// Starts scanning the specified path asynchronously
    /// </summary>
    public async Task<Models.ScanResult> ScanAsync(string rootPath, IProgress<int>? progress = null)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        _scannedBytes = 0;
        _scannedItems = 0;
        _lastReportTime = DateTime.UtcNow.Ticks;

        try
        {
            // Pre-scan checks so we can give a clearer error when the root path can't be accessed
            try
            {
                // Normalize root and check existence
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    Log.Warning("ScanAsync called with empty rootPath");
                    return new Models.ScanResult { Root = null, ErrorMessage = "No path supplied" };
                }

                var pathRoot = Path.GetPathRoot(rootPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(pathRoot))
                {
                    try
                    {
                        var drive = new DriveInfo(pathRoot);
                        if (!drive.IsReady)
                        {
                            Log.Warning("Drive {Drive} is not ready", pathRoot);
                            // If configured, attempt to wait for the drive to become ready
                            if (_options.RetryOnDriveNotReady)
                            {
                                var attempts = 0;
                                while (!drive.IsReady && attempts < _options.RetryCount)
                                {
                                    Log.Information("Drive {Drive} not ready, waiting {Delay}ms (attempt {Attempt}/{Max})", pathRoot, _options.RetryDelayMs, attempts + 1, _options.RetryCount);
                                    await Task.Delay(_options.RetryDelayMs);
                                    attempts++;
                                    // refresh drive info
                                    drive = new DriveInfo(pathRoot);
                                }

                                if (!drive.IsReady)
                                {
                                    Log.Warning("Drive {Drive} still not ready after {Attempts} attempts", pathRoot, _options.RetryCount);
                                    return new Models.ScanResult { Root = null, ErrorMessage = $"Drive {pathRoot} is not ready (no media)" };
                                }
                            }
                            else
                            {
                                return new Models.ScanResult { Root = null, ErrorMessage = $"Drive {pathRoot} is not ready (no media)" };
                            }
                        }
                    }
                    catch (ArgumentException) { /* not a drive path, ignore */ }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error while checking drive status for {PathRoot}", pathRoot);
                    }
                }

                if (!Directory.Exists(rootPath))
                {
                    Log.Warning("Scan root path does not exist: {RootPath}", rootPath);
                    return new Models.ScanResult { Root = null, ErrorMessage = $"Path does not exist: {rootPath}" };
                }

                // Attempt a very small enumeration to detect immediate permission errors or drive not ready
                try
                {
                    using var e = Directory.EnumerateFileSystemEntries(rootPath).GetEnumerator();
                    // just try to advance once - this will throw early if the root is inaccessible
                    _ = e.MoveNext();
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Warning(ex, "Access denied probing root path {RootPath}", rootPath);
                    var errNode = new Models.FileSystemNode
                    {
                        Name = new DirectoryInfo(rootPath).Name,
                        FullPath = rootPath,
                        IsDirectory = true,
                        HasError = true,
                        ErrorMessage = $"Access denied probing path: {ex.Message}"
                    };
                    return new Models.ScanResult { Root = errNode, ErrorMessage = $"Access denied probing path {rootPath}: {ex.Message}" };
                }
                catch (DriveNotFoundException ex)
                {
                    Log.Warning(ex, "Drive not found for {RootPath}", rootPath);
                    var errNode = new Models.FileSystemNode
                    {
                        Name = new DirectoryInfo(rootPath).Name,
                        FullPath = rootPath,
                        IsDirectory = true,
                        HasError = true,
                        ErrorMessage = $"Drive not found: {rootPath}"
                    };
                    return new Models.ScanResult { Root = errNode, ErrorMessage = $"Drive not found: {rootPath}" };
                }
                catch (IOException ex)
                {
                    Log.Warning(ex, "I/O error probing root path {RootPath}", rootPath);
                    var errNode = new Models.FileSystemNode
                    {
                        Name = new DirectoryInfo(rootPath).Name,
                        FullPath = rootPath,
                        IsDirectory = true,
                        HasError = true,
                        ErrorMessage = $"I/O error probing path: {ex.Message}"
                    };
                    return new Models.ScanResult { Root = errNode, ErrorMessage = $"I/O error probing path: {ex.Message}" };
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unexpected error during pre-scan checks for {RootPath}", rootPath);
                return new Models.ScanResult { Root = null, ErrorMessage = ex.Message };
            }

            var result = await Task.Run(() => ScanDirectory(rootPath, null, 0, progress, token), token);
            if (result == null)
            {
                // null could mean inaccessible folder or other error — return an error result
                Log.Warning("ScanDirectory returned null for root path {RootPath}", rootPath);
                // Before returning a generic message, attempt to clarify why it was null by checking root attributes
                try
                {
                    var dirInfo = new DirectoryInfo(rootPath);
                    var reasons = new List<string>();
                    if (dirInfo.Exists == false)
                        reasons.Add("Path does not exist");
                    if (!_options.ScanHiddenFiles && (dirInfo.Attributes & FileAttributes.Hidden) != 0)
                        reasons.Add("Hidden directory and scanning of hidden files is disabled");
                    if (!_options.ScanSystemFiles && (dirInfo.Attributes & FileAttributes.System) != 0)
                        reasons.Add("System directory and scanning of system files is disabled");

                    var message = reasons.Any() ? string.Join("; ", reasons) : "Scan returned no results (access denied or error)";
                    return new Models.ScanResult { Root = null, ErrorMessage = message };
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Unable to inspect root attributes for {RootPath}", rootPath);
                    return new Models.ScanResult { Root = null, ErrorMessage = "Scan returned no results (access denied or error)" };
                }
            }

            // If root returned but contains an error and no children, surface that as an ErrorMessage
            if (result.HasError && (result.Children == null || !result.Children.Any()))
            {
                var message = string.IsNullOrEmpty(result.ErrorMessage) ? "Scan encountered access issues or errors" : result.ErrorMessage;
                Log.Warning("Root scan for {RootPath} completed with errors: {Error}", rootPath, message);
                return new Models.ScanResult { Root = result, ErrorMessage = message };
            }

            // Force calculation of totals on background thread to prevent UI freeze later
            // Accessing the property triggers the recursive calculation and caching
            _ = result.TotalSize;
            _ = result.TotalItemCount;

            return new Models.ScanResult { Root = result };
        }
        catch (OperationCanceledException)
        {
            return new Models.ScanResult { IsCancelled = true };
        }
        catch (Exception ex)
        {
            return new Models.ScanResult { ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Cancels the current scan operation
    /// </summary>
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    private FileSystemNode? ScanDirectory(string path, FileSystemNode? parent, int depth, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check depth limit
        if (_options.MaxDepth >= 0 && depth > _options.MaxDepth)
            return null;

        try
        {
            // Report current directory (throttled)
            ReportProgress(path, false);

            var dirInfo = new DirectoryInfo(path);

            // Create a node early so return cases can return a descriptive error node instead of null
            var node = new FileSystemNode
            {
                Name = dirInfo.Name,
                FullPath = dirInfo.FullName,
                IsDirectory = true,
                LastModified = dirInfo.LastWriteTime,
                Parent = parent
            };

            // Skip hidden/system directories if not requested — return an error node so the UI shows the reason
            if (!_options.ScanHiddenFiles && (dirInfo.Attributes & FileAttributes.Hidden) != 0)
            {
                Log.Warning("Skipping hidden directory {DirectoryPath} because ScanHiddenFiles is disabled", path);
                node.HasError = true;
                node.ErrorMessage = "Hidden directory and scanning of hidden files is disabled";
                return node;
            }

            if (!_options.ScanSystemFiles && (dirInfo.Attributes & FileAttributes.System) != 0)
            {
                Log.Warning("Skipping system directory {DirectoryPath} because ScanSystemFiles is disabled", path);
                node.HasError = true;
                node.ErrorMessage = "System directory and scanning of system files is disabled";
                return node;
            }

            Log.Verbose("Scanning directory {DirectoryPath} (depth={Depth})", path, depth);

            // node was created earlier

            var files = new ConcurrentBag<FileSystemNode>();
            var directories = new ConcurrentBag<FileSystemNode>();

            // Scan files in parallel
            try
            {
                var fileInfos = dirInfo.GetFiles();
                Parallel.ForEach(fileInfos, new ParallelOptions { CancellationToken = cancellationToken }, fileInfo =>
                {
                    try
                    {
                        // Skip excluded extensions
                        if (_options.ExcludedExtensions.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase))
                            return;

                        // Skip hidden/system files if not requested
                        if (!_options.ScanHiddenFiles && (fileInfo.Attributes & FileAttributes.Hidden) != 0)
                            return;
                        
                        if (!_options.ScanSystemFiles && (fileInfo.Attributes & FileAttributes.System) != 0)
                            return;

                        var fileNode = new FileSystemNode
                        {
                            Name = fileInfo.Name,
                            FullPath = fileInfo.FullName,
                            Size = fileInfo.Length,
                            IsDirectory = false,
                            LastModified = fileInfo.LastWriteTime,
                            CreationTime = fileInfo.CreationTime,
                            Parent = node
                        };

                        files.Add(fileNode);
                        Interlocked.Add(ref _scannedBytes, fileNode.Size);
                        Interlocked.Increment(ref _scannedItems);
                        // Log.Debug("Created file node {FileName} size={Size} path={Path}", fileNode.Name, fileNode.Size, fileNode.FullPath);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        // Skip files we can't access
                        Log.Warning(ex, "Access denied to file {FilePath}", fileInfo.FullName);
                    }
                    catch (Exception)
                    {
                        // Skip other file access issues
                    }
                });

                Log.Debug("Found {FileCount} files in {DirectoryPath}", fileInfos.Length, path);

                // Scan subdirectories
                var dirInfos = dirInfo.GetDirectories();
                Log.Debug("Found {DirCount} subdirectories in {DirectoryPath}", dirInfos.Length, path);
                
                Parallel.ForEach(dirInfos, new ParallelOptions { CancellationToken = cancellationToken }, subDirInfo =>
                {
                    try
                    {
                        // Skip excluded folders
                        if (_options.ExcludedFolders.Contains(subDirInfo.Name, StringComparer.OrdinalIgnoreCase))
                            return;

                        // Skip reparse points (junctions/symlinks) unless enabled
                        if (!_options.FollowSymlinks && (subDirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                            return;

                        var subNode = ScanDirectory(subDirInfo.FullName, node, depth + 1, progress, cancellationToken);
                        if (subNode != null)
                        {
                            directories.Add(subNode);
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error scanning subdirectory {Path}", subDirInfo.FullName);
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                // Skip directories we can't access
                Log.Warning(ex, "Access denied to directory {DirectoryPath}", path);
                // ProgressChanged?.Invoke(this, new ScanProgressEventArgs($"Access denied: {path}", _scannedItems, _scannedBytes));
            }
            catch (DirectoryNotFoundException)
            {
                // Directory was deleted during scan
                return null;
            }

            // Add all children
            node.Children.AddRange(files);
            node.Children.AddRange(directories);
            // Calculate totals
            node.Size = files.Sum(f => f.Size);
            node.ItemCount = files.Count + directories.Count;

            // Force calculation of recursive totals now (in background thread)
            // This prevents the UI thread from freezing when it tries to sort/display nodes later
            _ = node.TotalSize;
            _ = node.TotalItemCount;

            // Report progress (throttled, but force update for large directories or completion)
            ReportProgress(node.FullPath, false);

            return node;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "Access denied while scanning {DirectoryPath}", path);
            // Don't throttle errors, but maybe we should? For now, let's report them.
            // Actually, for access denied on system folders, this can spam.
            // Let's throttle this too, or just log it.
            // We'll use the throttled reporter but with the error message.
            // ProgressChanged?.Invoke(this, new ScanProgressEventArgs($"Access denied: {path}", _scannedItems, _scannedBytes));
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scanning {DirectoryPath}", path);
            ProgressChanged?.Invoke(this, new ScanProgressEventArgs($"Error scanning {path}: {ex.Message}", _scannedItems, _scannedBytes));
            return null;
        }
    }

    private void ReportProgress(string currentPath, bool force)
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastReportTime);

        if (force || (now - last) > ReportIntervalTicks)
        {
            // Try to update the last report time. If another thread beat us to it, we skip reporting (unless forced)
            if (force || Interlocked.CompareExchange(ref _lastReportTime, now, last) == last)
            {
                CurrentDirectoryChanged?.Invoke(this, currentPath);
                ProgressChanged?.Invoke(this, new ScanProgressEventArgs($"Scanning: {currentPath}", _scannedItems, _scannedBytes));
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// Event arguments for scan progress updates
/// </summary>
public class ScanProgressEventArgs : EventArgs
{
    public string Message { get; }
    public int ItemsScanned { get; }
    public long BytesScanned { get; }

    public ScanProgressEventArgs(string message, int itemsScanned, long bytesScanned)
    {
        Message = message;
        ItemsScanned = itemsScanned;
        BytesScanned = bytesScanned;
    }
}
