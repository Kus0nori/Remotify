import Foundation

/// Видимое окно на ПК из `GET /api/apps` (как в Alt+Tab).
struct RemoteApp: Identifiable, Decodable, Hashable, Sendable {
    /// Handle окна в hex — по нему же запрашивается иконка.
    let id: String
    let title: String
    let processName: String
    /// Эвристика десктопа: `*` в заголовке окна.
    let mayHaveUnsavedChanges: Bool
}
