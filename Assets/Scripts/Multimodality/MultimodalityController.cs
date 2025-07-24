using Sven.Command;
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
            var semantizationCoreList = semantizationCores.ToList();
            if (_selectedObjects.Count != 0 && intersection)
            {
                // Call removeSelectedObjects to remove objects not in the new selection
                var toRemove = _selectedObjects.Except(semantizationCoreList).ToList();
                Debug.Log($"[MultimodalityController] Number of items to remove: {toRemove.Count}");
                foreach (var semantizationCore in toRemove)
                {
                    RemoveSelectedObject(semantizationCore);
                }
            }
            else
            {
                Debug.Log($"[MultimodalityController] Number of items to add: {semantizationCoreList.Count}");
                foreach (SemantizationCore semantizationCore in semantizationCoreList)
                {
                    if (semantizationCore == null)
                    {
                        Debug.LogWarning("[MultimodalityController] A null SemantizationCore was found in the list and will be skipped.");
                        continue;
                    }
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

        public static void RemoveSelectedObjects(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            Debug.Log($"[MultimodalityController] Number of items to remove: {semantizationCores.Count}");
            foreach (var semantizationCore in semantizationCores)
            {
                RemoveSelectedObject(semantizationCore);
            }
        }

        public static void ClearSelectedObjects()
        {
            Debug.Log($"[MultimodalityController] Number of items to clear: {_selectedObjects.Count}");
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
            if (Input.GetKeyDown(KeyCode.T))
            {
                TestCommandChain();
            }
            /*if (Input.GetKeyDown(KeyCode.Space))
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
            }*/
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

        private CommandChain _commandChain;

        private async void TestCommandChain()
        {
            _commandChain = new CommandChain();
            // now 1 seconds ago
            DateTime dateTime = DateTime.Now.AddSeconds(-1);
            _commandChain.AddCommand(new SelectCommand
            {
                Parameter = new PointOfViewFilter(dateTime)
            });
            _commandChain.AddCommand(new ColorizeCommand
            {
                Parameter = new ColorParameter
                {
                    Red = 1f,
                    Green = 0f,
                    Blue = 0f,
                    Tolerance = 0f
                }
            });
            _commandChain.AddCommand(new UnselectCommand
            {
                Parameter = new AllFilter(dateTime)
            });
            await _commandChain.Execute();
        }
    }
}