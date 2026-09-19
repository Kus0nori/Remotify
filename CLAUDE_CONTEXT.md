# Remotify — Desktop Client

## Описание проекта
Remotify — приложение для удалённого управления ПК с телефона по локальной сети.
- **Десктопная часть** (этот репозиторий): WPF, C#, .NET 10
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
- WPF (.NET 10)
- ASP.NET Core Minimal API (встроенный HTTP-сервер)
- H.NotifyIcon.Wpf — system tray
- Авторизация: статический Bearer token

## Структура проекта
```
Remotify/
├── App.xaml / App.xaml.cs      — точка входа, tray icon, жизненный цикл
├── MainWindow.xaml/.cs         — окно настроек
├── Services/
│   ├── ApiServer.cs            — REST API сервер (ASP.NET Core Minimal API)
│   ├── PowerService.cs         — выполнение команд (shutdown/reboot/sleep/hibernate)
│   ├── SettingsService.cs      — загрузка/сохранение настроек
│   ├── StartupService.cs       — автозапуск через реестр
│   └── FirewallService.cs      — управление правилами брандмауэра
└── Models/
    └── AppSettings.cs          — модель настроек
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

### Параметры подключения
- **Порт по умолчанию**: 5123
- **Токен**: генерируется автоматически при первом запуске, показывается в UI
- **Настройки хранятся**: `%LOCALAPPDATA%\Remotify\settings.json`

## Заметки
- Single instance — только один экземпляр приложения
- При закрытии окна приложение сворачивается в tray
- Автозапуск через `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Firewall: UAC prompt при включении, правило "Remotify" для TCP порта
