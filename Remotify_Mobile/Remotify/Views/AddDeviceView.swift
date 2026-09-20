import SwiftUI

struct AddDeviceView: View {
    @Environment(DeviceStore.self) private var store
    @Environment(\.dismiss) private var dismiss

    @State private var name = ""
    @State private var host = ""
    @State private var port = String(Device.defaultPort)
    @State private var token = ""
    @State private var check: CheckState = .idle

    private enum CheckState {
        case idle, checking, reachable, failed(String)
    }

    private var parsedPort: Int? {
        guard let value = Int(port), (1...65535).contains(value) else { return nil }
        return value
    }

    private var canSave: Bool {
        !name.trimmingCharacters(in: .whitespaces).isEmpty
            && !host.trimmingCharacters(in: .whitespaces).isEmpty
            && !token.trimmingCharacters(in: .whitespaces).isEmpty
            && parsedPort != nil
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Устройство") {
                    TextField("Название", text: $name)
                        .textInputAutocapitalization(.words)
                }

                Section {
                    TextField("IP-адрес или имя хоста", text: $host)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.URL)
                    TextField("Порт", text: $port)
                        .keyboardType(.numberPad)
                } header: {
                    Text("Адрес")
                } footer: {
                    Text("Например, 192.168.1.10 и порт \(Device.defaultPort).")
                }

                Section {
                    TextField("Токен", text: $token)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .font(.system(.body, design: .monospaced))
                } header: {
                    Text("Авторизация")
                } footer: {
                    Text("Токен показан в окне Remotify на компьютере.")
                }

                Section {
                    Button {
                        Task { await checkConnection() }
                    } label: {
                        HStack {
                            Text("Проверить подключение")
                            Spacer()
                            checkIndicator
                        }
                    }
                    .disabled(host.trimmingCharacters(in: .whitespaces).isEmpty || parsedPort == nil || isChecking)
                } footer: {
                    if case .failed(let message) = check {
                        Text(message).foregroundStyle(.red)
                    } else if case .reachable = check {
                        Text("Remotify отвечает по этому адресу.").foregroundStyle(.green)
                    }
                }
            }
            .navigationTitle("Новое устройство")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Отмена") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Сохранить", action: save).disabled(!canSave)
                }
            }
        }
    }

    private var isChecking: Bool {
        if case .checking = check { return true }
        return false
    }

    @ViewBuilder
    private var checkIndicator: some View {
        switch check {
        case .idle:
            EmptyView()
        case .checking:
            ProgressView()
        case .reachable:
            Image(systemName: "checkmark.circle.fill").foregroundStyle(.green)
        case .failed:
            Image(systemName: "xmark.circle.fill").foregroundStyle(.red)
        }
    }

    private func checkConnection() async {
        guard let parsedPort else { return }
        check = .checking
        do {
            try await APIClient.shared.ping(host: host.trimmingCharacters(in: .whitespaces), port: parsedPort)
            check = .reachable
        } catch {
            check = .failed(error.localizedDescription)
        }
    }

    private func save() {
        guard let parsedPort else { return }
        let device = Device(
            name: name.trimmingCharacters(in: .whitespaces),
            host: host.trimmingCharacters(in: .whitespaces),
            port: parsedPort
        )
        store.add(device, token: token.trimmingCharacters(in: .whitespaces))
        dismiss()
    }
}

#Preview {
    AddDeviceView()
        .environment(DeviceStore())
}
