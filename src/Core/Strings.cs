namespace Exotique.Core;

/// <summary>
/// Tous les textes de l'interface, en français, anglais et russe.
/// {0}, {1}… : valeurs insérées ; {n} : nombre d'un pluriel ; « | » sépare les formes du pluriel
/// (singulier|pluriel en français et en anglais ; 1|2-4|5+ en russe).
/// </summary>
internal static class Strings
{
    public static readonly Dictionary<string, (string Fr, string En, string Ru)> Table = new()
    {
        // ------------------------------------------------------------ barre de titre
        ["Tagline"] = ("lazer vers stable", "lazer to stable", "из lazer в stable"),
        ["DevelopedBy"] = ("développé par", "developed by", "разработчик"),
        ["LanguageTip"] = ("Langue", "Language", "Язык"),
        ["Change"] = ("Changer", "Change", "Изменить"),

        // ------------------------------------------------------------------ réglages
        ["SetupTitle"] = ("Où sont tes dossiers osu! ?", "Where are your osu! folders?", "Где твои папки osu!?"),
        ["SetupLead"] = (
            "Exotique compare ta bibliothèque lazer à ton dossier Songs de stable, puis copie seulement ce qui manque. lazer n'est jamais modifié.",
            "Exotique compares your lazer library with your stable Songs folder, then copies only what's missing. lazer is never modified.",
            "Exotique сравнивает твою библиотеку lazer с папкой Songs в stable и копирует только то, чего не хватает. lazer никогда не изменяется."),
        ["ChooseFolder"] = ("Choisir le dossier", "Choose folder", "Выбрать папку"),
        ["AnalyzeBoth"] = ("Analyser les deux bibliothèques", "Scan both libraries", "Проверить обе библиотеки"),
        ["LazerNotFound"] = (
            "Introuvable : choisis le dossier qui contient client.realm",
            "Not found: choose the folder that contains client.realm",
            "Не найдено: выбери папку, в которой лежит client.realm"),
        ["StableNotFound"] = (
            "Introuvable : choisis ton dossier Songs ou le dossier d'osu! stable",
            "Not found: choose your Songs folder or your osu! stable folder",
            "Не найдено: выбери папку Songs или папку osu! stable"),
        ["PickLazerTitle"] = (
            "Dossier de données osu! lazer (celui qui contient client.realm)",
            "osu! lazer data folder (the one containing client.realm)",
            "Папка данных osu! lazer (та, где лежит client.realm)"),
        ["PickStableTitle"] = (
            "Dossier Songs de osu! stable (ou dossier d'installation de stable)",
            "osu! stable Songs folder (or the stable install folder)",
            "Папка Songs в osu! stable (или папка установки stable)"),
        ["ErrNoLazerHere"] = (
            "Ce dossier ne contient pas de base lazer (client.realm) : {0}. Dans lazer, Paramètres > « Ouvrir le dossier osu! » montre le bon dossier.",
            "This folder has no lazer database (client.realm): {0}. In lazer, Settings > \"Open osu! folder\" shows the right one.",
            "В этой папке нет базы lazer (client.realm): {0}. В lazer пункт «Открыть папку osu!» в настройках покажет нужную папку."),
        ["ErrNoSongsHere"] = ("Pas de dossier Songs trouvé ici : {0}.", "No Songs folder found here: {0}.", "Здесь не найдена папка Songs: {0}."),
        ["ErrAnalysisFailed"] = ("L'analyse a échoué : {0}", "The scan failed: {0}", "Проверка не удалась: {0}"),

        // ------------------------------------------------------------------- analyse
        ["AnalyzingTitle"] = ("Analyse de tes bibliothèques", "Scanning your libraries", "Проверка твоих библиотек"),
        ["AnalyzingNote"] = (
            "Rien n'est modifié pendant l'analyse : lazer est lu depuis une copie, Songs est seulement lu.",
            "Nothing is changed during the scan: lazer is read from a copy, Songs is only read.",
            "Во время проверки ничего не меняется: lazer читается из копии, а Songs только читается."),
        ["StepLazer"] = ("Lecture de la base lazer", "Reading the lazer database", "Чтение базы lazer"),
        ["StepSongs"] = ("Lecture de ton dossier Songs", "Reading your Songs folder", "Чтение папки Songs"),
        ["StepCompare"] = ("Comparaison des deux bibliothèques", "Comparing both libraries", "Сравнение библиотек"),
        ["StepCopyingDb"] = ("copie de la base", "copying the database", "копирование базы"),
        ["StepSetsProgress"] = ("{0} / {1} maps", "{0} / {1} maps", "{0} / {1} карт"),
        ["StepInventory"] = ("inventaire des fichiers", "listing files", "поиск файлов"),
        ["StepInventoryCount"] = ("inventaire : {0} difficultés trouvées", "listing: {0} difficulties found", "поиск: найдено сложностей: {0}"),
        ["StepDiffProgress"] = ("{0} / {1} difficultés", "{0} / {1} difficulties", "{0} / {1} сложностей"),
        ["PMaps"] = ("{n} map|{n} maps", "{n} map|{n} maps", "{n} карта|{n} карты|{n} карт"),
        ["PDifficulties"] = ("{n} difficulté|{n} difficultés", "{n} difficulty|{n} difficulties", "{n} сложность|{n} сложности|{n} сложностей"),
        ["PMissingMaps"] = ("{n} map manquante|{n} maps manquantes", "{n} missing map|{n} missing maps",
            "{n} недостающая карта|{n} недостающие карты|{n} недостающих карт"),

        // ------------------------------------------------------------------- résumé
        ["HeadlineNone"] = ("Stable a déjà toutes tes maps lazer", "stable already has all your lazer maps", "В stable уже есть все твои карты из lazer"),
        ["PHeadlineMissing"] = (
            "{n} map de lazer manque dans stable|{n} maps de lazer manquent dans stable",
            "{n} lazer map is missing from stable|{n} lazer maps are missing from stable",
            "В stable не хватает {n} карты из lazer|В stable не хватает {n} карт из lazer|В stable не хватает {n} карт из lazer"),
        ["SubToCopy"] = ("{0} à copier.", "{0} to copy.", "Скопировать нужно {0}."),
        ["PSubPresent"] = (
            "{n} map est déjà dans stable|{n} maps sont déjà dans stable",
            "{n} map is already in stable|{n} maps are already in stable",
            "{n} карта уже есть в stable|{n} карты уже есть в stable|{n} карт уже есть в stable"),
        ["PSubIgnored"] = (", {n} est ignorée|, {n} sont ignorées", ", {n} is skipped|, {n} are skipped", ", {n} пропущена|, {n} пропущены|, {n} пропущено"),
        ["SubNothing"] = ("Rien à copier.", "Nothing to copy.", "Копировать нечего."),

        // --------------------------------------------------------- colonne gauche
        ["TabToCopy"] = ("À copier", "To copy", "Скопировать"),
        ["TabPresent"] = ("Déjà dans stable", "Already in stable", "Уже в stable"),
        ["TabIgnored"] = ("Ignorées", "Skipped", "Пропущены"),
        ["GroupModes"] = ("Modes de jeu", "Game modes", "Режимы игры"),
        ["GroupFormat"] = ("Format de copie", "Copy format", "Формат копирования"),
        ["FormatFolders"] = ("Directement dans Songs", "Straight into Songs", "Прямо в Songs"),
        ["FormatFoldersHint"] = ("Le plus rapide, puis F5 dans stable", "Fastest, then F5 in stable", "Быстрее всего, затем F5 в stable"),
        ["FormatFoldersTip"] = (
            "Exotique range chaque map dans Songs, comme stable le fait après un import. Le plus rapide. Dans stable, appuie ensuite sur F5 dans la sélection des musiques pour qu'il les voie.",
            "Exotique puts each map in Songs, the same way stable does after an import. Fastest. In stable, press F5 in song select afterwards so it picks them up.",
            "Exotique раскладывает каждую карту в Songs так же, как это делает stable после импорта. Быстрее всего. Потом нажми F5 в меню выбора песен в stable, чтобы он их увидел."),
        ["FormatOsz"] = ("En fichiers .osz", "As .osz files", "Файлами .osz"),
        ["FormatOszHint"] = ("Plus lent, stable les importe tout seul", "Slower, stable imports them by itself", "Медленнее, stable импортирует их сам"),
        ["FormatOszTip"] = (
            "Exotique crée un .osz par map dans Songs ; stable les importe lui-même au démarrage, comme des maps téléchargées. Plus lent.",
            "Exotique creates one .osz per map in Songs; stable imports them itself on startup, like downloaded maps. Slower.",
            "Exotique создаёт по одному .osz на карту в Songs; stable сам импортирует их при запуске, как скачанные карты. Медленнее."),
        ["GroupCollections"] = ("Collections", "Collections", "Коллекции"),
        ["PCollectionsLabel"] = (
            "Ajouter ma collection lazer à stable|Ajouter mes {n} collections lazer à stable",
            "Add my lazer collection to stable|Add my {n} lazer collections to stable",
            "Добавить {n} коллекцию lazer в stable|Добавить {n} коллекции lazer в stable|Добавить {n} коллекций lazer в stable"),
        ["CollNone"] = ("Tu n'as aucune collection dans lazer.", "You have no collections in lazer.", "В lazer нет коллекций."),
        ["CollNoInstall"] = (
            "Dossier d'installation de stable introuvable (collection.db). Choisis le dossier de stable plutôt que Songs.",
            "stable's install folder (collection.db) wasn't found. Choose the stable folder rather than Songs.",
            "Не найдена папка установки stable (collection.db). Выбери папку stable, а не Songs."),
        ["CollAllPresent"] = ("Elles sont déjà toutes dans stable.", "They're all in stable already.", "Они все уже есть в stable."),
        ["CollPreview"] = (
            "{0}, {1}. Rien n'est retiré, et collection.db est sauvegardé avant.",
            "{0}, {1}. Nothing is removed, and collection.db is backed up first.",
            "{0}, {1}. Ничего не удаляется, а collection.db сначала сохраняется в резервную копию."),
        ["PCollNew"] = ("{n} nouvelle|{n} nouvelles", "{n} new|{n} new", "{n} новая|{n} новые|{n} новых"),
        ["PCollUpdated"] = ("{n} complétée|{n} complétées", "{n} updated|{n} updated", "{n} дополнена|{n} дополнены|{n} дополнено"),
        ["PCollAdded"] = ("{n} ajoutée|{n} ajoutées", "{n} added|{n} added", "{n} добавлена|{n} добавлены|{n} добавлено"),
        ["CollUnreadable"] = ("collection.db de stable illisible : {0}", "Can't read stable's collection.db: {0}", "Не удалось прочитать collection.db из stable: {0}"),

        // -------------------------------------------------------------------- liste
        ["SearchWatermark"] = (
            "Chercher un titre, un artiste, un mappeur ou une difficulté",
            "Search a title, artist, mapper or difficulty",
            "Поиск: название, артист, маппер или сложность"),
        ["SortTip"] = ("Ordre de la liste", "List order", "Порядок списка"),
        ["CheckAll"] = ("Tout cocher", "Check all", "Отметить все"),
        ["CheckAllTip"] = ("Coche toutes les maps affichées", "Checks every map shown", "Отметить все показанные карты"),
        ["UncheckAll"] = ("Tout décocher", "Uncheck all", "Снять все"),
        ["UncheckAllTip"] = ("Décoche toutes les maps affichées", "Unchecks every map shown", "Снять отметку со всех показанных карт"),
        ["SortRecent"] = ("Ajoutées récemment", "Recently added", "Недавно добавленные"),
        ["SortArtist"] = ("Artiste", "Artist", "Артист"),
        ["SortTitle"] = ("Titre", "Title", "Название"),
        ["SortMapper"] = ("Mappeur", "Mapper", "Маппер"),
        ["SortStars"] = ("Difficulté la plus haute", "Highest difficulty", "Самая высокая сложность"),
        ["SortSize"] = ("Taille", "Size", "Размер"),
        ["EmptySearch"] = (
            "Aucune map ne correspond à ta recherche ou aux modes choisis.",
            "No map matches your search or the chosen modes.",
            "Ни одна карта не подходит под поиск или выбранные режимы."),
        ["EmptyToCopy"] = (
            "Rien à copier : chaque map de lazer est déjà dans stable.",
            "Nothing to copy: every lazer map is already in stable.",
            "Копировать нечего: все карты из lazer уже есть в stable."),
        ["EmptyPresent"] = (
            "Aucune de tes maps lazer n'est encore dans stable.",
            "None of your lazer maps is in stable yet.",
            "Ни одной твоей карты из lazer пока нет в stable."),
        ["EmptyIgnored"] = ("Aucune map ignorée.", "No skipped maps.", "Пропущенных карт нет."),
        ["CountOf"] = ("{0} sur {1}", "{0} of {1}", "{0} из {1}"),
        ["NoTitle"] = ("(sans titre)", "(untitled)", "(без названия)"),
        ["MappedBy"] = ("{0}, mappée par {1}", "{0}, mapped by {1}", "{0}, маппер: {1}"),
        ["StarsUnknown"] = ("étoiles inconnues", "unknown stars", "звёзды неизвестны"),

        // statuts de map (vocabulaire du site osu!)
        ["StatusLocal"] = ("locale", "local", "локальная"),
        ["StatusRanked"] = ("ranked", "ranked", "рейтинговая"),
        ["StatusApproved"] = ("approved", "approved", "одобренная"),
        ["StatusQualified"] = ("qualified", "qualified", "квалифицированная"),
        ["StatusLoved"] = ("loved", "loved", "любимая"),
        ["StatusPending"] = ("pending", "pending", "ожидающая"),
        ["StatusWip"] = ("WIP", "WIP", "в процессе"),
        ["StatusGraveyard"] = ("graveyard", "graveyard", "заброшенная"),

        // raisons
        ["ReasonSameSet"] = ("même ID de set dans Songs", "same set ID in Songs", "тот же ID сета в Songs"),
        ["ReasonSameFile"] = ("fichier .osu identique dans Songs", "identical .osu file in Songs", "такой же файл .osu в Songs"),
        ["ReasonSameDiff"] = ("même ID de difficulté dans Songs", "same difficulty ID in Songs", "тот же ID сложности в Songs"),
        ["ReasonDuplicate"] = ("en double dans lazer", "duplicate in lazer", "дубликат в lazer"),
        ["ReasonRuleset"] = (
            "Mode de jeu propre à lazer, que stable ne sait pas lire",
            "lazer-only game mode that stable can't read",
            "Режим только для lazer, stable его не поддерживает"),
        ["ReasonMissingFiles"] = ("Fichiers absents du stockage lazer : {0}", "Files missing from lazer's storage: {0}", "В хранилище lazer нет файлов: {0}"),
        ["ReasonNoOsuFile"] = ("aucun fichier .osu", "no .osu file", "нет файла .osu"),
        ["ReasonEdited"] = (
            "Enregistrée par l'éditeur lazer, à exporter depuis l'éditeur avec l'export compatible stable",
            "Saved by the lazer editor: export it from the editor with the stable-compatible export",
            "Сохранена редактором lazer: экспортируй её из редактора в формате для stable"),

        // ---------------------------------------------------------- copie en cours
        ["InProgress"] = ("En cours", "In progress", "Сейчас"),
        ["AlreadyCopied"] = ("Déjà copiées", "Copied so far", "Уже скопированы"),
        ["ExportHeadline"] = ("Copie vers stable", "Copying to stable", "Копирование в stable"),
        ["ExportCollectionsHeadline"] = ("Ajout des collections", "Adding collections", "Добавление коллекций"),
        ["ExportDetail"] = ("{0} sur {1}, {2} sur {3}.{4}", "{0} of {1}, {2} of {3}.{4}", "{0} из {1}, {2} из {3}.{4}"),
        ["ExportEta"] = (" Encore {0} environ.", " About {0} left.", " Осталось примерно {0}."),
        ["StopNote"] = (
            "Tu peux arrêter à tout moment : la map en cours est terminée, aucune ne reste à moitié copiée.",
            "You can stop at any time: the current map is finished first, so none is left half-copied.",
            "Можно остановить в любой момент: текущая карта докопируется, ни одна не останется скопированной наполовину."),
        ["StopButton"] = ("Arrêter après cette map", "Stop after this map", "Остановить после этой карты"),

        // -------------------------------------------------------------------- bilan
        ["DoneNone"] = ("Aucune map copiée", "No maps copied", "Ни одна карта не скопирована"),
        ["PDoneHeadline"] = (
            "{n} map copiée dans stable|{n} maps copiées dans stable",
            "{n} map copied to stable|{n} maps copied to stable",
            "{n} карта скопирована в stable|{n} карты скопированы в stable|{n} карт скопировано в stable"),
        ["DoneBytes"] = ("{0} en {1}.", "{0} in {1}.", "{0} за {1}."),
        ["DoneCancelled"] = (
            "Arrêtée à ta demande. Il reste {0} à copier : relance l'analyse pour les retrouver.",
            "Stopped as you asked. {0} left to copy: run the scan again to find them.",
            "Остановлено по твоей просьбе. Осталось скопировать: {0}. Запусти проверку снова, чтобы их найти."),
        ["DoneDiskFull"] = ("Arrêtée : le disque de Songs est plein.", "Stopped: the Songs drive is full.", "Остановлено: диск с Songs заполнен."),
        ["PDoneFailed"] = (
            "{n} map n'a pas pu être copiée.|{n} maps n'ont pas pu être copiées.",
            "{n} map couldn't be copied.|{n} maps couldn't be copied.",
            "{n} карту не удалось скопировать.|{n} карты не удалось скопировать.|{n} карт не удалось скопировать."),
        ["PDoneWarnings"] = (
            "{n} fichier manquait dans lazer.|{n} fichiers manquaient dans lazer.",
            "{n} file was missing in lazer.|{n} files were missing in lazer.",
            "{n} файл отсутствовал в lazer.|{n} файла отсутствовали в lazer.|{n} файлов отсутствовали в lazer."),
        ["DoneCollections"] = ("Collections : {0}, {1}.", "Collections: {0}, {1}.", "Коллекции: {0}, {1}."),
        ["DoneBackup"] = (" Ancien collection.db sauvegardé : {0}.", " Previous collection.db backed up as {0}.", " Старый collection.db сохранён как {0}."),
        ["DoneCollFailed"] = ("Les collections n'ont pas été ajoutées : {0}", "Collections weren't added: {0}", "Коллекции не добавлены: {0}"),
        ["CollStableWasOpen"] = (
            "osu! stable était ouvert pendant la copie, et il réécrit collection.db en quittant. Ferme stable puis clique sur « Revenir à la liste » pour ajouter les collections.",
            "osu! stable was open during the copy, and it rewrites collection.db when it quits. Close stable, then click \"Back to the list\" to add the collections.",
            "osu! stable был открыт во время копирования, а при выходе он перезаписывает collection.db. Закрой stable и нажми «Назад к списку», чтобы добавить коллекции."),
        ["NextTitle"] = ("Et maintenant", "What's next", "Что дальше"),
        ["NextFolders"] = (
            "Lance osu! stable, va dans la sélection des musiques et appuie sur F5 : stable relit le dossier Songs et ajoute les nouvelles maps.",
            "Launch osu! stable, go to song select and press F5: stable re-reads the Songs folder and adds the new maps.",
            "Запусти osu! stable, открой выбор песен и нажми F5: stable перечитает папку Songs и добавит новые карты."),
        ["NextOsz"] = (
            "Lance osu! stable : il importe tout seul les fichiers .osz posés dans Songs dès le démarrage, sans F5.",
            "Launch osu! stable: it imports the .osz files placed in Songs by itself on startup, no F5 needed.",
            "Запусти osu! stable: при запуске он сам импортирует файлы .osz из Songs, F5 не нужен."),
        ["NextUnchecked"] = (
            "Les maps que tu n'avais pas cochées restent dans lazer : clique sur « Revenir à la liste » pour les retrouver.",
            "Maps you didn't check stay in lazer: click \"Back to the list\" to find them.",
            "Неотмеченные карты остаются в lazer: нажми «Назад к списку», чтобы их найти."),
        ["ToCheck"] = ("À vérifier", "To check", "Проверь"),
        ["ToCheckNote"] = ("Le détail complet est dans le rapport.", "Full details are in the report.", "Подробности в отчёте."),
        ["BackToList"] = ("Revenir à la liste", "Back to the list", "Назад к списку"),
        ["OpenReport"] = ("Voir le rapport", "View report", "Открыть отчёт"),
        ["OpenSongs"] = ("Ouvrir le dossier Songs", "Open Songs folder", "Открыть папку Songs"),
        ["LaunchStable"] = ("Lancer osu! stable", "Launch osu! stable", "Запустить osu! stable"),
        ["Line"] = ("{0} : {1}", "{0}: {1}", "{0}: {1}"),
        ["WarnRejectedName"] = ("{0} : nom de fichier refusé « {1} »", "{0}: file name rejected \"{1}\"", "{0}: недопустимое имя файла «{1}»"),
        ["WarnMissingFile"] = ("{0} : « {1} » absent du stockage lazer", "{0}: \"{1}\" missing from lazer's storage", "{0}: «{1}» нет в хранилище lazer"),

        // ------------------------------------------------------------ bas de page
        ["PExportButton"] = (
            "Copier {n} map vers stable|Copier {n} maps vers stable",
            "Copy {n} map to stable|Copy {n} maps to stable",
            "Скопировать {n} карту в stable|Скопировать {n} карты в stable|Скопировать {n} карт в stable"),
        ["NothingToCopy"] = ("Rien à copier", "Nothing to copy", "Копировать нечего"),
        ["NoneChecked"] = ("Aucune map cochée", "No map checked", "Ничего не отмечено"),
        ["SelAllPresent"] = ("Tout est déjà dans stable.", "Everything is already in stable.", "Всё уже есть в stable."),
        ["SelCheckSome"] = ("Coche les maps à copier.", "Check the maps to copy.", "Отметь карты для копирования."),
        ["PSelChecked"] = ("{n} map cochée|{n} maps cochées", "{n} map checked|{n} maps checked", "Отмечена {n} карта|Отмечены {n} карты|Отмечено {n} карт"),
        ["SelLine"] = ("{0}, {1}.", "{0}, {1}.", "{0}, {1}."),
        ["SelFree"] = ("{0} {1} libres sur le disque de Songs.", "{0} {1} free on the Songs drive.", "{0} Свободно на диске с Songs: {1}."),
        ["SelNoSpace"] = (
            "{0} Pas assez de place : il reste {1} sur le disque de Songs.",
            "{0} Not enough space: only {1} left on the Songs drive.",
            "{0} Недостаточно места: на диске с Songs осталось {1}."),

        // --------------------------------------------------------------- osu! ouvert
        ["OsuBoth"] = ("osu! lazer et osu! stable sont ouverts", "osu! lazer and osu! stable are open", "osu! lazer и osu! stable открыты"),
        ["OsuLazer"] = ("osu! lazer est ouvert", "osu! lazer is open", "osu! lazer открыт"),
        ["OsuStable"] = ("osu! stable est ouvert", "osu! stable is open", "osu! stable открыт"),
        ["BannerExplain"] = (
            "Ferme-le avant de copier : lazer peut être en train d'écrire dans sa base, et stable réécrit ses fichiers en quittant.",
            "Close it before copying: lazer may be writing to its database, and stable rewrites its files when it quits.",
            "Закрой его перед копированием: lazer может записывать в свою базу, а stable перезаписывает свои файлы при выходе."),
        ["BannerFailed"] = (
            "osu! ne s'est pas fermé : une fenêtre de confirmation est peut-être ouverte dans osu!. Réponds-y, ou force la fermeture (ce qui n'est pas enregistré dans osu! sera perdu).",
            "osu! didn't close: a confirmation window may be open in osu!. Answer it, or force it to close (anything osu! hasn't saved will be lost).",
            "osu! не закрылся: возможно, в osu! открыто окно подтверждения. Ответь на него или закрой принудительно (всё несохранённое в osu! пропадёт)."),
        ["ForceClose"] = ("Forcer la fermeture", "Force close", "Закрыть принудительно"),
        ["CloseOsu"] = ("Fermer osu!", "Close osu!", "Закрыть osu!"),
        ["Closing"] = ("Fermeture…", "Closing…", "Закрываю…"),
        ["DialogOsuExplain"] = (
            "Exotique peut le fermer pour toi avant de copier. osu! enregistre tout normalement en quittant.",
            "Exotique can close it for you before copying. osu! saves everything as usual when it quits.",
            "Exotique может закрыть его за тебя перед копированием. При выходе osu! всё сохранит как обычно."),
        ["DialogOsuFailed"] = (
            "osu! ne s'est pas fermé : une fenêtre de confirmation est peut-être ouverte dans osu!. Réponds-y, ou force la fermeture.",
            "osu! didn't close: a confirmation window may be open in osu!. Answer it, or force it to close.",
            "osu! не закрылся: возможно, в osu! открыто окно подтверждения. Ответь на него или закрой принудительно."),

        // -------------------------------------------------------------- confirmation
        ["Cancel"] = ("Annuler", "Cancel", "Отмена"),
        ["CopyWithoutClosing"] = ("Copier sans fermer", "Copy without closing", "Копировать без закрытия"),
        ["CopyWithoutClosingTip"] = (
            "Les maps sont copiées, mais si stable est ouvert, les collections ne sont pas ajoutées : stable les écraserait en quittant.",
            "Maps are copied, but if stable is open the collections aren't added: stable would overwrite them when it quits.",
            "Карты будут скопированы, но если stable открыт, коллекции не добавятся: stable перезапишет их при выходе."),
        ["CloseThenCopy"] = ("Fermer osu! puis copier", "Close osu! and copy", "Закрыть osu! и копировать"),
        ["ClosingOsu"] = ("Fermeture d'osu!…", "Closing osu!…", "Закрываю osu!…"),
        ["PConfirmTitle"] = (
            "Copier {n} map vers stable ?|Copier {n} maps vers stable ?",
            "Copy {n} map to stable?|Copy {n} maps to stable?",
            "Скопировать {n} карту в stable?|Скопировать {n} карты в stable?|Скопировать {n} карт в stable?"),
        ["ConfirmAfter"] = (" Il restera environ {0} libres.", " About {0} will be left free.", " Останется примерно {0} свободного места."),
        ["ConfirmBodyOsz"] = (
            "{0} vont être écrits en fichiers .osz, que stable importera à son prochain lancement.{1}",
            "{0} will be written as .osz files, which stable will import next time it starts.{1}",
            "{0} будет записано в файлы .osz, stable импортирует их при следующем запуске.{1}"),
        ["ConfirmBodyFolders"] = (
            "{0} vont être copiés, une map par dossier.{1}",
            "{0} will be copied, one folder per map.{1}",
            "Будет скопировано {0}, по папке на карту.{1}"),
        ["Destination"] = ("Destination", "Destination", "Куда"),
        ["ConfirmCollections"] = (
            "Tes collections lazer seront aussi ajoutées à collection.db : {0}",
            "Your lazer collections will also be added to collection.db: {0}",
            "Твои коллекции lazer тоже будут добавлены в collection.db: {0}"),

        // ------------------------------------------------------------------ erreurs
        ["ErrorTitle"] = ("Une erreur inattendue s'est produite", "Something unexpected went wrong", "Произошла непредвиденная ошибка"),
        ["ErrorBody"] = (
            "Le détail est enregistré dans un fichier : envoie-le à Exotique pour qu'il corrige le problème. Tu peux continuer à utiliser l'appli.",
            "The details are saved in a file: send it to Exotique so the problem can be fixed. You can keep using the app.",
            "Подробности сохранены в файл: отправь его Exotique, чтобы проблему исправили. Можно продолжать работу."),
        ["OpenErrorLog"] = ("Ouvrir le fichier d'erreur", "Open error file", "Открыть файл ошибки"),
        ["Continue"] = ("Continuer", "Continue", "Продолжить"),
        ["FatalMessage"] = ("Exotique a rencontré une erreur et doit se fermer.", "Exotique ran into an error and has to close.", "В Exotique произошла ошибка, программа будет закрыта."),
        ["FatalLogged"] = ("Le détail est enregistré ici :", "Details are saved here:", "Подробности сохранены здесь:"),
        ["ErrNoFilesDir"] = (
            "Le dossier « files » est introuvable à côté de la base lazer ({0}).",
            "The \"files\" folder next to the lazer database wasn't found ({0}).",
            "Рядом с базой lazer не найдена папка «files» ({0})."),
        ["ErrOpenRealm"] = (
            "Impossible d'ouvrir la base lazer : {0}\nSi lazer vient d'être mis à jour, son format de base a peut-être changé : il faut une version plus récente d'Exotique.",
            "Can't open the lazer database: {0}\nIf lazer was just updated, its database format may have changed: you need a newer version of Exotique.",
            "Не удалось открыть базу lazer: {0}\nЕсли lazer только что обновился, формат базы мог измениться: нужна более новая версия Exotique."),
        ["ErrCollUnreadable"] = (
            "collection.db illisible : fichier abîmé ou d'un format inconnu.",
            "collection.db can't be read: the file is damaged or in an unknown format.",
            "collection.db не читается: файл повреждён или неизвестного формата."),
        ["ErrCollVerify"] = (
            "La relecture du nouveau collection.db ne correspond pas : fichier d'origine conservé.",
            "The new collection.db didn't read back correctly: the original file was kept.",
            "Новый collection.db не прошёл проверку: исходный файл сохранён."),

        // ------------------------------------------------------------------ rapport
        ["RptTitle"] = ("Exotique {0} : rapport du {1}", "Exotique {0}: report from {1}", "Exotique {0}: отчёт от {1}"),
        ["RptDate"] = ("dd/MM/yyyy à HH:mm", "yyyy-MM-dd HH:mm", "dd.MM.yyyy HH:mm"),
        ["RptLazer"] = ("Données lazer : {0}", "lazer data: {0}", "Данные lazer: {0}"),
        ["RptSongs"] = ("Songs stable : {0}", "stable Songs: {0}", "Songs в stable: {0}"),
        ["RptSelected"] = ("Maps sélectionnées : {0}", "Maps selected: {0}", "Выбрано карт: {0}"),
        ["RptCopied"] = ("Copiées : {0} ({1})", "Copied: {0} ({1})", "Скопировано: {0} ({1})"),
        ["RptFailed"] = ("Échecs : {0}", "Failed: {0}", "Ошибок: {0}"),
        ["RptCancelled"] = ("Copie arrêtée avant la fin à ta demande.", "Copy stopped early as you asked.", "Копирование остановлено досрочно по твоей просьбе."),
        ["RptDiskFull"] = ("Copie arrêtée : disque plein.", "Copy stopped: disk full.", "Копирование остановлено: диск заполнен."),
        ["RptCollections"] = (
            "Collections : {0} ajoutées, {1} complétées, {2} maps ajoutées aux collections.",
            "Collections: {0} added, {1} updated, {2} maps added to collections.",
            "Коллекции: добавлено {0}, дополнено {1}, карт добавлено в коллекции: {2}."),
        ["RptBackup"] = ("Sauvegarde de l'ancien collection.db : {0}", "Previous collection.db backup: {0}", "Резервная копия старого collection.db: {0}"),
        ["RptCollNot"] = ("Collections non exportées : {0}", "Collections not exported: {0}", "Коллекции не экспортированы: {0}"),
        ["RptSecCopied"] = ("Copiées", "Copied", "Скопированы"),
        ["RptSecFailed"] = ("Échecs de copie", "Copy failures", "Ошибки копирования"),
        ["RptSecWarnings"] = ("Avertissements", "Warnings", "Предупреждения"),
        ["RptSecNotCopied"] = ("Sélectionnées mais pas copiées (arrêt avant la fin)", "Selected but not copied (stopped early)", "Выбраны, но не скопированы (остановка)"),
        ["RptSecUnselected"] = ("Non sélectionnées", "Not selected", "Не выбраны"),
        ["RptSecEdited"] = ("Ignorées : modifiées dans l'éditeur lazer", "Skipped: edited in the lazer editor", "Пропущены: изменены в редакторе lazer"),
        ["RptSecRuleset"] = ("Ignorées : modes de jeu propres à lazer", "Skipped: lazer-only game modes", "Пропущены: режимы только для lazer"),
        ["RptSecMissing"] = ("Ignorées : fichiers manquants dans lazer", "Skipped: files missing in lazer", "Пропущены: нет файлов в lazer"),
    };
}
