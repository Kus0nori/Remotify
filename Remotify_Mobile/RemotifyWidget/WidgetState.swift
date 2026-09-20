import Foundation

/// Что виджет показывает прямо сейчас.
enum WidgetPhase {
    case unconfigured
    case idle
    /// Первый тап прошёл, ждём подтверждения до указанного момента.
    case armed(until: Date)
    case result(message: String, isError: Bool)
}

struct WidgetState: Codable, Sendable {
    var armedUntil: Date?
    var resultMessage: String?
    var resultIsError = false
    var resultAt: Date?

    func phase(at date: Date) -> WidgetPhase {
        if let armedUntil, armedUntil > date {
            return .armed(until: armedUntil)
        }
        if let resultAt, let resultMessage,
           date.timeIntervalSince(resultAt) < WidgetStateStore.resultWindow {
            return .result(message: resultMessage, isError: resultIsError)
        }
        return .idle
    }
}

/// Состояние «взвода» живёт в контейнере самого расширения: приложению оно не нужно,
/// поэтому App Group здесь не требуется.
enum WidgetStateStore {
    /// Сколько секунд виджет ждёт подтверждающего тапа.
    static let armWindow: TimeInterval = 5
    /// Сколько секунд показывается результат.
    static let resultWindow: TimeInterval = 60

    // Вычисляемое, а не хранимое: UserDefaults не Sendable, и Swift 6 запрещает
    // держать его в static let.
    private static var defaults: UserDefaults { .standard }

    /// Ключ привязан к цели действия, а не к экземпляру виджета: два одинаково
    /// настроенных виджета делят состояние, что на практике незаметно.
    static func key(host: String, port: Int, action: PowerAction) -> String {
        "state.\(host):\(port).\(action.rawValue)"
    }

    static func load(_ key: String) -> WidgetState {
        guard let data = defaults.data(forKey: key),
              let state = try? JSONDecoder().decode(WidgetState.self, from: data) else {
            return WidgetState()
        }
        return state
    }

    static func save(_ state: WidgetState, forKey key: String) {
        guard let data = try? JSONEncoder().encode(state) else { return }
        defaults.set(data, forKey: key)
    }
}
