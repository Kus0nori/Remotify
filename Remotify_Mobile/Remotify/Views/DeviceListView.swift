import SwiftUI

struct DeviceListView: View {
    @Environment(DeviceStore.self) private var store
    @State private var isAddingDevice = false

    var body: some View {
        NavigationStack {
            Group {
                if store.devices.isEmpty {
                    ContentUnavailableView {
                        Label("Нет устройств", systemImage: "desktopcomputer")
                    } description: {
                        Text("Добавьте компьютер с запущенным Remotify, чтобы управлять им отсюда.")
                    } actions: {
                        Button("Добавить устройство") { isAddingDevice = true }
                            .buttonStyle(.borderedProminent)
                    }
                } else {
                    List {
                        ForEach(store.devices) { device in
                            NavigationLink(value: device) {
                                DeviceRow(device: device)
                            }
                        }
                        .onDelete { store.remove(atOffsets: $0) }
                    }
                }
            }
            .navigationTitle("Устройства")
            .navigationDestination(for: Device.self) { device in
                DeviceDetailView(device: device)
            }
            .toolbar {
                ToolbarItem(placement: .primaryAction) {
                    Button("Добавить", systemImage: "plus") { isAddingDevice = true }
                }
            }
            .sheet(isPresented: $isAddingDevice) {
                AddDeviceView()
            }
        }
    }
}

private struct DeviceRow: View {
    let device: Device

    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: "desktopcomputer")
                .font(.title2)
                .foregroundStyle(.tint)
                .frame(width: 32)

            VStack(alignment: .leading, spacing: 2) {
                Text(device.name)
                    .font(.body)
                Text(device.displayAddress)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(.vertical, 4)
    }
}

#Preview {
    DeviceListView()
        .environment(DeviceStore())
        .environment(LocalNetworkMonitor())
}
