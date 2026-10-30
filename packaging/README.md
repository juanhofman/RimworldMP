# Packaging — single Setup.exe

Builds **`RimworldMP-Setup-<version>.exe`**: one file for Person 1 + Person 2.

## What the installer does

1. Installs the self-contained Avalonia launcher (win-x64)
2. Copies **RimworldMP** + **Harmony** into the RimWorld `Mods` folder (Steam path auto-detected)
3. Creates Desktop + Start Menu shortcuts
4. Optionally launches RimworldMP at the end

## Build (Windows)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/build-setup.ps1
```

Outputs:

- `packaging/dist/RimworldMP-Setup-<version>.exe`
- Copy on Desktop (default)
- Optional `-StorePath` for the Project Agent store

Requires: .NET 10 SDK, [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`ISCC.exe`).

Harmony **mod** zip is downloaded at build time from the official GitHub release (not vendored in git). Game DLLs are never packaged.
