# Contributing to lazer2stable

Thanks for helping. Bug reports, translations and pull requests are all welcome.

## Reporting a bug

[Open an issue](https://github.com/exotiquepy/lazer2stable/issues/new) with what you did, what you expected and what happened. If lazer2stable showed an error, attach the file it saved in `%LOCALAPPDATA%\lazer2stable\errors`. The report of the copy (`%LOCALAPPDATA%\lazer2stable\reports`) helps too.

## Building

Requires the .NET 8 SDK.

```
dotnet publish lazer2stable.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

lazer2stable reads lazer's database with the same Realm version as lazer (`RealmVersion` in `lazer2stable.csproj`). If lazer moves to a newer Realm, align it with `osu.Game/osu.Game.csproj` in [ppy/osu](https://github.com/ppy/osu).

## Project layout

| Folder | Content |
|---|---|
| `src/Core` | Reading lazer (Realm), scanning Songs, comparing, copying, `collection.db`, scores (`scores.db`, `.osr` replays), skins, undo, translations (`Strings.cs`) |
| `src/App` | Avalonia interface |
| `tests/FakeLibrary` | Generates a fake lazer library (maps, collections, scores, skins) and a stable install covering edge cases |
| `tests/Screens` | Renders every screen to PNG in each language (headless), runs a full copy and its undo (`e2e`), and renders the README images (`showcase`) |
| `tests/RealDbCheck` | Reads a real lazer database and prints a summary |

## Testing

```
dotnet run --project tests/FakeLibrary -- /tmp/fake 300 /path/to/backgrounds
cp -r /tmp/fake/stable /tmp/fake-pristine
dotnet run --project tests/Screens -- /tmp/fake/lazer /tmp/fake/stable /tmp/shots 1280 820 e2e /tmp/fake-pristine
```

The `e2e` run copies everything, undoes it and checks that `scores.db` and `collection.db` are byte-identical to the originals. Without `e2e`, `tests/Screens` writes a screenshot of every screen in French, English and Russian to `/tmp/shots`.

README images: generate the library with `FAKE_REAL_SIZES=1` and 0 bulk maps, run `tests/Screens` with `showcase`, then build `docs/demo.gif` from `frames/list.txt` with ffmpeg.

## Translations

All texts are in `src/Core/Strings.cs`, one entry per text with the French, English and Russian versions. Plurals use `|` between forms (singular|plural, or 1|2-4|5+ in Russian). To add a language, add a value to `Lang` in `Loc.cs` and a column in `Strings.cs`.

## Publishing a release

1. Raise `<Version>` in `lazer2stable.csproj` (and `version` in `app.manifest`), and write the notes in `.github/release-notes.md`.
2. Push to `main`, then **Actions** → **Release** → **Run workflow**.

The workflow builds `lazer2stable.exe`, creates the tag (`v2.5` for version `2.5.0`) and publishes the release with the exe and its SHA-256. It stops if that release already exists.
