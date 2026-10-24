# classification-banner

This is a classification banner used to indicate the classification level of the computer.
The classification level is determined by the organization that created the document and is used to protect sensitive information.
This application can show the classification at the top and bottom of the screen, and optionally in the system tray.

## Features

- **Multi-monitor support** - a top and bottom banner is created for every display reported by `Screen.AllScreens`.
- **AppBar docking** - banners register as Windows AppBars via `SHAppBarMessage` and are placed on the rectangle the shell grants during `ABM_QUERYPOS`, so they coexist with the taskbar instead of overlapping it, and maximized windows reserve space for them.
- **DPI-aware sizing** - the application opts into per-monitor-v2 DPI awareness via the `ApplicationHighDpiMode` project property, and `BannerHeight` is interpreted in device-independent units then converted to physical pixels using each window's DPI transform, so banners appear the same size at any display scaling factor.
- **Self-healing placement** - the enforcement timer re-asserts position, size, and Z-order on every tick, so a banner displaced by another application or a layout pass returns to its reserved area.
- **Always-on-top enforcement** - a timer periodically reasserts topmost Z-order using `SetWindowPos` with `SWP_NOACTIVATE`, so focus is never stolen from the active application.
- **Display-change resilience** - banners are rebuilt when `SystemEvents.DisplaySettingsChanged` fires, debounced by 500 ms so a burst of notifications triggers only one rebuild.
- **JSON configuration** - banner text, colors, height, and enforcement interval are read from a settings file at startup.
- **Selectable settings file** - the configuration file can be chosen on the command line, which is useful for per-environment or per-classification deployments.
- **Fail-fast on bad configuration** - an explicitly requested settings file that is missing or malformed aborts startup with an error dialog rather than silently displaying default markings.
- **Automatic startup registration** - the executable registers itself under `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, preserving the selected settings file, and fails silently when blocked by group policy.

## Known limitations

-By design, the banners cannot be moved, resized, or dismissed by the user - they are docked to the screen.	
The process must be terminated through Task Manager or by removing it from startup.

-AppBar space negotiation is performed by the shell primarily for the primary display. On secondary monitors the banner is positioned correctly, but the desktop work area may not be reduced, so maximized windows can be covered.

-A settings file can parse successfully while still being semantically wrong. A `BannerHeight` of `0` is clamped to a single pixel, and an unparseable color falls back to red on white, which misrepresents the configured classification color. Semantic validation is not yet implemented.

-The system tray option described above is not yet implemented.

## Configuration

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `ClassificationText` | string | `UNCLASSIFIED` | Text rendered in both banners |
| `BackgroundColorHex` | string | `#FF008000` | Banner background, `#AARRGGBB` or `#RRGGBB` |
| `ForegroundColorHex` | string | `#FFFFFF` | Banner text color |
| `BannerHeight` | int | `30` | Banner height in device-independent pixels |
| `EnforcementIntervalMs` | int | `500` | Z-order enforcement timer interval |

Configuration failure behavior differs depending on how the file was selected:

| Scenario | Result |
| --- | --- |
| No `--config`, default file present and valid | Loads file |
| No `--config`, default file absent or malformed | Silent fallback to built-in defaults |
| `--config <path>`, file valid | Loads file |
| `--config <path>`, file missing, malformed, or empty | Error dialog, process exits with code 1 |

## Build and run

| Argument | Description |
| --- | --- |
| `--config <path>`, `-c <path>` | Path to the settings file. Also accepts `--config=<path>` and `-c=<path>`. |
| `<path>` | A single bare path is treated as the settings file. |

Relative paths resolve against the current working directory. The selected path is written into the startup registry entry, so the choice persists across reboots.

## Tests

```
dotnet test
```

With coverage:

```
dotnet test --collect:"XPlat Code Coverage"
```

The suite covers the behavior that can be verified without a live desktop session: command line interpretation, configuration loading and its failure policy, and banner placement geometry. Tests assert on observable outcomes - which settings file is selected, whether startup proceeds or aborts, where a banner ends up on screen - rather than on how those results are reached.

The WPF hosts (`App`, `BannerWindow`) and the shell interop wrapper (`AppBarHelper`) are marked `[ExcludeFromCodeCoverage]`. They require a running message loop and a live shell, so their logic was factored into `CommandLineOptions`, `ConfigurationLoader`, and `BannerGeometry`, which are covered directly. Their remaining bodies are composition and P/Invoke declarations, and still need manual verification on a real multi-monitor desktop.
