using Sven.GraphManagement;
using Sven.OwlTime;
using System;
using UnityEngine;

namespace Sven.Multimodality
{
    public class MultimodalityController : MonoBehaviour
    {
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