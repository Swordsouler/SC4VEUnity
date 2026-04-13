using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
// This script is used to manage 1 atom
public class Atom : MonoBehaviour
{
    // Total number of atoms that has been instantiated
    protected static int atomCount = 0;
    public bool visited = false;

    public static Atom firstAtom;

    // The atom's type (C, N, O, H, etc.)
    public string type { get; set; }
    // The atom model's name (001, 002, 003, etc.)
    public string modelName { get; set; }

    // List of the connections of the atom
    public List<Connection> connections = new();

    // Representation mode of the atom
    private Representation.Type representation { get; set; } = Representation.Type.Normal;

    // The GameObject of the atom which is the model of the atom
    public GameObject model;

    public GameObject twinAtom;

    public static bool transformMode = false;

    private void Awake()
    {
        LoadDefaultMesh();
        LoadDefaultRigidbody();
        GetComponent<Rigidbody>().isKinematic = transformMode;
    }

    #region Load Prefabs, Default Rigidbody and Default Mesh
    // Store and Load prefabs of atom to instantiate them later
    public static GameObject prefabAtom;
    public static GameObject prefabNetworkAtom;
    public static void LoadPrefabs()
    {
        prefabAtom = Resources.Load<GameObject>("Prefabs/Atom");
        prefabNetworkAtom = Resources.Load<GameObject>("Prefabs/Network Atom");
    }

    private class RigidbodyData
    {
        public bool useGravity { get; set; }
        public bool isKinematic { get; set; }
        public float drag { get; set; }
        public float angularDrag { get; set; }
        public float maxAngularVelocity { get; set; }
        public float mass { get; set; }
        public CollisionDetectionMode collisionDetectionMode { get; set; }
    }
    private static RigidbodyData defaultRigidbody;
    private void LoadDefaultRigidbody()
    {
        if (defaultRigidbody != null) return;
        Rigidbody rb = GetComponent<Rigidbody>();
        // Load the default rigidbody
        defaultRigidbody = new RigidbodyData
        {
            useGravity = rb.useGravity,
            isKinematic = rb.isKinematic,
            drag = rb.linearDamping,
            angularDrag = rb.angularDamping,
            maxAngularVelocity = rb.maxAngularVelocity,
            mass = rb.mass,
            collisionDetectionMode = rb.collisionDetectionMode
        };
    }

    private static Mesh defaultMesh;
    private void LoadDefaultMesh()
    {
        if (defaultMesh != null) return;
        // Load the sphere mesh
        defaultMesh = model.GetComponent<MeshFilter>().mesh;
    }
    #endregion


    #region Atom Instantiation, Connection and Disconnection

    // Create an atom
    // position: the position of the atom
    // parent: the parent of the atom
    // name: the name of the atom
    // type: the type of the atom (C, N, O, H, etc.)
    public static Atom Create(Vector3 position, GameObject parent, string name, string type)
    {
        GameObject go = Instantiate(prefabAtom);
        go.transform.parent = parent.transform;
        go.transform.position = position;
        Atom atom = go.GetComponent<Atom>();
        if (firstAtom == null) firstAtom = atom;

        // Setup properties
        atom.modelName = name;
        atom.type = type[0].ToString();
        go.name = atomCount.ToString();
        Molecule.atoms.Add(atomCount, atom);

        atomCount++;


        return atom;
    }

    // Connect two atoms together
    // atom1: the parent atom
    // atom2: the child atom
    // type: the type of the bond (1, 2, 3, ar, am, etc.) (I don't know what it'll be used for yet)
    // isLock: if the connection is locked or not
    // When we connection two atoms, we "disable" the rigidbody until the connection has perform to avoid 
    // weird forces and collision that could make the atom move du ring the setup of the joint
    public static Connection Connect(Atom atom1, Atom atom2, string type, bool isLock = false)
    {
        //if already connected, do nothing
        Connection search = atom1.connections.Find(x => x.atom1 == atom2 || x.atom2 == atom2);
        if (search != null) return search;

        // Lock atoms to avoid weird forces and collision
        atom1.StartCoroutine(atom1.Freeze2Frames());
        atom2.StartCoroutine(atom2.Freeze2Frames());

        // Create a cylinder between the two atoms
        GameObject go;
        go = Instantiate(Bond.prefabBond);
        go.transform.parent = atom1.transform;
        Bond bond = go.GetComponent<Bond>();

        // Setup the connection between the two atoms
        Connection connection = new(atom1, atom2, bond);

        connection.SetPartOfCycle(isLock);

        Molecule.UpdateCycle(atom1);

        return connection;
    }

    // Disconnect two atoms
    // atom1: the parent atom
    // atom2: the child atom
    public static void Disconnect(Atom atom1, Atom atom2)
    {
        Connection connection = atom1.connections.Find(x => x.atom1 == atom2 || x.atom2 == atom2);
        if (connection == null) return;
        bool IsPartOfCycle = connection.IsPartOfCycle();
        connection.Remove();

        // We only need to update the cycle of one of the two atoms in the case the connection removed is part of a cycle
        // because the cycle is the same for both atoms in the case it exists
        if (IsPartOfCycle) Molecule.UpdateCycle(atom1);
    }

    #endregion

    // All the stuff about the visual representation of the atom
    #region Representation

    public void ChangeType(string type)
    {
        this.type = type;
        SetRepresentation(representation);
    }

    public string GetAtomType()
    {
        return type;
    }

    public void ToggleType()
    {
        if (type == "C") ChangeType("N");
        else if (type == "N") ChangeType("O");
        else if (type == "O") ChangeType("H");
        else if (type == "H") ChangeType("C");
    }

    // Return the next representation of the atom
    // We use this method in the case we need to toggle the representation of the atom
    public Representation.Type NextRepresentation()
    {
        //We skip Old Peppytide representation, because for this, the distance between atoms is not the same (smaller)
        Representation.Type newRepresentation = (Representation.Type)(((int)representation + 1) % System.Enum.GetValues(typeof(Representation.Type)).Length);
        if (newRepresentation == Representation.Type.OldPeppytide)
        {
            newRepresentation = (Representation.Type)(((int)representation + 2) % System.Enum.GetValues(typeof(Representation.Type)).Length);
        }
        return newRepresentation;
    }

    public Representation.Data GetRepresentation()
    {
        return Representation.data[representation];
    }

    // Update the representation of the atom according to its type
    public void SetRepresentation(Representation.Type representation)
    {
        this.representation = representation;
        Representation.Data data = Representation.data[representation];
        foreach (Connection connection in connections)
        {
            if (connection.atom1 != this) continue;
            connection.bond.UpdateRepresentation(data);
        }
        // Update the representation of the atom itself with custom model, or a sphere if no model is available
        Mesh mesh = data.meshes.ContainsKey(modelName) ? data.meshes[modelName] : defaultMesh;
        model.GetComponent<MeshFilter>().mesh = mesh;
        model.GetComponent<Collider>().isTrigger = !data.collision;
        model.GetComponent<MeshRenderer>().material = data.material;
        Renderer renderer = model.GetComponent<Renderer>();
        float atomScale = data.scale["atom"];

        if (data.scale.ContainsKey(type))
        {
            renderer.material.color = new Color(data.colors[type].r, data.colors[type].g, data.colors[type].b, data.material.color.a);
            model.transform.localScale = new Vector3(data.scale[type], data.scale[type], data.scale[type]) * atomScale;
        }
        else
        {
            renderer.material.color = new Color(data.colors["default"].r, data.colors["default"].g, data.colors["default"].b, data.material.color.a);
            model.transform.localScale = new Vector3(1f, 1f, 1f) * atomScale;
        }
    }

    #endregion

    // All the stuff related to the physics of the atom
    #region Physics

    public void Lock()
    {
        StartCoroutine(Freeze2Frames());
    }

    // Make the atom not kinematic a few frames
    // This is important in the case you need to perform connection of 2 atoms
    private IEnumerator Freeze2Frames()
    {
        if (GetComponent<Rigidbody>().isKinematic) yield break;
        // Lock physically the atom
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.maxAngularVelocity = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        // We need to wait 2 frames because, otherwise, molecule will make big movement on connection
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        // Unlock physically the atom
        ResetRigidbody();
    }


    // FixedUpdate is used to apply the torque to make the atoms rotate to their target angle
    private void FixedUpdate()
    {
        if (transformMode) return;

        // Update the angle to reach the target angle
        foreach (Connection connection in connections)
        {
            // We ignore the event if the connected atom has only one connection (in case of an hydrogen for example)
            //if (connection.atom1.connections.Count <= 1 || connection.atom2.connections.Count <= 1) continue;
            //if (connection.atom1 == this) continue;
            connection.UpdateAngleTorque(this);
        }

        //Target the position of the twin atom using forces
        if (twinAtom != null)
        {
            Vector3 direction = twinAtom.transform.position - transform.position;
            GetComponent<Rigidbody>().AddForce(direction * force, forceMode);

            /*Transform targetTransform = twinAtom.transform;
            if (targetTransform == null) return;
            Rigidbody rigidbody = gameObject.GetComponent<Rigidbody>();
            Vector3 currentPosition = transform.position;
            Vector3 targetPosition = targetTransform.position;

            float distance = Vector3.Distance(currentPosition, targetPosition);
            if (distance >= 0.01f)
            {
                Vector3 direction = targetPosition - currentPosition;
                rigidbody.velocity = direction * 1000.0f;
            }*/

        }
    }
    public float force = 700f;
    public ForceMode forceMode;

    // Setup the default rigidbody of the atom
    private void ResetRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = defaultRigidbody.useGravity;
        rb.isKinematic = defaultRigidbody.isKinematic;
        rb.linearDamping = defaultRigidbody.drag;
        rb.angularDamping = defaultRigidbody.angularDrag;
        rb.maxAngularVelocity = defaultRigidbody.maxAngularVelocity;
        rb.mass = defaultRigidbody.mass;
        rb.collisionDetectionMode = defaultRigidbody.collisionDetectionMode;
    }

    #endregion
}