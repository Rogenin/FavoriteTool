# Project Favorites Window

A lightweight Unity Editor tool for organizing frequently used project assets in one place.

`Project Favorites Window` lets you add folders, scripts, prefabs, materials, and other project assets to a dedicated Favorites window for fast access. It supports drag-and-drop, search, per-item color highlighting, reordering, and persistent saved state between Unity sessions.

---

## Features

- Custom Favorites window inside the Unity Editor
- Drag-and-drop support from the Project window
- Works with folders and regular assets
- Search by asset name, path, or type
- Quick actions for:
  - Select
  - Open
  - Remove
  - Move Up
  - Move Down
- Per-item custom color highlighting
- Persistent saved state between Unity sessions
- Missing asset detection
- Editor-only tool with no runtime dependency

---

## Installation

1. Import the package into your Unity project.
2. Wait for Unity to compile scripts.
3. Open the tool from:

`Tools/Favorites`

---

## How to Use

### Open the window
Go to:

`Tools/Favorites`

This will open the Favorites window.

### Add assets
Drag assets directly from the **Project** window into the drop area at the top of the Favorites window.

You can add:
- folders
- scripts
- prefabs
- materials
- textures
- other project assets

### Search
Use the search field in the toolbar to filter favorites.

The search works by:
- asset name
- asset path
- asset type

### Select an asset
Click **Select** to highlight the asset in the Project window.

### Open an asset
Click **Open** to open the asset.

- Regular assets will open normally.
- Folders will be revealed in the Project window.

### Reorder favorites
Use:
- **↑** to move an item up
- **↓** to move an item down

### Remove an item
Click **Remove** to delete the item from the favorites list.

This does **not** delete the original asset from the project.  
It only removes it from the Favorites window.

### Use color highlighting
Each favorite can have its own color.

- Enable the **Color** toggle for an item
- Pick the color you want
- Use **Reset** to restore the default color

This is useful for visually separating categories such as:
- gameplay scripts
- UI assets
- art folders
- important prefabs

### Clear all favorites
Click **Clear All** in the toolbar to remove the entire favorites list.

---

## Saved State

The tool saves the favorites list and color settings between Unity sessions.

Your saved data is stored in the project settings, so your Favorites window remains available after restarting Unity.

---

## Notes

- This tool is **Editor-only**
- It does **not** affect runtime builds
- It does **not** require scene setup
- It does **not** require external packages

If an asset is deleted or moved in a way that breaks its GUID reference, it will appear as a missing entry in the list until removed.

---

## Support

If you encounter an issue or have a feature request, feel free to contact the publisher.

---

## Please Rate

If this tool saves you time and improves your workflow, please consider leaving a rating and a review.

Your feedback helps a lot and supports further updates and improvements.

Thank you for using **Project Favorites Window**.