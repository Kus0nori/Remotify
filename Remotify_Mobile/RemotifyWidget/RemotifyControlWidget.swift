import AppIntents
import SwiftUI
import WidgetKit

struct RemotifyEntry: TimelineEntry {
    let date: Date
    let configuration: RemotifyWidgetConfiguration
    let phase: WidgetPhase
}

struct RemotifyProvider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> RemotifyEntry {
        RemotifyEntry(date: .now, configuration: RemotifyWidgetConfiguration(), phase: .idle)
    }

    func snapshot(for configuration: RemotifyWidgetConfiguration, in context: Context) async -> RemotifyEntry {
        RemotifyEntry(date: .now, configuration: configuration, phase: phase(for: configuration, at: .now))
    }

    func timeline(for configuration: RemotifyWidgetConfiguration, in context: Context) async -> Timeline<RemotifyEntry> {
        let now = Date.now
        let state = WidgetStateStore.load(configuration.stateKey)

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

        let entries = dates.sorted().map { date in
            RemotifyEntry(date: date, configuration: configuration, phase: phase(for: configuration, at: date))
        }
        return Timeline(entries: entries, policy: .atEnd)
    }

    private func phase(for configuration: RemotifyWidgetConfiguration, at date: Date) -> WidgetPhase {
        guard configuration.isConfigured else { return .unconfigured }
        return WidgetStateStore.load(configuration.stateKey).phase(at: date)
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
        .description("Первое нажатие спрашивает подтверждение, второе выполняет действие.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

struct RemotifyWidgetView: View {
    let entry: RemotifyEntry

    private var isUnconfigured: Bool {
        if case .unconfigured = entry.phase { return true }
        return false
    }

    var body: some View {
        Button(intent: RemotifyTapIntent(configuration: entry.configuration)) {
            VStack(alignment: .leading, spacing: 4) {
                header
                Spacer(minLength: 0)
                status
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
        }
        .font(.caption)
        .foregroundStyle(.secondary)
    }

    @ViewBuilder
    private var status: some View {
        switch entry.phase {
        case .unconfigured:
            Label("Настройте виджет", systemImage: "gearshape")
                .font(.footnote)
                .foregroundStyle(.secondary)

        case .idle:
            Label(entry.configuration.action.title, systemImage: entry.configuration.action.systemImage)
                .font(.headline)
                .foregroundStyle(entry.configuration.action.isDestructive ? Color.red : Color.primary)

        case .armed(let until):
            VStack(alignment: .leading, spacing: 6) {
                Text("Подтвердить?")
                    .font(.headline)
                Text("Нажмите ещё раз")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                ProgressView(timerInterval: entry.date...until, countsDown: true)
                    .labelsHidden()
                    .tint(entry.configuration.action.isDestructive ? .red : .accentColor)
            }

        case .result(let message, let isError):
            Label(message, systemImage: isError ? "exclamationmark.triangle.fill" : "checkmark.circle.fill")
                .font(.subheadline)
                .foregroundStyle(isError ? Color.red : Color.green)
                .lineLimit(2)
        }
    }
}
