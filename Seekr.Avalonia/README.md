# Seekr - Disk Usage Analyzer

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)

**Seekr** is a fast, cross-platform disk usage analyzer with beautiful visualizations and powerful duplicate file detection. Built with .NET 9 and Avalonia UI for native performance on Windows, macOS, and Linux.

## ✨ Features

### 📊 Disk Analysis
- **Lightning-fast scanning** with multi-threaded directory traversal
- **Interactive tree view** with expandable folders sorted by size
- **Real-time progress** showing current directory being scanned
- **Multiple visualization options**: Pie charts, Bar graphs, and Treemaps

### 🔍 Duplicate File Finder
- **Cross-drive duplicate detection** - scan multiple folders/drives at once
- **Smart HDD optimization** - uses metadata fingerprinting for instant results on slow drives
- **Content verification** - optional hash-based verification for 100% accuracy
- **Bulk selection strategies**: Keep Newest, Oldest, Shortest Path, Longest Path, Most Recent Access

### 🎨 User Interface
- **Modern Fluent design** with Light and Dark themes
- **Click-to-navigate** charts - click any slice or bar to drill down
- **Context menus** - right-click to open, reveal, copy path, or delete
- **Search functionality** - find files and folders instantly
- **Export options** - save results as CSV or JSON

### ⚙️ Settings
- **Theme selection** (Light/Dark)
- **Size unit display** (Auto, Bytes, KB, MB, GB)
- **Chart customization** (max slices, min percentage threshold)
- **Scan options** (include hidden/system files)
- **Portable mode** - settings saved next to executable

### 🔄 Auto-Updates
- **Check for updates** directly in the app
- **One-click install** - downloads and installs without leaving Seekr
- **Settings preserved** - your preferences survive updates
- **Update on startup** option in settings

## 🚀 Getting Started

### System Requirements
- **Windows**: Windows 10 or later (x64)
- **macOS**: macOS 10.15 Catalina or later (Intel or Apple Silicon)
- **Linux**: Ubuntu 18.04+ or equivalent (x64)

### Installation

**Seekr is portable - no installation required!**

1. Download the version for your platform
2. Extract to any location (including USB drives)
3. Run the executable

| Platform | File |
|----------|------|
| Windows | `Seekr.Avalonia.exe` |
| Linux | `Seekr.Avalonia` (run `chmod +x` first) |
| macOS Intel | `Seekr.Avalonia` + `.dylib` files |
| macOS Apple Silicon | `Seekr.Avalonia` + `.dylib` files |

## 📖 Usage

### Basic Scanning
1. Click **"Select Folders"** to choose one or more directories
2. Click **"Scan"** to analyze disk usage
3. Use the tabs to switch between visualizations:
   - **Pie Chart** - see proportional sizes at a glance
   - **Bar Graph** - compare folder sizes side by side
   - **Treemap** - hierarchical size visualization
   - **Top Files** - list of largest files
   - **Duplicates** - find and manage duplicate files

### Finding Duplicates
1. Scan one or more folders/drives
2. Switch to the **Duplicates** tab
3. Click **"Find Duplicates"**
4. Use the dropdown to select which files to keep
5. Check files you want to delete
6. Click **"Delete Selected"**

### Keyboard Shortcuts
- **Enter** - Start scan (when path selected)
- **Escape** - Cancel current operation
- **Delete** - Delete selected item (with confirmation)

## 🔧 Configuration

Settings are stored in `seekr-settings.json` next to the executable, making Seekr fully portable.

### Available Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Theme | Light or Dark mode | Light |
| Size Unit | Auto, Bytes, KB, MB, GB | Auto |
| Default Graph | Pie, Bar, or Treemap | Pie |
| Max Pie Slices | Maximum chart segments | 10 |
| Max Bar Items | Maximum bar chart items | 15 |
| Max Top Files | Files in "Top Files" list | 100 |
| Min Slice % | Smaller items grouped as "Other" | 2% |
| Remember Path | Remember last scanned location | Yes |
| Confirm Delete | Ask before deleting files | Yes |
| Show Hidden | Include hidden files in scan | Yes |
| Show System | Include system files in scan | Yes |
| Check for Updates | Auto-check on startup | Yes |
| Send Usage Data | Anonymous telemetry | Yes |

## 🔄 Updates

Seekr can update itself! Click the 🔄 button in the toolbar to check for updates. When an update is available, a green banner will appear with an **Install Update** button that downloads and installs the new version automatically.

**Your settings and logs are preserved** during updates.

## 🛠️ Technical Details

### Built With
- **.NET 9** - Cross-platform runtime
- **Avalonia UI 11.3** - Native cross-platform UI framework
- **LiveCharts2** - Beautiful, responsive charts
- **ReactiveUI** - Reactive MVVM framework
- **SkiaSharp** - Hardware-accelerated 2D graphics

### Architecture
- **Seekr.Core** - Platform-independent scanning and analysis
- **Seekr.Avalonia** - Cross-platform desktop UI

### Performance Features
- Multi-threaded directory scanning
- Adaptive parallelism (SSD vs HDD detection)
- Memory-efficient tree structures
- Throttled UI updates

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

```
MIT License

Copyright (c) 2025 CQNNQR

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📧 Contact

**CQNNQR** - [GitHub](https://github.com/CQNNQR)

---

Made with ❤️ by CQNNQR
