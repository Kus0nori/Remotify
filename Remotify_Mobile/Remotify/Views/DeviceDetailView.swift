import SwiftUI

struct DeviceDetailView: View {
    let device: Device

    @Environment(DeviceStore.self) private var store
    @Environment(LocalNetworkMonitor.self) private var network
    @Environment(\.scenePhase) private var scenePhase

    @State private var status: Status = .unknown
    /// Последний однозначный результат проверки. Пока идёт повторная проверка
    /// (`.unknown` / `.checking`), экран не переключается.
    @State private var isReachable: Bool?
    @State private var metrics: SystemMetrics?
    @State private var appCount: Int?
    @State private var metricsError: String?
    @State private var pendingAction: PowerAction?
    @State private var runningAction: PowerAction?
    @State private var result: ActionResult?
    @State private var copyCount = 0

    private enum Status {
        case unknown, checking, online, offline(String)
    }

    private struct ActionResult: Identifiable {
        let id = UUID()
        let message: String
        let isError: Bool
    }

    private enum Screen {
        /// Телефон не в той сети, где ПК.
        case offNetwork
        /// Первая проверка ещё не закончилась.
        case loading
        /// В локальной сети, пк отвечает
        case online
        /// В лоокальной сети, пк НЕ отвечает.
        case asleep
    }

    /// Перезапускает опрос при сворачивании приложения и смене сети.
    private struct PollTrigger: Equatable {
        let isActive: Bool
        let isOnLocalNetwork: Bool
    }

    private var isOnLocalNetwork: Bool {
        network.isOnSameNetwork(as: device.host)
    }

    private var screen: Screen {
        guard isOnLocalNetwork else { return .offNetwork }
        switch isReachable {
        case nil: return .loading
        case true?: return .online
        case false?: return .asleep
        }
    }

    var body: some View {
        Group {
            switch screen {
            case .offNetwork: offNetworkScreen
            case .loading: ProgressView()
            case .online: onlineScreen
            case .asleep: asleepScreen
            }
        }
        .animation(.default, value: screen)
        .navigationTitle(device.name)
        // Статус рисуем рядом с названием, а в большой заголовок свои вью не вставить.
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .principal) { titleView }
            ToolbarItem(placement: .primaryAction) { moreMenu }
        }
        // Задача отменяется, когда экран уходит из вида, приложение сворачивается или меняется сеть.
        .task(id: PollTrigger(isActive: scenePhase == .active, isOnLocalNetwork: isOnLocalNetwork)) {
            guard scenePhase == .active, isOnLocalNetwork else { return }
            await poll()
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

    // MARK: - Экраны

    private var onlineScreen: some View {
        List {
            actionsSection
            MetricsSection(device: device, metrics: metrics, appCount: appCount, errorMessage: metricsError)
            resultSection
        }
        .refreshable { await manualRefresh() }
    }

    private var asleepScreen: some View {
        List {
            Section {
                VStack(spacing: 8) {
                    Image(systemName: "powersleep")
                        .font(.system(size: 48))
                        .foregroundStyle(.secondary)
                        .padding(.bottom, 4)
                    Text("ПК не отвечает")
                        .font(.title3.bold())
                    Text("Он спит, выключен или Remotify на нём не запущен.")
                        .foregroundStyle(.secondary)
                    if case .offline(let message) = status {
                        Text(message)
                            .font(.footnote)
                            .foregroundStyle(.tertiary)
                    }
                }
                .multilineTextAlignment(.center)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 8)
                .listRowBackground(Color.clear)
            }

            Section {
                // TODO: Wake-on-LAN. Без multicast-entitlement broadcast в iOS запрещён,
                // остаётся unicast на IP ПК — будит из сна, но не после выключения.
                Button {} label: {
                    Label("Разбудить", systemImage: "power")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(true)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            } footer: {
                Text("Включение по сети (Wake-on-LAN) появится позже.")
                    .frame(maxWidth: .infinity)
                    .multilineTextAlignment(.center)
            }

            resultSection
        }
        .refreshable { await manualRefresh() }
    }

    private var offNetworkScreen: some View {
        ContentUnavailableView {
            Label("Не в локальной сети", systemImage: "wifi.slash")
        } description: {
            Text("Подключитесь к той же сети, что и «\(device.name)» (\(device.displayAddress)), чтобы управлять им.")
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

    private var moreMenu: some View {
        Menu("Ещё", systemImage: "ellipsis.circle") {
            Button("Обновить", systemImage: "arrow.clockwise") {
                Task { await manualRefresh() }
            }
            .disabled(isChecking || !isOnLocalNetwork)

            // Виджет не делит хранилище с приложением, поэтому адрес и токен
            // приходится переносить в его настройки вручную.
            Section("Для виджета") {
                Button("Скопировать адрес", systemImage: "doc.on.doc") {
                    UIPasteboard.general.string = device.displayAddress
                    copyCount += 1
                }
                Button("Скопировать токен", systemImage: "key") {
                    UIPasteboard.general.string = store.token(for: device) ?? ""
                    copyCount += 1
                }
            }
        }
        // Меню после выбора закрывается, подтверждаем копирование вибрацией.
        .sensoryFeedback(.success, trigger: copyCount)
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
        guard isOnLocalNetwork else { return "Не в локальной сети" }
        return switch status {
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
        guard isOnLocalNetwork else { return .secondary }
        return switch status {
        case .unknown, .checking: .secondary
        case .online: .green
        case .offline: .red
        }
    }

    /// Десктоп пересчитывает метрики раз в секунду, чаще спрашивать нет смысла.
    /// Опрос идёт и на экране «ПК не отвечает» — чтобы заметить, что ПК проснулся.
    private func poll() async {
        if case .unknown = status { status = .checking }
        while !Task.isCancelled {
            await refresh()
            try? await Task.sleep(for: .seconds(2))
        }
    }

    private func manualRefresh() async {
        status = .checking
        await refresh()
    }

    /// Отдельный ping не нужен: ответ на запрос метрик и так показывает, на связи ли ПК.
    private func refresh() async {
        guard isOnLocalNetwork else { return }
        guard let token = store.token(for: device) else {
            metricsError = "Токен для этого устройства не найден. Добавьте устройство заново."
            status = .online
            isReachable = true
            return
        }

        // Список окон нужен только ради количества; если он не пришёл, метрики всё равно показываем.
        async let apps = try? APIClient.shared.apps(from: device, token: token)
        do {
            metrics = try await APIClient.shared.metrics(from: device, token: token)
            appCount = await apps?.count
            metricsError = nil
            status = .online
            isReachable = true
        } catch APIError.transport(let message) {
            guard !Task.isCancelled else { return }
            status = .offline(message)
            isReachable = false
        } catch {
            guard !Task.isCancelled else { return }
            // ПК ответил, но не метриками (неверный токен, старая версия без /api/metrics) — он всё равно на связи.
            metricsError = error.localizedDescription
            status = .online
            isReachable = true
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
    .environment(LocalNetworkMonitor())
}
