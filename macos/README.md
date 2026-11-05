# 鼠标连点器 for macOS（v1.00.0 · 预览）

Windows 版的 macOS 移植，**功能是子集**，界面也是原生简单的排版，不是 Windows 版那种自绘风格。

> ⚠️ 这份代码是**在没有 Mac 的机器上写的**，没有真机编译验证过。
> 编译交给 GitHub Actions 的 `macos-latest`（见 `.github/workflows/build.yml`），
> 第一次跑 CI 如果报编译错误，会据此修。

---

## 现在能做什么

- 动作列表：**鼠标点击**（左/右）、**鼠标滚轮**（方向 + 格数）和**等待**
- 每条动作独立等待（双击动作编辑；滚轮还能改方向和格数）、可单独启用/停用（点左侧圆点）
- 循环次数（0 = 无限）、启动延迟、点击按住时长
- 添加 / 删除 / 上移 / 下移
- 快捷键：`F6` 开始/停止、`F7` 用鼠标位置更新选中动作、`F8` 急停、`F9` 测试一次
- 配置存在 `~/Library/Application Support/AutoClickerMac/settings.json`
- 跟随系统深色/浅色外观

## 还没有

键盘按键动作、录制、历史序列、窗口相对坐标、多显示器下的虚拟屏坐标。
这些在 Windows 版里有，Mac 版是后续再做。

## 权限（必须）

macOS 不允许 App 随便控制鼠标。第一次运行要：

1. 打开 App，点「权限检查」；
2. 系统会弹窗，或手动去 **系统设置 → 隐私与安全性 → 辅助功能**；
3. 把「鼠标连点器」勾上（勾完可能要退出重开一次）。

没勾上时，程序会提示「没有辅助功能权限，点不动」，不会静默失败。

## 编译

需要 macOS + Xcode 命令行工具（`xcode-select --install`）：

```bash
cd macos
chmod +x build.sh
./build.sh
open build/AutoClicker.app
```

或者直接跑 CI：推送到 GitHub 后，Actions 的 `build` 任务会产出
`AutoClicker-macOS.zip`（artifact），下载解压即可。

## 坐标系说明

坐标用的是 **主屏左上角为 (0,0)** 的屏幕像素坐标，和 Windows 版一致，
`F7` 取的就是当前鼠标位置换算出来的值。

## 文件

```
macos/
├─ Sources/
│  ├─ main.swift        入口 + 菜单
│  ├─ MainView.swift    界面 + 动作列表 + 快捷键
│  ├─ Model.swift       动作模型 + JSON 存档
│  ├─ InputEngine.swift CGEvent 移动/点击
│  └─ Runner.swift      执行线程
├─ Info.plist
└─ build.sh
```
