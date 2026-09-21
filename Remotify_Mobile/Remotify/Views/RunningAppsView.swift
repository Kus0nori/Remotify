import SwiftUI
import UIKit

/// Открытые на ПК окна из `GET /api/apps`.
struct RunningAppsView: View {
    let device: Device

    @Environment(DeviceStore.self) private var store

    @State private var apps: [RemoteApp]?
    @State private var errorMessage: String?
    @State private var icons = AppIconCache()

    var body: some View {
        List {
            if let errorMessage, apps != nil {
                // Список уже был — оставляем его, ошибку показываем сверху.
                Section {
                    Label(errorMessage, systemImage: "exclamationmark.triangle")
                        .font(.footnote)
                        .foregroundStyle(.red)
                }
            }

            if let apps, !apps.isEmpty {
                Section {
                    ForEach(apps) { app in
                        AppRow(app: app, icon: icons.images[app.id])
                            .task { await loadIcon(for: app) }
                    }
                } footer: {
                    Text("Окон: \(apps.count)")
                }
            }
        }
        .overlay { placeholder }
        .navigationTitle("Приложения")
        .refreshable { await load() }
        .task { await load() }
    }

    @ViewBuilder
    private var placeholder: some View {
        if let apps {
            if apps.isEmpty {
                ContentUnavailableView(
                    "Нет открытых окон",
                    systemImage: "macwindow",
                    description: Text("На ПК сейчас нет видимых приложений.")
                )
            }
        } else if let errorMessage {
            ContentUnavailableView {
                Label("Не удалось загрузить", systemImage: "exclamationmark.triangle")
            } description: {
                Text(errorMessage)
            } actions: {
                Button("Повторить") { Task { await load() } }
                    .buttonStyle(.bordered)
            }
        } else {
            ProgressView()
        }
    }

    private func load() async {
        guard let token = store.token(for: device) else {
            errorMessage = "Токен для этого устройства не найден. Добавьте устройство заново."
            return
        }

        do {
            let loaded = try await APIClient.shared.apps(from: device, token: token)
            apps = loaded
            errorMessage = nil
            icons.keepOnly(Set(loaded.map(\.id)))
        } catch {
            // Отмена pull-to-refresh — не ошибка.
            guard !Task.isCancelled else { return }
            errorMessage = error.localizedDescription
        }
    }

    private func loadIcon(for app: RemoteApp) async {
        guard icons.images[app.id] == nil, let token = store.token(for: device) else { return }
        guard let data = try? await APIClient.shared.appIcon(id: app.id, from: device, token: token),
              let image = UIImage(data: data) else { return }
        icons.images[app.id] = image
    }
}

/// Иконки по handle окна. Живёт, пока открыт экран, чтобы не качать их заново при обновлении.
@MainActor
@Observable
private final class AppIconCache {
    var images: [String: UIImage] = [:]

    /// Handle закрытого окна может достаться новому — выкидываем устаревшие.
    func keepOnly(_ ids: Set<String>) {
        images = images.filter { ids.contains($0.key) }
    }
}

private struct AppRow: View {
    let app: RemoteApp
    let icon: UIImage?

    var body: some View {
        HStack(spacing: 12) {
            Group {
                if let icon {
                    Image(uiImage: icon)
                        .resizable()
                        .interpolation(.high)
                        .scaledToFit()
                } else {
                    Image(systemName: "app.dashed")
                        .font(.title2)
                        .foregroundStyle(.secondary)
                }
            }
            .frame(width: 32, height: 32)

            VStack(alignment: .leading, spacing: 2) {
                Text(app.title)
                    .lineLimit(2)
                Text(app.processName)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                if app.mayHaveUnsavedChanges {
                    Label("Возможно, есть несохранённые изменения", systemImage: "pencil.circle.fill")
                        .font(.caption)
                        .foregroundStyle(.orange)
                }
            }
        }
        .padding(.vertical, 2)
    }
}

#Preview {
    NavigationStack {
        RunningAppsView(device: Device(name: "Домашний ПК", host: "192.168.1.10", port: 5123))
    }
    .environment(DeviceStore())
}
