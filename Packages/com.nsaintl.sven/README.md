# Semanticized Virtual ENvironment (SVEN) for Unity

[![Unity 2022.3+](https://img.shields.io/badge/unity-2022.3%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://gitlab.lisn.upsaclay.fr/nsaintl/SVENUnity/-/blob/main/LICENSE)

SVEN is a Unity package that allows you to semanticize your Virtual Environment (VE) into a Knowledge Graph (KG), making it possible to perform complex queries on your VE during the experience, as well as the ability to replay and analyze it afterwards.

It is based on [Semantic Web](https://en.wikipedia.org/wiki/Semantic_Web) and [Linked Data](https://en.wikipedia.org/wiki/Linked_data) technologies and uses the [SPARQL](https://en.wikipedia.org/wiki/SPARQL) query language to query the data. Using such technology enriches your virtual experience in several ways:

1. **Rule-based reasoning**: By using semantic rules, you can infer new information from existing data, especially if you have a complex domain or incomplete data.
2. **Data interoperability**: By using open standards and linked data, SVEN facilitates the integration and exchange of data between different systems and platforms.
3. **Computation delegation**: SVEN allows you to delegate complex computations to a remote server, saving local resources, which is particularly beneficial for VR/AR applications where resources are often limited.

## System Requirements

Unity **2022.3** or later versions.

## Installation

1. The package is available in **Releases** section. You can download the latest version from [here](https://github.com/Swordsouler/SVENUnity/releases/latest), then import it into your Unity project.

2. You can also install via git url by adding this entry in your **Packages/manifest.json**

```
"com.nsaintl.sven": "https://github.com/Swordsouler/SVENUnity.git#upm"
```

If you don't have openUPM CLI, you will need to install it first by following the instructions [here](https://openupm.com/docs/getting-started.html), or by adding this entry in your **Packages/manifest.json**. This step is important, otherwise the dependencies will not install properly.

```json
{
    "scopedRegistries": [
        {
            "name": "package.openupm.com",
            "url": "https://package.openupm.com",
            "scopes": ["com.dbrizov.naughtyattributes"],
            "overrideBuiltIns": false
        },
        {
            "name": "npm",
            "url": "https://registry.npmjs.org/",
            "scopes": ["com.kyrylokuzyk"]
        }
    ]
}
```

## Support

This project is developed as part of a PhD thesis at the LISN laboratory of the University of Paris-Saclay. For any questions, you can contact [Nicolas Saint-Léger](mailto:nicolas.saint-leger@universite-paris-saclay.fr).

# Overview

## Semanticize your Virtual ENvironment (VE) _(write your knowledge graph)_

To create a semanticized virtual environment, follow these steps:

1. Edit **SVEN Settings** (Window > SVEN Settings) to configure ontologies, semanticize frequency, buffer size, debugging, and endpoint for your knowledge graph.

![Instantiate SVEN Settings](./Assets/com.nsaintl.sven/Documentation~/instantiate_svensettings.png)

![SVEN Settings](./Assets/com.nsaintl.sven/Documentation~/svensettings.png)

| Property                      | Description                                                                                                                         |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| **Use inside**                | Enables or disables the "inside" mode to query instant (refer to Section 5.3 in article).                                           |
| **Show debug logs**           | Displays debug logs in the Unity console for troubleshooting SVEN’s behavior.                                                       |
| **Point of View Debug Color** | Color used to visually display the point-of-view (camera) during debugging Gizmos.                                                  |
| **Pointer Debug Color**       | Color used to render the pointer (raycast or laser beam) during debugging Gizmos.                                                   |
| **Grasp Area Debug Color**    | Color used to visualize the grasping/interactable (sphere) during debugging Gizmos.                                                 |
| **Endpoint URL**              | URL of the GraphDB/triplestore server where the VE sends triples data (e.g. http://localhost:7200/repositories/SVEN).               |
| **Graph Name**                | Name of the graph into which SVEN will insert generated triples. This is basically the name of your experiment (e.g., Experiment1). |
| **Base URI**                  | Base URI used to construct the URIs of entities created by the VE (e.g. https://sven.lisn.upsaclay.fr/ve/Experiment1/).             |
| **Username**                  | Username used for authentication with the triplestore.                                                                              |
| **Password**                  | Password used for authentication with the triplestore.                                                                              |
| **Semanticize Frequency**     | Frequency at which each properties are checked to be semanticize in seconds (e.g., 5 check per seconds).                            |
| **Buffer Size**               | Maximum number of triples the buffer can hold before being flushed to the endpoint triplestore.                                     |
| **Ontologies**                | List of ontologies currently imported into your VE                                                                                  |

2. Add a **Graph Controller** component to your scene (GameObject > SVEN > Graph Controller). This component initialize your knowledge graph with ontologies, prefixes, endpoint that you defined in **SVEN Settings**.

![Instantiate Graph Controller](./Assets/com.nsaintl.sven/Documentation~/instantiate_graphcontroller.png)

3. Add a **Semantization Core** component to each object you want to semanticize (GameObject > Semantic > Semantization Core). This component scans the object's components, allowing you to select which properties to observe. You can also choose whether the component should be dynamically observed or considered static and semanticized only at creation.

![Semantization Core](./Assets/com.nsaintl.sven/Documentation~/semantizationcore.png)

4. Your scene is now ready to be semanticized. You can run your application and observe the data from your virtual environment being saved into the knowledge graph.

## Replay your Semanticized Virtual ENvironment (SVEN) _(read your knowledge graph)_

To replay a semanticized virtual environment, follow these steps:

1. Download the **Replay Semanticized Virtual ENvironment** sample and open the **Replay SVEN** scene.

2. Start the scene, and it will read your knowledge graph to load (depending of your **SVEN Settings**) each instant of your virtual environment. You can navigate through the experience using the **Next Instant** and **Previous Instant** buttons, or by using the slider at the bottom.

![Reader Scene](./Assets/com.nsaintl.sven/Documentation~/reader_scene.png)

## How to support new components ?

To support non-generic components, you can add their descriptions to the `MapppedComponents` dictionary. For example, to add a description for the `Atom` component, you can use the following code:

```csharp
public class Atom : MonoBehaviour, IComponentMapping, ISemanticAnnotation
{
    public static string SemanticTypeName => "sven:Atom";
    [SerializeField] private AtomType type;
    public AtomType Type {
        get => type;
        set => type = value;
    }

    public static ComponentMapping ComponentMapping()
    {
        return new("AtomComponent",
            new List<Delegate>
            {
                (Func<Atom, PropertyDescription>)(atom => new PropertyDescription("enabled", () => atom, value => atom.enabled = value.ToString() == "true", 1)),
                (Func<Atom, PropertyDescription>)(atom => new PropertyDescription("atomType", () => atom, value => atom.type = value.ToString(), 1)),
            });
    }
}
```

This code snippet maps the `Atom` component to its properties, allowing SVEN to semantize and interact with it properly. It also enables custom getters and setters for observing their properties. Make sure to call this at the beginning of the scene to ensure everything works correctly.
