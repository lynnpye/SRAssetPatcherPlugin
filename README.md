# SRAssetPatcherPlugin
BepInEx based mod. 

This is based on SRPlugin.

Okay, let's be real here folks, this is a rough draft. I have tested it with exactly one clip, with an assetName of "Music/HongKong-TitleTheme-UI", which I used this plugin to swap in for the starting music.
Your .cfg would look something like this:

	[Features]
	MusicPatcherEnabled = true
	
	[Replacements]
	
	whatever-you-want.asset = Music/HongKong-TitleTheme-UI
	whatever-you-want.file = ..\musicpatches\theme.wav
	
	aslong-asit-matches.asset = Music/Surely-There-Must-Be-Others
	aslong-asit-matches.file = ..\..\some\other\folder\anotherwav.wav


And right now it only even attempts to work by converting a .WAV on disk to an AudioClip, and only specifically for Music requests. I plan to add more functionality.

At the moment, the WAV to AudioClip conversion is courtesy of [WavUtility.cs by deadlyfingers](https://github.com/deadlyfingers/UnityWav). Thank you, deadlyfingers.

That said, yes, I would like to see this expanded to allow for more dynamic loading options. And efficiently, too. The current implementation does no caching, for example. But.... beyond all of that... yes, the following could be said:

Allows dynamically replacing music tracks without needing to work with asset bundles. Just drop your replacement tracks into the appropriate folder, modify the .cfg file, install the plugin, and launch the game!

To use this mod:

## Install BepInEx
Go to the [BepInEx Releases page](https://github.com/BepInEx/BepInEx/releases/) and download BepInEx_win_x86_5.4.23.2.zip [(link to the release tag)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2) [(direct link to the zip)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x86_5.4.23.2.zip) and install it to your game folder as normal.

Run the game once now that BepInEx is installed. The game will likely crash. If it does not, exit the game as soon as the main menu appears.

When installing BepInEx to the game folder, a 'BepInEx/' folder was created. There should now be a 'config/' subfolder containing 'BepInEx.cfg'.

Edit 'BepInEx.cfg', finding the [Preloader.Entrypoint] section and changing the 'Type' value to 'Camera'. That section should now look like this:

```
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
```

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