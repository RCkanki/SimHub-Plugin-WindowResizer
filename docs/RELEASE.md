# Maintainer: GitHub Release checklist

1. **Version**
   - Bump `<Version>`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion` in `WindowResizer.csproj` if needed.
   - Add a section to `CHANGELOG.md` for the new version and move items out of `[Unreleased]` when appropriate.

2. **Build and ZIP (local; SimHub must be installed for HintPath references)**

   If SimHub is loading the plugin from `bin\Release\net48\`, **quit SimHub first** so `dotnet build` can overwrite `WindowResizer.dll`.

   ```powershell
   cd <repo-root>
   .\scripts\package-release.ps1
   ```

   Output: `artifacts/WindowResizer-v<version>.zip` containing `WindowResizer.dll`, `LICENSE`, `docs/INSTALL.txt`, and `CHANGELOG.md`.

3. **SHA-256 (optional, for release notes)**

   ```powershell
   Get-FileHash .\artifacts\WindowResizer-v0.1.0.zip -Algorithm SHA256
   ```

   Paste the hash into the GitHub Release description so users can verify the download.

4. **Git tag**

   ```text
   git tag v0.1.0
   git push origin v0.1.0
   ```

   Use the same version as in the `.csproj` (e.g. `v` + `Version`).

5. **GitHub Release**
   - Create a release from the tag.
   - Attach the ZIP from `artifacts/`.
   - Copy the relevant `CHANGELOG.md` section into the release notes.
   - Optional: paste the UI screenshot (`docs/window-resizer-ui.png`) into the description.

## GitHub Actions

`.github/workflows/repo-files.yml` checks that release-related files stay present.  
**Hosted runners do not include SimHub**, so automated builds of this project are not configured. Package the ZIP on a machine where SimHub (and matching HintPaths) is installed.
