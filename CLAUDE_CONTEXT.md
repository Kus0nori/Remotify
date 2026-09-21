# Remotify — Desktop Client

## Описание проекта
Remotify — приложение для удалённого управления ПК с телефона по локальной сети.
- **Десктопная часть** (этот репозиторий): WinUI 3, C#, .NET 10
- **Мобильная часть**: разрабатывается отдельно другим разработчиком

## Функциональность десктопной части
- Работа в фоне (system tray), окно открывается по требованию
- Автозапуск с Windows (включается/выключается в настройках)
- REST API для приёма команд от телефона:
  - `shutdown` — выключение ПК
  - `reboot` — перезагрузка
  - `sleep` — режим сна
- Защита API (авторизация), чтобы посторонние в сети не могли выполнить команды
- Простой UI для настроек и статуса

## Планы на будущее
- Управление через удалённый сервер (не только локальная сеть)
- Структура должна позволять добавить это без переписывания

## Технический стек
- WinUI 3 / Windows App SDK 2.5 (.NET 10)
- ASP.NET Core Minimal API (встроенный HTTP-сервер)
- H.NotifyIcon.WinUI — system tray
- Авторизация: статический Bearer token

## Структура проекта
```
Remotify_Desktop/
├── App.xaml / App.xaml.cs      — точка входа, tray icon, жизненный цикл
├── MainWindow.xaml/.cs         — окно настроек
├── Services/
│   ├── ApiServer.cs            — REST API сервер (ASP.NET Core Minimal API)
│   ├── PowerService.cs         — выполнение команд (shutdown/reboot/sleep/hibernate)
│   ├── SettingsService.cs      — загрузка/сохранение настроек
│   ├── StartupService.cs       — автозапуск через реестр
│   ├── FirewallService.cs      — управление правилами брандмауэра
│   ├── WindowService.cs        — список окон и иконки приложений
│   └── MetricsService.cs       — системные метрики (CPU, RAM, Network, Uptime)
└── Models/
    ├── AppSettings.cs          — модель настроек
    ├── AppInfo.cs              — модель информации о приложении
    └── SystemMetrics.cs        — модель системных метрик
```

## API для мобильного приложения

### Проверка соединения
```
GET /api/ping
Response: { "status": "ok" }
```

### Выполнение команды
```
POST /api/command
Headers:
  Authorization: Bearer <token>
  Content-Type: application/json
Body: { "action": "shutdown" }  // или "reboot", "sleep", "hibernate"
Response: { "success": true }
Errors:
  401 Unauthorized — неверный токен
  400 Bad Request — неизвестная команда
```

### Список открытых приложений
```
GET /api/apps
Headers:
  Authorization: Bearer <token>
Response: {
  "apps": [
    { "id": "1A2B3C", "title": "Notepad", "processName": "notepad", "mayHaveUnsavedChanges": false },
    { "id": "4D5E6F", "title": "* Document.txt", "processName": "notepad", "mayHaveUnsavedChanges": true }
  ]
}
Errors:
  401 Unauthorized — неверный токен
```
- Возвращает только видимые окна верхнего уровня (как в Alt+Tab)
- `id` — handle окна в hex формате
- `mayHaveUnsavedChanges` — эвристика на основе `*` в заголовке (как в Unity, Photoshop, Paint)

### Иконка приложения
```
GET /api/apps/{id}/icon
Headers:
  Authorization: Bearer <token>
Response: image/png (binary)
Errors:
  401 Unauthorized — неверный токен
  404 Not Found — иконка не найдена
```
- `id` — handle окна из списка `/api/apps`

### Системные метрики
```
GET /api/metrics
Headers:
  Authorization: Bearer <token>
Response: {
  "cpuUsage": 45.2,
  "ramUsage": 68.5,
  "ramUsedGb": 10.8,
  "ramTotalGb": 16.0,
  "networkUploadBps": 125000,
  "networkDownloadBps": 890000,
  "uptimeSeconds": 345600
}
Errors:
  401 Unauthorized — неверный токен
```
- `cpuUsage` — загрузка CPU в процентах
- `ramUsage` — использование RAM в процентах
- `ramUsedGb` / `ramTotalGb` — RAM в гигабайтах
- `networkUploadBps` / `networkDownloadBps` — скорость сети в bytes/sec
- `uptimeSeconds` — время работы системы в секундах

### Параметры подключения
- **Порт по умолчанию**: 5123
- **Токен**: генерируется автоматически при первом запуске, показывается в UI
- **Настройки хранятся**: `%LOCALAPPDATA%\Remotify\settings.json`

## Заметки
- Single instance — только один экземпляр приложения
- При закрытии окна приложение остаётся в tray (окно пересоздаётся при повторном открытии)
- Автозапуск через `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Firewall: UAC prompt при включении, правило "Remotify" для TCP порта
- Минимальная версия Windows: 10 (1809)
