# Building Silksong Neural Smart

This repository contains the C# source code for the **Silksong Neural Smart** BepInEx mod, as well as an interactive live training simulator.

## Requirements
- [.NET SDK 6.0, 7.0, or 8.0](https://dotnet.microsoft.com/download)
- Hollow Knight: Silksong with [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) or [BepInEx 6 IL2CPP](https://builds.bepinex.dev/projects/bepinex_be)

## Build Instructions (Command Line)

To compile the mod `.dll`:
```bash
# Build Release assembly
dotnet build SilksongNeuralSmart.csproj -c Release
```

The output DLL will be generated in:
`bin/Release/netstandard2.1/SilksongNeuralSmart.dll`

## Manual IDE Build
1. Open `SilksongNeuralSmart.csproj` in Visual Studio 2022, JetBrains Rider, or VS Code.
2. Ensure reference paths point to your Silksong `BepInEx/core` and `Silksong_Data/Managed/` directories if compiling against custom game assemblies.
3. Select `Release` build configuration.
4. Click **Build Solution**.
5. Copy `SilksongNeuralSmart.dll` into your `Silksong/BepInEx/plugins/` directory.

## Running the Live Web Simulator
```bash
node web/server.js
```
Open `http://localhost:3000` in your web browser.
