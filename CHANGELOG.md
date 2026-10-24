# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed

- **Application produced no visible windows on launch.** `App.xaml` declared `x:Class="ClassificationBannerApp.App"` while `App.xaml.cs` declared `namespace ClassificationBanner`. The mismatch caused the XAML compiler to emit its own `ClassificationBannerApp.App` entry point, so the real `OnStartup` override — and therefore all banner creation — never ran. `x:Class` now matches the code-behind namespace.
- **Process exited immediately during banner rebuilds.** With no main window, the default `ShutdownMode.OnLastWindowClose` terminated the app at the point `RebuildBanners` closed all existing banners before recreating them. `ShutdownMode` is now explicitly set to `OnExplicitShutdown` in `OnStartup`.

### Changed

- Removed `StartupUri="MainWindow.xaml"` from `App.xaml`; banner windows are constructed directly in `OnStartup`.
- Corrected the `xmlns:local` namespace in `BannerWindow.xaml` from `ClassificationBannerApp` to `ClassificationBanner`.
- Added `<RootNamespace>ClassificationBanner</RootNamespace>` to `ClassificationBannerApp.csproj` so newly added files default to the correct namespace.

### Removed

- Deleted the unused `MainWindow.xaml` and `MainWindow.xaml.cs` scaffolding, which resided in the mismatched `ClassificationBannerApp` namespace.