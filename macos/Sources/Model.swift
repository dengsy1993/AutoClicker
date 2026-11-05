//
//  Model.swift
//  鼠标连点器 for macOS
//
//  配置只存在一个 JSON 里，方便手工看。
//

import Foundation

enum ActionKind: String, Codable {
    case click
    case wait
}

enum MouseButtonKind: Int, Codable {
    case left = 0
    case right = 1

    var name: String {
        switch self {
        case .left: return "左键"
        case .right: return "右键"
        }
    }
}

struct MacroAction: Codable {
    var kind: ActionKind = .click
    var enabled: Bool = true
    var waitMs: Int = 200
    var x: Int = 0
    var y: Int = 0
    var button: MouseButtonKind = .left

    var title: String {
        switch kind {
        case .click:
            return "\(button.name)单击 (\(x), \(y))"
        case .wait:
            return "等待 \(waitMs) ms"
        }
    }
}

struct MacroSettings: Codable {
    var actions: [MacroAction] = []
    var loopCount: Int = 0        // 0 = 无限
    var startDelayMs: Int = 800
    var holdMs: Int = 40
}

enum Store {
    static var dataDir: URL {
        let fm = FileManager.default
        let base = fm.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSTemporaryDirectory())
        let dir = base.appendingPathComponent("AutoClickerMac", isDirectory: true)
        try? fm.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    static var fileURL: URL {
        return dataDir.appendingPathComponent("settings.json")
    }

    static func load() -> MacroSettings? {
        guard let data = try? Data(contentsOf: fileURL) else {
            return nil
        }
        return try? JSONDecoder().decode(MacroSettings.self, from: data)
    }

    static func save(_ settings: MacroSettings) {
        guard let data = try? JSONEncoder().encode(settings) else {
            return
        }
        try? data.write(to: fileURL, options: .atomic)
    }
}
