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
- **Per-edge, three-cell text** - the top and bottom banners can each show different text, and each carries independent left, center, and right cells.
- **Live system tokens** - cells can embed `{hostname}`, `{ipaddress}`, `{username}`, `{domain}`, `{macaddress}`, `{date}`, `{time}`, or `{datetime}`, refreshed periodically when the value can change.
- **Selectable settings file** - the configuration file can be chosen on the command line, which is useful for per-environment or per-classification deployments.
- **Fail-fast on bad configuration** - an explicitly requested settings file that is missing or malformed aborts startup with an error dialog rather than silently displaying default markings.
- **Automatic startup registration** - the executable registers itself under `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, preserving the selected settings file, and fails silently when blocked by group policy.
- **Continuous integration** - GitHub Actions workflows validate every pull request and, on merge to the default branch, version, tag, and publish a release with both deployment flavors attached.

## Known limitations

- By design, the banners cannot be moved, resized, or dismissed by the user - they are docked to the screen.	
  The process must be terminated through Task Manager or by removing it from startup.
- AppBar space negotiation is performed by the shell primarily for the primary display.
  On secondary monitors the banner is positioned correctly, but the desktop work area may not be reduced, so maximized windows can be covered.
- A settings file can parse successfully while still being semantically wrong.
  A `BannerHeight` of `0` is clamped to a single pixel, and an unparseable color falls back to red on white, which misrepresents the configured classification color.
- Semantic validation is not yet implemented.
- The system tray option described above is not yet implemented.

## Configuration

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `ClassificationText` | string | `UNCLASSIFIED` | Fallback text, centered, for any edge without its own configuration |
| `BackgroundColorHex` | string | `#FF008000` | Banner background, `#AARRGGBB` or `#RRGGBB` |
| `ForegroundColorHex` | string | `#FFFFFF` | Banner text color |
| `BannerHeight` | int | `30` | Banner height in device-independent pixels |
| `EnforcementIntervalMs` | int | `500` | Z-order enforcement timer interval |
| `TextRefreshIntervalSeconds` | int | `30` | How often text containing a time-varying token is re-evaluated; `0` disables refresh |
| `Top` | object | _unset_ | Left, center, and right text for the top banner |
| `Bottom` | object | _unset_ | Left, center, and right text for the bottom banner |

### Banner text

`Top` and `Bottom` are configured independently, and each accepts `Left`, `Center`, and `Right`. 
Any cell may be omitted.
The center cell is centered on the screen regardless of how wide the side cells are, so a long hostname never pushes the classification marking off-center.

```
{
  "ClassificationText": "TOP SECRET // SCI",
  "TextRefreshIntervalSeconds": 30,
  "Top": {
    "Left": "{hostname}",
    "Center": "TOP SECRET // SCI",
    "Right": "{ipaddress}"
  },
  "Bottom": {
    "Left": "{domain}\\{username}",
    "Center": "TOP SECRET // SCI",
    "Right": "{datetime}"
  }
}
```

If an edge is omitted entirely, or all three of its cells are blank, that banner falls back to `ClassificationText` centered.
Settings files written before this feature existed therefore render exactly as before.

### Tokens

| Token | Expands to | Example |
| --- | --- | --- |
| `{hostname}` | Machine name | `WS-12345` |
| `{ipaddress}` | IPv4 address of the first non-loopback interface | `10.1.2.3` |
| `{username}` | Logged-on user | `jdoe` |
| `{domain}` | User's domain | `CONTOSO` |
| `{macaddress}` | MAC of the first non-loopback interface | `00:1A:2B:3C:4D:5E` |
| `{date}` | Current date | `2024-03-07` |
| `{time}` | Current time | `14:05` |
| `{datetime}` | Current date and time | `2024-03-07 14:05` |

Token names are case-insensitive. Tokens may be mixed with literal text, as in `"Host: {hostname}"`.
Write `{{` or `}}` for a literal brace.

An **unrecognised token is left on screen verbatim**, braces included, rather than silently expanding to nothing.
A typo is then visible to the operator instead of quietly blanking part of a marking.
A recognised token whose value is unavailable - `{ipaddress}` on an offline machine, for instance - expands to an empty string.

Only `{ipaddress}`, `{macaddress}`, `{date}`, `{time}`, and `{datetime}` are re-evaluated on the refresh timer.
An edge containing only static text is never refreshed.

### Standard classification levels and colors

The colors below follow the convention used by US government marking guidance, where each
classification level has an associated background color with either black or white text
chosen for contrast. Verify them against your own organization's marking policy before
deployment; this table is a convenience, not an authority.

| Level | Background | Foreground | Swatch |
| --- | --- | --- | --- |
| UNCLASSIFIED | `#FF007A33` (green) | `#FFFFFFFF` (white) | ?? |
| CUI / CONTROLLED UNCLASSIFIED INFORMATION | `#FF502B85` (purple) | `#FFFFFFFF` (white) | ?? |
| CONFIDENTIAL | `#FF0033A0` (blue) | `#FFFFFFFF` (white) | ?? |
| SECRET | `#FFC8102E` (red) | `#FFFFFFFF` (white) | ?? |
| TOP SECRET | `#FFFF8C00` (orange) | `#FF000000` (black) | ?? |
| TOP SECRET // SCI | `#FFFCE300` (yellow) | `#FF000000` (black) | ?? |

Note the foreground change at TOP SECRET and above: orange and yellow backgrounds need
black text to stay legible, whereas the darker levels take white.

**Example - SECRET workstation**

```
{
  "ClassificationText": "SECRET",
  "BackgroundColorHex": "#FFC8102E",
  "ForegroundColorHex": "#FFFFFFFF",
  "BannerHeight": 30,
  "Top": {
    "Left": "{hostname}",
    "Center": "SECRET",
    "Right": "{ipaddress}"
  },
  "Bottom": {
    "Center": "SECRET"
  }
}
```

**Example - UNCLASSIFIED workstation, minimal**

```
{
  "ClassificationText": "UNCLASSIFIED",
  "BackgroundColorHex": "#FF007A33",
  "ForegroundColorHex": "#FFFFFFFF"
}
```

With no `Top` or `Bottom` section, both banners show `ClassificationText` centered.

**Example - TOP SECRET // SCI with dissemination controls**

```
{
  "ClassificationText": "TOP SECRET // SCI",
  "BackgroundColorHex": "#FFFCE300",
  "ForegroundColorHex": "#FF000000",
  "BannerHeight": 35,
  "TextRefreshIntervalSeconds": 30,
  "Top": {
    "Left": "{hostname}",
    "Center": "TOP SECRET // SCI // NOFORN",
    "Right": "{ipaddress}"
  },
  "Bottom": {
    "Left": "{domain}\\{username}",
    "Center": "TOP SECRET // SCI // NOFORN",
    "Right": "{datetime}"
  }
}
```

Colors accept either `#AARRGGBB` or `#RRGGBB`. The alpha channel is included above for
explicitness; `#007A33` is equivalent to `#FF007A33` since banners are fully opaque.
An unparseable color silently falls back to red on white, which would misrepresent the
level, so test any custom value before deploying it.

## Build and run

| Argument | Description |
| --- | --- |
| `--config <path>`, `-c <path>` | Path to the settings file. Also accepts `--config=<path>` and `-c=<path>`. |
| `<path>` | A single bare path is treated as the settings file. |

Relative paths resolve against the current working directory. 
The selected path is written into the startup registry entry, so the choice persists across reboots.

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

## Continuous integration

`.github/workflows/build.yml` validates pull requests and pushes to any branch other than `main` or `master`, and can be triggered manually from the Actions tab. Pushes to the default branch are handled by `release.yml` instead, described under [Versioning and releases](#versioning-and-releases).

The job runs on `windows-latest`; the `net9.0-windows` target framework with WPF and Windows Forms cannot be built on Linux runners. It restores, builds in `Release`, runs the test suite with coverage collection, verifies the coverage threshold, then publishes both deployment flavors.

Line coverage is gated at 85% by `build/Check-Coverage.ps1`, which reads the Cobertura report and exits non-zero if the threshold is not met. Run it locally the same way CI does:

```
dotnet test tests/ClassificationBannerApp.Tests.csproj --collect:"XPlat Code Coverage" --results-directory TestResults
./build/Check-Coverage.ps1 -ResultsDirectory TestResults -MinimumLineRate 85
```

Four artifacts are uploaded on each run:

| Artifact | Contents |
| --- | --- |
| `test-results` | `.trx` test report, uploaded even when tests fail |
| `coverage` | Cobertura coverage report, uploaded even when tests fail |
| `ClassificationBannerApp-win-x64-self-contained` | Single-file executable bundling the .NET runtime |
| `ClassificationBannerApp-win-x64-framework-dependent` | Single-file executable requiring the .NET runtime |

Both deployment flavors are built on every run so packaging problems surface on pull requests rather than at release time. `appsettings.json` is deliberately left as a separate file beside the executable rather than embedded, so the markings can be changed without a rebuild.

The workflow targets the project files directly rather than the solution. The solution uses the newer `.slnx` format, which older SDK versions cannot parse, so both the 9.x and 10.x SDKs are installed to keep CI aligned with local development.

## Versioning and releases

The version lives in a single file, `VERSION`, holding `MAJOR.MINOR.PATCH`. `Directory.Build.props` reads it and applies it to every assembly, so the version is never duplicated across project files. Versions are three-part throughout: the file version, product version, and informational version of a released binary all read exactly `MAJOR.MINOR.PATCH`, with no build or revision component appended.

Two workflows divide the work:

| Workflow | Trigger | Purpose |
| --- | --- | --- |
| `build.yml` | Pull requests, and pushes to any branch except `main`/`master` | Validation only - never mutates the repository |
| `release.yml` | Pushes to `main`/`master`, or manual dispatch | Validates, then versions, tags, and releases |

On a successful push to the default branch, `release.yml` builds, tests, and checks coverage **before** touching the version. Only once all gates pass does it:

1. Increment the patch component in `VERSION` via `build/Bump-Version.ps1`.
2. Confirm the resulting tag does not already exist.
3. Publish both deployment flavors and package each as a zip.
4. Commit the bumped `VERSION`, tag it `vMAJOR.MINOR.PATCH`, and push both.
5. Create a GitHub release with generated notes and both archives attached.

The ordering matters: a failing build never consumes a version number or leaves a dangling tag.

### Release assets

Each release carries two archives. Both are single-file executables built for `win-x64`, and both ship `appsettings.json` alongside the executable so markings can be changed without a rebuild. They differ only in whether the .NET runtime is bundled.

| Asset | Zipped | Extracted | Runtime required on target |
| --- | --- | --- | --- |
| `ClassificationBannerApp-vX.Y.Z-win-x64-self-contained.zip` | ~65 MB | ~162 MB | None |
| `ClassificationBannerApp-vX.Y.Z-win-x64-framework-dependent.zip` | ~0.1 MB | ~0.2 MB | .NET 9 Desktop Runtime |

**Choosing between them**

Take the **self-contained** build for locked-down, air-gapped, or kiosk workstations where you cannot assume a runtime is present or cannot install one. It has no external dependency beyond Windows itself.

Take the **framework-dependent** build where the .NET runtime is already managed centrally, for example by SCCM, Intune, or an imaging pipeline. It is roughly seven hundred times smaller, which matters when pushing the banner to many machines or updating it frequently.

**Dependencies**

Both builds require:

- Windows 10 version 1607 or later, 64-bit. The `win-x64` runtime identifier means neither archive runs on 32-bit Windows or on ARM64 without emulation.
- Permission to write `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` if automatic startup registration is wanted. The application fails silently when group policy blocks this, so the banner still runs but will not persist across reboots.

The framework-dependent build additionally requires the **.NET 9 Desktop Runtime** (`Microsoft.WindowsDesktop.App`), not the base ASP.NET or console runtime. WPF and Windows Forms assemblies live in the desktop pack, so installing only `Microsoft.NETCore.App` leaves the application unable to start. Install it with:

```
winget install Microsoft.DotNet.DesktopRuntime.9
```

Verify what is present on a target machine with:

```
dotnet --list-runtimes
```

A suitable entry looks like `Microsoft.WindowsDesktop.App 9.0.x`. If only `Microsoft.NETCore.App` is listed, the framework-dependent build will not run and the self-contained archive should be used instead.
