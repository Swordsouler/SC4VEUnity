// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sven.XsdData
{
    /// <summary>
    /// Represents a date and time in the XSD format
    /// </summary>
    public class XSDDateTimeStamp
    {
        /// <summary>
        /// The date and time in XSD format
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Initializes a new instance of the XSDDateTimeStamp class with the specified date and time.
        /// </summary>
        /// <param name="date">The date and time.</param>
        public XSDDateTimeStamp(DateTime date)
        {
            Value = date.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns the date and time in XSD format
        /// </summary>
        /// <returns>The date and time in XSD format</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Converts the date and time to a literal node.
        /// </summary>
        /// <param name="factory">The node factory.</param>
        /// <returns>The literal node.</returns>
        public ILiteralNode ToLiteralNode(INodeFactory factory)
        {
            return factory.CreateLiteralNode(Value, new Uri(XmlSpecsHelper.XmlSchemaDataTypeDateTime));
        }
    }
}