import Foundation

/// Компьютер с запущенным десктопным Remotify.
/// Токен здесь не хранится — он лежит в Keychain под `id` устройства.
struct Device: Identifiable, Codable, Hashable, Sendable {
    var id: UUID = UUID()
    var name: String
    var host: String
    var port: Int

    static let defaultPort = 5123

    var displayAddress: String { "\(host):\(port)" }

    /// `http://host:port`. Возвращает nil, если адрес не собирается в валидный URL.
    var baseURL: URL? {
        let trimmed = host.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return nil }

        var components = URLComponents()
        components.scheme = "http"
        // URLComponents ожидает IPv6-литерал в квадратных скобках.
        components.host = trimmed.contains(":") && !trimmed.hasPrefix("[") ? "[\(trimmed)]" : trimmed
        components.port = port
        return components.url
    }
}
