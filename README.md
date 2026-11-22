# Roundabout Builder — RELEASE 1.9.8

Cities: Skylines mod. Builds a roundabout on an existing junction in one click: the old junction
is wiped, the incoming roads are reconnected to a new ring, and the ring is built for you.

- Original mod and code: **Strad** (Steam Workshop 1625704117)
- Current fork and maintenance: **Strdate**
- Workshop page: _add the link after publishing_

## Features

Baseline (inherited from 1.9.7): one-click roundabouts, free cursor mode, elliptic roundabouts,
TMPE policies applied on build, snapping algorithms, prefab filter, unlimited radius, elevation
handling, level terrain, keep-window-open, undo, glitched-roads rescue tool.

Added in 1.9.8:

| Feature | Notes |
| --- | --- |
| Built-in localization | `Translations/lang.txt` (English), `lang_zh-cn.txt`, `lang_zh-tw.txt`. Any `lang_<code>.txt` dropped into the same folder is picked up at load — no recompile. |
| Spiral ramp tool | Radius / height / turns / end offset, live segment count and grade, right-click steps the start heading 15°, structure dropdown Auto / Elevated / Bridge via Network Anarchy. |
| Ellipse tool rebuilt | Adjust mode: hover preview + click to build. Main axis / minor axis / angle fields, ALT ± 15°, right-click ± 15°. Segment count derived from circumference (one per 12 m chord). Center snaps to a junction or is placed freely. Structure: Ground / Elevated / Bridge. |
| Free cursor structure | Same Ground / Elevated / Bridge dropdown; elevation from the Elevation field. |
| Network Skins Continued integration | Single optional setting `hidePillarsAndLights`, **off by default**. Registers built segments with Network Skins so elevated/bridge spirals, ellipses and free-cursor roundabouts carry no pillars and no street lights. Absent Network Skins, or an unrecognised lane prop, changes nothing and only logs. |
| Unified UI | `Dock to Unified UI` setting registers the roundabout button in the UUI toolbar. |
| UI width | Window width follows the current language so long modded road names fit. |

## Layout

```
RoundaboutBuilder/
  EllipseTool.cs            ellipse tool (adjust mode)
  FreeCursorTool.cs         free cursor + structure dropdown
  HelixTool.cs              spiral ramp tool
  Tools/
    Ellipse.cs              ellipse math, segment density
    FinalConnector.cs       node/segment construction, optional prefab override
    HelixPath.cs            spiral path
    StructureVariants.cs    reflection bridge to Network Anarchy variants
    NetworkSkinsHook.cs     reflection bridge to Network Skins Continued
    ...
  Localization/Loc.cs       lang_<code>.txt loader
  Translations/             language files
  UI/                       panels, UUI integration
```

## Building

Open `RoundaboutBuilder.sln` in Visual Studio and build in Release / Any CPU, or:

```
msbuild RoundaboutBuilder.sln /p:Configuration=Release "/p:Platform=Any CPU"
```

References that are not in the repo and must exist on your machine:

- `Assembly-CSharp.dll`, `ColossalManaged.dll`, `ICities.dll`, `UnityEngine.dll` from the game's
  `Cities_Data\Managed` folder (the csproj points at a local Steam path — edit it).
- `TrafficManager.dll` + `CSUtil.Commons.dll` — Traffic Manager: President Edition.
- `NetworkAnarchy.dll` — Network Anarchy. The csproj points at a workshop folder path.
- `UnifiedUILib.dll` — Unified UI.
- `FineRoadTool.dll` and `Ionic.Zip.dll` — legacy optional references. If you do not have them,
  either drop the references or build small stub assemblies with `csc /target:library`; the code
  only touches them through reflection-light code paths and the mod runs without them.

`FineRoadTool` in particular is no longer shipped on the workshop, so most builds use a stub.

## Translations

Copy `Translations/lang.txt` to `lang_<code>.txt`, translate the values after the first space,
keep the `RAB_` keys untouched, set `RAB_LANGUAGE_CODE` in the copy. Format: `KEY value`, one per
line, `\n` for a line break, `#` starts a comment. Multi-byte files must be saved as UTF-8.

## Credits

- **Strad** — original mod and most of this code.
- **Quboid** — Network Anarchy support (PR #22) and Network Anarchy itself.
- **kianzarrin** — elevation improvements (PR #13, PR #14).
- **Adam Dřínek**, **DerGatt** — earlier pull requests.
- **BetTD** — the UUI Fix fork, which is where Unified UI Continued support was worked out first.
- **Timiy**, **gzygood**, **Catnip.** — earlier Chinese localizations of this mod.

## Links

- Source: https://github.com/Strdate/AutomaticRoundaboutBuilder
- Issues: https://github.com/Strdate/AutomaticRoundaboutBuilder/issues
- Original: https://steamcommunity.com/sharedfiles/filedetails/?id=1625704117
