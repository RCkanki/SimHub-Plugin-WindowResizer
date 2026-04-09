using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowResizer
{
    public static class WindowManager
    {
        private const int GWL_STYLE = -16;
        private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_DLGFRAME = 0x00400000;
        private const int WS_BORDER = 0x00800000;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_DLGMODALFRAME = 0x00000001;
        private const int WS_EX_WINDOWEDGE = 0x00000100;
        private const int WS_EX_CLIENTEDGE = 0x00000200;
        private const int WS_EX_STATICEDGE = 0x00020000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;

        /// <summary>Minimum client extent (px) to treat a window as eligible for the picker list.</summary>
        private const int MinPickerWindowExtentPx = 16;

        private const int DWMWA_CLOAKED = 14;

        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_NOSENDCHANGING = 0x0400;
        private const int SWP_NOOWNERZORDER = 0x0200;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_FRAMECHANGED = 0x0020;
        private const int SWP_SHOWWINDOW = 0x0040;

        /// <summary>Brief topmost then back to normal stack (no focus); helps Z-order when another process is foreground.</summary>
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);

        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int maxLength);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder text, int maxLength);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        /// <summary>user32 IsWindow wrapper; guards invalid HWNDs.</summary>
        public static bool IsWindowAlive(IntPtr hWnd) => hWnd != IntPtr.Zero && IsWindow(hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWNOACTIVATE = 4;

        private const byte VkMenu = 0x12;
        private const uint KeyeventfKeyup = 0x0002;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // GetWindowLongPtr / SetWindowLongPtr wrappers for 32- and 64-bit.
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MonitorDefaultToNull = 0x00000000;
        private const uint MonitorDefaultToPrimary = 0x00000001;
        private const uint MonitorDefaultToNearest = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        // classContains parameter removed (class-name filter unused).
        public static IntPtr FindWindowForProcess(
            string processName,
            string titleContains = null)
        {
            // Legacy API: returns the first matching process/window without position/size scoring.
            // Kept for back-compat; not as selective as profile-based matching.
            if (string.IsNullOrWhiteSpace(processName))
            {
                return IntPtr.Zero;
            }

            IntPtr target = IntPtr.Zero;
            var processes = Process.GetProcessesByName(processName);
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[WindowResizer] FindWindowForProcess name={processName}, count={processes.Length}");
#endif
            if (processes.Length == 0) return IntPtr.Zero;

            HashSet<int> pidSet;
            try
            {
                pidSet = new HashSet<int>();
                foreach (var p in processes)
                {
                    pidSet.Add(p.Id);
                }
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }

            var titleBuilder = new StringBuilder(256);
            var classBuilder = new StringBuilder(256);

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (!pidSet.Contains(pid))
                    return true;

                titleBuilder.Clear();
                classBuilder.Clear();
                GetWindowText(hWnd, titleBuilder, 256);
                GetClassName(hWnd, classBuilder, 256);

                var title = titleBuilder.ToString();
                var cls = classBuilder.ToString();

                if (!string.IsNullOrEmpty(titleContains) && (title?.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                    return true;
                // if (!string.IsNullOrEmpty(classContains) && (cls?.IndexOf(classContains, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                //     return true;

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[WindowResizer] matched hwnd={hWnd}, pid={pid}, title='{title}', class='{cls}'");
#endif
                target = hWnd;
                return false;
            }, IntPtr.Zero);

            return target;
        }

        /// <summary>
        /// Picks the best-matching window among multiple processes/windows using profile position, size, and title.
        /// Aims for behavior similar to the Rust get_pid_from_profile + apply_profile path.
        /// </summary>
        public static IntPtr FindWindowForProfile(Profile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProcessName))
            {
                return IntPtr.Zero;
            }

            var processes = Process.GetProcessesByName(profile.ProcessName);

            HashSet<int> pidSet;
            try
            {
                if (processes.Length == 0) return IntPtr.Zero;

                pidSet = new HashSet<int>();
                foreach (var p in processes)
                {
                    pidSet.Add(p.Id);
                }
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }

            IntPtr bestHwnd = IntPtr.Zero;
            long? bestScore = null;

            var titleBuilder = new StringBuilder(256);

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (!pidSet.Contains(pid))
                    return true;

                titleBuilder.Clear();
                GetWindowText(hWnd, titleBuilder, 256);

                var title = titleBuilder.ToString();

                if (!string.IsNullOrEmpty(profile.WindowTitleContains) &&
                    (title?.IndexOf(profile.WindowTitleContains, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                {
                    return true;
                }

                // WindowClassContains unused (legacy filter).
                // var cls = classBuilder.ToString();
                // if (!string.IsNullOrEmpty(profile.WindowClassContains) &&
                //     (cls?.IndexOf(profile.WindowClassContains, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
                //     return true;

                if (!GetWindowRect(hWnd, out var r))
                {
                    return true;
                }

                var wx = r.Left;
                var wy = r.Top;
                var ww = r.Right - r.Left;
                var wh = r.Bottom - r.Top;

                // Score by deviation from profile position and size.
                long dx = (long)Math.Abs(wx - profile.X);
                long dy = (long)Math.Abs(wy - profile.Y);
                long dw = (long)Math.Abs(ww - profile.Width);
                long dh = (long)Math.Abs(wh - profile.Height);
                long score = dx + dy + dw + dh;

                if (bestScore == null || score < bestScore.Value)
                {
                    bestScore = score;
                    bestHwnd = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            return bestHwnd;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        /// <summary>True if the HWND's owning PID matches a process with the given name (no window enumeration).</summary>
        public static bool IsWindowOwnedByNamedProcess(IntPtr hWnd, string processName)
        {
            if (hWnd == IntPtr.Zero || string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            if (!IsWindow(hWnd))
            {
                return false;
            }

            GetWindowThreadProcessId(hWnd, out var pid);
            var processes = Process.GetProcessesByName(processName);
            try
            {
                foreach (var p in processes)
                {
                    if (p.Id == pid)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }
        }

        public class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string ProcessName { get; set; }
            public string Title { get; set; }
            public string ClassName { get; set; }

            public override string ToString()
            {
                // Disambiguate multiple windows in the same process using title.
                var title = string.IsNullOrEmpty(Title) ? "(no title)" : Title;
                return $"{ProcessName}.exe — {title}";
            }
        }

        /// <summary>
        /// Filters out HWNDs that are not meaningful layout targets (overlays, tool frames, minimized, etc.).
        /// </summary>
        private static bool IsSubstantiveTopLevelWindow(IntPtr hWnd)
        {
            if (IsIconic(hWnd))
            {
                return false;
            }

            if (!GetWindowRect(hWnd, out var r))
            {
                return false;
            }

            var w = r.Right - r.Left;
            var h = r.Bottom - r.Top;
            if (w < MinPickerWindowExtentPx || h < MinPickerWindowExtentPx)
            {
                return false;
            }

            var ex = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
            // Tool-style frame hidden from the taskbar unless WS_EX_APPWINDOW overrides.
            if ((ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0)
            {
                return false;
            }

            // Hidden by DWM (e.g. other virtual desktop).
            try
            {
                if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 &&
                    cloaked != 0)
                {
                    return false;
                }
            }
            catch
            {
                // Ignore if DWM is unavailable.
            }

            return true;
        }

        /// <summary>
        /// Enumerates current top-level windows for the profile editor UI.
        /// Lists each window separately even when multiple belong to the same process.
        /// </summary>
        public static WindowInfo[] EnumerateWindows()
        {
            var list = new List<WindowInfo>();
            var titleBuilder = new StringBuilder(256);
            var classBuilder = new StringBuilder(256);

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                if (!IsSubstantiveTopLevelWindow(hWnd))
                    return true;

                titleBuilder.Clear();
                classBuilder.Clear();
                GetWindowText(hWnd, titleBuilder, 256);
                GetClassName(hWnd, classBuilder, 256);

                int pid;
                GetWindowThreadProcessId(hWnd, out pid);

                try
                {
                    var proc = Process.GetProcessById(pid);
                    list.Add(new WindowInfo
                    {
                        Handle = hWnd,
                        ProcessName = proc.ProcessName,
                        Title = titleBuilder.ToString(),
                        ClassName = classBuilder.ToString()
                    });
                }
                catch
                {
                    // Skip if the process cannot be opened.
                }

                return true;
            }, IntPtr.Zero);

            list.Sort((a, b) =>
            {
                var c = string.Compare(a.ProcessName, b.ProcessName, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                c = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return a.Handle.ToInt64().CompareTo(b.Handle.ToInt64());
            });

            return list.ToArray();
        }

        /// <summary>
        /// Test helper: moves the active foreground window to the given position and size.
        /// Used from a SimHub verification action.
        /// </summary>
        public static void MoveActiveWindowTo(int x, int y, int width, int height)
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return;
            ResizeAndMoveWindow(hWnd, x, y, width, height);
        }

        public static bool TryGetWindowRect(IntPtr hWnd, out int x, out int y, out int width, out int height)
        {
            x = y = width = height = 0;
            if (hWnd == IntPtr.Zero) return false;

            if (!GetWindowRect(hWnd, out var r)) return false;

            x = r.Left;
            y = r.Top;
            width = r.Right - r.Left;
            height = r.Bottom - r.Top;
            return true;
        }

        public static bool RectMatchesProfile(IntPtr hWnd, Profile profile)
        {
            if (!TryGetWindowRect(hWnd, out var x, out var y, out var w, out var h))
            {
                return false;
            }

            return x == profile.X &&
                   y == profile.Y &&
                   w == profile.Width &&
                   h == profile.Height;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static bool TryGetMonitorBoundsFromHandle(IntPtr hMonitor, out RECT bounds)
        {
            bounds = default;
            if (hMonitor == IntPtr.Zero)
            {
                return false;
            }

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(hMonitor, ref mi))
            {
                return false;
            }

            bounds = mi.rcMonitor;
            return true;
        }

        private static RECT ResolveTargetBounds(IntPtr hWnd, int x, int y, int width, int height)
        {
            if (hWnd != IntPtr.Zero && IsWindow(hWnd))
            {
                var fromWindow = MonitorFromWindow(hWnd, MonitorDefaultToNull);
                if (TryGetMonitorBoundsFromHandle(fromWindow, out var windowBounds))
                {
                    return windowBounds;
                }
            }

            var safeW = Math.Max(1, width);
            var safeH = Math.Max(1, height);
            var center = new POINT
            {
                X = x + (safeW / 2),
                Y = y + (safeH / 2)
            };

            var fromPoint = MonitorFromPoint(center, MonitorDefaultToNearest);
            if (TryGetMonitorBoundsFromHandle(fromPoint, out var pointBounds))
            {
                return pointBounds;
            }

            var primary = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MonitorDefaultToPrimary);
            if (TryGetMonitorBoundsFromHandle(primary, out var primaryBounds))
            {
                return primaryBounds;
            }

            return new RECT
            {
                Left = 0,
                Top = 0,
                Right = Math.Max(1, safeW),
                Bottom = Math.Max(1, safeH)
            };
        }

        private static void ClampToScreenBounds(
            IntPtr hWnd,
            ref int x,
            ref int y,
            ref int width,
            ref int height)
        {
            var bounds = ResolveTargetBounds(hWnd, x, y, width, height);
            var boundsWidth = Math.Max(1, bounds.Right - bounds.Left);
            var boundsHeight = Math.Max(1, bounds.Bottom - bounds.Top);
            width = ClampInt(Math.Max(1, width), 1, boundsWidth);
            height = ClampInt(Math.Max(1, height), 1, boundsHeight);

            var minX = bounds.Left;
            var maxX = bounds.Right - width;
            var minY = bounds.Top;
            var maxY = bounds.Bottom - height;

            x = ClampInt(x, minX, maxX);
            y = ClampInt(y, minY, maxY);
        }

        /// <summary>
        /// Changes position and size only; does not alter Z-order or focus (avoids implicit topmost when <c>hWndInsertAfter</c> is unset).
        /// Use <see cref="BringToFront"/> to raise the window.
        /// </summary>
        public static void ResizeAndMoveWindow(
            IntPtr hWnd,
            int x,
            int y,
            int width,
            int height)
        {
            if (hWnd == IntPtr.Zero) return;
            ClampToScreenBounds(hWnd, ref x, ref y, ref width, ref height);
            const uint flags = SWP_SHOWWINDOW | SWP_FRAMECHANGED | SWP_NOZORDER | SWP_NOACTIVATE;
            SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, flags);
        }

        public static void SetBorderless(IntPtr hWnd, bool borderless)
        {
            if (hWnd == IntPtr.Zero) return;

            var style = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
            var exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();

            long frameFlags = (long)(uint)(WS_THICKFRAME | WS_DLGFRAME | WS_BORDER);
            long exFrameFlags = (long)(uint)(WS_EX_DLGMODALFRAME | WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE);

            if (borderless)
            {
                // Same approach as the Rust Resize Raccoon: toggle frame bits off to remove borders.
                style |= frameFlags;
                style ^= frameFlags;

                exStyle |= exFrameFlags;
                exStyle ^= exFrameFlags;
            }
            else
            {
                // Restore standard frame bits where applicable.
                style |= frameFlags;
                exStyle |= exFrameFlags;
            }

            SetWindowLongPtr(hWnd, GWL_STYLE, new IntPtr(style));
            SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(exStyle));

            uint uFlags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOSENDCHANGING | SWP_FRAMECHANGED;
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, uFlags);
        }

        /// <summary>
        /// Nudges foreground permission: simulates brief input so <see cref="SetForegroundWindow"/> and Z-order changes
        /// from SimHub are more likely to succeed (e.g. controller-only setups where SimHub never gets mouse input).
        /// </summary>
        private static void NudgeForegroundPermissionsForCallingProcess()
        {
            keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
            keybd_event(VkMenu, 0, KeyeventfKeyup, UIntPtr.Zero);
        }

        /// <summary>
        /// Raises the window toward the top of the normal Z-order stack without taking focus.
        /// Helps visually when <see cref="SetForegroundWindow"/> fails while another app is foreground.
        /// </summary>
        private static void PlaceWindowAbovePeersNoActivate(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            {
                return;
            }

            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }
            else
            {
                ShowWindow(hWnd, SW_SHOWNOACTIVATE);
            }

            const uint zFlags = SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE;
            // BringWindowToTop alone may fail when another process owns the foreground (e.g. NEXT via controller).
            SetWindowPos(hWnd, HwndTopMost, 0, 0, 0, 0, zFlags);
            SetWindowPos(hWnd, HwndNoTopMost, 0, 0, 0, 0, zFlags);
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, zFlags);
            BringWindowToTop(hWnd);
        }

        /// <summary>
        /// Temporarily attaches input to the foreground thread so <see cref="SetForegroundWindow"/> is less likely to fail when SimHub is not foreground.
        /// </summary>
        private static void TrySetForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            {
                return;
            }

            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }
            else if (!IsWindowVisible(hWnd))
            {
                ShowWindow(hWnd, SW_SHOW);
            }

            if (GetForegroundWindow() == hWnd)
            {
                return;
            }

            GetWindowThreadProcessId(hWnd, out var targetTid);
            var targetThread = unchecked((uint)targetTid);

            var fg = GetForegroundWindow();
            uint fgThread = 0;
            if (fg != IntPtr.Zero)
            {
                GetWindowThreadProcessId(fg, out var fgTid);
                fgThread = unchecked((uint)fgTid);
            }

            var selfThread = GetCurrentThreadId();
            var attachedFg = false;
            var attachedTarget = false;
            try
            {
                if (fg != IntPtr.Zero && fgThread != 0 && fgThread != selfThread)
                {
                    attachedFg = AttachThreadInput(selfThread, fgThread, true);
                }

                if (targetThread != 0 && targetThread != selfThread)
                {
                    attachedTarget = AttachThreadInput(selfThread, targetThread, true);
                }

                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
            }
            finally
            {
                if (attachedTarget)
                {
                    AttachThreadInput(selfThread, targetThread, false);
                }

                if (attachedFg)
                {
                    AttachThreadInput(selfThread, fgThread, false);
                }
            }
        }

        public static void BringToFront(IntPtr hWnd, bool takeFocus)
        {
            if (hWnd == IntPtr.Zero) return;

            NudgeForegroundPermissionsForCallingProcess();

            // Raise Z-order first, then try focus (often helps when a game is foreground).
            PlaceWindowAbovePeersNoActivate(hWnd);

            if (takeFocus)
            {
                TrySetForegroundWindow(hWnd);
                // If focus is blocked, Z-order was still raised earlier.
                if (GetForegroundWindow() != hWnd)
                {
                    PlaceWindowAbovePeersNoActivate(hWnd);
                }
            }
        }
    }
}

