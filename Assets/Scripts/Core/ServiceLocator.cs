using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Service Locator
    // Centralised service registry. Avoids singleton sprawl while keeping
    // systems loosely coupled. Systems register on boot, deregister on teardown.
    //
    // Usage:
    //   ServiceLocator.Register<IMyService>(new MyServiceImpl());
    //   var svc = ServiceLocator.Get<IMyService>();
    //   ServiceLocator.Deregister<IMyService>();
    // ==========================================================================

    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        // ── Registration ──────────────────────────────────────────────────────

        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
                Debug.LogWarning($"[ServiceLocator] Overwriting existing registration for <{type.Name}>.");

            _services[type] = service;
            Debug.Log($"[ServiceLocator] Registered <{type.Name}>.");
        }

        public static void Deregister<T>() where T : class
        {
            if (_services.Remove(typeof(T)))
                Debug.Log($"[ServiceLocator] Deregistered <{typeof(T).Name}>.");
        }

        // ── Resolution ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the registered service of type T.
        /// Logs an error and returns null if not found.
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return service as T;

            Debug.LogError($"[ServiceLocator] Service not registered: <{typeof(T).Name}>. " +
                           "Did GameBootstrapper.Boot() run before this call?");
            return null;
        }

        /// <summary>Non-throwing version. Returns true if found.</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                service = typed;
                return true;
            }
            service = null;
            return false;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>Clears all registrations. Call from GameBootstrapper.OnDestroy.</summary>
        public static void Clear()
        {
            _services.Clear();
            Debug.Log("[ServiceLocator] All services cleared.");
        }
    }
}
