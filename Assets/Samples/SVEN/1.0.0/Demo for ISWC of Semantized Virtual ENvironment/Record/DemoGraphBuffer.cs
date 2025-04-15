using System;
using System.Collections.Generic;
using Sven.Content;
using Sven.GraphManagement;

namespace Sven.Demo
{
    public class DemoGraphBuffer : GraphBuffer
    {
        public new void Awake()
        {
            graphName = DemoManager.graphName;
            endpoint = DemoManager.EndpointUri.ToString() + "/rdf-graphs/service";
            instantPerSecond = DemoManager.semantisationFrequency;
            MapppedComponents.AddComponentDescription(typeof(DemoSprayController), new("Spray",
                new List<Delegate>
                {
                    (Func<DemoSprayController, MapppedComponents.PropertyDescription>)(spray => new MapppedComponents.PropertyDescription("enabled", () => spray.enabled, value => spray.enabled = value.ToString() == "true", 1)),
                }, 1));
            base.Awake();
        }
    }
}
