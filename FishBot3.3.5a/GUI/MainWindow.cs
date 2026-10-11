using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FishBot3._3._5a
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public partial class MainWindow : Form
    {
        private const int WmHotKey = 0x0312;
        private const uint ModControl = 0x0002;
        private const uint ModNoRepeat = 0x4000;
        private const int AfkPreventionIntervalMinutes = 2;
        private const int StartFishingHotKeyId = 1;
        private const int StopFishingHotKeyId = 2;
        private const int CaptureLocationHotKeyId = 3;
        private const int ClickLocationHotKeyId = 4;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;

        private static Lua lua;

        private readonly HashSet<ulong> clickedBobberGuids = new HashSet<ulong>();
        private int Caught;
        private bool Fish;
        private Hook wowHook;
        private Process wowProcess;
        private Point? lootBindClickLocation;
        private bool captureLootBindClickLocation;

        public MainWindow()
        {
            InitializeComponent();
        }

        private static string Exe_Version => File.GetLastWriteTime(System.Reflection.Assembly.GetEntryAssembly().Location).ToString("yyyy.MM.dd");

        private readonly int LocalVersion = int.Parse(Application.ProductVersion.Split('.')[0]);

        private static int GetInventorySlotsLeft
        {
            get
            {
                var totalSlotsLeft = 0;

                for (var i = 0; i <= 4; i++)
                {
                    lua.DoString($"numberOfFreeSlots = GetContainerNumFreeSlots({i})");
                    var numberOfFreeSlots = Convert.ToInt32(lua.GetLocalizedText("numberOfFreeSlots"));
                    totalSlotsLeft += numberOfFreeSlots;
                }

                return totalSlotsLeft;
            }
        }

        public static bool IsFishing
        {
            get
            {
                lua.DoString("spellData = UnitChannelInfo('player')");
                var spell = lua.GetLocalizedText("spellData");

                if (spell.Length == 0)
                    return false;
                return true;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            cmdFish.Enabled = false;
            toolStripStatusLabel1.Text = string.Format(toolStripStatusLabel1.Text, Exe_Version);
            toolStripStatusLabel3.Text = string.Format(toolStripStatusLabel3.Text, LocalVersion);

            Shown += MainWindow_Shown;
            FormClosing += MainWindow_FormClosing;

            Log.Initialize(LogTextBox, this);
            RegisterHotKeys();
        }

        private void MainWindow_Shown(object sender = null, EventArgs e = null)
        {
            Log.Write("Make sure to run program as administrator", Color.Crimson);
            Log.Write("Turn off auto-loot for power fishing, or turn on auto loot to gather fish", Color.Crimson);

            try
            {
                Log.Write("Attempting to connect to running WoW.exe process...", Color.Black);

                while ((wowProcess = Process.GetProcessesByName("WoW").FirstOrDefault()) == null)
                {
                    var result = MessageBox.Show(
                        "Open the 32-bit WoW 3.3.5a client, log in, and select a character before using the bot. Retry?",
                        "FishBot", MessageBoxButtons.YesNo, MessageBoxIcon.Error);

                    if (result != DialogResult.Yes)
                    {
                        Application.Exit();
                        return;
                    }
                }

                if (Utility.Is64BitProcess(wowProcess))
                    throw new InvalidOperationException("This bot supports only the 32-bit WoW 3.3.5a client.");

                wowHook = new Hook(wowProcess);
                wowHook.InstallHook();
                if (!wowHook.Installed)
                    throw new InvalidOperationException("Could not install the game hook. Check the log and confirm the client is running as the same Windows user.");
                lua = new Lua(wowHook);
                lua.InitializeWorldStateMonitor();

                Log.Write("Connected to process with ID = " + wowProcess.Id, Color.Black);

                textBox1.Text = wowHook.Memory.ReadString(Offsets.PlayerName, Encoding.UTF8);

                lua.DoString("zoneData = GetZoneText()");
                // Thread.Sleep(100);
                Log.Write("Zone: " + lua.GetLocalizedText("zoneData"), Color.Black);

                Log.Write("Click 'Fish' to begin fishing.", Color.Green);
                cmdFish.Enabled = true;
            }
            catch (Exception ex)
            {
                Log.Write(ex.Message, Color.Red);
            }
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            NativeMethods.UnregisterHotKey(Handle, StartFishingHotKeyId);
            NativeMethods.UnregisterHotKey(Handle, StopFishingHotKeyId);
            NativeMethods.UnregisterHotKey(Handle, CaptureLocationHotKeyId);
            NativeMethods.UnregisterHotKey(Handle, ClickLocationHotKeyId);
            wowHook?.DisposeHooking();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotKey)
            {
                switch (message.WParam.ToInt32())
                {
                    case StartFishingHotKeyId:
                        if (cmdFish.Enabled)
                            cmdFish.PerformClick();
                        break;
                    case StopFishingHotKeyId:
                        if (cmdStop.Enabled)
                            cmdStop.PerformClick();
                        break;
                    case CaptureLocationHotKeyId:
                        CaptureLootBindClickLocation();
                        break;
                    case ClickLocationHotKeyId:
                        ClickLootBindLocation();
                        break;
                }

                return;
            }

            base.WndProc(ref message);
        }

        private void RegisterHotKeys()
        {
            RegisterHotKey(StartFishingHotKeyId, Keys.F6);
            RegisterHotKey(StopFishingHotKeyId, Keys.F7);
            RegisterHotKey(CaptureLocationHotKeyId, Keys.F8);
            RegisterHotKey(ClickLocationHotKeyId, Keys.F9);
        }

        private void RegisterHotKey(int id, Keys key)
        {
            if (!NativeMethods.RegisterHotKey(Handle, id, ModControl | ModNoRepeat, (uint)key))
                Log.Write("Could not register Ctrl+" + key + ": " +
                          new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message, Color.Red);
        }

        private void pickLootBindLocationButton_Click(object sender, EventArgs e)
        {
            if (!autoAcceptLootBindCheckBox.Checked)
            {
                Log.Write("Enable bind-popup auto-accept before setting a fallback click location.", Color.DarkOrange);
                return;
            }

            captureLootBindClickLocation = true;
            hotkeyHelpLabel.Text = "Show popup, point at Accept, then press Ctrl+F8";
            Log.Write("Show the bind confirmation and point at its Accept button. Press Ctrl+F8 to save its screen position.", Color.Black);
        }

        private void CaptureLootBindClickLocation()
        {
            if (!captureLootBindClickLocation)
                return;

            if (wowProcess == null || NativeMethods.GetForegroundWindow() != wowProcess.MainWindowHandle)
            {
                Log.Write("Bring the WoW client and bind confirmation to the foreground before capturing the button location.", Color.DarkOrange);
                return;
            }

            if (!NativeMethods.GetCursorPos(out var position))
            {
                Log.Write("Could not capture the cursor position: " +
                          new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message, Color.Red);
                return;
            }

            lootBindClickLocation = new Point(position.X, position.Y);
            captureLootBindClickLocation = false;
            hotkeyHelpLabel.Text = string.Format("Fallback point saved: {0}, {1}", position.X, position.Y);
            Log.Write(hotkeyHelpLabel.Text, Color.Green);
        }

        private void ClickLootBindLocation()
        {
            if (!autoAcceptLootBindCheckBox.Checked || !Fish)
                return;

            if (lootBindClickLocation == null)
            {
                Log.Write("Set a fallback click location before using Ctrl+F9.", Color.DarkOrange);
                return;
            }

            if (wowProcess == null || NativeMethods.GetForegroundWindow() != wowProcess.MainWindowHandle)
            {
                Log.Write("Fallback click skipped: bring the WoW client to the foreground first.", Color.DarkOrange);
                return;
            }

            if (!NativeMethods.GetCursorPos(out var originalPosition))
            {
                Log.Write("Could not read the cursor position: " +
                          new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message, Color.Red);
                return;
            }

            var clickLocation = lootBindClickLocation.Value;
            if (!NativeMethods.SetCursorPos(clickLocation.X, clickLocation.Y))
            {
                Log.Write("Could not move the cursor to the saved popup location: " +
                          new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message, Color.Red);
                return;
            }

            try
            {
                NativeMethods.mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
                NativeMethods.mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
                Log.Write("Clicked the saved popup location. Use Ctrl+F9 only when the bind confirmation is visible.", Color.Black);
            }
            finally
            {
                if (!NativeMethods.SetCursorPos(originalPosition.X, originalPosition.Y))
                    Log.Write("Could not restore the mouse cursor position: " +
                              new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message, Color.Red);
            }
        }

        private void cmdFish_Click(object sender, EventArgs e)
        {
            if (wowHook == null || !wowHook.Installed)
            {
                Log.Write("Connect to a supported WoW client before starting.", Color.Red);
                return;
            }

            cmdStop.Enabled = true;
            cmdFish.Enabled = false;

            Fish = true;
            var nextAfkPreventionInput = DateTime.UtcNow.AddMinutes(AfkPreventionIntervalMinutes);
            var waitingForWorld = false;

            while (Fish)
            {
                try
                {
                    Application.DoEvents();

                    if (!lua.IsPlayerInWorld())
                    {
                        if (!waitingForWorld)
                        {
                            Log.Write("Waiting for WoW to finish loading before resuming fishing...", Color.DarkOrange);
                            clickedBobberGuids.Clear();
                            waitingForWorld = true;
                        }

                        Thread.Sleep(500);
                        continue;
                    }

                    if (waitingForWorld)
                    {
                        Log.Write("WoW has finished loading; resuming fishing.", Color.Green);
                        waitingForWorld = false;
                    }

                    if (!preventAfkDisconnectCheckBox.Checked)
                    {
                        nextAfkPreventionInput = DateTime.UtcNow.AddMinutes(AfkPreventionIntervalMinutes);
                    }
                    else if (DateTime.UtcNow >= nextAfkPreventionInput)
                    {
                        nextAfkPreventionInput = DateTime.UtcNow.AddMinutes(AfkPreventionIntervalMinutes);

                        if (wowProcess == null || NativeMethods.GetForegroundWindow() != wowProcess.MainWindowHandle)
                        {
                            Log.Write("AFK-prevention input skipped because WoW is not the foreground window.", Color.DarkOrange);
                        }
                        else if (!NativeMethods.SendAfkPreventionInput(out var errorCode))
                        {
                            var error = errorCode == 0
                                ? "Windows did not accept the complete key input."
                                : new Win32Exception(errorCode).Message;
                            Log.Write("Could not send AFK-prevention input: " + error, Color.Red);
                        }
                    }

                    if (autoAcceptLootBindCheckBox.Checked)
                        lua.AcceptPendingLootBinding();

                    if (GetInventorySlotsLeft == 0)
                    {
                        Log.Write("Clearing out inventory slots...", Color.Black);

                        var invFull = true;

                        foreach (var item in dropItems.Items)
                        {
                            var bagId = 0;
                            for (var slotId = 0; slotId <= 16; slotId++)
                            {
                                lua.DoString($"itemLink=GetContainerItemLink({bagId},{slotId})");
                                Thread.Sleep(1000);
                                if (lua.GetLocalizedText("itemLink").Contains(item.ToString()))
                                {
                                    invFull = false;
                                    lua.DoString($"PickupContainerItem({bagId},{slotId})");
                                    Thread.Sleep(1000);
                                    lua.DoString($"DeleteCursorItem()");
                                    Thread.Sleep(1000);
                                }
                            }
                        }

                        if (invFull)
                        {
                            cmdStop.Enabled = false;
                            cmdFish.Enabled = true;

                            SystemSounds.Asterisk.Play();
                            Fish = false;
                        }
                    }

                    if (!IsFishing)
                    {
                        Log.Write("Fishing...", Color.Black);
                        lua.CastSpellByName("Fishing");
                        Thread.Sleep(200); // Give the lure a chance to be placed in the water before we start scanning for it
                                           // 200 ms is a good length, most people play with under that latency
                    }

                    var clientConnection = wowHook.Memory.Read<IntPtr>(Offsets.g_clientConnection);
                    var currentManager = wowHook.Memory.Read<IntPtr>(
                        IntPtr.Add(clientConnection, (int)Offsets.s_curMgrOffset));
                    var curObj = wowHook.Memory.Read<IntPtr>(
                        IntPtr.Add(currentManager, (int)Offsets.FirstObjectOffset));

                    while (curObj.ToInt64() != 0 && (curObj.ToInt64() & 1) == 0)
                    {
                        var type = wowHook.Memory.Read<int>(curObj + 0x14);
                        var cGUID = wowHook.Memory.Read<ulong>(curObj + 0x30);

                        if ((type == 5) && !clickedBobberGuids.Contains(cGUID))
                        {
                            var objectName = wowHook.Memory.ReadString(
                                wowHook.Memory.Read<IntPtr>(
                                    wowHook.Memory.Read<IntPtr>(curObj + 0x1A4) + 0x90
                                    ),
                                Encoding.UTF8, 50
                                );

                            if (objectName == "Fishing Bobber")
                            {
                                var bobberState = wowHook.Memory.Read<byte>(curObj + 0xBC);

                                if (bobberState == 1) // Fish has been caught
                                {
                                    Caught++;
                                    textBox2.Text = Caught.ToString();

                                    Log.Write("Caught something, hopefully a fish!", Color.Black);

                                    var MouseOverGUID = new IntPtr(0x00BD07A0);
                                    wowHook.Memory.Write(MouseOverGUID, cGUID);
                                    Thread.Sleep(100);

                                    lua.DoString("InteractUnit(\'mouseover\')");

                                    clickedBobberGuids.Add(cGUID);

                                    Thread.Sleep(200);

                                    break;
                                }
                            }
                        }

                        var nextObj = wowHook.Memory.Read<IntPtr>(IntPtr.Add(curObj, (int)Offsets.NextObjectOffset));
                        if (nextObj == curObj)
                            break;
                        curObj = nextObj;
                    }

                    Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    if (ex.Message == "Couldn't read 4 byte(s) from 0x90.")
                    {
                        // Do nothing
                    }
                    else
                    {
                        Log.Write(ex.Message, Color.Red);
                    }

                    Thread.Sleep(250);
                }
            }
        }

        private void cmdStop_Click(object sender, EventArgs e)
        {
            cmdStop.Enabled = false;
            cmdFish.Enabled = true;

            SystemSounds.Asterisk.Play();
            Fish = false;
        }

        private void preventAfkDisconnectCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var message = preventAfkDisconnectCheckBox.Checked
                ? "AFK prevention enabled: sends an unbound F13 key every two minutes while WoW is in the foreground."
                : "AFK prevention disabled.";
            Log.Write(message, Color.Black);
        }

        private void keptItems_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void keptToDropButton_Click(object sender, EventArgs e)
        {
            if (keptItems.SelectedItem != null)
            {
                dropItems.Items.Add(keptItems.SelectedItem);

                List<string> temp = new List<string>();

                foreach (string item in keptItems.Items)
                {
                    if (item.Contains(keptItems.SelectedItem.ToString()))
                    {
                        temp.Add(item);
                    }
                }

                foreach (string item in temp)
                {
                    keptItems.Items.Remove(item);
                }
            }
        }

        private void dropToKeptButton_Click(object sender, EventArgs e)
        {
            if (dropItems.SelectedItem != null)
            {
                keptItems.Items.Add(dropItems.SelectedItem);

                List<string> temp = new List<string>();

                foreach (string item in dropItems.Items)
                {
                    if (item.Contains(dropItems.SelectedItem.ToString()))
                    {
                        temp.Add(item);
                    }
                }

                foreach (string item in temp)
                {
                    dropItems.Items.Remove(item);
                }
            }
        }
    }
}