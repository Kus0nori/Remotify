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

## Виджет

`RemotifyWidgetExtension` — интерактивный виджет (`systemSmall` / `systemMedium`),
который едет внутри `Remotify.app/PlugIns/`, а не ставится отдельным приложением.

- **Настройка**: долгое нажатие → «Изменить виджет». Название, адрес, порт, токен
  и действие (`RemotifyWidgetConfiguration`).
- **Подтверждение**: первый тап взводит виджет на 5 секунд и показывает
  «Подтвердить?» с обратным отсчётом, второй — выполняет команду и показывает
  результат на минуту. Оба тапа обрабатывает `RemotifyTapIntent` внутри
  расширения, приложение не открывается.

## Структура

```
Remotify_Mobile/
├── Info.plist                  — NSLocalNetworkUsageDescription + ATS для HTTP в локалке
├── RemotifyWidget-Info.plist   — NSExtension + те же сетевые ключи
├── Remotify.xcodeproj
├── Shared/                     — явные file refs, входит в ОБА таргета
│   ├── Device.swift            — имя, хост, порт (токен здесь НЕ хранится)
│   ├── PowerAction.swift       — shutdown / reboot / sleep / hibernate + AppEnum
│   └── APIClient.swift         — ping + command поверх URLSession
├── Remotify/                   — synchronized folder (Xcode 16), файлы не надо
│   │                             регистрировать в project.pbxproj вручную
│   ├── RemotifyApp.swift
│   ├── Services/
│   │   ├── DeviceStore.swift   — @Observable, список в UserDefaults
│   │   └── Keychain.swift      — токены в Keychain по UUID устройства
│   └── Views/
└── RemotifyWidget/             — synchronized folder, таргет расширения
    ├── RemotifyWidgetBundle.swift
    ├── RemotifyControlWidget.swift      — Widget + TimelineProvider + View
    ├── RemotifyWidgetConfiguration.swift — параметры в настройках виджета
    ├── RemotifyTapIntent.swift          — взвод / выполнение
    └── WidgetState.swift                — состояние взвода и результата
```

## Заметки

- **App Group отсутствует намеренно.** Бесплатный Personal Team не выдаёт этот
  entitlement (`Provisioning profile ... doesn't match the entitlements file's value
  for com.apple.security.application-groups`), поэтому виджет самодостаточен: он
  не читает ни `DeviceStore`, ни Keychain приложения, а хранит свои параметры
  в конфигурации интента и своё состояние — в `UserDefaults` расширения.
  Токен при этом лежит не в Keychain — учитывать при переходе на платный аккаунт,
  где вариант с App Group становится доступен.
- **Локальная сеть из расширения.** Разрешение на локальную сеть выдаётся по
  запросу приложения; запустите приложение и разрешите доступ до того, как
  пользоваться виджетом.
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
