# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- **Command line selection of the settings file.** A new `--config <path>` switch (with `-c` short form, `--config=<path>` and `-c=<path>` inline forms, and a bare positional path) overrides the default `appsettings.json`. Paths are trimmed of stray quotes and resolved to absolute form against the working directory via `Path.GetFullPath`.
- **Persistence of the selected settings file across reboots.** `EnsureStartupRegistration` now appends `--config "<path>"` to the `HKCU\...\Run` value when an override is active, so a reboot no longer silently reverts to the default configuration.
- **Fail-fast handling for invalid configuration.** A new `FailFast` helper displays an error dialog and terminates with exit code 1 when an explicitly requested settings file is missing, unreadable, malformed, or deserializes to null. The implicit `appsettings.json` retains its silent fallback to built-in defaults.
- **DPI-aware banner thickness.** `BannerHeight` is now treated as a device-independent measurement and scaled to physical pixels using the window's `CompositionTarget.TransformToDevice`, so banners render at a consistent apparent size across display scaling factors.
- **Per-monitor-v2 DPI awareness.** Added `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` to the project file. Without it the process is system-DPI-aware and Windows virtualizes `Screen.Bounds` on monitors whose scaling differs from the primary display, offsetting AppBar placement. This is configured as a project property rather than in `app.manifest` because the WinForms SDK rejects manifest DPI settings with warning WFO0003.
- `AppBarHelper.NotifyPosChanged` sends `ABM_WINDOWPOSCHANGED` after placement so the shell refreshes its work-area bookkeeping.
- **Unit test suite.** New xUnit project at `tests/ClassificationBannerApp.Tests.csproj` with 76 tests covering command line interpretation, configuration loading and its failure policy, banner placement geometry, and the settings contract. Line coverage of the testable code is 100% (98.63% branch). Tests assert on observable behavior rather than implementation detail.
- **Solution file.** Added `ClassificationBannerApp.sln` containing the application and test projects, so the suite is discoverable by Test Explorer and `dotnet test`.
- **GitHub Actions build pipeline.** New `.github/workflows/build.yml` runs on push and pull request to `main`/`master`, and on manual dispatch. It restores, builds in `Release`, runs the test suite with coverage collection, and publishes the application on a `windows-latest` runner, which is required because `net9.0-windows` with WPF cannot be built on Linux. Test results, coverage reports, and published binaries are uploaded as artifacts; the first two use `if: always()` so failing runs remain diagnosable.

### Fixed

- **Banners were misaligned against the taskbar.** The top banner was offset downward by the taskbar thickness and the bottom banner overlapped the notification area. `RegisterBar` sent `ABM_QUERYPOS` but discarded the rectangle the shell returned, so the reserved area and the visible window never coincided. `RegisterBar` now returns the granted rectangle and `BannerWindow` moves itself onto it with `SetWindowPos`.
- **Banner thickness was lost during AppBar negotiation.** `ABM_QUERYPOS` adjusts only the docking edge, leaving the opposite edge at its original value. The opposite edge is now re-derived from the adjusted edge before `ABM_SETPOS`.
- **Banners were mispositioned and mis-sized under display scaling.** Placement mixed physical pixels from `Screen.Bounds` with device-independent `Window.Top/Left/Width/Height` values. Placement is now performed entirely in physical pixels via `SetWindowPos`.
- **Application produced no visible windows on launch.** `App.xaml` declared `x:Class="ClassificationBannerApp.App"` while `App.xaml.cs` declared `namespace ClassificationBanner`. The mismatch caused the XAML compiler to emit its own `ClassificationBannerApp.App` entry point, so the real `OnStartup` override - and therefore all banner creation - never ran. `x:Class` now matches the code-behind namespace.
- **Process exited immediately during banner rebuilds.** With no main window, the default `ShutdownMode.OnLastWindowClose` terminated the app at the point `RebuildBanners` closed all existing banners before recreating them. `ShutdownMode` is now explicitly set to `OnExplicitShutdown` in `OnStartup`.
- **Invalid configuration paths were written to the startup registry.** Configuration is now validated before `EnsureStartupRegistration` runs, so a bad path cannot be persisted and repeat the failure on every boot.
- **Top banner was still mis-located after the initial AppBar fix.** Placement was performed in `SourceInitialized`, which runs before WPF completes its own window placement during `Show()`. WPF then issued its own `SetWindowPos` using unset default `Left`/`Top`/`Width`/`Height` values, overwriting the negotiated coordinates. Placement now happens in the `Loaded` handler, and the granted rectangle is cached in `_grantedRect`.
- **WPF managed layout properties disagreed with the native window rectangle.** `Left`, `Top`, `Width`, and `Height` are now synced to the granted rectangle (converted from physical pixels to DIPs), so subsequent layout passes no longer reposition the banner.
- **Displaced banners never recovered.** The enforcement timer passed `SWP_NOMOVE | SWP_NOSIZE`, correcting only Z-order. It now re-asserts the full granted rectangle each tick.
- **`CommandLineOptions` hardened against inputs the previous parser mishandled.** A trailing `--config` with no value now yields no override instead of falling through to later arguments; null and empty argument entries are skipped rather than treated as paths.
- **`appsettings.json` was never deployed.** The file had no `CopyToOutputDirectory` setting, so it was absent from both build and publish output. `ConfigurationLoader` reads it from the executable directory, meaning a deployed build silently fell back to the built-in `UNCLASSIFIED` defaults instead of the configured markings - a significant defect for a classification-marking tool. Added a `None Update` item with `PreserveNewest`; the file now appears in publish output.

### Changed

- `AppBarHelper.RegisterBar` signature changed to `RECT RegisterBar(IntPtr hwnd, RECT desiredRect, ABEdge edge, int thickness)`. It previously returned `void` and took no thickness argument.
- `AppBarHelper.RECT` gained `Width` and `Height` convenience properties, and `SWP_NOZORDER` was added to the flag constants.
- `BannerWindow` no longer sets `Top`, `Left`, `Width`, or `Height`; `WindowStartupLocation` is set to `Manual` and all placement happens in `SourceInitialized`.
- Removed `StartupUri="MainWindow.xaml"` from `App.xaml`; banner windows are constructed directly in `OnStartup`.
- Corrected the `xmlns:local` namespace in `BannerWindow.xaml` from `ClassificationBannerApp` to `ClassificationBanner`.
- Added `<RootNamespace>ClassificationBanner</RootNamespace>` to `ClassificationBannerApp.csproj` so newly added files default to the correct namespace.
- **Extracted testable logic from the WPF hosts.** Three new units were introduced so behavior could be verified without a desktop session:
  - `CommandLineOptions` - argument grammar, path normalization, and construction of the startup registry command.
  - `ConfigurationLoader` - settings resolution and load outcome, returning a `ConfigLoadResult` that separates the decision to abort from the act of aborting. File access is injected.
  - `BannerGeometry` - thickness, desired rectangle, post-negotiation thickness re-derivation, and physical-to-logical bounds conversion.
  `App`, `BannerWindow`, and `AppBarHelper` now delegate to these units rather than carrying private copies of the same logic.
- Marked `App`, `BannerWindow`, and `AppBarHelper` `[ExcludeFromCodeCoverage]`; they require a live WPF message loop and shell, and their decision logic now lives in the extracted units.
- Excluded `tests\**` from the application project's default compile glob, which was otherwise pulling test sources into the WPF temporary project and breaking the build.
- Added `.vs/` and `publish/` to `.gitignore`; the Visual Studio workspace folder was previously untracked-but-not-ignored, and the pipeline's local publish output would otherwise appear as pending changes.

### Removed

- Deleted the unused `MainWindow.xaml` and `MainWindow.xaml.cs` scaffolding, which resided in the mismatched `ClassificationBannerApp` namespace.
- Deleted the superseded private `ParseConfigPathArgument`, `NormalizePath`, and `LoadConfiguration` members from `App`, which had become dead duplicates of the extracted units.

## Known limitations

By design, the banners cannot be moved, resized, or dismissed by the user - they are docked to the screen.
The process must be terminated through Task Manager or by removing it from startup.

A settings file can parse successfully while still being semantically wrong. A `BannerHeight` of `0` produces invisible banners, and an unparseable color falls back to red on white, which misrepresents the configured classification color. Semantic validation is not yet implemented.

The system tray option described above is not yet implemented.