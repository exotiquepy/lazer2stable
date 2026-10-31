<p align="center"><img src="docs/banner.png" alt="lazer2stable"></p>

<p align="center">
  <a href="https://github.com/exotiquepy/lazer2stable/releases/latest/download/lazer2stable.exe"><img src="https://img.shields.io/badge/Download-lazer2stable.exe-FF62A8?style=for-the-badge&logo=windows&logoColor=white" alt="Download lazer2stable.exe"></a>
</p>

<p align="center"><img src="https://img.shields.io/github/v/release/exotiquepy/lazer2stable?style=flat-square&color=FF62A8&label=latest" alt="Latest release"></p>

<p align="center"><a href="#faq">FAQ</a> · <a href="#français">Français</a> · <a href="#русский">Русский</a></p>

I play on lazer but still use stable, and none of my lazer maps or scores were there. So I made this.

lazer2stable looks at your lazer library and your stable install and copies over whatever stable doesn't have yet: maps, collections, scores (with their replays, straight into the Local leaderboard) and skins. lazer itself is never touched.

<p align="center"><img src="docs/demo.gif" width="880" alt="Scan, pick what to copy, copy, done"></p>

## How to use

Get `lazer2stable.exe` from the [latest release](https://github.com/exotiquepy/lazer2stable/releases/latest) and run it, there's nothing to install. It should find both osu! folders by itself (if not, hit **Change**). Untick what you don't want, click **Copy**, then press **F5** in stable's song select.

Your maps are already in stable and you only want the scores? Untick **Beatmaps**. Same thing for skins.

Windows will probably say "Windows protected your PC" because the exe isn't signed yet: **More info** → **Run anyway**. Each release has the SHA-256 if you want to check the file.

## What it copies

- **Maps** go into `Songs` as normal folders, or as `.osz` files that stable imports at launch if you prefer. A map counts as already there if stable has the same set ID, the same difficulty ID or the exact same `.osu`.
- **Collections** are merged into `collection.db`. If a collection with the same name exists, the missing maps are added to it. Nothing gets removed.
- **Scores** go into `scores.db`, replays into `Data\r`. Scores set in lazer are converted: you get lazer's Classic score and stable recomputes acc and rank. Replays can look a bit off in stable since lazer judges sliders differently. Replays of other players you watched in lazer are left out unless you tick the box.
- **Skins** go into `Skins`, one folder each. Skins you already have are detected even under another folder name.

What stable can't read stays in lazer, and the app tells you why for each one: maps edited in the lazer editor, custom rulesets, scores with lazer-only mods or mod settings stable doesn't have (DT at 1.3x…), and skins made in lazer's skin editor.

If you just want to dump your whole library to `.osz` or `.mp3`, [BeatmapExporter](https://github.com/kabiiQ/BeatmapExporter) does that well. This one is more for keeping stable in sync when you play on both.

<table>
  <tr>
    <td width="50%"><img src="docs/screenshot-list.png" alt="Maps to copy"></td>
    <td width="50%"><img src="docs/screenshot-scores.png" alt="Scores to copy"></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/screenshot-skins.png" alt="Skins to copy"></td>
    <td width="50%"><img src="docs/screenshot-done.png" alt="After the copy"></td>
  </tr>
</table>

## Is it safe?

That was the main thing for me.

- lazer is only read, from a temporary copy of its database.
- Nothing in stable is deleted or overwritten. `scores.db` and `collection.db` are backed up next to the original before every change.
- Maps are copied to a temp folder first, then moved into Songs, so stopping halfway never leaves a broken map.
- **Undo the last copy** removes exactly what the last copy added (maps and skins go to the Recycle Bin) and keeps whatever you did in stable since.
- If osu! is open, it tells you and can close it first. Otherwise stable would overwrite the new scores and collections when it quits.
- No network, no data collected.

## FAQ

**Do lazer scores give pp or show up online?**
No, they only go to the Local leaderboard. Nothing gets submitted.

**Can I run it again later?**
Yes, it only copies what's new each time.

**My osu! is in a custom folder.**
It reads lazer's `storage.ini` and stable's `BeatmapDirectory` setting. If it still doesn't find it, use **Change**.

**A score was skipped.**
The **Skipped** tab says why for each one. Usually the map isn't in stable and can't be copied, or the score uses mods stable doesn't have.

**Where are the settings and logs?**
Settings in `%APPDATA%\lazer2stable`, copy reports and the undo info in `%LOCALAPPDATA%\lazer2stable`.

Found a bug? [Open an issue](https://github.com/exotiquepy/lazer2stable/issues/new).

## Français

lazer2stable copie dans osu!stable ce que t'as dans lazer et qui manque à stable : maps, collections, scores (avec les replays, dans le classement Local) et skins. lazer n'est jamais modifié, rien n'est supprimé dans stable, `scores.db` et `collection.db` sont sauvegardés avant chaque changement, et **Annuler la dernière copie** retire exactement ce qui a été ajouté.

[Télécharge l'exe](https://github.com/exotiquepy/lazer2stable/releases/latest/download/lazer2stable.exe), lance-le, décoche ce que tu veux pas, clique sur **Copier**, puis **F5** dans la sélection des musiques de stable. Si t'as déjà tes maps dans stable, décoche **Beatmaps** pour copier seulement les scores. L'interface est en français si ton Windows l'est, sinon clique sur **FR** en haut.

## Русский

lazer2stable копирует в osu!stable то, что есть в lazer, но нет в stable: карты, коллекции, рекорды (с реплеями, в локальную таблицу) и скины. lazer не изменяется, в stable ничего не удаляется, `scores.db` и `collection.db` сохраняются перед каждым изменением, а **Отменить последнее копирование** убирает ровно то, что было добавлено.

[Скачай exe](https://github.com/exotiquepy/lazer2stable/releases/latest/download/lazer2stable.exe), запусти, сними отметки с ненужного, нажми **Скопировать**, потом **F5** в выборе песен stable. Если карты уже в stable, сними отметку с **Карты** и скопируются только рекорды. Интерфейс на русском, если Windows на русском, иначе нажми **RU** сверху.

## Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- Committers and reviewers: [Exotique](https://github.com/exotiquepy)
- Approvers: [Exotique](https://github.com/exotiquepy)

Only `lazer2stable.exe` built from this repository by the [Release workflow](.github/workflows/release.yml) is signed.

Privacy: this program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

---

MIT license, made by Exotique. Uses Avalonia, Realm and CommunityToolkit.Mvvm ([notices](THIRD-PARTY-NOTICES.md)). To build it or add a translation, see [CONTRIBUTING.md](CONTRIBUTING.md).

<sub>osu! is a trademark of ppy Pty Ltd. This is a fan-made tool, not affiliated with ppy.</sub>
