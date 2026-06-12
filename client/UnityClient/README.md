# UnityClient

Unity 2022.3 client project.

## GameFramework integration

`GameFramework` is consumed as a precompiled DLL under:

```text
Assets/Scripts/Framework/Plugins/GameFramework.dll
Assets/Scripts/Framework/Plugins/Google.Protobuf.dll
```

Unity-specific code stays in this Unity project:

```text
Assets/Scripts/App/GameBootstrap.cs
Assets/Scripts/Framework/Adapters/ResourcesProvider.cs
Assets/Scripts/Generated/
Assets/StreamingAssets/Config/
```

`GameBootstrap` is created automatically before the first scene loads. It registers the default framework modules, initializes `GameApp`, forwards Unity `Update` to `GameApp.Tick`, and shuts the framework down when the application quits.

`ResourcesProvider` adapts Unity `Resources` loading to `ResourceManager`. Paths may use `res://path/to/asset`; plain Resources paths are also accepted.

Generated C# code and exported `.cfgb` files are committed under `Assets/Scripts/Generated` and `Assets/StreamingAssets/Config`. Run the repository root `init.bat` after changing config tables, proto files, or generators.

## Sync DLLs

After changing `client/GameFramework`, rebuild and sync the DLLs from Unity:

```text
Tools/GameFramework/Build And Sync DLLs
```

Or sync already-built DLLs:

```text
Tools/GameFramework/Sync DLLs
```

The framework should remain Unity-agnostic. Add Unity APIs, Addressables, AssetBundle, HybridCLR, Input System, AudioMixer, and scene-specific behavior in this Unity project as adapters.
