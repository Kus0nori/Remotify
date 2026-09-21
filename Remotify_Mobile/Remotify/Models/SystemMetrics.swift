import Foundation

/// Снимок состояния ПК из `GET /api/metrics`.
struct SystemMetrics: Decodable, Equatable, Sendable {
    /// Проценты, 0–100.
    let cpuUsage: Double
    /// Проценты, 0–100.
    let ramUsage: Double
    let ramUsedGb: Double
    let ramTotalGb: Double
    /// Байт в секунду.
    let networkUploadBps: Int64
    /// Байт в секунду.
    let networkDownloadBps: Int64
    let uptimeSeconds: Int64
}
