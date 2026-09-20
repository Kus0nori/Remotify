import Foundation
import Observation

/// Список устройств в UserDefaults, токены — в Keychain.
@MainActor
@Observable
final class DeviceStore {
    private(set) var devices: [Device] = []

    private let defaultsKey = "devices"
    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        load()
    }

    func add(_ device: Device, token: String) {
        Keychain.setToken(token, for: device.id)
        devices.append(device)
        save()
    }

    func remove(atOffsets offsets: IndexSet) {
        for index in offsets {
            Keychain.removeToken(for: devices[index].id)
        }
        devices.remove(atOffsets: offsets)
        save()
    }

    func token(for device: Device) -> String? {
        Keychain.token(for: device.id)
    }

    private func load() {
        guard let data = defaults.data(forKey: defaultsKey),
              let decoded = try? JSONDecoder().decode([Device].self, from: data) else { return }
        devices = decoded
    }

    private func save() {
        guard let data = try? JSONEncoder().encode(devices) else { return }
        defaults.set(data, forKey: defaultsKey)
    }
}
