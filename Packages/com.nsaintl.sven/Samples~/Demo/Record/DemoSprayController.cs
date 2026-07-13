// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Demo
{
    public class DemoSprayController : MonoBehaviour, IComponentMapping, ISemanticAnnotation
    {
        public static string SemanticTypeName => "sven:Spray";

        public static ComponentMapping ComponentMapping()
        {
            return new("Spray",
                new List<Delegate>
                {
                    (Func<DemoSprayController, ComponentProperty>)(spray => new ComponentProperty("enabled", () => spray.enabled, value => spray.enabled = value.ToString() == "true", 1)),
                });
        }

        private ParticleSystem ps;

        // these lists are used to contain the particles which match
        // the trigger conditions each frame.
        private readonly List<ParticleSystem.Particle> enter = new();
        private ParticleSystem.ColliderData colliderData = new();

        private void Awake()
        {
            InitializeParticleSystem();
        }

        private void Start()
        {
            if (ps == null) return;
            // add to the trigger event callback list all object in scene with tag "Pickup"
            List<GameObject> pickups = new(GameObject.FindGameObjectsWithTag("Pickup"));
            int i = 0;
            foreach (GameObject pickup in pickups)
            {
                if (pickup.name.Contains("Interactable"))
                {
                    ps.trigger.SetCollider(i, pickup.transform);
                    i++;
                }
            }
        }

        private void InitializeParticleSystem()
        {
            if (!TryGetComponent(out ps)) return;
            var main = ps.main;
            main.startSize = 1f;
            main.loop = true;
            main.startLifetime = 0.5f;
            main.startSpeed = 5f;
            main.startSize = 0.05f;
            main.startRotation = 0f;
            main.startColor = new Color(1f, 1f, 1f, 1f);
            main.maxParticles = 1000;
            main.playOnAwake = false;
            main.startColor = GetComponent<Renderer>().material.color;

            // Emission module
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 150f;

            // Shape module
            var shape = ps.shape;
            shape.enabled = true;
            shape.position = new Vector3(-0.015f, 0.23f, 0f);
            shape.rotation = new Vector3(0f, -90f, 0f);
            shape.scale = new Vector3(0.02f, 0.02f, 0.2f);

            // Color over lifetime module
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 1f, 1f), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            };

            // Size over lifetime module
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;

            // Renderer module
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = true;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            renderer.material = Resources.Load<Material>("Materials/Spray");

        }

        private void OnParticleTrigger()
        {
            // get the particles which matched the trigger conditions this frame
            int numEnter = ps.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter, out colliderData);

            // iterate through the particles which entered the trigger and make them red
            for (int i = 0; i < numEnter; i++)
            {
                int numColliders = colliderData.GetColliderCount(i);
                for (int j = 0; j < numColliders; j++)
                {
                    var collider = colliderData.GetCollider(i, j);
                    if (collider != null)
                    {
                        GameObject obj = collider.gameObject;
                        if (obj.CompareTag("Pickup") && obj.name.Contains("Interactable"))
                        {
                            obj.GetComponent<Renderer>().material = GetComponent<Renderer>().material;
                        }
                    }
                }
            }

            // re-assign the modified particles back into the particle system
            ps.SetTriggerParticles(ParticleSystemTriggerEventType.Enter, enter);
        }
    }
}