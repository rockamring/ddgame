# UnityClient

Unity 2022.3 client project.

## GameFramework integration

`GameFramework` source code lives under:

```text
Assets/Scripts/Framework/Core/
Assets/Scripts/Framework/Data/
Assets/Scripts/Framework/Logging/
Assets/Scripts/Framework/Network/
Assets/Scripts/Framework/Save/
Assets/Scripts/Framework/Time/
Assets/Scripts/Framework/UI/
```

Unity-specific framework code stays in this Unity project:

```text
Assets/Scripts/App/GameBootstrap.cs
Assets/Scripts/Framework/Resource/
Assets/Scripts/Framework/Adapters/ResourcesProvider.cs
Assets/Scripts/Generated/
Assets/StreamingAssets/Config/
```

`GameBootstrap` is created automatically before the first scene loads. It registers the default framework modules, initializes `GameApp`, forwards Unity `Update` to `GameApp.Tick`, and shuts the framework down when the application quits.

`ResourcesProvider` adapts Unity `Resources` loading to `ResourceManager`. Paths may use `res://path/to/asset`; plain Resources paths are also accepted.

Generated C# code and exported `.cfgb` files are committed under `Assets/Scripts/Generated` and `Assets/StreamingAssets/Config`. Run the repository root `init.bat` after changing config tables, proto files, or generators.

## Sync dependency DLLs

`Google.Protobuf.dll` is consumed as a precompiled dependency under:

```text
Assets/Scripts/Framework/Plugins/Google.Protobuf.dll
```

After changing protocol dependencies, sync dependency DLLs from Unity:

```text
Tools/GameFramework/Sync Dependency DLLs
```

The framework source is now part of the Unity project. Keep Unity APIs, Addressables, AssetBundle, HybridCLR, Input System, AudioMixer, and scene-specific behavior in Unity-side folders such as `Resource`, `Adapters`, or feature-specific services.
