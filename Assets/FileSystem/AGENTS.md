# Persistence

- [GameFile](Scripts/GameFile.cs) reads and writes files, validates candidate
  data and recovers backups. [GameFileHandler](Scripts/GameFileHandler.cs)
  loads the configured files at startup.
- [SaveFile](Scripts/SaveFile.cs) owns the versioned campaign snapshot.
  [SettingsFile](Scripts/SettingsFile.cs) owns local preferences.
- [NetworkSaveables](Scripts/NetworkSaveables.cs) maps stable prefab IDs.
  [NetworkItemsHandler](Scripts/NetworkItemsHandler.cs) restores live objects
  and remembers which authored furniture was packed. Keep IDs stable when
  reordering the registry. Restoring a moved station must create exactly one
  replacement.
- [SaveGameButton](Scripts/SaveGameButton.cs) allows manual saves at the base
  after cleanup. [LoadSaveButton](Scripts/LoadSaveButton.cs) displays the state
  of its assigned slot. The host writes campaign progress after mission resolution.

Exercise format changes with a new slot, an older supported file, and a damaged
primary with a valid backup. Use temporary slots. Verify recovery and that a
newer-format slot survives an attempted write. For item changes, reload a
base once with a packed station, then again after placing it. Check the cube
or replacement and its original spot on each reload.
See [Systems](../../docs/SYSTEMS.md#persistence-and-settings).
