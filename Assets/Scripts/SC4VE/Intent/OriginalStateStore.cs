using Sven.Content;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Stocke l'état initial (transform + couleur) de chaque objet de la scène.
    /// Capturé automatiquement au chargement de la scène.
    /// Utilisé par ResetTransformCommand, ResetColorCommand et ResetSceneCommand.
    /// </summary>
    public static class OriginalStateStore
    {
        private struct ObjectState
        {
            public Vector3            Position;
            public Quaternion         Rotation;
            public Vector3            Scale;
            public UnityEngine.Color? Color;
        }

        // Stocke aussi les références pour retrouver les objets désactivés (soft-delete).
        private static readonly Dictionary<string, ObjectState>        _store      = new();
        private static readonly Dictionary<string, SemantizationCore>  _objectRefs = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() => CaptureAll();

        /// <summary>Capture l'état initial de tous les objets de la scène.</summary>
        public static void CaptureAll()
        {
            _store.Clear();
            _objectRefs.Clear();
            foreach (var obj in UnityEngine.Object.FindObjectsByType<SemantizationCore>(
                         FindObjectsInactive.Include))
                CaptureInternal(obj);
            Debug.Log($"[OriginalStateStore] {_store.Count} objet(s) capturé(s).");
        }

        /// <summary>Capture un objet s'il n'a pas encore été enregistré.</summary>
        public static void Capture(SemantizationCore obj)
        {
            if (!_store.ContainsKey(obj.GetUUID()))
                CaptureInternal(obj);
        }

        private static void CaptureInternal(SemantizationCore obj)
        {
            UnityEngine.Color? color = null;
            if (obj.TryGetComponent(out Renderer r) && r.material != null)
                color = r.material.color;

            _store[obj.GetUUID()] = new ObjectState
            {
                Position = obj.transform.position,
                Rotation = obj.transform.rotation,
                Scale    = obj.transform.localScale,
                Color    = color
            };
            _objectRefs[obj.GetUUID()] = obj;
        }

        public static void RestoreTransform(SemantizationCore obj)
        {
            if (!_store.TryGetValue(obj.GetUUID(), out var s))
            {
                Debug.LogWarning($"[OriginalStateStore] Pas d'état original pour {obj.GetUUID()} — capture de l'état actuel.");
                CaptureInternal(obj);
                return;
            }
            obj.transform.position   = s.Position;
            obj.transform.rotation   = s.Rotation;
            obj.transform.localScale = s.Scale;
        }

        public static void RestoreColor(SemantizationCore obj)
        {
            if (!_store.TryGetValue(obj.GetUUID(), out var s) || s.Color == null) return;
            if (obj.TryGetComponent(out Renderer r) && r.material != null)
                r.material.color = s.Color.Value;
        }

        /// <summary>Remet tous les objets de la scène à leur état initial.</summary>
        public static void RestoreAll()
        {
            foreach (var (_, obj) in _objectRefs)
            {
                if (obj == null) continue; // Détruit définitivement
                obj.gameObject.SetActive(true);
                RestoreTransform(obj);
                RestoreColor(obj);
            }
            Debug.Log("[OriginalStateStore] Scène réinitialisée.");
        }
    }
}
