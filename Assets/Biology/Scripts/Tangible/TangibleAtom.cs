using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// This script is used to represent an atom in the tangible mode (with a physical object)
public class TangibleAtom : Atom
{
    private readonly static string ATOMS_PATH = "Tangible/";
    public float scale;
    [System.NonSerialized] public List<Slot> slots = new List<Slot>();

    // A slot is a way to connect two atoms together
    // To determine the position, we get the center, we give an angle to find the direction of the connection,
    // and we give a distance to find the final position of the connection
    // We also need to know if the connection is male or female (RULE: the connection can occur only between male and female)
    public class Slot
    {
        public TangibleAtom connectedAtom;
        public float scale;
        public GameObject model;
        public bool isMale = false;
        public Vector3 direction;
        public Connection connection;
        public int angle;
        public Slot(Vector3 direction, int angle, bool isMale)
        {
            this.direction = direction;
            this.isMale = isMale;
            this.angle = angle;
        }

        public void SetIsMale(bool isMale)
        {
            this.isMale = isMale;

            if (model.TryGetComponent<MeshFilter>(out MeshFilter meshFilter))
            {
                meshFilter.mesh = Resources.Load<Mesh>("Models/Connections/" + (isMale ? "male" : "female"));
            }
            else
            {
                model.AddComponent<MeshFilter>().mesh = Resources.Load<Mesh>("Models/Connections/" + (isMale ? "male" : "female"));
            }
            model.transform.localPosition = Vector3.zero;
            model.transform.Translate(Vector3.left * (scale / 2) * 0.75f * (isMale ? 1.44f : 1) * Representation.data[Representation.Type.Tangible].scale["atom"]);
            model.name = isMale ? "Male" : "Female";
        }

        public void Disconnect()
        {
            connection = null;
            connectedAtom = null;
        }
    }

    // This class is used to store the data of the json file
    [System.Serializable]
    private class SlotData
    {
        public float directionX;
        public float directionY;
        public float directionZ;
        public int angle; // angle of the slot to match virtual connection with the physical object
    }

    // This class is used to store the data of the json file
    [System.Serializable]
    private class TangibleAtomData
    {
        public string atomType;
        public string modelType;
        public float scale;
        public List<SlotData> slots;
    }


    public static GameObject prefabTangibleAtom;
    public static new void LoadPrefabs()
    {
        prefabTangibleAtom = Resources.Load<GameObject>("Prefabs/Tangible Atom");
    }

    public static TangibleAtom Create(Vector3 position, GameObject parent, string name)
    {
        GameObject go = Instantiate(prefabTangibleAtom);
        go.transform.parent = parent.transform;
        go.transform.position = position;
        TangibleAtom atom = go.GetComponent<TangibleAtom>();

        // Setup properties
        atom.modelName = name;
        go.name = Atom.atomCount.ToString();
        Molecule.atoms.Add(atomCount, atom);

        atomCount++;

        return atom;
    }

    private void Update()
    {
        UpdateSlot(gameObject.GetComponent<Atom>());
    }

    private void InstantiateSlot(Slot slot)
    {
        slot.model = new GameObject();
        slot.model.name = slot.isMale ? "Male" : "Female";
        slot.model.transform.parent = transform;
        slot.model.transform.localPosition = Vector3.zero;
        slot.model.transform.localScale = Vector3.one * Representation.data[Representation.Type.Tangible].scale["atom"];

        //get the rotation of the slot depending on the direction (-right)
        slot.model.transform.rotation = Quaternion.FromToRotation(Vector3.right, -slot.direction);
        //giv the angle to the slot
        slot.model.transform.Rotate(Vector3.right, slot.angle);

        slot.model.tag = "Atom";
        slot.scale = scale;

        slot.SetIsMale(slot.isMale);
        slot.model.AddComponent<MeshRenderer>().material = Resources.Load<Material>("Materials/Default");
        slots.Add(slot);
    }

    // Load the atom from a json file
    // path: the path of the json file
    public void LoadTangibleAtom(string modelName)
    {
        string path = Application.streamingAssetsPath + "/" + ATOMS_PATH + modelName + ".json";
        try
        {
            string data = File.ReadAllText(path);
            TangibleAtomData atomData = JsonUtility.FromJson<TangibleAtomData>(data);
            type = atomData.atomType;
            scale = atomData.scale;
            foreach (SlotData slot in atomData.slots)
            {
                InstantiateSlot(new Slot(new Vector3(slot.directionX, slot.directionY, slot.directionZ), slot.angle, false));
            }
            gameObject.GetComponent<Atom>().SetRepresentation(Representation.Type.Tangible);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
        }
    }

    public void UpdateSlot(Atom atom)
    {
        Representation.Type representation = atom.GetRepresentation().type;
        foreach (Slot slot in slots)
        {
            if (slot.model != null)
            {
                slot.model.SetActive(representation == Representation.Type.Tangible);
                if (slot.connection != null)
                    slot.model.GetComponent<Renderer>().material.color = slot.connection.bond.ConnectionMode2Color(slot.connection.mode);
                else slot.model.GetComponent<Renderer>().material.color = Color.white;
            }
            Debug.DrawLine(slot.model.transform.position, slot.model.transform.position + slot.model.transform.up * 0.1f, Color.red);
        }
    }

    public static Connection Connect(TangibleAtom atomMale, TangibleAtom atomFemale, int slotMale, int slotFemale)
    {
        TangibleAtom atom1 = atomFemale;
        TangibleAtom atom2 = atomMale;

        GameObject molecule1;
        GameObject molecule2;
        List<Atom> moleculeAtoms1 = new List<Atom>();
        List<Atom> moleculeAtoms2 = new List<Atom>();
        if (Atom.transformMode)
        {
            molecule1 = new GameObject();
            molecule2 = new GameObject();

            molecule1.transform.position = atom1.transform.position;
            molecule2.transform.position = atom2.transform.position;
            molecule1.transform.rotation = atom1.transform.rotation;
            molecule2.transform.rotation = atom2.transform.rotation;
            molecule1.transform.parent = atom1.transform.parent;
            molecule2.transform.parent = atom2.transform.parent;
            moleculeAtoms1 = Molecule.GetMolecule(atom1.GetComponent<Atom>());
            moleculeAtoms2 = Molecule.GetMolecule(atom2.GetComponent<Atom>());
            foreach (Atom atom in moleculeAtoms1)
            {
                atom.transform.parent = molecule1.transform;
            }
            foreach (Atom atom in moleculeAtoms2)
            {
                atom.transform.parent = molecule2.transform;
            }
        }
        else
        {
            molecule1 = atom1.gameObject;
            molecule2 = atom2.gameObject;
        }

        int slot1 = slotFemale;
        int slot2 = slotMale;
        Slot slotAtom1 = atom1.slots[slot1];
        Slot slotAtom2 = atom2.slots[slot2];
        // Check if the atoms are already connected
        if (slotAtom1.connectedAtom != null || slotAtom2.connectedAtom != null)
        {
            Debug.Log("Already connected");
            return null;
        }
        // Check if the connection that we are about to do is male + female
        if (slotAtom1.isMale == slotAtom2.isMale)
        {
            Debug.Log("Can't connect male + male or female + female");
            return null;
        }

        slotAtom1.connectedAtom = atom2;
        slotAtom2.connectedAtom = atom1;
        // We know that all GameObjects with TangibleAtom have an Atom component
        Atom a1 = atom1.GetComponent<Atom>();
        Atom a2 = atom2.GetComponent<Atom>();

        MouseInteraction.DisableSelection(true);
        a1.Lock();
        a2.Lock();

        molecule1.transform.rotation = Quaternion.identity;
        molecule2.transform.rotation = Quaternion.identity;

        // Position and rotate the molecule 2 to face the molecule 1 through the slot
        // atom2.transform.position = atom1.transform.position + -slotAtom1.model.transform.right * (((atom1.scale + atom1.scale) / 2) + 0.44f);
        molecule2.transform.position = atom1.transform.position + -slotAtom1.model.transform.right * (((atom1.scale + atom2.scale) / 2) + 0.4f) * Representation.data[Representation.Type.Tangible].scale["atom"];

        // Make the atom 2 face the atom 1
        // depending on the slotAtom1.model.transform.right of both slot atoms
        // we need to rotate the atom 2 to face the atom 1
        //atom2.transform.rotation = Quaternion.LookRotation(slotAtom1.model.transform.forward, slotAtom2.model.transform.forward);

        Vector3 direction = atom1.transform.position - slotAtom1.model.transform.position;
        Vector3 direction2 = atom2.transform.position - slotAtom2.model.transform.position;
        molecule2.transform.rotation = Quaternion.FromToRotation(-direction2, direction);
        // align slot transform up of both slot atoms
        float angleDifference = Vector3.Angle(slotAtom1.model.transform.up, slotAtom2.model.transform.up);
        Debug.Log(slotAtom1.model.transform.up + " " + slotAtom2.model.transform.up + " " + angleDifference);
        // slotAtom2.model.transform.right = l'axe qui va de atom2 à atom1 (direction du slot)
        molecule2.transform.RotateAround(molecule2.transform.position, slotAtom2.model.transform.right, angleDifference);


        Connection connection = Atom.Connect(a1, a2, "1", false);

        slotAtom1.connection = connection;
        slotAtom2.connection = connection;
        connection.SetMode(Connection.ConnectionMode.Angle);

        if (Atom.transformMode)
        {
            foreach (Atom atom in moleculeAtoms1)
            {
                atom.transform.parent = molecule1.transform.parent;
            }
            foreach (Atom atom in moleculeAtoms2)
            {
                atom.transform.parent = molecule2.transform.parent;
            }
            GameObject.Destroy(molecule1);
            GameObject.Destroy(molecule2);
        }


        return connection;
    }

    public static void Disconnect(Slot slot1, Slot slot2)
    {
        // Check if the atoms are still connected
        if (slot1.connectedAtom == null || slot2.connectedAtom == null)
        {
            Debug.Log("Already disconnected");
            return;
        }

        slot1.connection.Remove();
        slot1.Disconnect();
        slot2.Disconnect();
    }

    public static void Disconnect(TangibleAtom atom1, TangibleAtom atom2)
    {
        int slotID1 = -1;
        int slotID2 = -1;
        foreach (Slot slot in atom1.slots)
        {
            if (slot.connectedAtom == atom2)
            {
                slotID1 = atom1.slots.IndexOf(slot);
                break;
            }
        }
        foreach (Slot slot in atom2.slots)
        {
            if (slot.connectedAtom == atom1)
            {
                slotID2 = atom2.slots.IndexOf(slot);
                break;
            }
        }

        Slot slot1 = atom1.slots[slotID1];
        Slot slot2 = atom2.slots[slotID2];

        Disconnect(slot1, slot2);
    }
}
