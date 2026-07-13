// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sven.Utils
{
    /// <summary>
    /// ManipulableObject class to control the object.
    /// </summary>
    [DisallowMultipleComponent]
    public class ManipulableObject : MonoBehaviour
    {
        private Vector3 offset;
        private float zCoord;
        private bool isDragging = false;

        void Update()
        {
            // Ignore input if the pointer is over a UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (IsMouseOverObject())
                {
                    isDragging = true;
                    zCoord = Camera.main.WorldToScreenPoint(gameObject.transform.position).z;
                    offset = gameObject.transform.position - GetMouseWorldPos();
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }

            if (isDragging && mouse.leftButton.isPressed)
            {
                transform.position = GetMouseWorldPos() + offset;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (IsMouseOverObject())
                {
                    isDragging = true;
                }
            }

            if (mouse.rightButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }

            if (isDragging && mouse.rightButton.isPressed)
            {
                float rotationSpeed = 10.0f;
                // 0.1f matches the sensitivity of the legacy "Mouse X"/"Mouse Y" axes
                Vector2 mouseDelta = mouse.delta.ReadValue() * 0.1f;
                float h = rotationSpeed * mouseDelta.x;
                float v = rotationSpeed * mouseDelta.y;
                transform.Rotate(Vector3.up, -h, Space.World);
                transform.Rotate(Vector3.right, v, Space.World);
            }

            if (IsMouseOverObject())
            {
                // clamp to one notch so it matches the legacy "Mouse ScrollWheel" step (0.1)
                float scroll = Mathf.Clamp(mouse.scroll.ReadValue().y, -1f, 1f) * 0.1f;
                if (scroll != 0.0f)
                {
                    float scaleSpeed = 1f;
                    transform.localScale += Vector3.one * scroll * scaleSpeed;
                }
            }

            if (GetComponent<Renderer>() != null && IsMouseOverObject())
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.numpad1Key.wasPressedThisFrame) ChangeColor(Color.red);
                    if (keyboard.numpad2Key.wasPressedThisFrame) ChangeColor(Color.green);
                    if (keyboard.numpad3Key.wasPressedThisFrame) ChangeColor(Color.blue);
                    if (keyboard.numpad4Key.wasPressedThisFrame) ChangeColor(Color.yellow);
                    if (keyboard.numpad5Key.wasPressedThisFrame) ChangeColor(Color.cyan);
                    if (keyboard.numpad6Key.wasPressedThisFrame) ChangeColor(Color.magenta);
                    if (keyboard.numpad7Key.wasPressedThisFrame) ChangeColor(Color.white);
                    if (keyboard.numpad8Key.wasPressedThisFrame) ChangeColor(Color.black);
                    if (keyboard.numpad9Key.wasPressedThisFrame) ChangeColor(Color.gray);
                }
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                if (IsMouseOverObject())
                {
                    isDragging = true;
                    zCoord = Camera.main.WorldToScreenPoint(gameObject.transform.position).z;
                    offset = gameObject.transform.position - GetMouseWorldPos();
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (isDragging && Input.GetMouseButton(0))
            {
                transform.position = GetMouseWorldPos() + offset;
            }

            if (Input.GetMouseButtonDown(1))
            {
                if (IsMouseOverObject())
                {
                    isDragging = true;
                }
            }

            if (Input.GetMouseButtonUp(1))
            {
                isDragging = false;
            }

            if (isDragging && Input.GetMouseButton(1))
            {
                float rotationSpeed = 10.0f;
                float h = rotationSpeed * Input.GetAxis("Mouse X");
                float v = rotationSpeed * Input.GetAxis("Mouse Y");
                transform.Rotate(Vector3.up, -h, Space.World);
                transform.Rotate(Vector3.right, v, Space.World);
            }

            if (IsMouseOverObject())
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0.0f)
                {
                    float scaleSpeed = 1f;
                    transform.localScale += Vector3.one * scroll * scaleSpeed;
                }
            }

            if (GetComponent<Renderer>() != null && IsMouseOverObject())
            {
                if (Input.GetKeyDown(KeyCode.Keypad1)) ChangeColor(Color.red);
                if (Input.GetKeyDown(KeyCode.Keypad2)) ChangeColor(Color.green);
                if (Input.GetKeyDown(KeyCode.Keypad3)) ChangeColor(Color.blue);
                if (Input.GetKeyDown(KeyCode.Keypad4)) ChangeColor(Color.yellow);
                if (Input.GetKeyDown(KeyCode.Keypad5)) ChangeColor(Color.cyan);
                if (Input.GetKeyDown(KeyCode.Keypad6)) ChangeColor(Color.magenta);
                if (Input.GetKeyDown(KeyCode.Keypad7)) ChangeColor(Color.white);
                if (Input.GetKeyDown(KeyCode.Keypad8)) ChangeColor(Color.black);
                if (Input.GetKeyDown(KeyCode.Keypad9)) ChangeColor(Color.gray);
            }
#endif
        }

        private void ChangeColor(Color color)
        {
            GetComponent<Renderer>().material.color = color;
        }

        private Vector3 GetMouseWorldPos()
        {
#if ENABLE_INPUT_SYSTEM
            Vector3 mousePoint = Mouse.current.position.ReadValue();
#else
            Vector3 mousePoint = Input.mousePosition;
#endif
            mousePoint.z = zCoord;
            return Camera.main.ScreenToWorldPoint(mousePoint);
        }

        private bool IsMouseOverObject()
        {
#if ENABLE_INPUT_SYSTEM
            if (Camera.main == null || Mouse.current == null) return false;
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
#else
            if (Camera.main == null) return false;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
#endif
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                return hit.collider != null && hit.collider.gameObject == gameObject;
            }
            return false;
        }
    }
}