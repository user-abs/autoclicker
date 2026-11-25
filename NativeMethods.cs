using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AutoContinue;

internal static class NativeMethods
{
    private const uint InputMouse = 0;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventVirtualDesk = 0x4000;
    private const uint MouseEventAbsolute = 0x8000;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

    internal static void ClickAt(Point point)
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);
        if (width <= 1 || height <= 1)
        {
            throw new InvalidOperationException("Windows did not report a valid virtual screen.");
        }

        if (point.X < left || point.X >= left + width || point.Y < top || point.Y >= top + height)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "The coordinates are outside the virtual screen.");
        }

        var x = (int)Math.Round((point.X - left) * 65535d / (width - 1));
        var y = (int)Math.Round((point.Y - top) * 65535d / (height - 1));
        var moveFlags = MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk;
        var inputs = new[]
        {
            CreateMouseInput(x, y, moveFlags),
            CreateMouseInput(0, 0, MouseEventLeftDown),
            CreateMouseInput(0, 0, MouseEventLeftUp)
        };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != (uint)inputs.Length)
        {
            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode != 0)
            {
                throw new Win32Exception(errorCode, "Windows could not send the mouse click.");
            }

            throw new InvalidOperationException("Windows did not complete the mouse click.");
        }
    }

    private static Input CreateMouseInput(int x, int y, uint flags) =>
        new()
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    Dx = x,
                    Dy = y,
                    Flags = flags
                }
            }
        };
}
