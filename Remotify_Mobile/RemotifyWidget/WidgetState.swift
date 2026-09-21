import Foundation

/// Что виджет показывает прямо сейчас.
enum WidgetPhase {
    case unconfigured
    /// Телефон не в той сети, где ПК.
    case offNetwork
    /// ПК не отвечает. Когда появится Wake-on-LAN, тап будет его будить.
    case asleep
    /// ПК на связи, ждём тапа.
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
    var reachability: Reachability?
    var checkedAt: Date?

    /// Взвод и результат важнее состояния ПК: после «Сон» сначала показываем
    /// «Отправлено», и только потом — что ПК спит.
    func phase(at date: Date) -> WidgetPhase {
        if let armedUntil, armedUntil > date {
            return .armed(until: armedUntil)
        }
        if let resultAt, let resultMessage,
           date.timeIntervalSince(resultAt) < WidgetStateStore.resultWindow {
            return .result(message: resultMessage, isError: resultIsError)
        }
        switch reachability {
        case .offNetwork: return .offNetwork
        case .asleep: return .asleep
        case .online, nil: return .idle
        }
    }

    /// Интент только что проверил ПК — провайдеру незачем стучаться второй раз.
    func isReachabilityFresh(at date: Date) -> Bool {
        guard let checkedAt else { return false }
        return date.timeIntervalSince(checkedAt) < WidgetStateStore.reachabilityTTL
    }
}

/// Состояние живёт в контейнере самого расширения: приложению оно не нужно,
/// поэтому App Group здесь не требуется.
enum WidgetStateStore {
    /// Сколько секунд виджет ждёт подтверждающего тапа.
    static let armWindow: TimeInterval = 5
    /// Сколько секунд показывается результат.
    static let resultWindow: TimeInterval = 60
    /// Сколько секунд считается свежей последняя проверка ПК.
    static let reachabilityTTL: TimeInterval = 15
    /// Как часто перепроверять ПК, когда ничего не происходит. Чаще WidgetKit не даст —
    /// у виджета дневной бюджет на обновления.
    static let refreshInterval: TimeInterval = 15 * 60

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

    /// Проверяет ПК и сохраняет результат. Перечитывает состояние перед записью,
    /// чтобы не затереть взвод, который интент мог сохранить, пока шёл запрос.
    static func refreshReachability(of device: Device, key: String) async -> WidgetState {
        let reachability = await Reachability.check(device)
        var state = load(key)
        state.reachability = reachability
        state.checkedAt = .now
        save(state, forKey: key)
        return state
    }
}
