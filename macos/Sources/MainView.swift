//
//  MainView.swift
//  鼠标连点器 for macOS
//
//  一个窗口 + 一个自绘的动作列表。界面刻意做得简单，先保证能用。
//

import Cocoa

/// 把按钮点击收进闭包，省掉一堆 @objc 方法。
final class ActionTarget: NSObject {
    private let handler: () -> Void

    init(_ handler: @escaping () -> Void) {
        self.handler = handler
        super.init()
    }

    @objc func fire(_ sender: Any?) {
        handler()
    }
}

/// 自绘的动作列表：一行一个动作，点左侧圆点切换启用，双击改等待。
final class ActionList: NSView {

    var actions: [MacroAction] = []
    var selectedIndex: Int = -1
    var onSelect: ((Int) -> Void)?
    var onToggle: ((Int) -> Void)?
    var onEditWait: ((Int) -> Void)?

    let rowHeight: CGFloat = 34

    override var isFlipped: Bool {
        return true
    }

    override var acceptsFirstResponder: Bool {
        return true
    }

    func reload() {
        let height = max(rowHeight, CGFloat(actions.count) * rowHeight)
        let width = superview?.bounds.width ?? bounds.width
        setFrameSize(NSSize(width: width, height: height))
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.textBackgroundColor.setFill()
        NSBezierPath(rect: bounds).fill()

        if actions.isEmpty {
            let attrs: [NSAttributedString.Key: Any] = [
                .font: NSFont.systemFont(ofSize: 13),
                .foregroundColor: NSColor.secondaryLabelColor
            ]
            let text = "还没有动作。点下面的「添加点击」，或者把鼠标放到目标位置再按 F7 更新坐标。"
            (text as NSString).draw(in: NSRect(x: 16, y: 24, width: bounds.width - 32, height: 20),
                                    withAttributes: attrs)
            return
        }

        for (index, action) in actions.enumerated() {
            let top = CGFloat(index) * rowHeight
            let rowRect = NSRect(x: 0, y: top, width: bounds.width, height: rowHeight)

            if index == selectedIndex {
                NSColor.selectedContentBackgroundColor.withAlphaComponent(0.18).setFill()
                NSBezierPath(rect: rowRect).fill()
                NSColor.controlAccentColor.setFill()
                NSBezierPath(rect: NSRect(x: 0, y: top, width: 3, height: rowHeight)).fill()
            }

            // 启用圆点
            let dotRect = NSRect(x: 12, y: top + 11, width: 12, height: 12)
            let dot = NSBezierPath(ovalIn: dotRect)
            if action.enabled {
                NSColor.controlAccentColor.setFill()
                dot.fill()
            } else {
                NSColor.tertiaryLabelColor.setStroke()
                dot.lineWidth = 1.5
                dot.stroke()
            }

            let titleColor = action.enabled ? NSColor.labelColor : NSColor.tertiaryLabelColor
            let numberAttrs: [NSAttributedString.Key: Any] = [
                .font: NSFont.monospacedDigitSystemFont(ofSize: 11, weight: .regular),
                .foregroundColor: NSColor.tertiaryLabelColor
            ]
            let titleAttrs: [NSAttributedString.Key: Any] = [
                .font: NSFont.systemFont(ofSize: 13),
                .foregroundColor: titleColor
            ]
            let waitAttrs: [NSAttributedString.Key: Any] = [
                .font: NSFont.monospacedDigitSystemFont(ofSize: 12, weight: .regular),
                .foregroundColor: NSColor.secondaryLabelColor
            ]

            let number = String(format: "%02d", index + 1)
            (number as NSString).draw(in: NSRect(x: 36, y: top + 10, width: 28, height: 16),
                                      withAttributes: numberAttrs)
            (action.title as NSString).draw(in: NSRect(x: 66, y: top + 9, width: max(20, bounds.width - 200), height: 18),
                                            withAttributes: titleAttrs)
            let waitText = "\(action.waitMs) ms"
            (waitText as NSString).draw(in: NSRect(x: bounds.width - 120, y: top + 10, width: 100, height: 16),
                                        withAttributes: waitAttrs)

            NSColor.separatorColor.setStroke()
            let line = NSBezierPath()
            line.move(to: NSPoint(x: 0, y: top + rowHeight - 0.5))
            line.line(to: NSPoint(x: bounds.width, y: top + rowHeight - 0.5))
            line.lineWidth = 0.5
            line.stroke()
        }
    }

    override func mouseDown(with event: NSEvent) {
        let point = convert(event.locationInWindow, from: nil)
        let row = Int(point.y / rowHeight)
        guard row >= 0 && row < actions.count else {
            return
        }
        if point.x < 32 {
            onToggle?(row)
            return
        }
        onSelect?(row)
        if event.clickCount == 2 {
            onEditWait?(row)
        }
    }
}

final class MainView: NSView {

    private var settings = MacroSettings()
    private let runner = Runner()
    private var targets: [ActionTarget] = []

    private let titleLabel = NSTextField(labelWithString: "鼠标连点器")
    private let subtitleLabel = NSTextField(labelWithString: "macOS · 屏幕坐标 · 点击 / 等待")
    private let statusLabel = NSTextField(labelWithString: "就绪")
    private let detailLabel = NSTextField(labelWithString: "F6 开始/停止 · F8 急停 · F7 取坐标")

    private let scrollView = NSScrollView()
    private let listView = ActionList()

    private let loopField = NSTextField(string: "0")
    private let delayField = NSTextField(string: "800")
    private let holdField = NSTextField(string: "40")

    private let startButton = NSButton()
    private let footerLabel = NSTextField(labelWithString: "")

    // 事件监视器必须自己持有，否则会被释放掉
    private var globalMonitor: Any?
    private var localMonitor: Any?

    override var isFlipped: Bool {
        return true
    }

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        buildUi()
        loadSettings()
        refreshList()
        refreshStatus()
        installHotkeys()
        needsLayout = true
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        buildUi()
        loadSettings()
        refreshList()
        refreshStatus()
        installHotkeys()
    }

    // MARK: - 构建界面

    private func buildUi() {
        wantsLayer = true
        layer?.backgroundColor = NSColor.windowBackgroundColor.cgColor

        titleLabel.font = NSFont.systemFont(ofSize: 22, weight: .bold)
        subtitleLabel.font = NSFont.systemFont(ofSize: 12)
        subtitleLabel.textColor = NSColor.secondaryLabelColor
        statusLabel.font = NSFont.systemFont(ofSize: 13, weight: .semibold)
        statusLabel.alignment = .right
        detailLabel.font = NSFont.systemFont(ofSize: 11)
        detailLabel.textColor = NSColor.secondaryLabelColor
        detailLabel.alignment = .right
        footerLabel.font = NSFont.systemFont(ofSize: 11)
        footerLabel.textColor = NSColor.tertiaryLabelColor

        addSubview(titleLabel)
        addSubview(subtitleLabel)
        addSubview(statusLabel)
        addSubview(detailLabel)
        addSubview(footerLabel)

        scrollView.hasVerticalScroller = true
        scrollView.borderType = .bezelBorder
        scrollView.drawsBackground = true
        scrollView.backgroundColor = NSColor.textBackgroundColor
        scrollView.documentView = listView
        addSubview(scrollView)

        listView.onSelect = { [weak self] index in
            self?.listView.selectedIndex = index
            self?.listView.needsDisplay = true
        }
        listView.onToggle = { [weak self] index in
            guard let self = self, index < self.settings.actions.count else { return }
            self.settings.actions[index].enabled.toggle()
            self.persist()
            self.listView.needsDisplay = true
        }
        listView.onEditWait = { [weak self] index in
            self?.editWait(at: index)
        }

        addButton("添加点击", x: 20, y: 396, w: 96) { [weak self] in self?.addClick() }
        addButton("添加等待", x: 122, y: 396, w: 96) { [weak self] in self?.addWait() }
        addButton("删除", x: 224, y: 396, w: 76) { [weak self] in self?.deleteSelected() }
        addButton("上移", x: 306, y: 396, w: 54) { [weak self] in self?.move(-1) }
        addButton("下移", x: 366, y: 396, w: 54) { [weak self] in self?.move(1) }
        addButton("用鼠标位置更新 (F7)", x: 430, y: 396, w: 160) { [weak self] in self?.updateFromMouse() }

        addLabel("循环次数", x: 20, y: 442, w: 70)
        addNumberField(loopField, x: 92, y: 438, w: 80)
        addLabel("0 = 无限", x: 178, y: 442, w: 80)

        addLabel("启动延迟", x: 268, y: 442, w: 70)
        addNumberField(delayField, x: 340, y: 438, w: 80)
        addLabel("ms", x: 426, y: 442, w: 24)

        addLabel("点击按住", x: 470, y: 442, w: 70)
        addNumberField(holdField, x: 542, y: 438, w: 70)
        addLabel("ms", x: 618, y: 442, w: 24)

        startButton.title = "开始执行 (F6)"
        startButton.bezelStyle = .rounded
        startButton.controlSize = .large
        startButton.font = NSFont.systemFont(ofSize: 14, weight: .medium)
        startButton.bezelColor = .controlAccentColor
        startButton.contentTintColor = .white
        startButton.frame = NSRect(x: 20, y: 486, width: 200, height: 40)
        let startTarget = ActionTarget { [weak self] in self?.toggleStart() }
        targets.append(startTarget)
        startButton.target = startTarget
        startButton.action = #selector(ActionTarget.fire(_:))
        addSubview(startButton)

        let testTarget = ActionTarget { [weak self] in self?.runTest() }
        targets.append(testTarget)
        let testButton = NSButton(title: "测试 (F9)", target: testTarget, action: #selector(ActionTarget.fire(_:)))
        testButton.frame = NSRect(x: 232, y: 488, width: 100, height: 34)
        testButton.bezelStyle = .rounded
        addSubview(testButton)

        let permTarget = ActionTarget { [weak self] in self?.checkPermission(showAlert: true) }
        targets.append(permTarget)
        let permButton = NSButton(title: "权限检查", target: permTarget, action: #selector(ActionTarget.fire(_:)))
        permButton.frame = NSRect(x: 344, y: 488, width: 100, height: 34)
        permButton.bezelStyle = .rounded
        addSubview(permButton)

        footerLabel.stringValue = "F6 开始/停止 · F7 用鼠标位置更新选中动作 · F8 急停 · F9 测试一次 · 双击动作改等待"

        runner.onUpdate = { [weak self] in self?.refreshStatus() }
        runner.onFinish = { [weak self] error in
            self?.refreshStatus()
            if let error = error {
                self?.alertUser(title: "执行出错", text: error)
            }
        }
    }

    private func addButton(_ title: String, x: CGFloat, y: CGFloat, w: CGFloat, handler: @escaping () -> Void) {
        let target = ActionTarget(handler)
        targets.append(target)
        let button = NSButton(title: title, target: target, action: #selector(ActionTarget.fire(_:)))
        button.bezelStyle = .rounded
        button.frame = NSRect(x: x, y: y, width: w, height: 30)
        addSubview(button)
    }

    private func addLabel(_ text: String, x: CGFloat, y: CGFloat, w: CGFloat) {
        let label = NSTextField(labelWithString: text)
        label.font = NSFont.systemFont(ofSize: 12)
        label.textColor = NSColor.secondaryLabelColor
        label.frame = NSRect(x: x, y: y, width: w, height: 18)
        addSubview(label)
    }

    private func addNumberField(_ field: NSTextField, x: CGFloat, y: CGFloat, w: CGFloat) {
        field.font = NSFont.monospacedDigitSystemFont(ofSize: 12, weight: .regular)
        field.alignment = .right
        field.frame = NSRect(x: x, y: y, width: w, height: 24)
        field.bezelStyle = .roundedBezel
        addSubview(field)
    }

    override func layout() {
        super.layout()
        let width = bounds.width

        titleLabel.frame = NSRect(x: 20, y: 16, width: 360, height: 28)
        subtitleLabel.frame = NSRect(x: 20, y: 46, width: 360, height: 18)
        statusLabel.frame = NSRect(x: width - 300, y: 18, width: 280, height: 20)
        detailLabel.frame = NSRect(x: width - 300, y: 40, width: 280, height: 18)

        scrollView.frame = NSRect(x: 20, y: 84, width: width - 40, height: 300)
        listView.reload()

        footerLabel.frame = NSRect(x: 20, y: bounds.height - 30, width: width - 40, height: 18)
    }

    // MARK: - 数据

    private func loadSettings() {
        if let loaded = Store.load() {
            settings = loaded
        } else {
            var demo = MacroAction()
            demo.kind = .click
            demo.x = 500
            demo.y = 400
            demo.waitMs = 200
            settings.actions = [demo]
            settings.loopCount = 0
        }
        if settings.actions.isEmpty {
            var demo = MacroAction()
            demo.x = 500
            demo.y = 400
            settings.actions = [demo]
        }
        loopField.stringValue = String(settings.loopCount)
        delayField.stringValue = String(settings.startDelayMs)
        holdField.stringValue = String(settings.holdMs)
    }

    private func persist() {
        settings.loopCount = Int(loopField.stringValue) ?? 0
        settings.startDelayMs = Int(delayField.stringValue) ?? 800
        settings.holdMs = max(1, Int(holdField.stringValue) ?? 40)
        Store.save(settings)
    }

    private func refreshList() {
        listView.actions = settings.actions
        if listView.selectedIndex >= settings.actions.count {
            listView.selectedIndex = settings.actions.count - 1
        }
        listView.reload()
    }

    private func refreshStatus() {
        if runner.isRunning {
            statusLabel.stringValue = "运行中 · 第 \(runner.loopsDone + 1) 轮 · 已点 \(runner.doneCount) 次"
            statusLabel.textColor = NSColor.controlAccentColor
            startButton.title = "停止 (F6)"
            startButton.bezelColor = .systemRed
        } else {
            statusLabel.stringValue = "就绪 · 共 \(settings.actions.count) 个动作"
            statusLabel.textColor = NSColor.labelColor
            startButton.title = "开始执行 (F6)"
            startButton.bezelColor = .controlAccentColor
        }

        if !InputEngine.isTrusted() {
            detailLabel.stringValue = "⚠️ 没有辅助功能权限，点不动"
            detailLabel.textColor = NSColor.systemOrange
        } else {
            detailLabel.stringValue = "F6 开始/停止 · F8 急停 · F7 取坐标"
            detailLabel.textColor = NSColor.secondaryLabelColor
        }
    }

    // MARK: - 动作编辑

    private func addClick() {
        let p = InputEngine.mouseLocation()
        var action = MacroAction()
        action.kind = .click
        action.x = Int(p.x.rounded())
        action.y = Int(p.y.rounded())
        action.waitMs = 200
        settings.actions.append(action)
        listView.selectedIndex = settings.actions.count - 1
        persist()
        refreshList()
        refreshStatus()
    }

    private func addWait() {
        var action = MacroAction()
        action.kind = .wait
        action.waitMs = 500
        settings.actions.append(action)
        listView.selectedIndex = settings.actions.count - 1
        persist()
        refreshList()
        refreshStatus()
    }

    private func deleteSelected() {
        let index = listView.selectedIndex
        guard index >= 0 && index < settings.actions.count else {
            return
        }
        settings.actions.remove(at: index)
        listView.selectedIndex = min(index, settings.actions.count - 1)
        persist()
        refreshList()
        refreshStatus()
    }

    private func move(_ delta: Int) {
        let index = listView.selectedIndex
        let target = index + delta
        guard index >= 0, index < settings.actions.count, target >= 0, target < settings.actions.count else {
            return
        }
        let action = settings.actions.remove(at: index)
        settings.actions.insert(action, at: target)
        listView.selectedIndex = target
        persist()
        refreshList()
    }

    private func updateFromMouse() {
        let index = listView.selectedIndex
        guard index >= 0 && index < settings.actions.count else {
            alertUser(title: "先选一个动作", text: "在列表里点一下要更新的动作，再按 F7。")
            return
        }
        guard settings.actions[index].kind == .click else {
            alertUser(title: "不是点击动作", text: "只有点击动作有坐标。")
            return
        }
        let p = InputEngine.mouseLocation()
        settings.actions[index].x = Int(p.x.rounded())
        settings.actions[index].y = Int(p.y.rounded())
        persist()
        refreshList()
    }

    private func editWait(at index: Int) {
        guard index >= 0 && index < settings.actions.count else {
            return
        }
        let alert = NSAlert()
        alert.messageText = "这条动作之后等待多久？"
        alert.informativeText = "单位毫秒（ms）。填 0 表示紧接着做下一条。"
        alert.addButton(withTitle: "确定")
        alert.addButton(withTitle: "取消")
        let field = NSTextField(string: String(settings.actions[index].waitMs))
        field.frame = NSRect(x: 0, y: 0, width: 200, height: 24)
        alert.accessoryView = field
        if alert.runModal() == .alertFirstButtonReturn {
            settings.actions[index].waitMs = max(0, Int(field.stringValue) ?? settings.actions[index].waitMs)
            persist()
            refreshList()
        }
    }

    // MARK: - 运行

    private func toggleStart() {
        if runner.isRunning {
            runner.stop()
            return
        }
        persist()
        runner.start(settings)
        refreshStatus()
    }

    private func runTest() {
        if runner.isRunning {
            runner.stop()
            return
        }
        var test = settings
        test.loopCount = 1
        test.startDelayMs = 0
        persist()
        runner.start(test)
        refreshStatus()
    }

    private func checkPermission(showAlert: Bool) {
        if InputEngine.isTrusted() {
            if showAlert {
                alertUser(title: "权限正常", text: "已经拿到辅助功能权限，可以合成鼠标事件。")
            }
            refreshStatus()
            return
        }

        InputEngine.requestTrust()
        if showAlert {
            let alert = NSAlert()
            alert.messageText = "需要「辅助功能」权限"
            alert.informativeText = "macOS 不允许 App 随便控制鼠标。请到 系统设置 → 隐私与安全性 → 辅助功能，"
                + "把「鼠标连点器」勾上（勾完可能要退出重开一次）。"
            alert.addButton(withTitle: "打开设置")
            alert.addButton(withTitle: "知道了")
            if alert.runModal() == .alertFirstButtonReturn {
                InputEngine.openAccessibilitySettings()
            }
        }
        refreshStatus()
    }

    private func alertUser(title: String, text: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = text
        alert.addButton(withTitle: "好")
        alert.runModal()
    }

    // MARK: - 全局快捷键

    private func installHotkeys() {
        globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: .keyDown) { [weak self] event in
            self?.handleHotkey(event)
        }
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            self?.handleHotkey(event)
            return event
        }
    }

    private func handleHotkey(_ event: NSEvent) {
        // 带 Command / Control 的组合不抢
        if event.modifierFlags.contains(.command) || event.modifierFlags.contains(.control) {
            return
        }
        switch event.keyCode {
        case 97:   // F6
            DispatchQueue.main.async { [weak self] in self?.toggleStart() }
        case 98:   // F7
            DispatchQueue.main.async { [weak self] in self?.updateFromMouse() }
        case 100:  // F8
            DispatchQueue.main.async { [weak self] in self?.runner.stop() }
        case 101:  // F9
            DispatchQueue.main.async { [weak self] in self?.runTest() }
        default:
            break
        }
    }
}
