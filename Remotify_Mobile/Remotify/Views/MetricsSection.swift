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
            HStack(spacing: 10) {
                rateView(metrics.networkDownloadBps, systemImage: "arrow.down")
                rateView(metrics.networkUploadBps, systemImage: "arrow.up")
            }
        } label: {
            Label("Сеть", systemImage: "network")
        }
    }

    /// Ширина зарезервирована под самое длинное значение, поэтому при смене
    /// количества цифр стрелки и соседнее значение не прыгают.
    private func rateView(_ bytesPerSecond: Int64, systemImage: String) -> some View {
        HStack(spacing: 2) {
            Image(systemName: systemImage)
                .font(.caption)
            ZStack(alignment: .trailing) {
                Text(Self.rate(999_900_000)).hidden()
                Text(Self.rate(bytesPerSecond))
            }
        }
        .lineLimit(1)
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

    /// Всегда один знак после запятой и не меньше КБ — так длина строки почти не меняется
    /// (`.byteCount` скачет между «0 байт», «890 КБ» и «1,2 МБ»).
    private static func rate(_ bytesPerSecond: Int64) -> String {
        let units = ["КБ/с", "МБ/с", "ГБ/с"]
        var value = Double(max(bytesPerSecond, 0)) / 1000
        var unit = 0
        // Округляем заранее, чтобы 999,96 КБ/с не превращалось в «1000,0 КБ/с».
        while (value * 10).rounded() / 10 >= 1000, unit < units.count - 1 {
            value /= 1000
            unit += 1
        }
        return "\(value.formatted(.number.precision(.fractionLength(1)))) \(units[unit])"
    }

    private static func uptime(_ seconds: Int64) -> String {
        Duration.seconds(seconds).formatted(
            .units(allowed: [.days, .hours, .minutes], width: .abbreviated, maximumUnitCount: 2)
        )
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
