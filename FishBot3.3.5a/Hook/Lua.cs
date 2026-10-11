using System;
using System.Text;

namespace FishBot3._3._5a
{
    public class Lua
    {
        private readonly Hook _wowHook;

        public Lua(Hook wowHook)
        {
            _wowHook = wowHook;
        }

        public void DoString(string command)
        {
            if (_wowHook.Installed)
            {
                // Allocate memory
                IntPtr doStringArgCodecave = _wowHook.Memory.AllocateMemory(Encoding.UTF8.GetBytes(command).Length + 1);
                // Write value:
                _wowHook.Memory.WriteBytes(doStringArgCodecave, Encoding.UTF8.GetBytes(command));

                // Write the asm stuff for Lua_DoString
                var asm = new[]
                {
                    "mov eax, " + doStringArgCodecave,
                    "push 0",
                    "push eax",
                    "push eax",
                    //"mov eax, " + ( (uint) Offsets.FrameScript__Execute + _wowHook.Process.BaseOffset()) , // Lua_DoString
                    "mov eax, " + ( (uint) Offsets.FrameScript__Execute ), // Lua_DoString
                    "call eax",
                    "add esp, 0xC",
                    "retn"
                };
                // Inject
                _wowHook.InjectAndExecute(asm);
                // Free memory allocated
                _wowHook.Memory.FreeMemory(doStringArgCodecave);
            }
        }

        internal string GetLocalizedText(string localVar)
        {
            if (_wowHook.Installed)
            {
                IntPtr Lua_GetLocalizedText_Space = _wowHook.Memory.AllocateMemory(Encoding.UTF8.GetBytes(localVar).Length + 1);

                // If you building for MoP or higher then you need to use
                // ClntObjMgrGetActivePlayerObj = Memory.MainModule.BaseAddress + new IntPtr(0xADDRESS);
                // FrameScript__GetLocalizedText = Memory.MainModule.BaseAddress + new IntPtr(0xADDRESS);

                _wowHook.Memory.Write<byte>(Lua_GetLocalizedText_Space, Encoding.UTF8.GetBytes(localVar), false);

                String[] asm = new String[]
                {
                    "call " + (uint) Offsets.ClntObjMgrGetActivePlayerObj,
                    "mov ecx, eax",
                    "push -1",
                    "mov edx, " + Lua_GetLocalizedText_Space + "",
                    "push edx",
                    //"call " + ((uint) Offsets.FrameScript__GetLocalizedText + _wowHook.Process.BaseOffset()) ,
                    "call " + ((uint) Offsets.FrameScript__GetLocalizedText) ,
                    "retn",
                };

                string sResult = Encoding.UTF8.GetString(_wowHook.InjectAndExecute(asm));

                // Free memory allocated
                _wowHook.Memory.FreeMemory(Lua_GetLocalizedText_Space);
                return sResult;
            }
            return "WoW Hook not installed";
        }

        public void CastSpellByName(string spell)
        {
            // Check if spell is ready, if not skip this spell
            DoString("start, duration, enabled = GetSpellCooldown('" + spell + "')");
            var result = GetLocalizedText("duration");

            if (result != "0")
                return;

            DoString(string.Format("CastSpellByName('{0}')", spell));
        }

        public void AcceptPendingLootBinding()
        {
            DoString(
                "for index = 1, STATICPOPUP_NUMDIALOGS do " +
                "local popup = _G['StaticPopup' .. index]; " +
                "if popup and popup:IsShown() and popup.which == 'LOOT_BIND' then " +
                "local acceptButton = _G[popup:GetName() .. 'Button1']; " +
                "if acceptButton and acceptButton:IsEnabled() == 1 then acceptButton:Click() end; " +
                "break; " +
                "end " +
                "end");
        }

        public void InitializeWorldStateMonitor()
        {
            DoString(
                "fishBotWorldReady = UnitExists('player') and '1' or '0'; " +
                "if not fishBotWorldStateFrame then fishBotWorldStateFrame = CreateFrame('Frame') end; " +
                "fishBotWorldStateFrame:RegisterEvent('PLAYER_LEAVING_WORLD'); " +
                "fishBotWorldStateFrame:RegisterEvent('PLAYER_ENTERING_WORLD'); " +
                "fishBotWorldStateFrame:SetScript('OnEvent', function(self, event) " +
                "if event == 'PLAYER_LEAVING_WORLD' then fishBotWorldReady = '0' " +
                "else fishBotWorldReady = UnitExists('player') and '1' or '0' end end)");
        }

        public bool IsPlayerInWorld()
        {
            return GetLocalizedText("fishBotWorldReady") == "1";
        }

        public bool IsPlayerCastingOrChanneling()
        {
            DoString(
                "fishBotPlayerBusy = (UnitCastingInfo('player') or UnitChannelInfo('player')) and '1' or '0'");
            return GetLocalizedText("fishBotPlayerBusy") == "1";
        }
    }
}