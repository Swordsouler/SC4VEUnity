// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using PrimeTween;
using Sven.Content;
using Sven.Context;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sven.Demo
{
    [RequireComponent(typeof(Rigidbody))]
    public class DemoCharacterController : User
    {
        public bool lockMouse = false;

        [Range(1, 10)]
        public float mouseSensitivity = 5;
        public float moveSpeed = 5f;
        private Rigidbody _rb;

        private float yRotation = 0F;

        private bool isGrounded = true;
        public float jumpForce = 5f;

        private float horizontalInput;
        private float verticalInput;
        private bool jumpInput;

        public Transform fruitHolder;
        public Transform pumpkinHolder;
        public Transform sprayCanHolder;
        private GameObject heldObject;
        public float pickupRange = 2f;

        private Material _focusMaterial;

        private Dictionary<GameObject, Material> _focusObjects = new();
        public List<TextMeshPro> textMeshes = new();
        public static DemoCharacterController _instance;
        public static DemoCharacterController Instance => _instance;

        private void Awake()
        {
            _instance = this;

            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
        }

        public new void Start()
        {
            base.Start();
            if (lockMouse) Cursor.lockState = CursorLockMode.Locked;

            pointOfView.cameraComponent.transform.SetParent(transform, false);
            pointOfView.cameraComponent.transform.localPosition = new Vector3(0, 1f, 0);

            _focusMaterial = Resources.Load<Material>("Materials/Focus");
        }

        public new void Update()
        {
            base.Update();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            // 0.1f matches the sensitivity of the legacy "Mouse X"/"Mouse Y" axes
            Vector2 lookDelta = mouse != null ? mouse.delta.ReadValue() * 0.1f : Vector2.zero;
            float xRotation = pointOfView.cameraComponent.transform.localEulerAngles.y + lookDelta.x * mouseSensitivity;

            yRotation += lookDelta.y * mouseSensitivity;
            yRotation = Mathf.Clamp(yRotation, -90f, 90f);

            pointOfView.cameraComponent.transform.localEulerAngles = new Vector3(-yRotation, xRotation, 0);

            // physical key positions: wKey/aKey = Z/Q on an AZERTY layout
            horizontalInput = keyboard == null ? 0f
                : (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            verticalInput = keyboard == null ? 0f
                : (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            jumpInput = keyboard != null && keyboard.spaceKey.isPressed;

            // crouch
            if (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.leftShiftKey.isPressed))
                pointOfView.cameraComponent.transform.DOLocalMove(new Vector3(0, 0.5f, 0), 0.2f);
            else
                pointOfView.cameraComponent.transform.DOLocalMove(new Vector3(0, 1f, 0), 0.2f);

            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                if (heldObject == null)
                {
                    TryPickupObject();
                }
                else
                {
                    DropObject();
                }
            }

            // enter
            List<GameObject> newFocusObjects = new();
            foreach (Pointer pointer in pointers)
            {
                foreach (SemantizationCore semanticObject in pointer.currentInteractedObjects)
                {
                    GameObject obj = semanticObject.gameObject;
                    if (obj.CompareTag("Pickup") && obj != heldObject) newFocusObjects.Add(obj);
                    break;
                }
            }

            // enter if the object is not already in the list
            foreach (GameObject obj in newFocusObjects)
            {
                if (!_focusObjects.ContainsKey(obj))
                {
                    _focusObjects.Add(obj, obj.GetComponent<Renderer>().material);
                    obj.GetComponent<Renderer>().material = _focusMaterial;

                    TextMeshPro textMesh = obj.GetComponentInChildren<TextMeshPro>();
                    if (textMesh == null)
                    {
                        GameObject textObject = new("PickupText");
                        textObject.transform.SetParent(obj.transform);
                        if (obj.name.Contains("Interactable"))
                            textObject.transform.localPosition = obj.name.Contains("Pumpkin") ? new Vector3(0, 0.6f, 0) : new Vector3(0, 2.5f, 0);
                        else if (obj.name.Contains("Spray"))
                            textObject.transform.localPosition = new Vector3(0, 0.3f, 0);

                        textMesh = textObject.AddComponent<TextMeshPro>();
                        textMesh.text = "<b>F</b> to pickup";
                        if (obj.name.Contains("Interactable") && heldObject != null && heldObject.name.Contains("Spray"))
                            textMesh.text += "\n<b>Left-Click</b> to paint";
                        textMesh.fontSize = 1;
                        textMesh.color = Color.white;
                        textMesh.alignment = TextAlignmentOptions.Center;

                        textMeshes.Add(textMesh);
                    }
                }
            }

            // exit if the object is not in the new list
            foreach (GameObject obj in _focusObjects.Keys)
            {
                if (!newFocusObjects.Contains(obj))
                {
                    if (obj.GetComponent<Renderer>().material.name.Replace(" (Instance)", "") == _focusMaterial.name)
                        obj.GetComponent<Renderer>().material = _focusObjects[obj];

                    TextMeshPro textMesh = obj.GetComponentInChildren<TextMeshPro>();
                    if (textMesh != null)
                    {
                        textMeshes.Remove(textMesh);
                        Destroy(textMesh.gameObject);
                    }

                    _focusObjects.Remove(obj);
                    break;
                }
            }

            // rotate text meshes to face the camera
            foreach (TextMeshPro textMesh in textMeshes)
            {
                if (textMesh != null)
                {
                    textMesh.transform.rotation = Quaternion.LookRotation(
                        textMesh.transform.position - pointOfView.cameraComponent.transform.position
                    );
                }
            }

            //if fire 1
            if (heldObject != null && heldObject.name.Contains("Spray"))
            {
                ParticleSystem particleSystem = heldObject.GetComponent<ParticleSystem>();

                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    particleSystem.Play();
                }
                else if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
                {
                    particleSystem.Stop();
                }
            }
        }

        private void FixedUpdate()
        {
            Vector3 forward = pointOfView.cameraComponent.transform.forward;
            Vector3 right = pointOfView.cameraComponent.transform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (forward * verticalInput + right * horizontalInput).normalized;

            Vector3 targetVelocity = moveDirection * moveSpeed;
            targetVelocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = targetVelocity;

            if (jumpInput && isGrounded)
            {
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, jumpForce, _rb.linearVelocity.z);
                isGrounded = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Ground"))
            {
                isGrounded = true;
            }
        }

        public static void TryPickupObjectStatic()
        {
            if (Instance == null) return;
            Instance.TryPickupObject();
        }

        public static void DropObjectStatic()
        {
            if (Instance == null) return;
            Instance.DropObject();
        }

        public static void PickupObjectStatic(GameObject obj)
        {
            if (Instance == null) return;
            Instance.PickupObject(obj);
        }

        /// <summary>
        /// Renvoie le matériau "réel" de l'objet, en ignorant la surbrillance temporaire : quand
        /// le pointeur survole un objet, son matériau est remplacé par _focusMaterial et l'original
        /// est conservé dans _focusObjects. Hors surbrillance, renvoie le matériau courant.
        /// </summary>
        public static Material GetUnhighlightedMaterial(GameObject obj)
        {
            if (obj == null) return null;
            if (Instance != null && Instance._focusObjects.TryGetValue(obj, out Material original) && original != null)
                return original;
            return obj.TryGetComponent(out Renderer r) ? r.material : null;
        }

        private void TryPickupObject()
        {
            foreach (GameObject obj in _focusObjects.Keys)
            {
                if (obj.CompareTag("Pickup"))
                {
                    PickupObject(obj);
                    return;
                }
            }
        }

        /// <summary>
        /// Place <paramref name="obj"/> dans la main du joueur, exactement comme la touche F :
        /// rend le Rigidbody kinematic, parente l'objet au bon holder selon son nom, puis remet
        /// sa transform locale à zéro. Sans effet si l'objet n'a pas de Rigidbody.
        /// </summary>
        public void PickupObject(GameObject obj)
        {
            if (obj == null || !obj.TryGetComponent(out Rigidbody rb)) return;
            if (heldObject != null) DropObject();   // une seule main : relâcher l'objet courant d'abord
            heldObject = obj;
            rb.isKinematic = true;
            if (heldObject.name.Contains("Interactable"))
                heldObject.transform.SetParent(heldObject.name.Contains("Pumpkin") ? pumpkinHolder : fruitHolder);
            else if (heldObject.name.Contains("Spray"))
                heldObject.transform.SetParent(sprayCanHolder);
            heldObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void DropObject()
        {
            if (heldObject != null)
            {
                if (heldObject.name.Contains("Spray"))
                    heldObject.GetComponent<ParticleSystem>().Stop();
                heldObject.GetComponent<Rigidbody>().isKinematic = false;
                heldObject.transform.SetParent(null);
                heldObject = null;
            }
        }
    }
}