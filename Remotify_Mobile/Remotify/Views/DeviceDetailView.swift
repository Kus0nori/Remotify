import SwiftUI

struct DeviceDetailView: View {
    let device: Device

    @Environment(DeviceStore.self) private var store
    @Environment(\.scenePhase) private var scenePhase

    @State private var status: Status = .unknown
    @State private var metrics: SystemMetrics?
    @State private var appCount: Int?
    @State private var metricsError: String?
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
            actionsSection
            offlineSection
            MetricsSection(device: device, metrics: metrics, appCount: appCount, errorMessage: metricsError)
            widgetSection
            resultSection
        }
        .navigationTitle(device.name)
        // Статус рисуем рядом с названием, а в большой заголовок свои вью не вставить.
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .principal) { titleView }
            ToolbarItem(placement: .primaryAction) {
                Button("Обновить", systemImage: "arrow.clockwise") {
                    Task { await refreshStatus() }
                }
                .disabled(isChecking)
            }
        }
        .refreshable { await refreshStatus() }
        .task { await refreshStatus() }
        // Задача отменяется, когда экран уходит из вида или приложение сворачивается.
        .task(id: scenePhase) {
            guard scenePhase == .active else { return }
            await pollMetrics()
        }
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

    private var titleView: some View {
        HStack(spacing: 6) {
            Text(device.name)
                .font(.headline)
                .lineLimit(1)
            statusDot
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(device.name)
        .accessibilityValue(statusTitle)
    }

    @ViewBuilder
    private var offlineSection: some View {
        if case .offline(let message) = status {
            Section {
                Label(message, systemImage: "wifi.exclamationmark")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }
        }
    }

    /// Ряд плиток, как быстрые действия на карточке в «Контактах».
    private var actionsSection: some View {
        Section {
            HStack(spacing: 8) {
                ForEach(PowerAction.allCases) { action in
                    Button {
                        pendingAction = action
                    } label: {
                        VStack(spacing: 6) {
                            Group {
                                if runningAction == action {
                                    ProgressView()
                                } else {
                                    Image(systemName: action.systemImage)
                                }
                            }
                            .font(.title3)
                            .frame(height: 24)
                            .foregroundStyle(action.isDestructive ? Color.red : Color.accentColor)

                            Text(action.title)
                                .font(.caption)
                                .lineLimit(1)
                                .minimumScaleFactor(0.7)
                        }
                    }
                    .buttonStyle(ActionTileButtonStyle())
                    .disabled(runningAction != nil)
                }
            }
            .listRowBackground(Color.clear)
            .listRowInsets(EdgeInsets())
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

    /// Точка-индикатор, как у статуса «в сети» в мессенджерах. Пульсирует во время проверки.
    private var statusDot: some View {
        Image(systemName: "circle.fill")
            .font(.system(size: 8))
            .foregroundStyle(statusColor)
            .symbolEffect(.pulse, isActive: isChecking)
            .animation(.default, value: statusColor)
    }

    private var statusColor: Color {
        switch status {
        case .unknown, .checking: .secondary
        case .online: .green
        case .offline: .red
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

    /// Десктоп пересчитывает метрики раз в секунду, чаще спрашивать нет смысла.
    private func pollMetrics() async {
        guard let token = store.token(for: device) else {
            metricsError = "Токен для этого устройства не найден. Добавьте устройство заново."
            return
        }

        while !Task.isCancelled {
            // Список окон нужен только ради количества; если он не пришёл, метрики всё равно показываем.
            async let apps = try? APIClient.shared.apps(from: device, token: token)
            do {
                metrics = try await APIClient.shared.metrics(from: device, token: token)
                appCount = await apps?.count
                metricsError = nil
            } catch {
                guard !Task.isCancelled else { return }
                metricsError = error.localizedDescription
            }
            try? await Task.sleep(for: .seconds(2))
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

/// Плитка действия. Свой стиль ещё и нужен, чтобы кнопки в одной строке `List`
/// нажимались по отдельности, а не все разом тапом по строке.
private struct ActionTileButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .foregroundStyle(.primary)
            .frame(maxWidth: .infinity)
            .padding(.vertical, 10)
            .padding(.horizontal, 4)
            .background(
                Color(.secondarySystemGroupedBackground),
                in: .rect(cornerRadius: 12)
            )
            .opacity(configuration.isPressed ? 0.6 : isEnabled ? 1 : 0.4)
            .animation(.easeOut(duration: 0.15), value: configuration.isPressed)
    }
}

#Preview {
    NavigationStack {
        DeviceDetailView(device: Device(name: "Домашний ПК", host: "192.168.1.10", port: 5123))
    }
    .environment(DeviceStore())
}
