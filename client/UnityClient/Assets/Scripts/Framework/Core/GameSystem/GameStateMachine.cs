using System;
using System.Collections.Generic;

namespace GameFramework.Core.GameSystem
{
    /// <summary>
    /// 游戏状态接口
    /// </summary>
    public interface IGameState
    {
        /// <summary>
        /// 状态名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 进入状态
        /// </summary>
        void OnEnter();

        /// <summary>
        /// 离开状态
        /// </summary>
        void OnExit();

        /// <summary>
        /// 状态帧更新
        /// </summary>
        void OnUpdate(float deltaTime);
    }

    /// <summary>
    /// 状态基类，方便继承使用
    /// </summary>
    public abstract class BaseGameState : IGameState
    {
        public virtual string Name => GetType().Name;
        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void OnUpdate(float deltaTime) { }
    }

    /// <summary>
    /// 有限状态机，管理游戏状态转换。
    /// 同一时间只有一个激活状态。
    /// </summary>
    public class GameStateMachine
    {
        private readonly Dictionary<string, IGameState> _states = new();
        private IGameState? _currentState;

        /// <summary>
        /// 当前状态名称
        /// </summary>
        public string? CurrentStateName => _currentState?.Name;

        /// <summary>
        /// 当前状态
        /// </summary>
        public IGameState? CurrentState => _currentState;

        /// <summary>
        /// 注册状态
        /// </summary>
        public void RegisterState(IGameState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _states[state.Name] = state;
        }

        /// <summary>
        /// 注销状态
        /// </summary>
        public void UnregisterState(string name)
        {
            _states.Remove(name);
        }

        /// <summary>
        /// 切换到指定状态
        /// </summary>
        public void ChangeState<T>() where T : IGameState
        {
            var name = typeof(T).Name;
            ChangeState(name);
        }

        /// <summary>
        /// 切换到指定名称的状态
        /// </summary>
        public void ChangeState(string stateName)
        {
            if (!_states.TryGetValue(stateName, out var newState))
            {
                throw new InvalidOperationException($"State '{stateName}' is not registered.");
            }

            if (_currentState == newState)
                return;

            _currentState?.OnExit();
            _currentState = newState;
            _currentState.OnEnter();
        }

        /// <summary>
        /// 更新当前状态
        /// </summary>
        public void Update(float deltaTime)
        {
            _currentState?.OnUpdate(deltaTime);
        }

        /// <summary>
        /// 清空所有状态（不调用 Exit）
        /// </summary>
        public void Clear()
        {
            _currentState?.OnExit();
            _currentState = null;
            _states.Clear();
        }
    }
}
