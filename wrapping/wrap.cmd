
set SRPLUGINDIR=..
REM location of 7zip utility
set CMD7Z=7z

rd /q /s plugins
rd /q /s working

mkdir plugins
mkdir working

for %%p in (SRRAssetPatcherPlugin
DFDCAssetPatcherPlugin
SRHKAssetPatcherPlugin) do (
rmdir /q /s %%p
del %%p.zip
xcopy /e /i ModBepInExCfg working\%%p
xcopy configs\%%p.cfg working\%%p\BepInEx\config
xcopy %SRPLUGINDIR%\%%p\bin\Debug\net35\%%p.dll working\%%p\BepInEx\plugins
cd working\%%p
%CMD7Z% a ..\..\plugins\%%p.zip BepInEx
cd ..\..
)

