# Seekr v1.0.0 Release Notes

**Release Date:** December 12, 2025  
**Developer:** CQNNQR

---

## 🎉 Initial Release

We're excited to announce the first public release of **Seekr**, a powerful cross-platform disk usage analyzer!

---

## ✨ Features

### Disk Analysis
- ⚡ **High-performance scanning** - Multi-threaded directory traversal for fast results
- 📁 **Interactive tree view** - Expandable folders sorted by size with drill-down navigation
- 📊 **Multiple visualizations**:
  - Pie Chart with click-to-navigate
  - Bar Graph with horizontal layout
  - Treemap for hierarchical view
  - Top Files list for quick wins
- 🔍 **Search functionality** - Find files and folders by name
- 📤 **Export options** - Save results as CSV or JSON

### Duplicate File Finder
- 🔄 **Cross-drive scanning** - Detect duplicates across multiple folders and drives
- 🏎️ **HDD optimization** - Instant metadata-based detection for slow drives
- ✅ **Content verification** - Optional hash verification for 100% accuracy
- 🎯 **Smart selection** - Auto-select duplicates to delete based on:
  - Keep Newest / Keep Oldest
  - Keep Shortest Path / Keep Longest Path
  - Keep Most Recent Access

### User Experience
- 🎨 **Modern UI** - Fluent design with Light and Dark themes
- 🖱️ **Context menus** - Right-click for Open, Reveal, Copy Path, Delete
- ⌨️ **Keyboard shortcuts** - Efficient navigation
- 📱 **Responsive design** - Resizable panels and windows

### Cross-Platform Support
- ✅ Windows 10/11 (x64)
- ✅ macOS (Intel x64)
- ✅ macOS (Apple Silicon arm64)
- ✅ Linux (x64)

### Portable Mode
- 💾 **No installation required** - Run directly from any location
- 🔌 **USB-ready** - Perfect for IT professionals and technicians
- ⚙️ **Settings persistence** - Configuration saved next to executable

---

## 📦 Download

| Platform | Architecture | File |
|----------|-------------|------|
| Windows | x64 | `Seekr.Avalonia.exe` |
| Linux | x64 | `Seekr.Avalonia` |
| macOS | Intel x64 | `Seekr.Avalonia` + libs |
| macOS | Apple Silicon | `Seekr.Avalonia` + libs |

---

## 🔧 System Requirements

### Minimum Requirements
- **OS**: Windows 10, macOS 10.15, or Ubuntu 18.04+
- **RAM**: 256 MB
- **Disk**: 50 MB for application

### Recommended
- **RAM**: 512 MB+ for scanning large drives
- **Display**: 1280x720 or higher

---

## ⚙️ Settings

All settings are customizable and saved in portable `seekr-settings.json`:

| Category | Settings |
|----------|----------|
| **Appearance** | Theme (Light/Dark), Size Unit Display |
| **Charts** | Default Graph, Max Slices, Min Percentage |
| **Behavior** | Remember Path, Confirm Delete |
| **Scanning** | Include Hidden Files, Include System Files |

---

## 🐛 Known Limitations

- macOS builds require accompanying `.dylib` files (not fully single-file)
- Some protected system folders cannot be scanned (permission errors are handled gracefully)
- Treemap visualization is placeholder in this release

---

## 🔐 Security

- **No network access** - Seekr works entirely offline
- **No telemetry** - Your data stays on your machine
- **Read-only by default** - Delete operations require explicit confirmation

---

## 📄 License

MIT License - Copyright © 2025 CQNNQR

Free for personal and commercial use. See LICENSE file for full terms.

---

## 🙏 Acknowledgments

Built with these amazing open-source projects:
- [.NET 9](https://dotnet.microsoft.com/)
- [Avalonia UI](https://avaloniaui.net/)
- [LiveCharts2](https://lvcharts.com/)
- [ReactiveUI](https://reactiveui.net/)
- [SkiaSharp](https://github.com/mono/SkiaSharp)
- [Serilog](https://serilog.net/)

---

## 📧 Support

For issues, feature requests, or contributions:
- GitHub: [https://github.com/CQNNQR/Seekr](https://github.com/CQNNQR/Seekr)

---

**Thank you for using Seekr!**

Made with ❤️ by CQNNQR
