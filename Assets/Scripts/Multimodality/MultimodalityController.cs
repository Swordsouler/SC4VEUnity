using Sven.Content;
using Sven.GraphManagement;
using Sven.OwlTime;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sven.Multimodality
{
    public class MultimodalityController : MonoBehaviour
    {
        private static List<SemantizationCore> _selectedObjects = new();
        public static IReadOnlyList<SemantizationCore> SelectedObjects => _selectedObjects;

        public static void AddSelectedObject(SemantizationCore semantizationCore)
        {
            if (!_selectedObjects.Contains(semantizationCore))
            {
                _selectedObjects.Add(semantizationCore);
                SetHighlight(semantizationCore, true);
            }
        }

        public static void AddSelectedObjects(IEnumerable<SemantizationCore> semantizationCores, bool intersection)
        {
            if (_selectedObjects.Count != 0 && intersection)
            {
                // Call removeSelectedObjects to remove objects not in the new selection
                var toRemove = _selectedObjects.Except(semantizationCores).ToList();
                foreach (var semantizationCore in toRemove)
                {
                    RemoveSelectedObject(semantizationCore);
                }
            }
            else
            {
                // Add new objects to the selection
                foreach (var semantizationCore in semantizationCores)
                {
                    AddSelectedObject(semantizationCore);
                }
            }
        }

        public static void RemoveSelectedObject(SemantizationCore semantizationCore)
        {
            if (_selectedObjects.Contains(semantizationCore))
            {
                _selectedObjects.Remove(semantizationCore);
                SetHighlight(semantizationCore, false);
            }
        }

        public static void RemoveSelectedObjects(IEnumerable<SemantizationCore> semantizationCores)
        {
            foreach (var semantizationCore in semantizationCores)
            {
                RemoveSelectedObject(semantizationCore);
            }
        }

        public static void ClearSelectedObjects()
        {
            foreach (var obj in _selectedObjects)
            {
                SetHighlight(obj, false);
            }
            _selectedObjects.Clear();
        }

        private static void SetHighlight(SemantizationCore semantizationCore, bool highlight)
        {
            if (highlight)
            {
                // add outline
                if (semantizationCore.TryGetComponent(out Outline outline))
                    outline.enabled = true;
                else
                    semantizationCore.gameObject.AddComponent<Outline>();
            }
            else
            {
                // remove outline
                if (semantizationCore.TryGetComponent(out Outline outline))
                    outline.enabled = false;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Keypad1))
            {
                Rollback(1f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                Rollback(2f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad3))
            {
                Rollback(3f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                Rollback(4f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad5))
            {
                Rollback(5f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad6))
            {
                Rollback(6f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad7))
            {
                Rollback(7f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad8))
            {
                Rollback(8f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad9))
            {
                Rollback(9f);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                Rollback(10f);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Récupère tous les objets SemantizationCore dans la scène
                var semantizationCores = new List<SemantizationCore>(FindObjectsByType<SemantizationCore>(FindObjectsSortMode.None));
                AddSelectedObjects(semantizationCores, false);
            }

            if (Input.GetKeyDown(KeyCode.LeftAlt))
            {
                // Récupère tous les objets SemantizationCore dans la scène
                var semantizationCores = new List<SemantizationCore>(FindObjectsByType<SemantizationCore>(FindObjectsSortMode.None));
                RemoveSelectedObjects(semantizationCores);
            }
        }

        private async void Rollback(float time)
        {
            // Calculate the new time
            DateTime targetDateTime = DateTime.Now.AddSeconds(-time);
            Instant targetInstant = GraphManager.SearchInstant(targetDateTime);
            Debug.Log($"Rolling back to {targetDateTime} ({targetInstant})");
            await GraphManager.SaveToEndpoint();
            await GraphManager.RetrieveSceneFromEndpoint(targetInstant);
            //await GraphManager.RetrieveSceneFromMemory(targetInstant);
        }
    }
}