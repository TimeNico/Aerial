using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.ComponentModel;

namespace Aerial
{
    public struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    public enum ABMsg : int
    {
        ABM_NEW = 0,
        ABM_REMOVE = 1,
        ABM_QUERYPOS = 2,
        ABM_SETPOS = 3,
        ABM_GETSTATE = 4,
        ABM_GETTASKBARPOS = 5,
        ABM_ACTIVATE = 6,
        ABM_GETAUTOHIDEBAR = 7,
        ABM_SETAUTOHIDEBAR = 8,
        ABM_WINDOWPOSCHANGED = 9,
        ABM_SETSTATE = 10
    }

    public enum ABEdge : int
    {
        ABE_LEFT = 0,
        ABE_TOP = 1,
        ABE_RIGHT = 2,
        ABE_BOTTOM = 3,
    }

    public class ProgramForm : Form
    {
        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, [In] ref APPBARDATA pData);

        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            Settings.ExtractArgs(args);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ProgramForm());
        }

        public ProgramForm()
        {
            FormClosing += OnClosing;
            Load += OnLoad;
            ShowInTaskbar = false;
            MaximizeBox = false;
            Name = "ProgramForm";
            Text = "Aerial";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Opacity = 0;
            Size = new Size(0, 0);
            AutoScaleDimensions = new SizeF(6f, 13f);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(0, 0);
            Icon = Properties.Resources.Aerial_Logo_100;
        }

        public void OnClosing(object? sender, EventArgs e) => Reset();
        public void OnLoad(object? sender, EventArgs e)
        {
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    if (process.ProcessName == "Aerial" && process.Id != Environment.ProcessId)
                    {
                        process.Kill();
                    }
                }
            }
            catch { }
            SuspendLayout();
            Hide();
            ResumeLayout(false);
            PerformLayout();
            InitializeTray();
            CreatePadding();
        }

        public void InitializeTray()
        {
            ComponentResourceManager resources = new ComponentResourceManager(typeof(ProgramForm));
            var strip = new ContextMenuStrip();

            var tray = new NotifyIcon()
            {
                Icon = Properties.Resources.Aerial_Logo_100,
                Text = "Aerial",
                ContextMenuStrip = strip,
                Visible = true
            };

            strip.Items.Add("quit", null, (s, e) =>
            {
                tray.Visible = false;
                Application.Exit();
            });
        }

        public static void Reset()
        {
            foreach (Form ff in Application.OpenForms)
            {
                var abd = new APPBARDATA();
                abd.cbSize = Marshal.SizeOf(abd);
                abd.hWnd = ff.Handle;
                SHAppBarMessage((int)ABMsg.ABM_REMOVE, ref abd);
            }
        }

        public void CreatePadding()
        {
            try
            {
                foreach (var form in this.OwnedForms)
                {
                    form.Close();
                }
            }
            catch { }

            Thread.Sleep(300);
            Application.DoEvents();

            foreach (var (screen, padding) in Settings.Screens)
            {
                if (padding.TopAmount > 0)
                    CreateForm(screen, ABEdge.ABE_TOP, padding.TopAmount);
                if (padding.BottomAmount > 0)
                    CreateForm(screen, ABEdge.ABE_BOTTOM, padding.BottomAmount);
                if (padding.LeftAmount > 0)
                    CreateForm(screen, ABEdge.ABE_LEFT, padding.LeftAmount);
                if (padding.RightAmount > 0)
                    CreateForm(screen, ABEdge.ABE_RIGHT, padding.RightAmount);
            }
        }

        public void CreateForm(Screen screen, ABEdge edge, int amount)
        {
            var abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf(abd);

            var ff = new Form();

            ff.AutoScaleBaseSize = new Size(1, 1);
            ff.ClientSize = new Size(100, 100);
            ff.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ff.Opacity = 0;
            ff.Name = "AerialSpacer";
            ff.ShowInTaskbar = false;
            ff.Show();

            abd.hWnd = ff.Handle;

            abd.uEdge = (int)edge;
            abd.rc.left = screen.WorkingArea.Left;
            abd.rc.right = screen.WorkingArea.Right;
            abd.rc.top = screen.WorkingArea.Top;
            abd.rc.bottom = screen.WorkingArea.Bottom;

            SHAppBarMessage((int)ABMsg.ABM_NEW, ref abd);
            Application.DoEvents();
            SHAppBarMessage((int)ABMsg.ABM_QUERYPOS, ref abd);
            Application.DoEvents();

            switch (edge)
            {
                case ABEdge.ABE_TOP:
                    abd.rc.bottom = abd.rc.top + amount;
                    break;
                case ABEdge.ABE_BOTTOM:
                    abd.rc.top = abd.rc.bottom - amount;
                    break;
                case ABEdge.ABE_LEFT:
                    abd.rc.right = abd.rc.left + amount;
                    break;
                case ABEdge.ABE_RIGHT:
                    abd.rc.left = abd.rc.right - amount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(edge));
            }

            SHAppBarMessage((int)ABMsg.ABM_SETPOS, ref abd);
            Application.DoEvents();
            Thread.Sleep(150);
            Application.DoEvents();
        }
    }

    public static class Settings
    {
        public static List<(Screen, ScreenPadding)> Screens = new List<(Screen, ScreenPadding)>();

        public class ScreenPadding
        {
            public int TopAmount;
            public int BottomAmount;
            public int LeftAmount;
            public int RightAmount;

            public ScreenPadding(int top, int bottom, int left, int right)
            {
                TopAmount = top;
                BottomAmount = bottom;
                LeftAmount = left;
                RightAmount = right;
            }
        }

        public static void ExtractArgs(string[] args)
        {
            var index = 0;
            foreach (var screen in Screen.AllScreens)
            {
                var top = 0;
                var bottom = 0;
                var left = 0;
                var right = 0;
                var regex = screen.Primary
                    ? new Regex(@"-(p|a|1)([tlbra])=(\d+)")
                    : new Regex($@"-(s|a|{index})([tlbra])=(\d+)");

                foreach (var argument in args)
                {
                    var result = regex.Match(argument);
                    if (!result.Success)
                        continue;

                    switch (result.Groups[2].Value)
                    {
                        case "t":
                            top = int.Parse(result.Groups[3].Value);
                            break;
                        case "l":
                            left = int.Parse(result.Groups[3].Value);
                            break;
                        case "b":
                            bottom = int.Parse(result.Groups[3].Value);
                            break;
                        case "r":
                            right = int.Parse(result.Groups[3].Value);
                            break;
                        case "a":
                            top = int.Parse(result.Groups[3].Value);
                            left = int.Parse(result.Groups[3].Value);
                            bottom = int.Parse(result.Groups[3].Value);
                            right = int.Parse(result.Groups[3].Value);
                            break;
                    }
                }

                var tuple = (screen, new ScreenPadding(top, bottom, left, right)
                {
                    TopAmount = top,
                    BottomAmount = bottom,
                    LeftAmount = left,
                    RightAmount = right
                });

                // ensure that primary is at the front of the list
                if (screen.Primary)
                    Screens.Insert(0, tuple);
                else
                    Screens.Add(tuple);

                index++;
            }
        }
    }
}
