namespace VisualGGPK3;

/// <summary>
/// Локализация интерфейса на русский язык.
/// </summary>
public static class L {
	// MainWindow
	public const string Loading = "Загрузка . . .";
	public const string ProgramNotCompleted = "Эта программа еще не завершена";
	public const string Warning = "Предупреждение";
	public const string Error = "Ошибка";
	public const string Done = "Готово";
	public const string Ready = "Готово";

	// File dialog
	public const string GGPKIndexFile = "GGPK/Index файл";
	public const string AllFiles = "Все файлы";

	// Menu
	public const string MenuFile = "&Файл";
	public const string MenuPatternManager = "&Менеджер шаблонов...";
	public const string MenuAdvancedSearch = "&Расширенный поиск...";
	public const string MenuDatabaseCompare = "&Сравнение баз...";
	public const string MenuExit = "&Выход";

	// Context menu
	public const string Extract = "Извлечь";
	public const string Replace = "Заменить";
	public const string CopyPath = "Копировать путь";
	public const string ExportDdsToPng = "Экспортировать .dds в .png";
	public const string SaveAsPng = "Сохранить как png";

	// Messages
	public const string TextFileTooLarge = "Этот текстовый файл слишком большой, будут показаны только первые 100КБ";
	public const string PleaseLoadGGPK = "Пожалуйста, сначала загрузите GGPK файл";
	public const string OnlyZipAllowed = "Разрешен только один zip файл";
	public const string ExtractedBytes = "Извлечено {0} байт в\r\n{1}";
	public const string ExtractedFiles = "Извлечено {0} файлов в\r\n{1}";
	public const string ReplacedBytes = "Заменено {0} байт из\r\n{1}";
	public const string ReplacedFiles = "Заменено {0} файлов из\r\n{1}";
	public const string NotDdsImage = "Выбранный файл не является dds изображением";
	public const string SavedFile = "Сохранен файл {0}";
	public const string ExportedFiles = "Экспортировано {0} файлов в\r\n{1}";
	public const string ExportedFilesWithFailed = "Экспортировано {0} файлов в\r\n{1}\r\n{2} файлов не удалось!";
	public const string FailedToParsePaths = "\n\nПредупреждение: Не удалось разобрать путь для {0} файлов, ваш ggpk файл может быть поврежден.";

	// Pattern Manager
	public const string PatternManager = "Менеджер шаблонов";
	public const string ColEnabled = "Включен";
	public const string ColName = "Имя";
	public const string ColType = "Тип";
	public const string ColFilePattern = "Шаблон файла";
	public const string ColDescription = "Описание";

	// Pattern Manager buttons
	public const string New = "Новый";
	public const string Edit = "Изменить";
	public const string Delete = "Удалить";
	public const string Duplicate = "Дублировать";
	public const string Up = "Вверх";
	public const string Down = "Вниз";
	public const string ApplySelected = "Применить выбранное";
	public const string ApplyAllEnabled = "Применить все включенные";
	public const string Search = "Поиск";
	public const string Load = "Загрузить";
	public const string Save = "Сохранить";
	public const string SaveAs = "Сохранить как";
	public const string Import = "Импорт";
	public const string Export = "Экспорт";
	public const string Backups = "Резервные копии";
	public const string Close = "Закрыть";

	// Pattern Manager messages
	public const string NoSelection = "Нет выбора";
	public const string SelectPatternToEdit = "Пожалуйста, выберите шаблон для редактирования";
	public const string SelectPatternToDelete = "Пожалуйста, выберите шаблон для удаления";
	public const string SelectPatternToDuplicate = "Пожалуйста, выберите шаблон для дублирования";
	public const string SelectPatternToApply = "Пожалуйста, выберите шаблон для применения";
	public const string SelectPatternToSearch = "Пожалуйста, выберите шаблон для поиска";
	public const string ConfirmDelete = "Подтверждение удаления";
	public const string ConfirmDeletePattern = "Вы уверены, что хотите удалить '{0}'?";
	public const string NoDirectoryAvailable = "Директория недоступна для применения шаблонов";
	public const string NoDirectoryForSearch = "Директория недоступна для поиска";
	public const string NoEnabledPatterns = "Нет включенных шаблонов для применения";
	public const string NoPatterns = "Нет шаблонов";
	public const string ConfirmApply = "Подтверждение применения";
	public const string ConfirmApplyPatterns = "Применить {0} шаблон(ов) ко всем файлам?\nЭта операция создаст резервную копию.";
	public const string ApplyingPatterns = "Применение шаблонов...";
	public const string OperationCancelled = "Операция отменена";
	public const string ErrorApplyingPatterns = "Ошибка применения шаблонов: {0}";
	public const string ApplyComplete = "Применение завершено";
	public const string ApplyResultFormat = "Применено {0} шаблон(ов):\n- Файлов изменено: {1}\n- Всего замен: {2}\n- Ошибок: {3}";
	public const string Searching = "Поиск...";
	public const string SearchResults = "Результаты поиска";
	public const string FoundMatches = "Найдено {0} совпадений:\n\n{1}";
	public const string AndMore = "\n\n... и еще {0}";
	public const string Processing = "Обработка: {0}";
	public const string FailedToLoad = "Не удалось загрузить шаблоны: {0}";
	public const string FailedToSave = "Не удалось сохранить шаблоны: {0}";
	public const string ImportComplete = "Импорт завершен";
	public const string ImportedPatterns = "Импортировано {0} шаблон(ов)";
	public const string FailedToImport = "Не удалось импортировать шаблоны: {0}";
	public const string ExportComplete = "Экспорт завершен";
	public const string PatternsExported = "Шаблоны успешно экспортированы";
	public const string FailedToExport = "Не удалось экспортировать шаблоны: {0}";
	public const string PatternFiles = "Файлы шаблонов";

	// Pattern Editor
	public const string NewPattern = "Новый шаблон";
	public const string EditPattern = "Редактирование шаблона";
	public const string PatternNamePlaceholder = "Имя шаблона";
	public const string FilePatternPlaceholder = "например, *.txt или Data/**/*.dat (пусто = все файлы)";
	public const string HexBytes = "Hex (байты)";
	public const string TextUtf8 = "Текст (UTF-8)";
	public const string TextUtf16 = "Текст (UTF-16)";
	public const string ReplaceAllOccurrences = "Заменить все вхождения";
	public const string Enabled = "Включен";
	public const string TagsPlaceholder = "тег1, тег2, тег3";
	public const string OK = "ОК";
	public const string Cancel = "Отмена";
	public const string TestPattern = "Тестировать шаблон";
	public const string LabelName = "Имя:";
	public const string LabelDescription = "Описание:";
	public const string LabelFilePattern = "Шаблон файла (glob):";
	public const string LabelPatternType = "Тип шаблона:";
	public const string LabelSearchPattern = "Искать:";
	public const string LabelReplaceWith = "Заменить на:";
	public const string LabelTags = "Теги (через запятую):";
	public const string ValidationError = "Ошибка валидации";
	public const string EnterPatternName = "Пожалуйста, введите имя шаблона";
	public const string EnterSearchPattern = "Пожалуйста, введите шаблон для поиска";
	public const string InvalidPattern = "Неверный шаблон: {0}";
	public const string PatternTest = "Тест шаблона";
	public const string PatternTestResult = "Шаблон поиска ({0} байт):\n{1}\n\nШаблон замены ({2} байт):\n{3}";

	// Backup Manager
	public const string BackupManager = "Менеджер резервных копий";
	public const string ColDate = "Дата";
	public const string ColFiles = "Файлов";
	public const string ColFilePath = "Путь к файлу";
	public const string ColBackupTime = "Время резервной копии";
	public const string RestoreSession = "Восстановить сессию";
	public const string RestoreSelected = "Восстановить выбранное";
	public const string DeleteSession = "Удалить сессию";
	public const string CleanupOld = "Очистить старые";
	public const string BackupSessions = "Сессии резервных копий:";
	public const string TotalBackupSize = "Общий размер резервных копий: {0}";
	public const string FileResolverNotAvailable = "Резолвер файлов недоступен";
	public const string SelectSessionToRestore = "Пожалуйста, выберите сессию для восстановления";
	public const string SessionNotFound = "Сессия не найдена";
	public const string ConfirmRestore = "Подтверждение восстановления";
	public const string ConfirmRestoreSession = "Восстановить все {0} файлов из сессии '{1}'?";
	public const string RestoreComplete = "Восстановление завершено";
	public const string RestoredFiles = "Восстановлено {0} из {1} файлов";
	public const string SelectSessionFirst = "Пожалуйста, сначала выберите сессию";
	public const string SelectFilesToRestore = "Пожалуйста, выберите файлы для восстановления";
	public const string SelectSessionToDelete = "Пожалуйста, выберите сессию для удаления";
	public const string ConfirmDeleteSession = "Удалить сессию резервных копий '{0}'?\nЭто действие нельзя отменить.";
	public const string ConfirmCleanup = "Подтверждение очистки";
	public const string ConfirmCleanupOld = "Удалить старые сессии, оставив только 10 последних?";
	public const string CleanupComplete = "Очистка завершена";

	// Advanced Search
	public const string AdvancedSearch = "Расширенный поиск";
	public const string SearchInFiles = "Поиск в файлах";
	public const string FilterByExtension = "Фильтр по расширению:";
	public const string FilterByType = "Фильтр по типу:";
	public const string FilterByFolder = "Фильтр по папке:";
	public const string SearchText = "Текст поиска:";
	public const string SearchInContent = "Искать в содержимом";
	public const string SearchInFileName = "Искать в имени файла";
	public const string CaseSensitive = "С учетом регистра";
	public const string UseRegex = "Использовать регулярные выражения";
	public const string AllTypes = "Все типы";
	public const string Images = "Изображения";
	public const string TextFiles = "Текстовые файлы";
	public const string DataFiles = "Файлы данных";
	public const string AudioFiles = "Аудио файлы";
	public const string VideoFiles = "Видео файлы";
	public const string OtherFiles = "Другие файлы";
	public const string ColSize = "Размер";
	public const string ColPath = "Путь";
	public const string SearchResultsFound = "Найдено {0} файлов";
	public const string SearchInProgress = "Поиск...";
	public const string GoToFile = "Перейти к файлу";
	public const string CopyPathToClipboard = "Копировать путь";
	public const string ExtractSelected = "Извлечь выбранное";
	public const string ReplaceSelected = "Заменить выбранное";
	public const string ClearResults = "Очистить результаты";
	public const string NoSearchResults = "Результаты поиска отсутствуют";
	public const string ExtensionPlaceholder = "например, .png, .txt, .dat";
	public const string FolderPlaceholder = "например, Art/2DArt или Metadata";

	// Database Compare
	public const string DatabaseCompare = "Сравнение баз данных";
	public const string CreateSnapshot = "Создать отпечаток";
	public const string LoadSnapshot = "Загрузить отпечаток";
	public const string CompareWithCurrent = "Сравнить с текущей";
	public const string CompareSnapshots = "Сравнить отпечатки";
	public const string SnapshotInfo = "Информация об отпечатке";
	public const string CurrentDatabase = "Текущая база";
	public const string LoadedSnapshot = "Загруженный отпечаток";
	public const string NotLoaded = "Не загружен";
	public const string TotalFiles = "Всего файлов: {0}";
	public const string TotalSize = "Общий размер: {0}";
	public const string CreatedAt = "Создан: {0}";
	public const string ComparisonResults = "Результаты сравнения";
	public const string AddedFiles = "Добавленные файлы";
	public const string RemovedFiles = "Удаленные файлы";
	public const string ModifiedFiles = "Измененные файлы";
	public const string UnchangedFiles = "Неизмененные файлы";
	public const string ColStatus = "Статус";
	public const string ColOldSize = "Старый размер";
	public const string ColNewSize = "Новый размер";
	public const string StatusAdded = "Добавлен";
	public const string StatusRemoved = "Удален";
	public const string StatusModified = "Изменен";
	public const string StatusUnchanged = "Не изменен";
	public const string SnapshotFiles = "Файлы отпечатков";
	public const string SaveSnapshot = "Сохранить отпечаток";
	public const string CreatingSnapshot = "Создание отпечатка...";
	public const string SnapshotCreated = "Отпечаток создан";
	public const string SnapshotCreatedFiles = "Создан отпечаток с {0} файлами";
	public const string FailedToCreateSnapshot = "Не удалось создать отпечаток: {0}";
	public const string SnapshotLoaded = "Отпечаток загружен";
	public const string SnapshotLoadedFiles = "Загружен отпечаток с {0} файлами";
	public const string FailedToLoadSnapshot = "Не удалось загрузить отпечаток: {0}";
	public const string CompareComplete = "Сравнение завершено";
	public const string CompareResultFormat = "Результаты сравнения:\n- Добавлено: {0}\n- Удалено: {1}\n- Изменено: {2}\n- Без изменений: {3}";
	public const string NoSnapshotLoaded = "Отпечаток не загружен";
	public const string PleaseLoadSnapshot = "Пожалуйста, загрузите отпечаток для сравнения";
	public const string ExportResults = "Экспортировать результаты";
	public const string FilterAdded = "Показать добавленные";
	public const string FilterRemoved = "Показать удаленные";
	public const string FilterModified = "Показать измененные";
	public const string FilterUnchanged = "Показать неизмененные";
	public const string Comparing = "Сравнение...";
}
