import Foundation

enum APIError: LocalizedError {
    case invalidAddress
    case unauthorized
    case badRequest(String)
    case unexpectedStatus(Int)
    case notRemotify
    case transport(String)

    var errorDescription: String? {
        switch self {
        case .invalidAddress:
            "Некорректный адрес устройства."
        case .unauthorized:
            "Неверный токен. Проверьте его в окне Remotify на ПК."
        case .badRequest(let message):
            message
        case .unexpectedStatus(let code):
            "Сервер ответил кодом \(code)."
        case .notRemotify:
            "По этому адресу отвечает не Remotify."
        case .transport(let message):
            message
        }
    }
}

/// Результат отправки команды.
enum CommandOutcome {
    /// ПК подтвердил выполнение.
    case confirmed
    /// Запрос ушёл, но ответа не было. Для `sleep`/`hibernate` это нормально:
    /// десктоп засыпает прямо внутри обработчика и не успевает ответить.
    case noResponse
}

struct APIClient: Sendable {
    static let shared = APIClient()

    private let session: URLSession

    init() {
        let config = URLSessionConfiguration.ephemeral
        config.timeoutIntervalForRequest = 5
        config.timeoutIntervalForResource = 10
        config.waitsForConnectivity = false
        config.allowsCellularAccess = true
        session = URLSession(configuration: config)
    }

    /// `GET /api/ping` — без авторизации, поэтому годится для проверки адреса до ввода токена.
    func ping(host: String, port: Int) async throws {
        let probe = Device(name: "", host: host, port: port)
        guard let url = probe.baseURL?.appending(path: "api/ping") else {
            throw APIError.invalidAddress
        }

        let (data, response) = try await perform(URLRequest(url: url))

        guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
            throw APIError.unexpectedStatus((response as? HTTPURLResponse)?.statusCode ?? -1)
        }

        struct PingResponse: Decodable { let status: String }
        guard let ping = try? JSONDecoder().decode(PingResponse.self, from: data), ping.status == "ok" else {
            throw APIError.notRemotify
        }
    }

    /// `POST /api/command` с Bearer-токеном.
    func send(_ action: PowerAction, to device: Device, token: String) async throws -> CommandOutcome {
        guard let url = device.baseURL?.appending(path: "api/command") else {
            throw APIError.invalidAddress
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONEncoder().encode(["action": action.rawValue])

        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await perform(request)
        } catch let error as URLError where Self.mayHaveBeenDelivered(error) {
            // ПК оборвал соединение или не ответил — типично для sleep/shutdown.
            return .noResponse
        }

        guard let http = response as? HTTPURLResponse else {
            throw APIError.unexpectedStatus(-1)
        }

        switch http.statusCode {
        case 200:
            return .confirmed
        case 401:
            throw APIError.unauthorized
        case 400:
            struct ErrorResponse: Decodable { let error: String }
            let message = (try? JSONDecoder().decode(ErrorResponse.self, from: data))?.error
            throw APIError.badRequest(message ?? "Некорректный запрос.")
        default:
            throw APIError.unexpectedStatus(http.statusCode)
        }
    }

    /// Авторизованный `GET` для эндпоинтов, которые отдают данные.
    /// Возвращает тело ответа 200, остальные коды превращает в `APIError`.
    func fetch(_ path: String, from device: Device, token: String) async throws -> Data {
        guard let url = device.baseURL?.appending(path: path) else {
            throw APIError.invalidAddress
        }

        var request = URLRequest(url: url)
        request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")

        let (data, response) = try await perform(request)

        guard let http = response as? HTTPURLResponse else {
            throw APIError.unexpectedStatus(-1)
        }

        switch http.statusCode {
        case 200:
            return data
        case 401:
            throw APIError.unauthorized
        default:
            throw APIError.unexpectedStatus(http.statusCode)
        }
    }

    private func perform(_ request: URLRequest) async throws -> (Data, URLResponse) {
        do {
            return try await session.data(for: request)
        } catch let error as URLError {
            throw APIError.transport(Self.message(for: error))
        }
    }

    /// Ошибки, при которых запрос, скорее всего, уже дошёл до ПК.
    private static func mayHaveBeenDelivered(_ error: URLError) -> Bool {
        switch error.code {
        case .timedOut, .networkConnectionLost: true
        default: false
        }
    }

    private static func message(for error: URLError) -> String {
        switch error.code {
        case .cannotConnectToHost:
            "Не удалось подключиться. Проверьте, что Remotify запущен на ПК и порт открыт в брандмауэре."
        case .timedOut:
            "Превышено время ожидания."
        case .cannotFindHost:
            "Адрес не найден в сети."
        case .notConnectedToInternet, .networkConnectionLost:
            "Нет соединения с сетью."
        default:
            error.localizedDescription
        }
    }
}
