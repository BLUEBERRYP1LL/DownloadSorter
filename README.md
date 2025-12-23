# DownloadSorter

A command-line tool that automatically organizes your downloads into categorized folders.

## Features

- Automatically sorts files by type (documents, media, archives, etc.)
- Watch multiple download folders simultaneously
- Continuous monitoring mode with `--loop`
- Interactive dashboard with live stats
- File browser to move and organize files
- Customizable category rules
- Search through sorting history

## Installation

1. Download `sorter.exe` from the releases
2. Place it somewhere in your PATH (or run directly)
3. Run `sorter init` to set up your folder structure

## Quick Start

```bash
# First-time setup - creates your organized folder structure
sorter init

# Add your browser's download folder to watch
sorter watch add "C:\Users\YourName\Downloads"

# Start continuous monitoring (runs until Ctrl+C)
sorter sort --loop
```

## Commands

### Setup & Status

| Command | Description |
|---------|-------------|
| `sorter init [path]` | First-time setup wizard |
| `sorter status` | Show current stats and configuration |

### Sorting Files

| Command | Description |
|---------|-------------|
| `sorter sort` | Sort all files from watch folders now |
| `sorter sort -n` | Dry run - preview what would happen |
| `sorter sort --loop` | Continuous monitoring (Ctrl+C to stop) |
| `sorter sort -l -i 60` | Loop mode with 60-second interval |
| `sorter sort -f <path>` | Sort from a specific folder (one-time) |

### Managing Watch Folders

You can watch multiple download folders (browser downloads, torrent folder, etc.):

| Command | Description |
|---------|-------------|
| `sorter watch` | List all watched folders |
| `sorter watch add <path>` | Add a folder to watch |
| `sorter watch remove <#>` | Remove folder by its number |

**Example:**
```bash
# Add multiple download locations
sorter watch add "C:\Users\Me\Downloads"
sorter watch add "D:\Torrents\Complete"

# Check what's being watched
sorter watch

# Output:
# 0. 00_INBOX (default)
# 1. C:\Users\Me\Downloads
# 2. D:\Torrents\Complete
```

### Interactive Tools

| Command | Description |
|---------|-------------|
| `sorter dashboard` | Live dashboard with tabs and keyboard controls |
| `sorter browse [category]` | File browser with move/pin actions |

### History & Search

| Command | Description |
|---------|-------------|
| `sorter history` | Show recently sorted files |
| `sorter history -n 100` | Show last 100 sorted files |
| `sorter search <query>` | Search file history by name |

### Configuration

| Command | Description |
|---------|-------------|
| `sorter rules` | List all category routing rules |
| `sorter rules add` | Add a new rule interactively |
| `sorter rules edit <#>` | Edit an existing rule |
| `sorter rules delete <#>` | Delete a rule |
| `sorter rules test` | Test which category a filename would go to |
| `sorter config` | Show current settings |
| `sorter config set <key> <value>` | Change a setting |

### Import Existing Files

```bash
# Import and sort files from an existing folder
sorter import "D:\Old Downloads"
```

## Folder Structure

After running `sorter init`, this structure is created:

```
Your Chosen Location/
├── 00_INBOX/         # Drop files here (always watched)
├── 00_PINNED/        # Protected files (won't be moved)
├── 10_Documents/     # PDF, DOC, XLS, TXT, etc.
├── 20_Executables/   # EXE, MSI, installers
├── 30_Archives/      # ZIP, RAR, 7Z, TAR
├── 40_Media/         # Images, video, audio
├── 50_Code/          # Source code, configs
├── 60_ISOs/          # Disk images
├── 80_Big_Files/     # Files larger than 1GB
└── _UNSORTED/        # Unknown file types
```

## Dashboard Controls

The interactive dashboard (`sorter dashboard`) supports these keyboard shortcuts:

| Key | Action |
|-----|--------|
| `1` `2` `3` | Switch between tabs |
| `Tab` | Next tab |
| `↑` `↓` | Navigate lists |
| `s` | Sort files now |
| `r` | Refresh display |
| `?` | Show help |
| `q` or `Esc` | Quit |

## File Browser Controls

The file browser (`sorter browse`) lets you manage sorted files:

| Key | Action |
|-----|--------|
| `↑` `↓` or `j` `k` | Navigate files |
| `←` `→` or `h` `l` | Switch between panels |
| `Enter` | Open selected file |
| `p` | Pin file (move to 00_PINNED) |
| `m` | Move to different category |
| `q` or `Esc` | Quit |

## Configuration Options

View with `sorter config`, change with `sorter config set`:

| Setting | Default | Description |
|---------|---------|-------------|
| `SettleTimeSeconds` | 180 | Seconds a file must be unchanged before sorting |
| `BigFileThreshold` | 1GB | Files larger than this go to 80_Big_Files |
| `EnableBigFileRouting` | true | Whether to use the big file rule |
| `ShowNotifications` | true | Show notifications when files are sorted |

**Example:**
```bash
# Change settle time to 60 seconds
sorter config set SettleTimeSeconds 60

# Increase big file threshold to 2GB
sorter config set BigFileThreshold 2147483648
```

## Category Rules

Default file routing:

| Category | Extensions |
|----------|------------|
| Documents | .pdf, .doc, .docx, .xls, .xlsx, .ppt, .pptx, .txt, .rtf, .csv, .epub |
| Executables | .exe, .msi, .msix, .appx, .bat, .cmd |
| Archives | .zip, .rar, .7z, .tar, .gz, .bz2, .xz, .tgz |
| Media | .jpg, .png, .gif, .mp4, .mkv, .avi, .mp3, .flac, .wav |
| Code | .json, .xml, .yml, .py, .js, .ts, .cs, .java, .go, .rs |
| ISOs | .iso, .img, .dmg, .vhd, .vhdx, .vmdk |

**Customize with:**
```bash
# Add a rule for font files
sorter rules add
# Follow the prompts to select category and extensions

# Test where a file would go
sorter rules test "myfont.ttf"
```

## Typical Workflows

### Set Up Browser Downloads
```bash
# Point your browser downloads to the INBOX
# Or add your existing downloads folder:
sorter watch add "C:\Users\Me\Downloads"

# Run in background
sorter sort --loop
```

### One-Time Cleanup
```bash
# Preview what would happen
sorter sort -n

# If it looks good, sort for real
sorter sort
```

### Organize Old Downloads
```bash
# Import from an old folder
sorter import "D:\Old Stuff\Downloads"
```

### Quick File Management
```bash
# Open the file browser
sorter browse

# Or jump to a specific category
sorter browse Media
```

## Tips

1. **Set your browser to download to `00_INBOX`** - Files dropped here are automatically sorted when running `--loop`

2. **Use multiple watch folders** - Add your browser downloads, torrent client output, etc.

3. **Pin important files** - Use `sorter browse` and press `p` to protect files from being moved

4. **Settle time matters** - The default 3-minute wait ensures downloads complete before sorting. Adjust with `sorter config set SettleTimeSeconds <seconds>`

5. **Check unknown types** - Periodically check `_UNSORTED` and add rules for common file types you download

## Data Locations

- **Config:** `%LOCALAPPDATA%\DownloadSorter\appsettings.json`
- **Database:** `%LOCALAPPDATA%\DownloadSorter\sorter.db`

## Troubleshooting

**Files not being sorted?**
- Check if the file extension is in a category rule: `sorter rules`
- Verify the watch folder is configured: `sorter watch`
- Make sure the file has been stable for the settle time (default 3 minutes)

**File conflicts?**
- Files are never overwritten. Duplicates get renamed: `file.pdf` → `file (2).pdf`

**Want to undo?**
- Check history: `sorter history`
- Search for a file: `sorter search filename`
- Use the browser to move files back: `sorter browse`
