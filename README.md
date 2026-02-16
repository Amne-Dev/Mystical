# Mystical

## WinUI 3 Rewrite (Windows 11 Fluent)

A WinUI 3 implementation lives .

### Current state

- Fluent shell with `NavigationView`, Mica backdrop fallback, and Windows 11-focused card layout.
- Library page with search, platform filter, favorites, launch, empty/loading states, and animated grid entry.
- Settings page with local persisted preferences.
- SQLite-backed game library in `%LocalAppData%\MysticalWinUI\mystical_library.db`.
- Platform detection migrated from the Qt architecture:
  - Steam: `libraryfolders.vdf` + `appmanifest_*.acf` parsing.
  - Epic: `LauncherInstalled.dat` parsing.
  - GOG: Galaxy SQLite + registry fallback.
  - Minecraft: `.minecraft` + `launcher_profiles.json` parsing.
  - Xbox + Microsoft Store: `XboxGames` / `ModifiableWindowsApps` roots, `MicrosoftGame.config`, and `AppxManifest.xml` package fallback.
- Linked-account discovery:
  - Steam owned games (installed + not installed), with automatic local Steam user detection and optional Web API key.
  - Epic account cache discovery from launcher `.item` manifests (installed + not installed).
- Cover art support:
  - Remote cover URL ingestion from detectors.
  - Manual cover import from file picker into `%LocalAppData%\MysticalWinUI\covers`.
- Library view modes:
  - Switch between Grid and List views from the library toolbar.

### Run

```powershell
cd winui\Mystical.WinUI
dotnet run
```

### Notes
- WinUI detectors are Windows-only and rely on local launcher files/registry presence.

## Website

- Static site source: `site/`
- Privacy policy page: `site/privacy.html`
- GitHub Pages deployment workflow: `.github/workflows/deploy-pages.yml`
- Local preview:

```powershell
python -m http.server 8080 -d site
```
