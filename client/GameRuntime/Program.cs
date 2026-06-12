using System;
using System.IO;
using System.Threading;
using GameFramework.Core;
using GameFramework.Core.EventSystem;
using GameFramework.Core.GameSystem;
using GameFramework.Data;
using GameFramework.Network;
using GameFramework.Network.Connection;
using GameFramework.Network.Protobuf;
using GameFramework.Resource;
using GameFramework.UI;
using GameLogic.Network.Handlers;

namespace GameRuntime
{
    internal class Program
    {
        // ---- 自定义事件示例 ----
        public class PlayerLevelUpEvent : IEvent
        {
            public int PlayerId { get; set; }
            public int NewLevel { get; set; }
            public string PlayerName { get; set; } = "";
        }

        // ---- 自定义UI窗口示例 ----
        public class MainMenuWindow : UIWindow
        {
            public override UILayer Layer => UILayer.Bottom;
            public override string WindowName => "MainMenu";

            protected override void OnInit()
            {
                Console.WriteLine($"  [UI] {WindowName}: OnInit");
            }

            protected override void OnOpen()
            {
                Console.WriteLine($"  [UI] {WindowName}: OnOpen → 显示主菜单");
            }

            protected override void OnClose()
            {
                Console.WriteLine($"  [UI] {WindowName}: OnClose");
            }

            protected override void OnRefresh()
            {
                Console.WriteLine($"  [UI] {WindowName}: OnRefresh");
            }
        }

        public class SettingWindow : UIWindow
        {
            public override UILayer Layer => UILayer.Middle;

            protected override void OnInit()
            {
                Console.WriteLine($"  [UI] {WindowName}: OnInit");
            }

            protected override void OnOpen()
            {
                Console.WriteLine($"  [UI] {WindowName}: OnOpen → 显示设置面板");
            }

            protected override void OnClose()
            {
                Console.WriteLine($"  [UI] {WindowName}: OnClose");
            }
        }

        // ---- 自定义模块示例 ----
        public class PlayerModule : GameModule
        {
            public override string ModuleName => "PlayerModule";
            private int _tickCount;

            protected override void OnInit()
            {
                Console.WriteLine("  [Player] 模块初始化");
            }

            protected override void OnUpdate(float deltaTime)
            {
                _tickCount++;
                if (_tickCount % 10 == 0)
                {
                    Console.WriteLine($"  [Player] Tick... (totalTime={GameApp.Instance.TotalTime:F1}s)");
                }
            }

            protected override void OnShutdown()
            {
                Console.WriteLine("  [Player] 模块关闭");
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  GameFramework 独立运行模式");
            Console.WriteLine("========================================\n");

            // ========== 1. 初始化 GameApp ==========
            Console.WriteLine("[Step 1] 初始化 GameApp...");
            var app = GameApp.Instance;

            // ========== 2. 注册模块 ==========
            Console.WriteLine("[Step 2] 注册游戏模块...");

            // UI管理器
            var uiManager = new UIManager();
            app.RegisterModule(uiManager);

            // 数据管理器
            app.RegisterModule(new DataManager());

            // 网络管理器
            var networkManager = new NetworkManager();
            app.RegisterModule(networkManager);

            // 资源管理器
            var resourceManager = new ResourceManager();
            app.RegisterModule(resourceManager);

            // 自定义游戏模块
            var playerModule = new PlayerModule();
            app.RegisterModule(playerModule);

            // ========== 3. 初始化 ==========
            Console.WriteLine("[Step 3] 启动 GameApp...");
            app.Initialize();

            // ========== 4. 演示 EventSystem ==========
            Console.WriteLine("\n[Demo] EventSystem — 事件系统演示");
            EventBus.Register<PlayerLevelUpEvent>(e =>
            {
                Console.WriteLine($"  → 事件回调: {e.PlayerName} 升到 {e.NewLevel} 级!");
            }, priority: 10);

            EventBus.RegisterOnce<PlayerLevelUpEvent>(e =>
            {
                Console.WriteLine($"  → (一次性) 首次升级: {e.PlayerName} 达到 {e.NewLevel} 级");
            });

            // 派发事件
            Console.WriteLine("  派发 PlayerLevelUpEvent...");
            EventBus.Dispatch(new PlayerLevelUpEvent
            {
                PlayerId = 1001,
                PlayerName = "勇者小智",
                NewLevel = 5
            });

            Console.WriteLine("  再次派发（验证一次性监听已移除）...");
            EventBus.Dispatch(new PlayerLevelUpEvent
            {
                PlayerId = 1001,
                PlayerName = "勇者小智",
                NewLevel = 6
            });

            // ========== 5. 演示 UISystem ==========
            Console.WriteLine("\n[Demo] UISystem — UI系统演示");

            var mainMenu = uiManager.Open<MainMenuWindow>();
            Console.WriteLine($"  当前活跃窗口: {app.GetModule<UIManager>()?.Get<MainMenuWindow>()?.WindowName ?? "none"}");

            var setting = uiManager.Open<SettingWindow>();
            Console.WriteLine($"  设置窗口 IsOpen: {setting.IsOpen}");

            Console.WriteLine("  关闭设置窗口...");
            uiManager.Close<SettingWindow>();

            // ========== 6. 演示 游戏循环 ==========
            Console.WriteLine("\n[Demo] GameLoop — 游戏循环模拟（5帧）");
            Console.WriteLine($"  初始状态: {app.StateMachine.CurrentStateName ?? "none"}");

            for (int i = 0; i < 5; i++)
            {
                float dt = 0.016f; // 模拟 60fps (~16ms/帧)
                app.Tick(dt);
                Thread.Sleep(10);
            }

            Console.WriteLine($"  运行结束: Time={app.TotalTime:F2}s, FPS≈{app.FrameRate:F0}");

            // ========== 7. 演示 ServiceLocator ==========
            Console.WriteLine("\n[Demo] ServiceLocator — 服务定位器演示");
            var resolvedUi = app.Services.Get<UIManager>();
            Console.WriteLine($"  从 ServiceLocator 获取 UIManager: {(resolvedUi != null ? "OK" : "FAIL")}");

            // ========== 8. 演示 DataManager ==========
            Console.WriteLine("\n[Demo] DataManager — 策划配置数据访问");
            try
            {
                // 构建 Unity StreamingAssets/Config/ItemConfig.cfgb 路径
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "..",
                    "client", "UnityClient", "Assets", "StreamingAssets", "Config", "ItemConfig.cfgb");
                Console.WriteLine($"  加载: {Path.GetFullPath(configPath)}");
                DataManager.Load<GameFramework.Data.Generated.Config_ItemConfig>(
                    Path.GetFullPath(configPath));

                Console.WriteLine($"  总行数: {DataManager.Count<GameFramework.Data.Generated.Config_ItemConfig>()}");
                var item = DataManager.Get<GameFramework.Data.Generated.Config_ItemConfig>(1001);
                Console.WriteLine($"  按 ID 获取: 1001 → Name={item.Name}, Type={item.Type}");

                Console.WriteLine("  遍历所有:");
                foreach (var row in DataManager.All<GameFramework.Data.Generated.Config_ItemConfig>())
                {
                    Console.WriteLine($"    [{row.Id}] {row.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [SKIP] {ex.Message}");
            }

            // ========== 9. 演示 Network — 协议消息系统 ==========
            Console.WriteLine("\n[Demo] Network — 协议消息系统");

            // CG_ 消息：直接构造发送，无需注册处理器
            Console.WriteLine("  CG_ 消息（客户端→服务器，直接发送）:");
            var heartbeatReq = new CG_Heartbeat { ClientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            Console.WriteLine($"    new CG_Heartbeat {{ ClientTime = {heartbeatReq.ClientTime} }}");
            Console.WriteLine($"    networkManager.Send((ushort)EProtocol.CG_Heartbeat, heartbeatReq);");

            // GC_ 消息：自动注册处理器
            Console.WriteLine("  GC_ 消息（服务器→客户端，自动注册处理器）:");
            GameHandler.RegisterAll(networkManager);
            Console.WriteLine($"    已注册 {networkManager.Dispatcher.Count} 个 GC_ 处理器");

            Console.WriteLine("  收到 GC_Login 时自动转发到 GameHandler 业务处理:");

            // ========== 10. 演示 ResourceSystem ==========
            Console.WriteLine("\n[Demo] ResourceSystem - sync/async loading and ref counting");
            var builtinProvider = new MemoryResourceProvider(name: "Builtin", priority: 10);
            builtinProvider.Add("texts/welcome", "Welcome to the resource system.");
            var remoteProvider = new MemoryResourceProvider(name: "Remote", priority: 20, canLoadSync: false);
            remoteProvider.Add("remote/tip", "Async resource loaded from the higher-priority provider.");
            resourceManager.AddProvider(builtinProvider);
            resourceManager.AddProvider(remoteProvider);

            using (var handleA = resourceManager.LoadHandle<string>("texts/welcome"))
            using (var handleB = resourceManager.LoadHandle<string>("texts/welcome"))
            {
                Console.WriteLine($"  Sync load: {handleA.Asset}");
                Console.WriteLine($"  Shared cached asset: {ReferenceEquals(handleA.Asset, handleB.Asset)}");
                Console.WriteLine($"  RefCount after two handles: {handleA.RefCount}");
            }
            Console.WriteLine($"  Loaded after disposing handles: {resourceManager.IsLoaded("texts/welcome")}");

            var asyncHandle = resourceManager.LoadHandleAsync<string>("remote/tip").GetAwaiter().GetResult();
            Console.WriteLine($"  Async load: {asyncHandle.Asset}");
            asyncHandle.Dispose();

            try
            {
                resourceManager.Load<string>("remote/tip");
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine($"  Sync unsupported check: {ex.Message}");
            }

            resourceManager.Clear();
            Console.WriteLine($"  Loaded after Clear: {resourceManager.IsLoaded("remote/tip")}");

            // ========== 11. 关闭 ==========
            Console.WriteLine("\n[Step Final] 关闭 GameApp...");
            app.Shutdown();

            Console.WriteLine("\n========================================");
            Console.WriteLine("  所有系统演示完成");
            Console.WriteLine("========================================");
        }
    }
}
