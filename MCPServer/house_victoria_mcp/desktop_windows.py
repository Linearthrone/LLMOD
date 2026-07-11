"""Win32 helpers to list and focus desktop windows (Windows only)."""

from __future__ import annotations

import platform
import sys
from typing import Any


def _windows_only() -> bool:
    return platform.system() == "Windows"


def list_desktop_windows(include_minimized: bool = True) -> dict[str, Any]:
    """Return visible top-level windows with titles and screen bounds."""
    if not _windows_only():
        return {
            "success": False,
            "error": "list_desktop_windows is only available on Windows.",
            "windows": [],
        }

    import ctypes
    from ctypes import wintypes

    user32 = ctypes.windll.user32

    class RECT(ctypes.Structure):
        _fields_ = [
            ("left", ctypes.c_long),
            ("top", ctypes.c_long),
            ("right", ctypes.c_long),
            ("bottom", ctypes.c_long),
        ]

    windows: list[dict[str, Any]] = []
    foreground = user32.GetForegroundWindow()

    def _is_alt_tab_candidate(hwnd: int) -> bool:
        if not user32.IsWindowVisible(hwnd):
            return False
        if user32.GetWindow(hwnd, 4):  # GW_OWNER — owned popups
            return False
        style = user32.GetWindowLongW(hwnd, -16)  # GWL_STYLE
        if style & 0x08000000:  # WS_EX_NOACTIVATE
            return False
        ex_style = user32.GetWindowLongW(hwnd, -20)  # GWL_EXSTYLE
        if ex_style & 0x00000080:  # WS_EX_TOOLWINDOW
            return False
        return True

    EnumWindowsProc = ctypes.WINFUNCTYPE(
        wintypes.BOOL, wintypes.HWND, wintypes.LPARAM
    )

    def callback(hwnd: int, _lparam: int) -> bool:
        if not _is_alt_tab_candidate(hwnd):
            return True

        length = user32.GetWindowTextLengthW(hwnd)
        if length <= 0:
            return True

        buff = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buff, length + 1)
        title = buff.value.strip()
        if not title:
            return True

        rect = RECT()
        if not user32.GetWindowRect(hwnd, ctypes.byref(rect)):
            return True

        width = rect.right - rect.left
        height = rect.bottom - rect.top
        minimized = bool(user32.IsIconic(hwnd))

        if minimized and not include_minimized:
            return True

        windows.append(
            {
                "hwnd": int(hwnd),
                "title": title,
                "is_foreground": hwnd == foreground,
                "is_minimized": minimized,
                "left": rect.left,
                "top": rect.top,
                "width": width,
                "height": height,
            }
        )
        return True

    user32.EnumWindows(EnumWindowsProc(callback), 0)

    windows.sort(
        key=lambda w: (
            0 if w["is_foreground"] else 1,
            0 if not w["is_minimized"] else 1,
            w["title"].lower(),
        )
    )

    return {
        "success": True,
        "foreground_hwnd": int(foreground) if foreground else None,
        "count": len(windows),
        "windows": windows,
    }


def focus_desktop_window(title_contains: str, exact: bool = False) -> dict[str, Any]:
    """Bring a visible window to the foreground by partial title match."""
    if not _windows_only():
        return {"success": False, "error": "focus_desktop_window is only available on Windows."}

    needle = (title_contains or "").strip()
    if not needle:
        return {"success": False, "error": "title_contains is required."}

    listed = list_desktop_windows(include_minimized=True)
    if not listed.get("success"):
        return listed

    matches = []
    needle_lower = needle.lower()
    for win in listed["windows"]:
        title = win["title"]
        if exact and title.lower() == needle_lower:
            matches.append(win)
        elif not exact and needle_lower in title.lower():
            matches.append(win)

    if not matches:
        return {
            "success": False,
            "error": f"No window title matching '{needle}'.",
            "hint": "Call list_desktop_windows first, then focus_desktop_window with an exact substring from the title.",
            "windows_sample": [w["title"] for w in listed["windows"][:12]],
        }

    # Prefer non-minimized, then foreground candidate, then largest area
    def rank(w: dict[str, Any]) -> tuple:
        area = max(0, w["width"]) * max(0, w["height"])
        return (
            0 if not w["is_minimized"] else 1,
            0 if w["is_foreground"] else 1,
            -area,
        )

    target = sorted(matches, key=rank)[0]
    hwnd = target["hwnd"]

    import ctypes

    user32 = ctypes.windll.user32
    SW_RESTORE = 9

    if user32.IsIconic(hwnd):
        user32.ShowWindow(hwnd, SW_RESTORE)

    focused = _try_set_foreground(hwnd)

    rect_after = _window_rect(hwnd)
    return {
        "success": focused,
        "focused": focused,
        "hwnd": hwnd,
        "title": target["title"],
        "bounds": rect_after,
        "message": (
            f"Brought '{target['title']}' to the foreground."
            if focused
            else f"Matched '{target['title']}' but could not steal focus — try again or click its taskbar icon with computer_use."
        ),
    }


def _window_rect(hwnd: int) -> dict[str, int] | None:
    import ctypes

    class RECT(ctypes.Structure):
        _fields_ = [
            ("left", ctypes.c_long),
            ("top", ctypes.c_long),
            ("right", ctypes.c_long),
            ("bottom", ctypes.c_long),
        ]

    rect = RECT()
    if not ctypes.windll.user32.GetWindowRect(hwnd, ctypes.byref(rect)):
        return None
    return {
        "left": rect.left,
        "top": rect.top,
        "width": rect.right - rect.left,
        "height": rect.bottom - rect.top,
    }


def _try_set_foreground(hwnd: int) -> bool:
    import ctypes
    from ctypes import wintypes

    user32 = ctypes.windll.user32
    kernel32 = ctypes.windll.kernel32

    current = user32.GetForegroundWindow()
    if current == hwnd:
        return True

    # Restore if minimized before focus dance
    if user32.IsIconic(hwnd):
        user32.ShowWindow(hwnd, 9)

    fg_thread = user32.GetWindowThreadProcessId(current, None)
    target_thread = user32.GetWindowThreadProcessId(hwnd, None)

    attached = False
    if fg_thread and target_thread and fg_thread != target_thread:
        attached = bool(user32.AttachThreadInput(fg_thread, target_thread, True))

    try:
        user32.BringWindowToTop(hwnd)
        user32.SetForegroundWindow(hwnd)
    finally:
        if attached:
            user32.AttachThreadInput(fg_thread, target_thread, False)

    return user32.GetForegroundWindow() == hwnd
