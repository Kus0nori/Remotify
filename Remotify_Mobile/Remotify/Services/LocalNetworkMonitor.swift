import Foundation
import Network
import Observation

/// Следит за сменой сети и держит актуальные подсети телефона (см. `IPv4Subnet`).
@MainActor
@Observable
final class LocalNetworkMonitor {
    private(set) var subnets: [IPv4Subnet]

    private let monitor = NWPathMonitor()

    init() {
        subnets = IPv4Subnet.current()
        // Путь меняется при переключении Wi-Fi, уходе на сотовую сеть и т.п. — пересчитываем подсети.
        monitor.pathUpdateHandler = { [weak self] _ in
            let subnets = IPv4Subnet.current()
            Task { @MainActor [weak self] in
                self?.subnets = subnets
            }
        }
        monitor.start(queue: DispatchQueue(label: "LocalNetworkMonitor"))
    }

    func isOnSameNetwork(as host: String) -> Bool {
        IPv4Subnet.isLocal(host: host, in: subnets)
    }
}
