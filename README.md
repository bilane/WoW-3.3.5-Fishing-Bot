
# World of Warcraft - 3.3.5 - Fishing Bot

This is a light fishing bot application that I picked up from the [WoW Devs](https://github.com/WowDevs/Fishbot-3.3.5) GitHub. Their project was last contributed to 4 years ago from the time of me writing this, and is no longer working. It is my goal to breathe some life back into it, clean it up, refactor, and otherwise make it meet modern and personal code standards. As of this repo's init, the project is working.

Botting is against Blizzard terms and conditions, and will result in your account being banned.

### Technologies --

This project is written wholly in C#, using the [GrayMagic and FASM packages](https://github.com/benjamin-t-wilson/GreyMagic).

### Building from source --

On Windows 11, install Visual Studio with the **.NET desktop development** and **Desktop development with C++** workloads, including **C++/CLI support**. The C++ project uses the **MSVC v142 - VS 2019 C++ x64/x86 build tools** component; install it from Visual Studio Installer's **Individual components** tab if using Visual Studio 2022. The .NET Framework 4.0 and 4.7.2 reference assemblies are restored as NuGet dependencies, so separate Developer Packs are not needed.

The [GreyMagic dependency](https://github.com/benjamin-t-wilson/GreyMagic) is pinned as a submodule in this repository. The FASM object required by its native C++/CLI wrapper is included under `BuildDependencies` with its license; the build copies it to the dependency project before linking. After cloning this repository, open **Developer PowerShell for Visual Studio**, change to the repository folder, and run:

```powershell
git submodule update --init --recursive
nuget restore FishBot3.3.5a.sln
msbuild FishBot3.3.5a.sln /m /p:Configuration=Release /p:Platform="Any CPU"
```

For a fresh clone, use `git clone --recurse-submodules` instead of the separate submodule update command. The executable is produced at `FishBot3.3.5a\bin\Release\FishBot3.3.5a.exe`. Both configurations target x86. The application requests administrator privileges at launch and requires a 32-bit WoW 3.3.5a client; the client executable's memory layout must match the offsets in the source. A successful build does not verify compatibility with a particular game client.

### Launch --

After a successful Release build, run `FishBot3.3.5a\bin\Release\FishBot3.3.5a.exe` after starting WoW and logging into your character; Windows will display a UAC prompt because the application needs administrator privileges. Do not use the checked-in Debug executable as the updated build.

The application intercepts memory created / used by the WoW.exe system process in order to interact directly with the game engine. In addition to requiring admin perms to accomplish, this means it does not hijack the use of your mouse, keyboard, or active window.

During fishing, enable **Auto-accept bind-on-pickup** to accept only the client's `LOOT_BIND` confirmation. This permanently binds that item to the character. If a modified client uses a popup the automatic check cannot recognize, enable the option, click **Set fallback click location**, display the bind popup, move the pointer over its Accept button, and press **Ctrl+F8** to save the screen position. While fishing with WoW in the foreground, press **Ctrl+F9** when that popup is visible to click the saved position; this fallback is deliberately manual so it never blindly clicks unrelated UI. The cursor is restored after the click, and the location is remembered only until the bot closes.

Global hotkeys are **Ctrl+F6** to start fishing, **Ctrl+F7** to stop, **Ctrl+F8** to capture the fallback position after selecting it, and **Ctrl+F9** to click that position. If another application has already registered a shortcut, the bot logs that it could not register it.

### Future Features to Implement

- Look for classes that are agnostic to bot functionality and make them their own project ( ex. WowHook )
- Create repos for each level of the project
- Change excessive use of raw strings into constants
- ~Add a list of all items gatherable via fishing by name and level~ Didn't do level, oops.
- ~Add an interface to select which items should be removed from the inventory~ Interface has been added, does not affect functionality yet
- ~Run the inventory check / clean every few fish~ Runs when there are 0 slots remaining
- ~Stop bot when inventory is full~ Part of above solution
- Add interfaces for uses to add in bag slot numbers to check them instead of just inventory bag
- Make the bot not stop responding when clearing inventory ( it always comes back, I am honestly not sure what the problem is though )
- ~Remove unrelated bot features, there's some buttons with nothing to do with fishing~
