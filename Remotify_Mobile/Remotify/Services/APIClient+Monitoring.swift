import Foundation

/// Эндпоинты только для приложения: виджету они не нужны,
/// поэтому модели лежат в таргете приложения, а не в `Shared/`.
extension APIClient {
    /// `GET /api/apps`
    func apps(from device: Device, token: String) async throws -> [RemoteApp] {
        struct AppsResponse: Decodable { let apps: [RemoteApp] }
        let data = try await fetch("api/apps", from: device, token: token)
        return try decode(AppsResponse.self, from: data).apps
    }

    /// `GET /api/apps/{id}/icon` — PNG. `nil`, если у окна нет иконки (404).
    func appIcon(id: String, from device: Device, token: String) async throws -> Data? {
        do {
            return try await fetch("api/apps/\(id)/icon", from: device, token: token)
        } catch APIError.unexpectedStatus(let code) where code == 404 {
            return nil
        }
    }

    /// `GET /api/metrics`
    func metrics(from device: Device, token: String) async throws -> SystemMetrics {
        let data = try await fetch("api/metrics", from: device, token: token)
        return try decode(SystemMetrics.self, from: data)
    }

    private func decode<T: Decodable>(_ type: T.Type, from data: Data) throws -> T {
        do {
            return try JSONDecoder().decode(type, from: data)
        } catch {
            throw APIError.badRequest("Не удалось разобрать ответ ПК. Возможно, версия Remotify на ПК устарела.")
        }
    }
}
