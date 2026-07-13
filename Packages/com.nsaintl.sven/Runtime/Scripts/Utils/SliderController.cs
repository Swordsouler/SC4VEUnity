// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sven.Utils
{
    [RequireComponent(typeof(Slider))]
    public class SliderController : MonoBehaviour
    {
        [BoxGroup("View")] public TextMeshProUGUI valueText;

        public void Awake()
        {
            Slider slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(OnValueChanged);
            valueText.text = slider.value.ToString();
        }

        private void OnValueChanged(float arg0)
        {
            valueText.text = arg0.ToString();
        }
    }
}