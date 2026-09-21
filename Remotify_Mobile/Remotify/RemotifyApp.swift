import SwiftUI

@main
struct RemotifyApp: App {
    @State private var store = DeviceStore()
    @State private var network = LocalNetworkMonitor()

    var body: some Scene {
        WindowGroup {
            DeviceListView()
                .environment(store)
                .environment(network)
        }
    }
}
