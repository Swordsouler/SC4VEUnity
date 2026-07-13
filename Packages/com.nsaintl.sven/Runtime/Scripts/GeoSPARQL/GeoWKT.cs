// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using UnityEngine;
using VDS.RDF;

namespace Sven.GeoData
{
    /// <summary>
    /// Represents a geometry in WKT format
    /// </summary>
    public class GeoWKT
    {
        /// <summary>
        /// The geometry in WKT format
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Initializes a new instance of the GeoWKT class with the specified geometry.
        /// </summary>
        /// <param name="bounds">The bounds.</param>
        public GeoWKT(Bounds bounds)
        {
            Vector3[] corners = new Vector3[30];
            // face 1
            corners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            corners[1] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
            corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
            corners[3] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
            corners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);

            // face 2
            corners[5] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
            corners[6] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
            corners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);
            corners[8] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
            corners[9] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);

            // face 3
            corners[10] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
            corners[11] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
            corners[12] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
            corners[13] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);
            corners[14] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);

            // face 4
            corners[15] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
            corners[16] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            corners[17] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
            corners[18] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
            corners[19] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);

            // face 5
            corners[20] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
            corners[21] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
            corners[22] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);
            corners[23] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
            corners[24] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);

            // face 6
            corners[25] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            corners[26] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
            corners[27] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
            corners[28] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
            corners[29] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);

            string wkt = "MULTIPOLYGON(((";
            for (int i = 0; i < corners.Length; i += 5)
            {
                for (int j = 0; j < 5; j++)
                {
                    wkt += $"{corners[i + j].x.ToString(System.Globalization.CultureInfo.InvariantCulture)} {corners[i + j].y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {corners[i + j].z.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    if (j < 4)
                        wkt += ",";
                }
                if (i < corners.Length - 5)
                    wkt += ")),((";
            }
            wkt += ")))";
            Value = wkt;
            //Value = $"POLYGON(({bounds.min.x.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.min.y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.min.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{bounds.min.x.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.max.y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.max.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{bounds.max.x.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.max.y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.max.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{bounds.max.x.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.min.y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.min.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{bounds.min.x.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.min.y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {bounds.min.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}))";
        }

        /// <summary>
        /// Returns the geometry in WKT format
        /// </summary>
        /// <returns>The geometry in WKT format</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Converts the geometry to a literal node.
        /// </summary>
        /// <param name="factory">The node factory.</param>
        /// <returns>The literal node.</returns>
        public ILiteralNode ToLiteralNode(INodeFactory factory)
        {
            return factory.CreateLiteralNode(Value, new Uri("http://www.opengis.net/ont/geosparql#wktLiteral"));
        }

        /// <summary>
        /// Checks if two GeoWKT objects are equal.
        /// </summary>
        /// <param name="left">The left GeoWKT object.</param>
        /// <param name="right">The right GeoWKT object.</param>
        /// <returns>True if the objects are equal, otherwise false.</returns>
        public static bool operator ==(GeoWKT left, GeoWKT right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <summary>
        /// Checks if two GeoWKT objects are not equal.
        /// </summary>
        /// <param name="left">The left GeoWKT object.</param>
        /// <param name="right">The right GeoWKT object.</param>
        /// <returns>True if the objects are not equal, otherwise false.</returns>
        public static bool operator !=(GeoWKT left, GeoWKT right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Checks if this GeoWKT object is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns>True if the objects are equal, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is GeoWKT other)
            {
                return Value == other.Value;
            }
            return false;
        }

        /// <summary>
        /// Gets the hash code for this GeoWKT object.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}