//
//  Runner.swift
//  鼠标连点器 for macOS
//
//  执行线程 + 可中断等待。UI 通过回调拿状态，回调都切回主线程。
//

import Foundation

final class Runner {

    private(set) var isRunning = false
    private var stopFlag = false
    private var thread: Thread?

    private(set) var doneCount = 0
    private(set) var loopsDone = 0
    private(set) var currentIndex = -1
    private(set) var phase = "就绪"

    var onUpdate: (() -> Void)?
    var onFinish: ((String?) -> Void)?

    func start(_ settings: MacroSettings) {
        if isRunning {
            return
        }
        isRunning = true
        stopFlag = false
        doneCount = 0
        loopsDone = 0
        currentIndex = -1
        phase = "运行中"

        let snapshot = settings
        let t = Thread { [weak self] in
            self?.run(snapshot)
        }
        t.stackSize = 512 * 1024
        thread = t
        t.start()
        notify()
    }

    func stop() {
        stopFlag = true
        phase = "停止中"
        notify()
    }

    private func notify() {
        DispatchQueue.main.async { [weak self] in
            self?.onUpdate?()
        }
    }

    /// 可以被 stop 打断的等待。
    private func sleep(ms: Int) -> Bool {
        var remain = max(0, ms)
        while remain > 0 {
            if stopFlag {
                return false
            }
            let chunk = min(10, remain)
            usleep(useconds_t(chunk * 1000))
            remain -= chunk
        }
        return !stopFlag
    }

    private func run(_ settings: MacroSettings) {
        var error: String? = nil

        if !InputEngine.isTrusted() {
            error = "没有「辅助功能」权限，点不动。请到 系统设置 → 隐私与安全性 → 辅助功能 里勾上本程序。"
            finish(error)
            return
        }

        if settings.startDelayMs > 0 {
            phase = "启动延迟"
            notify()
            if !sleep(ms: settings.startDelayMs) {
                finish(nil)
                return
            }
        }

        phase = "运行中"
        notify()

        var loops = 0
        while !stopFlag {
            if settings.loopCount > 0 && loops >= settings.loopCount {
                break
            }

            var index = 0
            while index < settings.actions.count {
                if stopFlag {
                    break
                }
                let action = settings.actions[index]
                currentIndex = index
                index += 1

                if !action.enabled {
                    continue
                }

                if action.kind == .click {
                    let point = CGPoint(x: action.x, y: action.y)
                    _ = InputEngine.click(at: point, button: action.button, holdMs: settings.holdMs)
                    doneCount += 1
                    notify()
                } else if action.kind == .wheel {
                    let point = CGPoint(x: action.x, y: action.y)
                    _ = InputEngine.wheel(at: point, notches: action.wheelDelta)
                    doneCount += 1
                    notify()
                }

                if !sleep(ms: action.waitMs) {
                    break
                }
            }

            if stopFlag {
                break
            }
            loops += 1
            loopsDone = loops
            notify()
        }

        phase = stopFlag ? "已停止" : "完成"
        finish(error)
    }

    private func finish(_ error: String?) {
        isRunning = false
        currentIndex = -1
        DispatchQueue.main.async { [weak self] in
            self?.onFinish?(error)
        }
    }
}
