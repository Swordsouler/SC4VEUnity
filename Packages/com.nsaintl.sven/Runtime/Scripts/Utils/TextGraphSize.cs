// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using TMPro;
using UnityEngine;

namespace Sven.Utils
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextGraphSize : MonoBehaviour
    {
        private TextMeshProUGUI tmp;

        void Awake()
        {
            tmp = GetComponent<TextMeshProUGUI>();
        }

        void Update()
        {
            tmp.text = GraphManager.Count.ToString();
        }
    }
}