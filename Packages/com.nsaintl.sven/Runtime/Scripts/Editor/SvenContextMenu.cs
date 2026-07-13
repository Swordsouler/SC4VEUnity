// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if UNITY_EDITOR
using Sven.Content;
using Sven.GraphManagement;
using UnityEditor;
using UnityEngine;

namespace Sven.Editor
{
    /// <summary>
    /// Menu to instantiate SVEN components.
    /// </summary>
    public class SvenContextMenu
    {
        /// <summary>
        /// Instantiate the semantization core.
        /// </summary>
        [MenuItem("GameObject/SVEN/Graph Controller", false, 10)]
        private static void InstantiateGraphController()
        {
            GameObject go = new("Graph Controller");
            go.AddComponent<GraphController>();
        }

        /// <summary>
        /// Instantiate the semantization core.
        /// </summary>
        [MenuItem("GameObject/SVEN/Semantization Core", false, 10)]
        private static void InstantiateSemantizationCore()
        {
            GameObject go = new("Semantization Core");
            go.AddComponent<SemantizationCore>();
        }
    }
}
#endif