#!/usr/bin/env bash
# HiAuRo 本地开发构建脚本（Windows CMD / WSL 通用）
# 自动检测环境，WSL 下通过 cmd.exe 转发到 Windows 构建

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CMD_EXE="/mnt/c/Windows/System32/cmd.exe"

is_wsl() {
    [[ -f /proc/sys/fs/binfmt_misc/WSLInterop ]] && return 0
    grep -qi microsoft /proc/version 2>/dev/null && return 0
    return 1
}

# Windows 路径 → WSL 路径 (C:\foo → /mnt/c/foo)
win2wsl() { echo "$1" | sed 's|\\|/|g; s|^\([A-Za-z]\):|/mnt/\L\1|'; }

# WSL 路径 → Windows 路径 (/mnt/c/foo → C:\foo)
wsl2win() { echo "$1" | sed 's|^/mnt/\([a-z]\)|\U\1:|; s|/|\\|g'; }

# 清 HiAuRo.Sdk NuGet 缓存
clear_cache() {
    if is_wsl; then
        local wp="$("$CMD_EXE" /c 'echo %USERPROFILE%' 2>/dev/null | tr -d '\r\n')"
        local cp="$(win2wsl "$wp")/.nuget/packages/hiauro.sdk"
    else
        local cp="${USERPROFILE:-$HOME}/.nuget/packages/hiauro.sdk"
    fi
    if [[ -d "$cp" ]]; then
        rm -rf "$cp"
        echo "[build] NuGet 缓存已清除"
    fi
}

build_hiauro() {
    local proj="HiAuRo.slnx"
    if is_wsl; then
        echo "[build] WSL 环境，通过 cmd.exe 转发..."
        clear_cache
        local win_dir="$(wsl2win "$SCRIPT_DIR")"
        "$CMD_EXE" /c "dotnet build ${win_dir}\\${proj} -c Debug -nologo" || {
            echo "[FAIL] HiAuRo 构建失败"
            exit 1
        }
    else
        clear_cache
        dotnet build "${SCRIPT_DIR}/${proj}" -c Debug -nologo || {
            echo "[FAIL] HiAuRo 构建失败"
            exit 1
        }
    fi
    echo "[build] HiAuRo 构建完成"
}

build_hiauro
