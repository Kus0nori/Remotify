import SwiftUI

@main
struct RemotifyApp: App {
    @State private var store = DeviceStore()

    var body: some Scene {
        WindowGroup {
            DeviceListView()
                .environment(store)
        }
    }
}
