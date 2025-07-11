// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using DG.Tweening;
using Sven.Content;
using Sven.Context;
using TMPro;
using UnityEngine;

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

        private void Awake()
        {
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
            float xRotation = pointOfView.cameraComponent.transform.localEulerAngles.y + Input.GetAxis("Mouse X") * mouseSensitivity;

            yRotation += Input.GetAxis("Mouse Y") * mouseSensitivity;
            yRotation = Mathf.Clamp(yRotation, -90f, 90f);

            pointOfView.cameraComponent.transform.localEulerAngles = new Vector3(-yRotation, xRotation, 0);

            horizontalInput = Input.GetAxisRaw("Horizontal");
            verticalInput = Input.GetAxisRaw("Vertical");
            jumpInput = Input.GetButton("Jump");

            // crouch
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftShift))
                pointOfView.cameraComponent.transform.DOLocalMove(new Vector3(0, 0.5f, 0), 0.2f);
            else
                pointOfView.cameraComponent.transform.DOLocalMove(new Vector3(0, 1f, 0), 0.2f);

            if (Input.GetKeyDown(KeyCode.F))
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

                if (Input.GetButtonDown("Fire1"))
                {
                    particleSystem.Play();
                }
                else if (Input.GetButtonUp("Fire1"))
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

        private void TryPickupObject()
        {
            foreach (GameObject obj in _focusObjects.Keys)
            {
                if (obj.CompareTag("Pickup"))
                {
                    heldObject = obj;
                    heldObject.GetComponent<Rigidbody>().isKinematic = true;
                    if (heldObject.name.Contains("Interactable"))
                        heldObject.transform.SetParent(heldObject.name.Contains("Pumpkin") ? pumpkinHolder : fruitHolder);
                    else if (heldObject.name.Contains("Spray"))
                        heldObject.transform.SetParent(sprayCanHolder);
                    heldObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    return;
                }
            }
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