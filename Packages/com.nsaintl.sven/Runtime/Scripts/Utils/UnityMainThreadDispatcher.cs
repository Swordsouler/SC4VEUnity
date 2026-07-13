// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Sven.Utils
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _executionQueue = new();
        private static UnityMainThreadDispatcher _instance;
        private static int _mainThreadId;

        /// <summary>
        /// Indique si le thread actuel est le thread principal de Unity.
        /// </summary>
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Tente de trouver une instance existante avant d'en créer une nouvelle.
                    _instance = FindFirstObjectByType<UnityMainThreadDispatcher>();
                    if (_instance == null)
                    {
                        var obj = new GameObject("UnityMainThreadDispatcher");
                        _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            // Assurer le pattern singleton et ne pas détruire l'objet au changement de scène.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
        }

        public void Enqueue(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _executionQueue.Enqueue(action);
        }

        private void Update()
        {
            // Exécuter toutes les actions en attente sur le thread principal
            while (_executionQueue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"UnityMainThreadDispatcher: Exception lors de l'exécution d'une action : {ex}");
                }
            }
        }
    }
}
