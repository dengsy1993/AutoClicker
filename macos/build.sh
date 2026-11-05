#!/bin/bash
#
# 在 macOS 上编译出 AutoClicker.app
# 用法：cd macos && ./build.sh
#
# 需要 Xcode 命令行工具（xcode-select --install）
#

set -e
cd "$(dirname "$0")"

APP="build/AutoClicker.app"
rm -rf build
mkdir -p "$APP/Contents/MacOS"
mkdir -p "$APP/Contents/Resources"

echo "==> 编译 Swift 源码"
swiftc -O \
    -o "$APP/Contents/MacOS/AutoClicker" \
    -framework Cocoa \
    -framework CoreGraphics \
    -framework ApplicationServices \
    Sources/*.swift

echo "==> 写入 Info.plist"
cp Info.plist "$APP/Contents/Info.plist"

echo "==> 本地临时签名（ad-hoc）"
codesign --force --deep --sign - "$APP" 2>/dev/null || echo "    (跳过签名，不影响本地运行)"

echo ""
echo "编译完成：$(pwd)/$APP"
echo "双击打开，或者：open \"$APP\""
echo ""
echo "第一次运行要去 系统设置 → 隐私与安全性 → 辅助功能 里勾上它。"
