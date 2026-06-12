using System;
using GameFramework.Core.GameSystem;
using GameFramework.Logging;
using GameFramework.Save;
using GameFramework.Time;
using UnityEngine;

namespace GameClient
{
    public sealed class FrameworkFeatureValidation : MonoBehaviour
    {
        [SerializeField] private string savePath = "validation/framework_check.txt";
        [SerializeField] private float timerInterval = 0.5f;
        [SerializeField] private int timerTicks = 3;

        private TimerHandle _timerHandle;

        private void Start()
        {
            RunValidation();
        }

        private void OnDestroy()
        {
            _timerHandle.Cancel();
        }

        [ContextMenu("Run Framework Feature Validation")]
        public void RunValidation()
        {
            var app = GameApp.Instance;
            var logger = app.GetModule<LoggerManager>();
            var saveManager = app.GetModule<SaveManager>();
            var timerManager = app.GetModule<TimerManager>();

            logger?.Info("Unity framework feature validation started.", "Validation");

            if (saveManager != null)
            {
                var content = $"validated_at={DateTime.UtcNow:O}";
                saveManager.SaveText(savePath, content);
                var loaded = saveManager.LoadText(savePath);
                logger?.Info($"Save validation OK. Path={saveManager.GetFullPath(savePath)}, Content={loaded}", "Validation");
            }
            else
            {
                Debug.LogWarning("[Validation] SaveManager is not registered.");
            }

            if (timerManager != null)
            {
                _timerHandle.Cancel();
                _timerHandle = timerManager.Repeat(timerInterval, tick =>
                {
                    logger?.Info($"Timer validation tick {tick}/{timerTicks}.", "Validation");
                }, timerTicks);
            }
            else
            {
                Debug.LogWarning("[Validation] TimerManager is not registered.");
            }
        }
    }
}
