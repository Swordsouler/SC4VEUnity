// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using UnityEngine;

namespace Sven.Content
{
    /// <summary>
    /// Represents a resource that can be uniquely identified.
    /// </summary>
    public abstract class Resource
    {
        /// <summary>
        /// Generates a unique identifier for the resource.
        /// </summary>
        /// <returns>Unique identifier.</returns>
        private static readonly Dictionary<Resource, string> resourceUUIDs = new();

        /// <summary>
        /// Generates a unique identifier for the resource.
        /// </summary>
        private void GenerateUUID()
        {
            resourceUUIDs[this] = System.Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Destroys the identifier for the resource. WARNING: Not doing this will cause memory leaks.
        /// </summary>
        public void DestroyUUID()
        {
            if (resourceUUIDs.ContainsKey(this))
                resourceUUIDs.Remove(this);
        }

        /// <summary>
        /// Gets the resource identifier for the resource.
        /// </summary>
        /// <returns>Unique identifier.</returns>
        public string GetUUID()
        {
            try
            {
                if (!resourceUUIDs.ContainsKey(this)) GenerateUUID();
                return resourceUUIDs[this];
            }
            catch (KeyNotFoundException e)
            {
                Debug.LogError(e);
                return "";
            }
        }
    }
}