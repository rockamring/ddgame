using System.IO;
using System.Linq;
using GameClient.Framework;
using GameFramework.Core.GameSystem;
using GameFramework.Data;
using GameFramework.Network;
using GameFramework.Resource;
using GameFramework.UI;
using UnityEngine;

namespace GameClient
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const string BootstrapObjectName = "[GameFramework]";

        [SerializeField] private bool registerDefaultModules = true;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool shutdownOnDestroy = true;

        private static GameBootstrap? s_instance;
        private GameApp? _app;

        public GameApp App => _app ?? GameApp.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBootstrap()
        {
            if (s_instance != null)
                return;

            var existing = FindObjectOfType<GameBootstrap>();
            if (existing != null)
            {
                s_instance = existing;
                return;
            }

            var go = new GameObject(BootstrapObjectName);
            s_instance = go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            _app = GameApp.Instance;
            if (registerDefaultModules)
                RegisterDefaultModules(_app);

            _app.Initialize();
            Debug.Log("[GameFramework] Initialized.");
        }

        private void Update()
        {
            _app?.Tick(Time.deltaTime);
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            if (s_instance != this)
                return;

            if (shutdownOnDestroy)
                Shutdown();

            s_instance = null;
        }

        private static void RegisterDefaultModules(GameApp app)
        {
            DataManager.ConfigDirectory = Path.Combine(Application.streamingAssetsPath, "Config");

            if (app.GetModule<UIManager>() == null)
                app.RegisterModule(new UIManager());

            if (app.GetModule<DataManager>() == null)
                app.RegisterModule(new DataManager());

            if (app.GetModule<NetworkManager>() == null)
                app.RegisterModule(new NetworkManager());

            var resourceManager = app.GetModule<ResourceManager>();
            if (resourceManager == null)
                resourceManager = app.RegisterModule(new ResourceManager());

            if (resourceManager.Providers.All(provider => provider.Name != "Resources"))
                resourceManager.AddProvider(new ResourcesProvider());
        }

        private void Shutdown()
        {
            if (_app == null || !_app.IsRunning)
                return;

            _app.Shutdown();
            Debug.Log("[GameFramework] Shutdown.");
        }
    }
}
