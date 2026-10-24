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

### Changed

- `AppBarHelper.RegisterBar` signature changed to `RECT RegisterBar(IntPtr hwnd, RECT desiredRect, ABEdge edge, int thickness)`. It previously returned `void` and took no thickness argument.
- `AppBarHelper.RECT` gained `Width` and `Height` convenience properties, and `SWP_NOZORDER` was added to the flag constants.
- `BannerWindow` no longer sets `Top`, `Left`, `Width`, or `Height`; `WindowStartupLocation` is set to `Manual` and all placement happens in `SourceInitialized`.
- Removed `StartupUri="MainWindow.xaml"` from `App.xaml`; banner windows are constructed directly in `OnStartup`.
- Corrected the `xmlns:local` namespace in `BannerWindow.xaml` from `ClassificationBannerApp` to `ClassificationBanner`.
- Added `<RootNamespace>ClassificationBanner</RootNamespace>` to `ClassificationBannerApp.csproj` so newly added files default to the correct namespace.
- `LoadConfiguration` now accepts a nullable override path and distinguishes explicit from implicit configuration sources when handling errors.
- Termination on fatal configuration errors uses `Environment.Exit` rather than `Application.Shutdown`, because `Shutdown` only posts a dispatcher request and would allow `OnStartup` to continue into registry writes and banner creation.

### Removed

- Deleted the unused `MainWindow.xaml` and `MainWindow.xaml.cs` scaffolding, which resided in the mismatched `ClassificationBannerApp` namespace.

## Known limitations

By design, the banners cannot be moved, resized, or dismissed by the user - they are docked to the screen.
The process must be terminated through Task Manager or by removing it from startup.

A settings file can parse successfully while still being semantically wrong. A `BannerHeight` of `0` produces invisible banners, and an unparseable color falls back to red on white, which misrepresents the configured classification color. Semantic validation is not yet implemented.

The system tray option described above is not yet implemented.