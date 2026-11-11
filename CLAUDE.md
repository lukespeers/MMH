# MMH (Multi Monitor Helper)

WPF app (.NET 10, `net10.0-windows7.0`) that switches between display profiles: which displays are enabled and which is primary.

## Build

`dotnet build` fails (MSB4803) because of the COM reference in `MMH.csproj`. Build with Visual Studio's MSBuild instead:

```
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" MMH.csproj -nologo -v:m
```

Output: `bin\Debug\net10.0-windows7.0\MMH.exe`. A running MMH.exe locks this folder, so close it before building.

## How it works

- **Applying a profile**: `MainWindow.ApplyProfile` generates a PowerShell script using the [DisplayConfig](https://www.powershellgallery.com/packages/DisplayConfig) module (`Enable-Display`, `Disable-Display`, `Set-DisplayPrimary`), writes it to `scripts\AlterDisplays.ps1` and runs it elevated (UAC).
  - `WrapWithDiagnostics` wraps each line in its own try/catch by default. Multi-line blocks must use `perLine: false`, because appending `-ErrorAction Stop` after a `}` breaks parsing.
- **Orientation**: after an Apply, each monitor is compared with the orientation last confirmed **for that profile** (`orientation.json`, keyed `Profile{n}|{monitor DeviceID}`). On a mismatch, `OrientationPromptWindow` shows a four-petal "DOWN" clover, and `OrientationConfirmWindow` offers Keep/Revert (15 s auto-revert). Rotation uses `ChangeDisplaySettingsEx` in `MonitorHelper.SetOrientation` and needs no admin rights.
  - DMDO math: picking the petal rotated `k` quarter-turns clockwise sets orientation `(current - k) mod 4`. Verified on real hardware.
  - Windows can't detect a physical rotation of a monitor. Only a change in Windows' own orientation or aspect ratio is detected; the **Check Orientation** button covers the rest.
- **Identify Monitors**: `MonitorBadgeWindow`, a full-screen overlay in the same clover style. Esc or a click closes every monitor's overlay.

## Files at runtime

- `profiles.json` and `orientation.json`: next to the exe.
- Logs: `%LocalAppData%\MMH\logs\mmh-YYYY-MM-DD.log`. They contain the generated script and a transcript with DisplayConfig's before/after display table.

## Workflow

GitHub Issues are the tickets. Each change goes on a branch `feature/<issue#>-<slug>` (created via `gh issue develop`), is tested locally, then merged through a PR whose body says `Closes #<issue#>`. Never commit straight to `master`.
