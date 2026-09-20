import Foundation
import Security

/// Токены устройств — это учётные данные, поэтому живут в Keychain, а не в UserDefaults.
enum Keychain {
    private static let service = "com.ivankostiuk.Remotify.deviceToken"

    static func setToken(_ token: String, for deviceID: UUID) {
        let account = deviceID.uuidString
        guard let data = token.data(using: .utf8) else { return }

        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]

        let attributes: [String: Any] = [
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlock
        ]

        let status = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        if status == errSecItemNotFound {
            SecItemAdd(query.merging(attributes) { _, new in new } as CFDictionary, nil)
        }
    }

    static func token(for deviceID: UUID) -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: deviceID.uuidString,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]

        var item: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess,
              let data = item as? Data else { return nil }

        return String(data: data, encoding: .utf8)
    }

    static func removeToken(for deviceID: UUID) {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: deviceID.uuidString
        ]
        SecItemDelete(query as CFDictionary)
    }
}
