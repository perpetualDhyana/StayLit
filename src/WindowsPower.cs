using System;
using System.Runtime.InteropServices;

namespace StayLit
{
    internal sealed class WindowsPower : IPowerController
    {
        private const uint Continuous = 0x80000000;
        private const uint SystemRequired = 0x00000001;
        private const uint DisplayRequired = 0x00000002;
        private readonly Func<IntPtr> getWindow;

        public WindowsPower(Func<IntPtr> getWindow) { this.getWindow = getWindow; }

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, uint message,
            UIntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);

        public void Acquire()
        {
            if (SetThreadExecutionState(Continuous | SystemRequired | DisplayRequired) == 0)
                throw new InvalidOperationException("Windows 未能启用持续亮屏，请重试。");
        }

        public void Release()
        {
            if (SetThreadExecutionState(Continuous) == 0)
                throw new InvalidOperationException("Windows 未能解除持续亮屏，请点击停止重试。");
        }

        public void TurnOffDisplay()
        {
            // Our own top-level window routes this documented system command
            // through DefWindowProc. Avoid broadcasting to other apps, which
            // could block the timer behind an unresponsive window.
            UIntPtr result;
            IntPtr sent = SendMessageTimeout(getWindow(), 0x0112,
                new UIntPtr(0xF170), new IntPtr(2), 0x0002, 1500, out result);
            if (sent == IntPtr.Zero)
                throw new InvalidOperationException("息屏指令发送失败，请重试。当前已停止亮屏计时。");
        }
    }
}
