using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sven.Demo
{
    public class Apple : Fruit, IComponentMapping
    {
        public static ComponentMapping ComponentMapping()
        {
            return new("AppleComponent",
                new List<Delegate>
                {
                    (Func<Apple, ComponentProperty>)(apple => new ComponentProperty("enabled", () => apple.enabled, value => apple.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
