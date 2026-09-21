import AppIntents
import SwiftUI
import WidgetKit

struct RemotifyEntry: TimelineEntry {
    let date: Date
    let configuration: RemotifyWidgetConfiguration
    let phase: WidgetPhase
    let reachability: Reachability?
}

struct RemotifyProvider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> RemotifyEntry {
        RemotifyEntry(date: .now, configuration: RemotifyWidgetConfiguration(), phase: .idle, reachability: .online)
    }

    /// Снапшот для галереи и превью — без сети, из последнего сохранённого состояния.
    func snapshot(for configuration: RemotifyWidgetConfiguration, in context: Context) async -> RemotifyEntry {
        guard configuration.isConfigured else { return unconfiguredEntry(configuration) }
        let state = WidgetStateStore.load(configuration.stateKey)
        return entry(configuration, state: state, at: .now)
    }

    func timeline(for configuration: RemotifyWidgetConfiguration, in context: Context) async -> Timeline<RemotifyEntry> {
        guard configuration.isConfigured else {
            // Новые параметры сами перезагрузят таймлайн.
            return Timeline(entries: [unconfiguredEntry(configuration)], policy: .never)
        }

        let now = Date.now
        var state = WidgetStateStore.load(configuration.stateKey)
        if !state.isReachabilityFresh(at: now) {
            state = await WidgetStateStore.refreshReachability(of: configuration.device, key: configuration.stateKey)
        }

        // Запись на «сейчас» плюс моменты, когда взвод и результат сами истекают,
        // иначе виджет застрянет на «Подтвердить?».
        var dates: [Date] = [now]
        if let armedUntil = state.armedUntil, armedUntil > now {
            dates.append(armedUntil)
        }
        if let resultAt = state.resultAt {
            let expiry = resultAt.addingTimeInterval(WidgetStateStore.resultWindow)
            if expiry > now { dates.append(expiry) }
        }

        let entries = dates.sorted().map { entry(configuration, state: state, at: $0) }
        // Когда результат истечёт — перепроверить ПК сразу: после «Сон» он уже спит.
        // В остальное время — по расписанию.
        let policy: TimelineReloadPolicy = dates.count > 1
            ? .atEnd
            : .after(now.addingTimeInterval(WidgetStateStore.refreshInterval))
        return Timeline(entries: entries, policy: policy)
    }

    private func entry(_ configuration: RemotifyWidgetConfiguration, state: WidgetState, at date: Date) -> RemotifyEntry {
        RemotifyEntry(date: date, configuration: configuration, phase: state.phase(at: date), reachability: state.reachability)
    }

    private func unconfiguredEntry(_ configuration: RemotifyWidgetConfiguration) -> RemotifyEntry {
        RemotifyEntry(date: .now, configuration: configuration, phase: .unconfigured, reachability: nil)
    }
}

struct RemotifyControlWidget: Widget {
    static let kind = "RemotifyControlWidget"

    var body: some WidgetConfiguration {
        AppIntentConfiguration(
            kind: Self.kind,
            intent: RemotifyWidgetConfiguration.self,
            provider: RemotifyProvider()
        ) { entry in
            RemotifyWidgetView(entry: entry)
        }
        .configurationDisplayName("Управление ПК")
        .description("Показывает, на связи ли ПК. Первое нажатие спрашивает подтверждение, второе выполняет действие.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

struct RemotifyWidgetView: View {
    let entry: RemotifyEntry

    private var action: PowerAction { entry.configuration.action }

    private var isUnconfigured: Bool {
        if case .unconfigured = entry.phase { return true }
        return false
    }

    var body: some View {
        Button(intent: RemotifyTapIntent(configuration: entry.configuration)) {
            VStack(alignment: .leading, spacing: 4) {
                header
                Spacer(minLength: 0)
                content
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        }
        .buttonStyle(.plain)
        .disabled(isUnconfigured)
        .containerBackground(.fill.tertiary, for: .widget)
    }

    private var header: some View {
        HStack(spacing: 4) {
            Image(systemName: "desktopcomputer")
            Text(entry.configuration.name)
                .lineLimit(1)
            if let color = statusColor {
                Image(systemName: "circle.fill")
                    .font(.system(size: 6))
                    .foregroundStyle(color)
            }
        }
        .font(.caption)
        .foregroundStyle(.secondary)
    }

    /// Та же точка, что в приложении рядом с названием ПК.
    private var statusColor: Color? {
        switch entry.reachability {
        case .online: .green
        case .asleep: .red
        case .offNetwork: .secondary
        case nil: nil
        }
    }

    @ViewBuilder
    private var content: some View {
        switch entry.phase {
        case .unconfigured:
            Label("Настройте виджет", systemImage: "gearshape")
                .font(.footnote)
                .foregroundStyle(.secondary)

        case .offNetwork:
            statusBlock(
                systemImage: "wifi.slash",
                title: "Не в сети ПК",
                subtitle: "Подключитесь к его Wi-Fi"
            )

        case .asleep:
            // TODO: Wake-on-LAN — здесь будет «Разбудить».
            statusBlock(
                systemImage: "powersleep",
                title: "ПК спит",
                subtitle: "Нажмите, чтобы проверить"
            )

        case .idle:
            VStack(alignment: .leading, spacing: 4) {
                Image(systemName: action.systemImage)
                    .font(.title)
                Text(action.title)
                    .font(.headline)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
            }
            .foregroundStyle(action.isDestructive ? Color.red : Color.accentColor)

        case .armed(let until):
            VStack(alignment: .leading, spacing: 6) {
                Text("Подтвердить?")
                    .font(.headline)
                Text("Нажмите ещё раз")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                ProgressView(timerInterval: entry.date...until, countsDown: true)
                    .labelsHidden()
                    .tint(action.isDestructive ? .red : .accentColor)
            }

        case .result(let message, let isError):
            Label(message, systemImage: isError ? "exclamationmark.triangle.fill" : "checkmark.circle.fill")
                .font(.subheadline)
                .foregroundStyle(isError ? Color.red : Color.green)
                .lineLimit(2)
        }
    }

    private func statusBlock(systemImage: String, title: String, subtitle: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Image(systemName: systemImage)
                .font(.title)
                .foregroundStyle(.secondary)
            Text(title)
                .font(.headline)
                .lineLimit(1)
            Text(subtitle)
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(2)
        }
    }
}

#Preview(as: .systemSmall) {
    RemotifyControlWidget()
} timeline: {
    let configuration = RemotifyWidgetConfiguration()
    RemotifyEntry(date: .now, configuration: configuration, phase: .idle, reachability: .online)
    RemotifyEntry(date: .now, configuration: configuration, phase: .armed(until: .now.addingTimeInterval(5)), reachability: .online)
    RemotifyEntry(date: .now, configuration: configuration, phase: .asleep, reachability: .asleep)
    RemotifyEntry(date: .now, configuration: configuration, phase: .offNetwork, reachability: .offNetwork)
}
