import AppIntents
import WidgetKit

/// Тап по виджету. Первый тап «взводит», второй — выполняет.
/// Интент несёт все параметры в себе, поэтому выполняется прямо в расширении,
/// не открывая приложение.
struct RemotifyTapIntent: AppIntent {
    static let title: LocalizedStringResource = "Подтвердить и выполнить"
    static let isDiscoverable = false
    static let openAppWhenRun = false

    @Parameter(title: "Название") var name: String
    @Parameter(title: "Адрес") var host: String
    @Parameter(title: "Порт") var port: Int
    @Parameter(title: "Токен") var token: String
    @Parameter(title: "Действие") var action: PowerAction

    init() {}

    init(configuration: RemotifyWidgetConfiguration) {
        name = configuration.name
        host = configuration.host
        port = configuration.port
        token = configuration.token
        action = configuration.action
    }

    func perform() async throws -> some IntentResult {
        let key = WidgetStateStore.key(host: host, port: port, action: action)
        var state = WidgetStateStore.load(key)

        if let armedUntil = state.armedUntil, armedUntil > .now {
            state.armedUntil = nil
            state.resultAt = .now
            do {
                let outcome = try await APIClient.shared.send(
                    action,
                    to: Device(name: name, host: host, port: port),
                    token: token
                )
                switch outcome {
                case .confirmed: state.resultMessage = "Выполнено"
                case .noResponse: state.resultMessage = "Отправлено"
                }
                state.resultIsError = false
            } catch {
                state.resultMessage = Self.shortMessage(for: error)
                state.resultIsError = true
            }
        } else {
            state.armedUntil = Date.now.addingTimeInterval(WidgetStateStore.armWindow)
            state.resultMessage = nil
            state.resultAt = nil
        }

        WidgetStateStore.save(state, forKey: key)
        await WidgetCenter.shared.reloadTimelines(ofKind: RemotifyControlWidget.kind)
        return .result()
    }

    /// В виджете места мало, поэтому длинные сетевые ошибки схлопываем.
    private static func shortMessage(for error: Error) -> String {
        guard let apiError = error as? APIError else { return "Не удалось" }
        switch apiError {
        case .unauthorized: return "Неверный токен"
        case .invalidAddress: return "Неверный адрес"
        case .notRemotify: return "Не Remotify"
        case .badRequest: return "Отклонено"
        case .unexpectedStatus(let code): return "Ошибка \(code)"
        case .transport: return "Нет связи"
        }
    }
}
