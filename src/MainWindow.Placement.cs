using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Point = System.Drawing.Point;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace WindowsProxySwitch
{
    public partial class MainWindow
    {
        private enum DockEdge { None, Left, Top, Right }

        private readonly DispatcherTimer dockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        private Forms.NotifyIcon trayIcon;
        private EdgeTab edgeTab;
        private IntPtr windowHandle;
        private DockEdge dockEdge;
        private Rectangle dockArea;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(windowHandle).AddHook(WindowMessage);
            dockTimer.Tick += HideAtEdge;

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示窗口", null, (sender, args) => Dispatcher.BeginInvoke(new Action(ShowFromTray)));
            menu.Items.Add("退出", null, (sender, args) => Dispatcher.BeginInvoke(new Action(Close)));
            using (var iconStream = typeof(MainWindow).Assembly.GetManifestResourceStream("ProxySwitch.ico"))
            {
                trayIcon = new Forms.NotifyIcon
                {
                    Icon = new System.Drawing.Icon(iconStream, Forms.SystemInformation.SmallIconSize),
                    Text = "系统代理",
                    ContextMenuStrip = menu,
                    Visible = true
                };
            }
            trayIcon.MouseClick += (sender, args) =>
            {
                if (args.Button == Forms.MouseButtons.Left)
                    ShowFromTray();
            };
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized && trayIcon != null)
            {
                Undock();
                Hide();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            dockTimer.Stop();
            edgeTab?.Dispose();
            if (trayIcon != null)
            {
                var icon = trayIcon.Icon;
                trayIcon.Visible = false;
                trayIcon.ContextMenuStrip.Dispose();
                trayIcon.Dispose();
                icon.Dispose();
            }
            base.OnClosed(e);
        }

        private void ShowFromTray()
        {
            Undock();
            ShowActivated = true;
            WindowState = WindowState.Normal;
            Show();
            // Recover the window if its old display was disconnected while it was hidden.
            if (TryGetFrame(out Rectangle frame) && !Forms.Screen.FromRectangle(frame).WorkingArea.IntersectsWith(frame))
            {
                Rectangle area = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
                MoveWindow(area.Left, area.Top);
            }
            Activate();
        }

        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int EnterSizeMove = 0x0231;
            const int ExitSizeMove = 0x0232;
            if (message == EnterSizeMove)
                dockTimer.Stop();
            if (message == ExitSizeMove)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DockIfNearEdge));
            return IntPtr.Zero;
        }

        private void DockIfNearEdge()
        {
            if (!IsVisible || WindowState != WindowState.Normal || !TryGetFrame(out Rectangle frame))
                return;

            Point cursor = Forms.Cursor.Position;
            Forms.Screen screen = Forms.Screen.FromPoint(cursor);
            dockArea = screen.WorkingArea;
            int distance = (int)Math.Ceiling(16 * VisualTreeHelper.GetDpi(this).DpiScaleX);
            dockEdge = FindEdge(frame, dockArea, screen.Bounds, cursor, distance);
            if (dockEdge == DockEdge.None || !MoveToEdge())
            {
                Undock();
                return;
            }

            dockTimer.Start();
        }

        private static DockEdge FindEdge(Rectangle frame, Rectangle area, Rectangle screen, Point cursor, int distance)
        {
            int left = Math.Min(Math.Abs(frame.Left - area.Left), Math.Abs(cursor.X - screen.Left));
            int top = Math.Min(Math.Abs(frame.Top - area.Top), Math.Abs(cursor.Y - screen.Top));
            int right = Math.Min(Math.Abs(frame.Right - area.Right), Math.Abs(cursor.X - (screen.Right - 1)));
            int closest = Math.Min(top, Math.Min(left, right));
            if (closest > distance)
                return DockEdge.None;
            if (closest == top)
                return DockEdge.Top;
            return closest == left ? DockEdge.Left : DockEdge.Right;
        }

        private bool MoveToEdge()
        {
            if (!TryGetFrame(out Rectangle frame))
                return false;

            dockArea = Forms.Screen.FromRectangle(dockArea).WorkingArea;
            int x = dockEdge == DockEdge.Left ? dockArea.Left
                : dockEdge == DockEdge.Right ? dockArea.Right - frame.Width
                : Math.Min(frame.Left, dockArea.Right - frame.Width);
            int y = dockEdge == DockEdge.Top ? dockArea.Top : Math.Min(frame.Top, dockArea.Bottom - frame.Height);
            return MoveWindow(Math.Max(dockArea.Left, x), Math.Max(dockArea.Top, y));
        }

        private void HideAtEdge(object sender, EventArgs e)
        {
            if (!TryGetFrame(out Rectangle frame) || frame.Contains(Forms.Cursor.Position))
                return;

            dockTimer.Stop();
            var dpi = VisualTreeHelper.GetDpi(this);
            int thickness = (int)Math.Ceiling(14 * dpi.DpiScaleX);
            int length = (int)Math.Ceiling(56 * dpi.DpiScaleX);
            if (edgeTab == null)
            {
                edgeTab = new EdgeTab();
                edgeTab.MouseEnter += RevealFromEdge;
            }

            edgeTab.Edge = dockEdge;
            edgeTab.Bounds = dockEdge == DockEdge.Top
                ? new Rectangle(frame.Left + (frame.Width - length) / 2, frame.Top, length, thickness)
                : new Rectangle(dockEdge == DockEdge.Left ? frame.Left : frame.Right - thickness,
                    frame.Top + (frame.Height - length) / 2, thickness, length);
            Hide();
            edgeTab.Show();
            edgeTab.Invalidate();
        }

        private void RevealFromEdge(object sender, EventArgs e)
        {
            edgeTab.Hide();
            ShowActivated = false;
            Show();
            if (MoveToEdge())
                dockTimer.Start();
            else
                Undock();
            RefreshWindow();
        }

        private void Undock()
        {
            dockTimer.Stop();
            edgeTab?.Hide();
            dockEdge = DockEdge.None;
        }

        private bool TryGetFrame(out Rectangle frame)
        {
            bool result = GetWindowRect(windowHandle, out NativeRect rect);
            frame = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return result;
        }

        private bool MoveWindow(int x, int y)
        {
            const uint KeepSizeOrderAndFocus = 0x0001 | 0x0004 | 0x0010;
            return SetWindowPos(windowHandle, IntPtr.Zero, x, y, 0, 0, KeepSizeOrderAndFocus);
        }

        private sealed class EdgeTab : Forms.Form
        {
            internal DockEdge Edge { get; set; }

            internal EdgeTab()
            {
                StartPosition = Forms.FormStartPosition.Manual;
                FormBorderStyle = Forms.FormBorderStyle.None;
                AutoScaleMode = Forms.AutoScaleMode.None;
                ShowInTaskbar = false;
                TopMost = true;
                BackColor = Color.FromArgb(25, 103, 112);
                Cursor = Forms.Cursors.Hand;
                AccessibleName = "展开系统代理";
            }

            protected override bool ShowWithoutActivation => true;

            protected override Forms.CreateParams CreateParams
            {
                get
                {
                    Forms.CreateParams parameters = base.CreateParams;
                    const int ToolWindowWithoutActivation = 0x00000080 | 0x08000000;
                    parameters.ExStyle |= ToolWindowWithoutActivation;
                    return parameters;
                }
            }

            protected override void OnPaint(Forms.PaintEventArgs e)
            {
                base.OnPaint(e);
                string arrow = Edge == DockEdge.Left ? "›" : Edge == DockEdge.Top ? "⌄" : "‹";
                Forms.TextRenderer.DrawText(e.Graphics, arrow, Font, ClientRectangle, Color.White,
                    Forms.TextFormatFlags.HorizontalCenter | Forms.TextFormatFlags.VerticalCenter);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    }
}
