<p align="center"><img src="Assets/app.png" width="96" alt="lazer2stable"></p>

<h1 align="center">lazer2stable</h1>

<p align="center">Copy the beatmaps, scores and skins you have in <b>osu!lazer</b> to <b>osu!stable</b>, only the ones stable doesn't have yet.<br>
Windows · English / Français / Русский · developed by Exotique</p>

<p align="center"><img src="docs/screenshot-list.png" width="820" alt="lazer2stable: list of maps to copy"></p>

## Download and use

1. Download `lazer2stable.exe` from the [Releases](../../releases) page. Nothing to install.
   The exe isn't code-signed, so Windows may show "Windows protected your PC": click **More info**, then **Run anyway**.
2. Launch it. lazer2stable finds your lazer and stable folders and scans both libraries. You can change either folder with **Change**.
3. Review the lists. **Beatmaps**, **Scores** and **Skins** each have a checkbox: uncheck what you don't need (for example copy only your scores, if your maps are already in stable). Uncheck what you don't want in each list, then click **Copy…** and confirm.
4. Launch osu!stable, go to song select and press **F5**: stable re-reads the Songs folder and adds the new maps.
   (With the `.osz` format, stable imports them by itself on startup, no F5 needed.)
   Your scores are in the **Local Ranking** leaderboard of song select, your skins in **Options > Skin**.

Changed your mind? **Undo the last copy** (at the top of the list, or on the summary screen) removes from stable exactly what that copy added.

If osu! is open, lazer2stable tells you and can close it for you before copying. The interface follows your Windows language; switch with **FR / EN / RU** in the title bar.

## What it does

- **lazer is never modified.** Its database (`client.realm`) is read from a temporary copy.
- **Only missing maps are copied.** A map counts as already in stable when Songs has the same set ID, the same difficulty ID, or an identical `.osu` file, including in an `.osz` that stable hasn't imported yet.
- **Safe copies.** Maps are written to a temporary folder and then moved into Songs (as `123456 Artist - Title`), so a stopped copy never leaves a half-copied map. You can stop at any time.
- **Collections (optional).** Your lazer collections are merged into stable's `collection.db`: collections with the same name get the missing maps, nothing is removed, and the previous file is backed up next to it (`collection.db.lazer2stable-backup-…`).
- **Scores (optional).** Your lazer scores go to stable's Local leaderboard with their replays: the replay is added to `Data\r` and the score to `scores.db`, which is backed up first (`scores.db.lazer2stable-backup-…`); nothing is removed. Scores you played in lazer are converted: the score is lazer's "Classic" display, and stable recalculates accuracy and rank. Their replays may play back a little differently in stable, because lazer judges sliders differently. A score whose map isn't in stable is copied together with its map. Replays of other players that you downloaded or watched in lazer are left out unless you include them.
- **Skins (optional).** Your lazer skins are copied to stable's `Skins` folder, one folder each, named as they were in stable when lazer knows that name. Skins already in stable are recognised, even under another folder name. Skins made in lazer's own skin editor (argon, triangles…) are left out: stable can't read them.
- **Undo.** The last copy is recorded in `%LOCALAPPDATA%\lazer2stable`. Undoing it removes exactly what it added: maps and skins go to the Recycle Bin, and only the added scores and collection entries are taken out of `scores.db` and `collection.db` (each backed up first), so whatever you did in stable since then is kept.
- **A report** of every copy is written to `%LOCALAPPDATA%\lazer2stable\reports`. Your folder choices and language are remembered in `%APPDATA%\lazer2stable`.

<p align="center"><img src="docs/screenshot-scores.png" width="820" alt="lazer2stable: list of scores to copy"></p>

<p align="center"><img src="docs/screenshot-skins.png" width="400" alt="Skins to copy, with previews"> <img src="docs/screenshot-done.png" width="400" alt="Summary after copying"></p>

## Skipped maps and scores

- **Edited in the lazer editor.** lazer saves them in a format (`v128`) that stable can't read. Export them from the lazer editor with the stable-compatible export.
- **lazer-only game modes** (custom rulesets): stable can't play them.
- **Deleted or built-in lazer content**: never shown.
- **Scores with lazer-only mods**, or mods set up differently than in stable (DT at 1.3x, for example): stable couldn't replay them.
- **Scores whose map isn't in stable** and can't be copied there, or whose map stable has in a different version.
- **Skins made in lazer** (based on argon or triangles): stable can't read them.

## Build from source

Requires the .NET 8 SDK.

```
dotnet publish lazer2stable.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

lazer2stable reads lazer's database with the same Realm version as lazer (`RealmVersion` in `lazer2stable.csproj`). If lazer ever moves to a newer Realm, align it with `osu.Game/osu.Game.csproj` in [ppy/osu](https://github.com/ppy/osu).

### Publishing a release

1. Raise `<Version>` in `lazer2stable.csproj` (and `version` in `app.manifest`), and write the notes in `.github/release-notes.md`.
2. Push to `main`, then in **Actions** → **Release** → **Run workflow**.

The workflow builds `lazer2stable.exe`, creates the tag (`v2.5` for version `2.5.0`), and publishes the release with the exe and its SHA-256. It stops if that release already exists.

### Project layout

| Folder | Content |
|---|---|
| `src/Core` | Reading lazer (Realm), scanning Songs, comparing, copying, `collection.db`, scores (`scores.db`, `.osr` replays), skins, undo, translations (`Strings.cs`) |
| `src/App` | Avalonia interface |
| `tests/FakeLibrary` | Generates a fake lazer library (maps, collections, scores, skins) + stable install covering edge cases |
| `tests/Screens` | Renders every screen to PNG in each language, and runs a full copy and its undo (`e2e`) headless |
| `tests/RealDbCheck` | Reads a real lazer database and prints a summary |

### Translations

All texts are in `src/Core/Strings.cs`, one line per text with the French, English and Russian versions. Plurals use `|` between forms (singular|plural, or 1|2-4|5+ in Russian). To add a language, add a value to `Lang` in `Loc.cs` and a column in `Strings.cs`.

## Français

lazer2stable copie vers osu!stable les beatmaps, les scores et les skins de osu!lazer qu'il n'a pas encore. lazer n'est jamais modifié, seules les maps absentes de stable sont copiées, et tes collections peuvent être ajoutées (l'ancien `collection.db` est sauvegardé). Les scores vont dans le classement Local de stable avec leur replay (l'ancien `scores.db` est sauvegardé) ; décoche **Beatmaps** pour ne copier que les scores. Les skins vont dans le dossier Skins de stable. **Annuler la dernière copie** retire de stable exactement ce qu'elle avait ajouté. Après la copie, appuie sur **F5** dans la sélection des musiques de stable. L'interface est en français si Windows l'est ; sinon, choisis **FR** dans la barre de titre.

## Русский

lazer2stable копирует в osu!stable карты, рекорды и скины из osu!lazer, которых там ещё нет. lazer никогда не изменяется, копируются только недостающие карты, а коллекции можно добавить (старый `collection.db` сохраняется). Рекорды попадают в локальную таблицу stable вместе с реплеями (старый `scores.db` сохраняется); сними отметку с **Карты**, чтобы скопировать только рекорды. Скины попадают в папку Skins в stable. **Отменить последнее копирование** убирает из stable ровно то, что было добавлено. После копирования нажми **F5** в меню выбора песен в stable. Интерфейс на русском, если Windows на русском; иначе выбери **RU** в заголовке окна.

## Privacy

lazer2stable works offline: it sends nothing anywhere and collects no data. It only reads your osu! folders and writes to osu!stable's folders when you confirm a copy. Settings, reports and the record of the last copy stay on your computer in `%APPDATA%\lazer2stable` and `%LOCALAPPDATA%\lazer2stable`.

## Credits and license

Developed by **Exotique**, released under the [MIT license](LICENSE). Built with Avalonia, Realm and CommunityToolkit.Mvvm, see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

osu! is a trademark of ppy Pty Ltd. lazer2stable is an independent community tool, not affiliated with or endorsed by ppy.
