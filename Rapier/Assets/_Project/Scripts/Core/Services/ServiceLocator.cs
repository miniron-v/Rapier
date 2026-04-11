using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전역 시스템 단일 접근점.
    /// 남용 금지 — InputManager 등 진짜 전역 시스템만 등록한다.
    /// 씬 전환 시 Unregister를 반드시 호출할 것.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        /// <summary>서비스를 등록한다. 중복 등록 시 경고 후 덮어쓴다.</summary>
public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
                UnityEngine.Debug.LogWarning($"[ServiceLocator] {type.Name} 이미 등록됨. 덮어씁니다.");
            _services[type] = service;
        }

        /// <summary>서비스를 가져온다. 없으면 null 반환 + 경고.</summary>
public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return service as T;
            UnityEngine.Debug.LogWarning($"[ServiceLocator] {typeof(T).Name} 등록되지 않음.");
            return null;
        }

        /// <summary>서비스를 가져온다. 없으면 null 반환 (경고 없음).</summary>
        public static T TryGet<T>() where T : class
        {
            _services.TryGetValue(typeof(T), out var service);
            return service as T;
        }

        /// <summary>서비스를 해제한다. 씬 정리 시 반드시 호출.</summary>
        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>모든 서비스 해제. 씬 전체 초기화 시 사용.</summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}
