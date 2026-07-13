// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.Content;
using Sven.GraphManagement;
using Sven.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using VDS.RDF;

namespace Sven.Context
{
    /// <summary>
    /// Represents the user in the scene.
    /// </summary>
    public class User : MonoBehaviour
    {
        /// <summary>
        /// The UUID of the user.
        /// </summary>
        public string UUID => this.GetUUID();

        /// <summary>
        /// The input events of the user.
        /// </summary>
        private Dictionary<string, InputEvent> _inputEvents = new();

        /// <summary>
        /// The point of view of the user.
        /// </summary>
        public PointOfView pointOfView;

        /// <summary>
        /// The set of currently visible objects.
        /// </summary>
        public List<Pointer> pointers;

        /// <summary>
        /// The grasp area of the user.
        /// </summary>
        [SerializeField]
        public GraspArea graspArea;

        /// <summary>
        /// Start is called before the first frame update.
        /// </summary>
        public void Start()
        {
            // Master switch: when SVEN is disabled, the user is not semantized.
            if (!SvenSettings.Enabled) return;
            InitializeAsync();
        }

        /// <summary>
        /// Initializes the user.
        /// </summary>
        private async void InitializeAsync()
        {
            bool isGraphInitialized = false;
            for (int i = 0; i < 5; i++)
            {
                isGraphInitialized = GraphManager.IsGraphInitialized;
                if (isGraphInitialized)
                    break;
                else await Task.Delay(2000);
            }
            if (!isGraphInitialized)
            {
                Debug.LogError("GraphManager is not initialized. Please check your settings.");
                return;
            }
            IUriNode userNode = GraphManager.CreateUriNode(":" + UUID);

            GraphManager.Assert(new Triple(userNode, GraphManager.CreateUriNode("rdf:type"), GraphManager.CreateUriNode("sven:User")));
            if (pointOfView != null) GraphManager.Assert(new Triple(userNode, GraphManager.CreateUriNode("sven:pointOfView"), GraphManager.CreateUriNode(":" + pointOfView.GetComponent<SemantizationCore>().GetUUID())));
            if (graspArea != null) GraphManager.Assert(new Triple(userNode, GraphManager.CreateUriNode("sven:graspArea"), GraphManager.CreateUriNode(":" + graspArea.GetComponent<SemantizationCore>().GetUUID())));
            if (pointers != null)
                foreach (Pointer pointer in pointers)
                    GraphManager.Assert(new Triple(userNode, GraphManager.CreateUriNode("sven:pointer"), GraphManager.CreateUriNode(":" + pointer.GetComponent<SemantizationCore>().GetUUID())));
        }

        public void OnDestroy()
        {
            foreach (KeyValuePair<string, InputEvent> inputEvent in _inputEvents)
            {
                inputEvent.Value.End(GraphManager.CurrentInstant);
                inputEvent.Value.Semanticize();
            }
            this.DestroyUUID();
        }

        public void Update()
        {
            // Master switch: when SVEN is disabled, key-press input events are not recorded.
            if (!SvenSettings.Enabled) return;
            // check for input events (press/release) on the keyboard
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.anyKey.wasPressedThisFrame)
                {
                    foreach (KeyControl keyControl in keyboard.allKeys)
                        if (keyControl.wasPressedThisFrame) StartInputEvent(keyControl.keyCode.ToString());
                }
                foreach (KeyControl keyControl in keyboard.allKeys)
                    if (keyControl.wasReleasedThisFrame) EndInputEvent(keyControl.keyCode.ToString());
            }

            // mouse buttons (legacy KeyCode names kept for continuity of semantized data)
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) StartInputEvent("Mouse0");
                if (mouse.leftButton.wasReleasedThisFrame) EndInputEvent("Mouse0");
                if (mouse.rightButton.wasPressedThisFrame) StartInputEvent("Mouse1");
                if (mouse.rightButton.wasReleasedThisFrame) EndInputEvent("Mouse1");
                if (mouse.middleButton.wasPressedThisFrame) StartInputEvent("Mouse2");
                if (mouse.middleButton.wasReleasedThisFrame) EndInputEvent("Mouse2");
            }
        }

        /// <summary>
        /// Starts recording an input event if it is not already active.
        /// </summary>
        private void StartInputEvent(string key)
        {
            if (_inputEvents.ContainsKey(key)) return;
            if (SvenSettings.Debug) Debug.Log(key + " pressed");
            InputEvent inputEvent = new(this, key);
            inputEvent.Start(GraphManager.CurrentInstant);
            inputEvent.Semanticize();
            _inputEvents.Add(key, inputEvent);
        }

        /// <summary>
        /// Ends a currently recorded input event.
        /// </summary>
        private void EndInputEvent(string key)
        {
            if (!_inputEvents.TryGetValue(key, out InputEvent inputEvent)) return;
            if (SvenSettings.Debug) Debug.Log(key + " released");
            inputEvent.End(GraphManager.CurrentInstant);
            inputEvent.Semanticize();
            _inputEvents.Remove(key);
        }
    }
}