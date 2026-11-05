//
//  InputEngine.swift
//  鼠标连点器 for macOS
//
//  用 CoreGraphics 的 CGEvent 合成鼠标事件。
//  在 macOS 上这需要「辅助功能」权限，第一次运行会弹系统授权。
//

import Foundation
import AppKit
import CoreGraphics
import ApplicationServices

enum InputEngine {

    /// 当前进程有没有「辅助功能」权限（没有就点不动）。
    static func isTrusted() -> Bool {
        return AXIsProcessTrusted()
    }

    /// 请求权限：会弹系统对话框，并把 App 加进列表。
    static func requestTrust() {
        let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
        let options = [key: true] as CFDictionary
        AXIsProcessTrustedWithOptions(options)
    }

    /// 打开「辅助功能」设置页，方便手动勾选。
    static func openAccessibilitySettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
    }

    /// 当前鼠标位置。CGEvent 的坐标系是「主屏左上角为原点」，和界面上显示的坐标一致。
    static func mouseLocation() -> CGPoint {
        if let event = CGEvent(source: nil) {
            return event.location
        }
        return CGPoint.zero
    }

    /// 把光标移动到指定位置。
    static func move(to point: CGPoint) -> Bool {
        CGWarpMouseCursorPosition(point)
        CGAssociateMouseAndMouseCursorPosition(1)
        return true
    }

    /// 在指定位置点一下（按下 → 等 holdMs → 抬起）。
    @discardableResult
    static func click(at point: CGPoint, button: MouseButtonKind, holdMs: Int) -> Bool {
        let source = CGEventSource(stateID: .hidSystemState)
        let cgButton: CGMouseButton = (button == .right) ? .right : .left
        let downType: CGEventType = (button == .right) ? .rightMouseDown : .leftMouseDown
        let upType: CGEventType = (button == .right) ? .rightMouseUp : .leftMouseUp

        // 先把光标挪过去，再按下 —— 不移动直接点，很多程序会点到别处。
        _ = move(to: point)
        usleep(useconds_t(12 * 1000))

        guard let down = CGEvent(mouseEventSource: source, mouseType: downType,
                                 mouseCursorPosition: point, mouseButton: cgButton),
              let up = CGEvent(mouseEventSource: source, mouseType: upType,
                               mouseCursorPosition: point, mouseButton: cgButton) else {
            return false
        }

        down.post(tap: .cghidEventTap)
        let hold = max(1, holdMs)
        usleep(useconds_t(hold * 1000))
        up.post(tap: .cghidEventTap)
        return true
    }
}
