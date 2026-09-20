import AppIntents
import Foundation

/// Команды, которые понимает `POST /api/command` десктопной части.
enum PowerAction: String, CaseIterable, Identifiable, Sendable {
    case shutdown
    case reboot
    case sleep
    case hibernate

    var id: String { rawValue }

    var title: String {
        switch self {
        case .shutdown: "Выключить"
        case .reboot: "Перезагрузить"
        case .sleep: "Сон"
        case .hibernate: "Гибернация"
        }
    }

    var systemImage: String {
        switch self {
        case .shutdown: "power"
        case .reboot: "arrow.clockwise"
        case .sleep: "moon.zzz"
        case .hibernate: "snowflake"
        }
    }

    var isDestructive: Bool {
        switch self {
        case .shutdown, .reboot: true
        case .sleep, .hibernate: false
        }
    }

    func confirmationMessage(deviceName: String) -> String {
        switch self {
        case .shutdown: "Выключить «\(deviceName)»?"
        case .reboot: "Перезагрузить «\(deviceName)»?"
        case .sleep: "Перевести «\(deviceName)» в режим сна?"
        case .hibernate: "Перевести «\(deviceName)» в гибернацию?"
        }
    }
}

/// Даёт выпадающий список действий в настройках виджета.
extension PowerAction: AppEnum {
    static var typeDisplayRepresentation: TypeDisplayRepresentation { "Действие" }

    static var caseDisplayRepresentations: [PowerAction: DisplayRepresentation] {
        [
            .shutdown: DisplayRepresentation(title: "Выключить", image: .init(systemName: "power")),
            .reboot: DisplayRepresentation(title: "Перезагрузить", image: .init(systemName: "arrow.clockwise")),
            .sleep: DisplayRepresentation(title: "Сон", image: .init(systemName: "moon.zzz")),
            .hibernate: DisplayRepresentation(title: "Гибернация", image: .init(systemName: "snowflake"))
        ]
    }
}
