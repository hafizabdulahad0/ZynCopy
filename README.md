<div align="center">

# ⚡ ZynCopy
### Ultra-Fast Cloud & Local File Transfer Utility with Zero Disk Caching

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg?logo=windows)](https://microsoft.com/windows)
[![Architecture](https://img.shields.io/badge/Architecture-x64%20%7C%20Any%20CPU-success.svg)]()
[![Zero Cache](https://img.shields.io/badge/C%3A%20Cache-0%20Bytes-brightgreen.svg)]()
[![Executable Size](https://img.shields.io/badge/App%20Size-23%20KB-orange.svg)]()

**ZynCopy** is an open-source, ultra-lightweight Windows desktop utility engineered to eliminate the freezing, latency, and SSD wear caused by Windows File Explorer when copying or moving files to cloud drives, virtual mounts, and local storage.

Developed by **ZynOps Solutions**.

</div>

---

## 🚀 Why ZynCopy?

When you copy large files or thousands of items to cloud drives (Google Drive, OneDrive, TeraBox, Rclone mounts) using standard Windows File Explorer, Windows attempts to:
1. **Copy sequentially on a single thread**, causing severe network lag.
2. **Force local disk caching onto `C:`**, filling up SSDs and causing "Disk Full" errors on large transfers.
3. **Hang or freeze `explorer.exe`** whenever network latency delays file handle inspection.

**ZynCopy fixes this permanently:**

| Feature | Windows Explorer | TeraCopy / FastCopy | ZynCopy |
| :--- | :---: | :---: | :---: |
| **C: Drive Cache Staging** | ⚠️ Fills `C:` drive | ⚠️ Fails on VFS drives | **✅ 0 Bytes (Pure RAM Streaming)** |
| **Multi-Threaded Streams** | ❌ Single-threaded (1x) | ⚠️ Local-disk only | **✅ 4x, 8x, 16x Parallel Streams** |
| **Cloud Drive Awareness** | ❌ None | ❌ None | **✅ Adaptive Cloud Remote Translation** |
| **UI Responsiveness** | ❌ Frequently freezes | ⚠️ Medium | **✅ 100% Non-Blocking Async Engine** |
| **Right-Click Integration** | Default | Heavy COM DLL | **✅ Lightweight Native Shell Verb** |
| **Memory / GPU Footprint** | Bloated | Medium | **✅ < 20 MB RAM, 0% GPU** |
| **Instant Directory Purge** | ❌ Slow (Recycle Bin scan) | ⚠️ Standard | **✅ Kernel-Bypass Instant Delete** |

---

## ✨ Features

- **🚀 8x / 16x Multi-Stream Pipeline:** Transfers up to 16 files simultaneously, fully saturating your network and storage bandwidth.
- **🛡️ 100% Zero-Cache Engine:** Streams directly using in-memory chunk buffers (`--vfs-cache-mode off`). No temporary files are ever created on your `C:` drive.
- **📁 Windows Explorer Right-Click Integration:** Right-click any file or directory in File Explorer and choose **"Copy with ZynCopy"**.
- **⚡ Adaptive Cloud Translation:** Automatically detects mounted cloud drives (e.g. `J:\`, `K:\`, `L:\`) and maps them directly to high-speed cloud endpoints, bypassing virtual driver latency.
- **🗑️ Instant Purge:** Instantly deletes huge directories containing 50,000+ files in seconds without explorer freezing.
- **🎨 Sleek Dark-Mode UI:** Clean, modern interface showing real-time throughput (MB/s), elapsed time, transferred bytes, and live logs.
- **🪶 Ultra-Lightweight Binary:** Self-contained executable under **25 KB** with zero external frameworks or heavy runtimes required.

---

## 📦 Installation

### Option 1: One-Click Installer (Recommended)
1. Download **`ZynCopy-Setup.exe`** from the [bin/](bin/) directory or the latest GitHub Release.
2. Run `ZynCopy-Setup.exe`.
3. Choose your preferences (Desktop Shortcut, Start Menu, Explorer Right-Click Menu) and click **Install**.

### Option 2: Portable Executable (No Install Required)
1. Download **`ZynCopy.exe`** from [bin/](bin/).
2. Run it from any folder or USB drive.
3. Toggle the *"Explorer Right-Click Menu"* checkbox inside the app whenever you want context menu integration!

---

## 🛠️ How to Use

### 1. Via Windows File Explorer (Right-Click Menu)
* Right-click any file or folder in File Explorer.
* Click **Copy with ZynCopy**.
* Select your target destination drive and stream speed (**🚀 Turbo 8x** or **🌪️ Hyper 16x**).
* Click **Turbo Copy** or **Turbo Move**!

### 2. Via Drag & Drop
* Open ZynCopy from your Desktop or Start Menu.
* Drag and drop any folder or file directly into the **Source** box.
* Select your target destination from the dropdown.
* Watch the live transfer stream in real time.

---

## 🏗️ Building from Source

ZynCopy is written in clean, native C# and requires **no external SDKs or heavy build tools**. It builds directly with the C# compiler (`csc.exe`) built into Windows:

```cmd
git clone https://github.com/ZynOps-Solutions/ZynCopy.git
cd ZynCopy
build.bat
```

The compiled binaries will be output immediately to `bin\ZynCopy.exe` and `bin\ZynCopy-Setup.exe`.

---

## 🔬 Reverse Engineering & Technical Whitepaper

For a deep technical analysis of the reverse-engineered bottlenecks in Windows Explorer, TeraCopy, Google Drive for Desktop, and TeraBox, read our whitepaper:
👉 **[ARCHITECTURE_AND_GAPS.md](ARCHITECTURE_AND_GAPS.md)**

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!
1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📄 License

Distributed under the **MIT License**. See [`LICENSE`](LICENSE) for more information.

---

<div align="center">
  <b>Built with ❤️ by ZynOps Solutions</b>
</div>
