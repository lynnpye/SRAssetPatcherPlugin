# SRAssetPatcherPlugin
This is based on [SRPluginTemplate](https://github.com/lynnpye/SRPluginTemplate).

The goal of this mod is to allow on-the-fly asset replacement from disk for assets requested by name. The purpose is to avoid needing to do things like rebundle campaign assets just to replace music.

Because music swapping was my initial focus, that is the only replacement feature I have working at the moment.

## MusicPatcher feature
This requires the `MusicPatcherEnabled` feature flag to be set to `true`.

I use the `UnityEngine.WWW` class to load music files from disk, so any music file formats it recognizes should be able to load. I did find that attempting to use it to load MP3 files does not appear to work.
However I have tested with WAV and OGG and these seem to work fine.

There is another feature flag, `LogMusicAssetsRequested`, which if set to `true` will print Music assets that are requested by name, based on HBS's naming convention of prefixing Music assets with `'Music/'`.
This may be helpful if you're fishing around for music asset names. This flags defaults to `false`.

Additionally, HBS seems to have made music prefabs, which start with `Music/` and end with `-Prefab` by convention. When using `LogMusicAssetsRequested`, if it sees one of these it will call it as well.

For your file paths, you can specify full paths or relative paths. Relative paths are relative to [UnityEngine.Application.persistentDataPath](https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html).
On your first launch with the plugin, it will generate a .cfg file with the persistentDataPath location called out in a comment. On Windows this is typically at `C:\Users\<user>\AppData\LocalLow\<Harebrained Schemes>\<game folder>\`.

Here is an example:

	[Features]
	MusicPatcherEnabled = true
	LogMusicAssetsRequested = true
	
	[MusicReplacements]
	
	whatever-you-want.asset = Music/HongKong-TitleTheme-UI
	whatever-you-want.file = ..\musicpatches\theme.wav
	
	aslong-asit-matches.asset = Music/Surely-There-Must-Be-Others
	aslong-asit-matches.file = ..\..\some\other\folder\anotherwav.wav

In this example, we can see:

- `[Features]` - the section where features are enabled/disabled
- `MusicPatcherEnable = true` - this enables the Music Patcher feature; in the future there may be other asset patching options which will have their own feature flags; if this is not true, anything in `[MusicReplacements]` will be ignored
- `LogMusicAssetsRequested = true` - this enables logging of music asset requests to aid in finding asset names to replace
- `[MusicReplacements]` - the section where replacement information is configred
- `whatever-you-want.asset = Music/HongKong-TitleTheme-UI`
	- `whatever-you-want` - you can use any replacement key you want to for this portion, but it has to have both the `.asset` and `.file` entries to process correctly
	- `.asset` - this marks this entry as the `.asset` part of the replacement pair; whatever value you provide here is what the code will respond to when requests to load by that name come in
	- `Music/HongKong-TitleTheme-UI` - this is the asset name that SRHK loads for the title screen; for DFDC this is `Music/Berlin-TitleTheme` and for SRR it is `Music/Seattle-TitleTheme`
- `whatever-you-want.file = ..\musicpatches\theme.wav`
	- `.file` - this marks this entry as the `.file` part of the replacement pair; see `.asset` above; this can be a full path or relative to the persistentDataPath for Unity
- `aslong-asit-matches.asset = Music/Surely-There-Must-Be-Others`
	- `aslong-asit-matches` - this is a new replacement key, representing a different pair of asset/file
	- `Music/Surely-There-Must-Be-Others` - and there are!; this would be another asset you are searching for

To use this mod:

## Install BepInEx
Go to the [BepInEx Releases page](https://github.com/BepInEx/BepInEx/releases/) and download BepInEx_win_x86_5.4.23.2.zip [(link to the release tag)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2) [(direct link to the zip)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x86_5.4.23.2.zip) and install it to your game folder as normal.

Run the game once now that BepInEx is installed. The game will likely crash. If it does not, exit the game as soon as the main menu appears.

When installing BepInEx to the game folder, a 'BepInEx/' folder was created. There should now be a 'config/' subfolder containing 'BepInEx.cfg'.

Edit 'BepInEx.cfg', finding the [Preloader.Entrypoint] section and changing the 'Type' value to 'Camera'. That section should now look like this:

	[Preloader.Entrypoint]
	
	## The local filename of the assembly to target.
	# Setting type: String
	# Default value: UnityEngine.dll
	Assembly = UnityEngine.dll

	## The name of the type in the entrypoint assembly to search for the entrypoint method.
	# Setting type: String
	# Default value: Application
	Type = Camera

	## The name of the method in the specified entrypoint assembly and type to hook and load Chainloader from.
	# Setting type: String
	# Default value: .cctor
	Method = .cctor

## Developing your BepInEx plugin (i.e. your mod)
### For Shadowrun Returns
Add a new Environment Variable (personal or system, doesn't matter):
	SRRInstallDir
	<The folder where your Shadowrun.exe is located>
So if your Shadowrun.exe is located at "C:\Program Files (x86)\Steam\steamapps\common\Shadowrun Returns\Shadowrun.exe", then SRRInstallDir would be set to "C:\Program Files (x86)\Steam\steamapps\common\Shadowrun Returns".

### For Shadowrun Dragonfall Director's Cut
Add a new Environment Variable (personal or system, doesn't matter):
	DFDCInstallDir
	<The folder where your Dragonfall.exe is located>
So if your Dragonfall.exe is located at "C:\Program Files (x86)\Steam\steamapps\common\Shadowrun Dragonfall Director's Cut\Dragonfall.exe", then DFDCInstallDir would be set to "C:\Program Files (x86)\Steam\steamapps\common\Shadowrun Dragonfall Director's Cut".

### For Shadowrun Hong Kong
Add a new Environment Variable (personal or system, doesn't matter):
	SRHKInstallDir
	<The folder where your SRHK.exe is located>
So if your SRHK.exe is located at "C:\Program Files (x86)\Steam\steamapps\common\Shadowrun Hong Kong\SRHK.exe", then SRHKInstallDir would be set to "C:\Program Files (x86)\Steam\steamapps\common\Shadowrun Hong Kong".

### Selective compilation
While the SRPluginShared code is mostly compatible across versions, there are of course some difference, so when you are creating patches, you may need to opt to exclude them from one or two of the three game versions. I'm using preprocessor directives with functional names.


### And then
Download this template and edit things like project names, versions, etc. You may need to edit the .csproj manually.

Create your plugin per guidelines from HarmonyX and BepInEx.

Install the built .dll to the BepInEx/plugins folder to have it take effect.

Congratulations, you are modding Shadowrun without using dnSpy to edit the Assembly-CSharp.dll directly.

## More notes
Steps I went through to arrive at this template...
... actually I did most of this with SRHKPlugin, and copied it over and applied it to Dragonfall, but it all worked out remarkably similarly. But sure, continue reading :) ...

As mentioned above, I started with the BepInEx_win_x86_5.4.23.2.zip file and installed it.

I created the environment variable because I use it in the .csproj to create relative references to ShadowrunDTO.dll (which contains a lot of types) and Assembly-CSharp.dll (which contains all the logic).

I initially was following steps for creating BepInEx plugins from their [setup](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/1_setup.html) and [plugin start](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/2_plugin_start.html) pages.

I ran into a problem when trying to run 'dotnet new bepinex5plugin...' with the error:
```
Template "BepInEx 5 Plugin Template" could not be created.
Failed to create template.
Details: Object reference not set to an instance of an object.
```

Whereupon I followed the steps on [this StackOverflow page](https://stackoverflow.com/questions/42077229/switch-between-dotnet-core-sdk-versions/42078060#42078060) to create a global.json. It turns out I had .NET 8 on my machine, had to install .NET 7 (7.0.410), and then was able to run the command above to generate the template.

I was then able to delete the global.json file.

Initially all that is created is the .csproj and source files, no .sln file. When you open the .csproj and then click File>Close Solution, it asks you if you wish to save the .sln. I did. When I had first opened the .csproj, the BepInEx NuGet source URL was already set up. When I reopened the .sln file, it was missing and I had to manually add it via Tools>NuGet Package Manager>Manage NuGet Packages for Solution.

That URL is https://nuget.bepinex.dev/v3/index.json

I also manually modified the .csproj file to add references to the two DLLs that ship with the game. As I mentioned above, I use the environment variable to make the referencing easier. I also had to mark Assembly-CSharp.dll as ExternallyResolved=True because there is a version mismatch on mscorlib.dll between the target framework in the project and what is shipped with the game.

## And then
I've since created a single solution setup with projects for both Shadowrun Hong Kong and Shadowrun Dragonfall Director's Cut. So far it seems to work well enough.