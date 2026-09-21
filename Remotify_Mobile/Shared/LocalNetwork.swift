import Foundation

/// IPv4-адрес интерфейса с маской. Адреса хранятся в порядке байтов хоста.
/// Нужен, чтобы понять, в одной ли сети телефон и ПК, — без запросов по сети
/// и без разрешений, поэтому работает, даже когда ПК спит.
struct IPv4Subnet: Equatable, Sendable {
    let address: UInt32
    let mask: UInt32

    func contains(_ ip: UInt32) -> Bool {
        ip & mask == address & mask
    }

    /// Находится ли `host` в одной из `subnets`.
    static func isLocal(host: String, in subnets: [IPv4Subnet] = current()) -> Bool {
        guard let ip = parse(host.trimmingCharacters(in: .whitespaces)) else {
            // Имя хоста или IPv6 — подсеть не сравнить. Считаем, что мы дома,
            // если есть хоть какая-то локальная сеть, а дальше всё решит запрос к ПК.
            return !subnets.isEmpty
        }
        return subnets.contains { $0.contains(ip) }
    }

    static func parse(_ string: String) -> UInt32? {
        var addr = in_addr()
        guard inet_pton(AF_INET, string, &addr) == 1 else { return nil }
        return UInt32(bigEndian: addr.s_addr)
    }

    /// Подсети Wi-Fi/Ethernet (`en*`) и режима модема (`bridge*`).
    /// Сотовая сеть (`pdp_ip*`) и VPN (`utun*`) в локальную не превращают.
    static func current() -> [IPv4Subnet] {
        var head: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&head) == 0, let first = head else { return [] }
        defer { freeifaddrs(head) }

        var result: [IPv4Subnet] = []
        for pointer in sequence(first: first, next: { $0.pointee.ifa_next }) {
            let interface = pointer.pointee
            let flags = Int32(bitPattern: interface.ifa_flags)
            guard flags & (IFF_UP | IFF_RUNNING) == (IFF_UP | IFF_RUNNING),
                  flags & IFF_LOOPBACK == 0,
                  let address = interface.ifa_addr,
                  address.pointee.sa_family == sa_family_t(AF_INET),
                  let netmask = interface.ifa_netmask else { continue }

            let name = String(cString: interface.ifa_name)
            guard name.hasPrefix("en") || name.hasPrefix("bridge") else { continue }

            result.append(IPv4Subnet(address: ipv4(address), mask: ipv4(netmask)))
        }
        return result
    }

    private static func ipv4(_ address: UnsafeMutablePointer<sockaddr>) -> UInt32 {
        address.withMemoryRebound(to: sockaddr_in.self, capacity: 1) {
            UInt32(bigEndian: $0.pointee.sin_addr.s_addr)
        }
    }
}

/// Состояние ПК с точки зрения телефона.
enum Reachability: String, Codable, Sendable {
    /// Телефон не в той сети, где ПК.
    case offNetwork
    /// В сети ПК, но он не отвечает: спит, выключен или Remotify не запущен.
    case asleep
    case online

    /// Подсеть плюс `GET /api/ping`.
    static func check(_ device: Device) async -> Reachability {
        guard IPv4Subnet.isLocal(host: device.host) else { return .offNetwork }
        do {
            try await APIClient.shared.ping(host: device.host, port: device.port)
            return .online
        } catch APIError.transport {
            return .asleep
        } catch {
            // Кто-то по адресу ответил — не спит. Ошибку покажет попытка выполнить действие.
            return .online
        }
    }
}
