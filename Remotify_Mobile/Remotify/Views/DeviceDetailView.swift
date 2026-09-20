import SwiftUI

struct DeviceDetailView: View {
    let device: Device

    @Environment(DeviceStore.self) private var store

    @State private var status: Status = .unknown
    @State private var pendingAction: PowerAction?
    @State private var runningAction: PowerAction?
    @State private var result: ActionResult?
    @State private var copied: String?

    private enum Status {
        case unknown, checking, online, offline(String)
    }

    private struct ActionResult: Identifiable {
        let id = UUID()
        let message: String
        let isError: Bool
    }

    var body: some View {
        List {
            statusSection
            actionsSection
            widgetSection
            resultSection
        }
        .navigationTitle(device.name)
        .navigationBarTitleDisplayMode(.large)
        .task { await refreshStatus() }
        .confirmationDialog(
            pendingAction?.confirmationMessage(deviceName: device.name) ?? "",
            isPresented: Binding(get: { pendingAction != nil }, set: { if !$0 { pendingAction = nil } }),
            titleVisibility: .visible
        ) {
            if let action = pendingAction {
                Button(action.title, role: action.isDestructive ? .destructive : nil) {
                    Task { await run(action) }
                }
            }
            Button("Отмена", role: .cancel) {}
        }
    }

    private var statusSection: some View {
        Section("Состояние") {
            HStack {
                statusIcon
                VStack(alignment: .leading, spacing: 2) {
                    Text(statusTitle)
                    Text(device.displayAddress)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Button("Обновить") { Task { await refreshStatus() } }
                    .buttonStyle(.bordered)
                    .disabled(isChecking)
            }
            .padding(.vertical, 4)

            if case .offline(let message) = status {
                Text(message)
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private var actionsSection: some View {
        Section("Действия") {
            ForEach(PowerAction.allCases) { action in
                Button {
                    pendingAction = action
                } label: {
                    HStack {
                        Label(action.title, systemImage: action.systemImage)
                        Spacer()
                        if runningAction == action {
                            ProgressView()
                        }
                    }
                }
                .disabled(runningAction != nil)
                .foregroundStyle(action.isDestructive ? Color.red : Color.accentColor)
            }
        }
    }

    /// Виджет не делит хранилище с приложением, поэтому адрес и токен
    /// приходится переносить в его настройки вручную.
    private var widgetSection: some View {
        Section {
            Button("Скопировать адрес", systemImage: "doc.on.doc") {
                UIPasteboard.general.string = device.displayAddress
                copied = "Адрес скопирован"
            }
            Button("Скопировать токен", systemImage: "key") {
                UIPasteboard.general.string = store.token(for: device) ?? ""
                copied = "Токен скопирован"
            }
        } header: {
            Text("Для виджета")
        } footer: {
            Text(copied ?? "Виджет настраивается отдельно: вставьте эти значения в его параметрах.")
                .foregroundStyle(copied == nil ? Color.secondary : Color.green)
        }
    }

    @ViewBuilder
    private var resultSection: some View {
        if let result {
            Section {
                Label(result.message, systemImage: result.isError ? "exclamationmark.triangle" : "checkmark.circle")
                    .foregroundStyle(result.isError ? Color.red : Color.green)
                    .font(.footnote)
            }
        }
    }

    private var isChecking: Bool {
        if case .checking = status { return true }
        return false
    }

    private var statusTitle: String {
        switch status {
        case .unknown: "Неизвестно"
        case .checking: "Проверка…"
        case .online: "На связи"
        case .offline: "Недоступен"
        }
    }

    @ViewBuilder
    private var statusIcon: some View {
        switch status {
        case .unknown:
            Image(systemName: "questionmark.circle.fill").foregroundStyle(.secondary)
        case .checking:
            ProgressView().frame(width: 20)
        case .online:
            Image(systemName: "checkmark.circle.fill").foregroundStyle(.green)
        case .offline:
            Image(systemName: "xmark.circle.fill").foregroundStyle(.red)
        }
    }

    private func refreshStatus() async {
        status = .checking
        do {
            try await APIClient.shared.ping(host: device.host, port: device.port)
            status = .online
        } catch {
            status = .offline(error.localizedDescription)
        }
    }

    private func run(_ action: PowerAction) async {
        guard let token = store.token(for: device) else {
            result = ActionResult(message: "Токен для этого устройства не найден. Добавьте устройство заново.", isError: true)
            return
        }

        runningAction = action
        result = nil
        defer { runningAction = nil }

        do {
            switch try await APIClient.shared.send(action, to: device, token: token) {
            case .confirmed:
                result = ActionResult(message: "«\(action.title)» — выполнено.", isError: false)
            case .noResponse:
                result = ActionResult(
                    message: "«\(action.title)» — команда отправлена, ПК не ответил. Обычно это значит, что он уже её выполняет.",
                    isError: false
                )
            }
            status = .unknown
        } catch {
            result = ActionResult(message: error.localizedDescription, isError: true)
        }
    }
}

#Preview {
    NavigationStack {
        DeviceDetailView(device: Device(name: "Домашний ПК", host: "192.168.1.10", port: 5123))
    }
    .environment(DeviceStore())
}
