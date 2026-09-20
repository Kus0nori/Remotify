# Remotify — iOS-клиент

SwiftUI-приложение для управления ПК с запущенным десктопным Remotify.

- **Минимальная версия**: iOS 18.0
- **Swift**: 6 (строгая конкурентность)
- **Bundle ID**: `com.ivankostiuk.Remotify`

## Экраны

1. **Устройства** (`Views/DeviceListView.swift`) — список сохранённых ПК, кнопка «+»,
   свайп для удаления. Добавление — в модальном `AddDeviceView` (адрес, порт, токен,
   кнопка «Проверить подключение» через неавторизованный `GET /api/ping`).
2. **Устройство** (`Views/DeviceDetailView.swift`) — статус связи и четыре действия
   (выключить / перезагрузить / сон / гибернация) с подтверждением.

## Структура

```
Remotify_Mobile/
├── Info.plist                  — NSLocalNetworkUsageDescription + ATS для HTTP в локалке
├── Remotify.xcodeproj
└── Remotify/                   — synchronized folder (Xcode 16), файлы не надо
    │                             регистрировать в project.pbxproj вручную
    ├── RemotifyApp.swift
    ├── Models/
    │   ├── Device.swift        — имя, хост, порт (токен здесь НЕ хранится)
    │   └── PowerAction.swift   — shutdown / reboot / sleep / hibernate
    ├── Services/
    │   ├── APIClient.swift     — ping + command поверх URLSession
    │   ├── DeviceStore.swift   — @Observable, список в UserDefaults
    │   └── Keychain.swift      — токены в Keychain по UUID устройства
    └── Views/
```

## Заметки

- **Токены** лежат в Keychain (`kSecAttrAccessibleAfterFirstUnlock`), а не в UserDefaults.
- **ATS**: десктоп отдаёт обычный HTTP, поэтому включён `NSAllowsLocalNetworking`.
  Для будущего удалённого сервера понадобится TLS.
- **Отсутствие ответа — не ошибка.** `sleep`/`hibernate` на десктопе вызывают
  блокирующий `SetSuspendState`, а `shutdown` убивает соединение, поэтому HTTP-ответ
  часто не приходит. `APIClient` трактует таймаут и обрыв соединения как
  `CommandOutcome.noResponse` и показывает это как успех с пояснением.

## Сборка

```sh
# симулятор
xcodebuild -project Remotify.xcodeproj -scheme Remotify \
  -destination 'platform=iOS Simulator,name=iPhone 16 Pro' build

# устройство (требует разблокированной связки ключей — запускать из GUI-сессии)
xcodebuild -project Remotify.xcodeproj -scheme Remotify \
  -destination 'id=<UDID>' -allowProvisioningUpdates build
```
