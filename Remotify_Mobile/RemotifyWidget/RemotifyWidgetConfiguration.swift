import AppIntents
import WidgetKit

/// Настройки виджета (долгое нажатие → «Изменить виджет»).
/// Виджет самодостаточен: без App Group он не видит список устройств приложения,
/// поэтому адрес и токен вводятся здесь.
struct RemotifyWidgetConfiguration: WidgetConfigurationIntent {
    static let title: LocalizedStringResource = "Управление ПК"
    static let description = IntentDescription(
        "Компьютер и действие, которое выполнит виджет. Адрес и токен показаны в окне Remotify на ПК."
    )

    @Parameter(title: "Название", default: "Мой ПК")
    var name: String

    @Parameter(title: "Адрес", default: "192.168.1.10")
    var host: String

    @Parameter(title: "Порт", default: 5123)
    var port: Int

    @Parameter(title: "Токен", default: "")
    var token: String

    @Parameter(title: "Действие", default: .sleep)
    var action: PowerAction

    static var parameterSummary: some ParameterSummary {
        Summary("\(\.$action): \(\.$name)") {
            \.$host
            \.$port
            \.$token
        }
    }

    var isConfigured: Bool {
        !host.trimmingCharacters(in: .whitespaces).isEmpty
            && !token.trimmingCharacters(in: .whitespaces).isEmpty
    }

    var device: Device {
        Device(name: name, host: host, port: port)
    }

    var stateKey: String {
        WidgetStateStore.key(host: host, port: port, action: action)
    }
}
