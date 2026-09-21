import SwiftUI

/// Секция с метриками ПК. Опрос живёт в `DeviceDetailView`, здесь только отображение.
struct MetricsSection: View {
    let device: Device
    let metrics: SystemMetrics?
    /// Количество открытых окон; `nil`, если список не пришёл.
    let appCount: Int?
    let errorMessage: String?

    var body: some View {
        Section {
            if let metrics {
                Group {
                    usageRow("Процессор", systemImage: "cpu", percent: metrics.cpuUsage) {
                        Text(Self.percent(metrics.cpuUsage))
                    }
                    usageRow("Память", systemImage: "memorychip", percent: metrics.ramUsage) {
                        Text("\(Self.gigabytes(metrics.ramUsedGb)) из \(Self.gigabytes(metrics.ramTotalGb)) ГБ")
                    }
                    networkRow(metrics)
                    LabeledContent {
                        Text(Self.uptime(metrics.uptimeSeconds))
                    } label: {
                        Label("Работает", systemImage: "clock")
                    }
                    NavigationLink {
                        RunningAppsView(device: device)
                    } label: {
                        LabeledContent {
                            Text(appCount.map(String.init) ?? "—")
                        } label: {
                            Label("Приложения", systemImage: "macwindow.on.rectangle")
                        }
                    }
                }
                // Если ПК перестал отвечать, показываем последние данные приглушёнными.
                .opacity(errorMessage == nil ? 1 : 0.5)
            } else if errorMessage == nil {
                HStack {
                    Text("Загрузка…").foregroundStyle(.secondary)
                    Spacer()
                    ProgressView()
                }
            }

            if let errorMessage {
                Text(errorMessage)
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }
        } header: {
            Text("Метрики")
        }
        .monospacedDigit()
        .contentTransition(.numericText())
        .animation(.default, value: metrics)
        .animation(.default, value: appCount)
    }

    private func usageRow(
        _ title: String,
        systemImage: String,
        percent: Double,
        @ViewBuilder value: () -> some View
    ) -> some View {
        VStack(spacing: 8) {
            LabeledContent {
                value()
            } label: {
                Label(title, systemImage: systemImage)
            }
            ProgressView(value: min(max(percent, 0), 100), total: 100)
                .tint(Self.tint(for: percent))
        }
        .padding(.vertical, 2)
    }

    private func networkRow(_ metrics: SystemMetrics) -> some View {
        LabeledContent {
            VStack(alignment: .trailing, spacing: 2) {
                Label(Self.rate(metrics.networkDownloadBps), systemImage: "arrow.down")
                Label(Self.rate(metrics.networkUploadBps), systemImage: "arrow.up")
            }
            .labelStyle(TrailingIconLabelStyle())
        } label: {
            Label("Сеть", systemImage: "network")
        }
    }

    private static func tint(for percent: Double) -> Color {
        switch percent {
        case 90...: .red
        case 75..<90: .orange
        default: .accentColor
        }
    }

    private static func percent(_ value: Double) -> String {
        (value / 100).formatted(.percent.precision(.fractionLength(0)))
    }

    private static func gigabytes(_ value: Double) -> String {
        value.formatted(.number.precision(.fractionLength(1)))
    }

    private static func rate(_ bytesPerSecond: Int64) -> String {
        "\(max(bytesPerSecond, 0).formatted(.byteCount(style: .decimal)))/с"
    }

    private static func uptime(_ seconds: Int64) -> String {
        Duration.seconds(seconds).formatted(
            .units(allowed: [.days, .hours, .minutes], width: .abbreviated, maximumUnitCount: 2)
        )
    }
}

/// Значение слева, стрелка справа — чтобы цифры в столбик выравнивались по правому краю.
private struct TrailingIconLabelStyle: LabelStyle {
    func makeBody(configuration: Configuration) -> some View {
        HStack(spacing: 4) {
            configuration.title
            configuration.icon
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }
}

#Preview {
    let device = Device(name: "Домашний ПК", host: "192.168.1.10", port: 5123)
    NavigationStack {
        List {
            MetricsSection(
                device: device,
                metrics: SystemMetrics(
                    cpuUsage: 45.2,
                    ramUsage: 81.5,
                    ramUsedGb: 13.04,
                    ramTotalGb: 16,
                    networkUploadBps: 125_000,
                    networkDownloadBps: 890_000,
                    uptimeSeconds: 345_600
                ),
                appCount: 7,
                errorMessage: nil
            )
            MetricsSection(device: device, metrics: nil, appCount: nil, errorMessage: nil)
        }
    }
    .environment(DeviceStore())
}
