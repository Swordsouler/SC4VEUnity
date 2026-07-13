using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OPMolecule : MonoBehaviour
{
    // Offset of the molecule
    public readonly static Dictionary<int, float> MOLECULEOFFSET = new Dictionary<int, float>()
    {
        {0, 0f},
		//METHYL
		{1, 0f}, //MOLECULE DON'T EXIST
        {10, -36f}, //MOLECULE NOT WORKING
        {11, 23f}, //DONE
        {12, 25f}, //DONE

		//AMIDE :  N is odd, C is even
		{41, 0f}, {42, 0f}, //MOLECULE DON'T EXIST
        {43, 0f}, {44, 0f}, //MOLECULE DON'T EXIST
        {45, 0f}, {46, 180f}, //MOLECULE DON'T EXIST
        {47, -60f}, {48, -85f}, //DONE

		//CARBONALPHA : N is 128, M is 129, C is 130...
		{128, 0f}, {129, 0f}, {130, 0f}, //MOLECULE DON'T EXIST
        {131, 110f}, {132, -165f}, {133, 15f}, //DONE
        {134, -120f}, {135, 75f}, {136, 30f}, //DONE
		{137, 100f}, {138, 120f}, {139, 85f}, //DONE

		//N term
		{253, 0f}, //DONE

		//C term
		{254, 95f} //DONE
    };

    private static int numberOfMolecule = 0;
    public int id { get; set; }

    //protected static float scale = Representation.data[Representation.Type.Peppytide].scale["atom"];
    protected static float scale = Representation.data[Representation.Type.Peppytide].scale["atom"] / 2.3f;

    // List of Slot for the molecule
    // A Slot is a position where an atom can be connected to the molecule
    public class Slot
    {
        public Vector3 direction { get; set; }
        // The atom connected to the slot
        public Atom atom { get; set; }
        // The offset of the slot from the center of the atom (in local space)
        public Vector3 offsetPosition { get; set; }

        public Slot(Atom atom, Vector3 offsetPosition)
        {
            this.atom = atom;
            this.offsetPosition = offsetPosition;
            this.direction = atom.transform.localPosition - offsetPosition;
        }

        //Give the position of the slot depending of the rotation, the position of the atom and the offset
        public Vector3 GetPosition()
        {
            return atom.transform.position + atom.transform.rotation * offsetPosition;
        }
    }
    [System.NonSerialized] public List<Slot> slots = new List<Slot>();
    public List<Atom> atoms = new();

    private void Start()
    {
        id = numberOfMolecule;
        numberOfMolecule++;
        Molecule.SetMoleculeRepresentation(atoms[0], Representation.Type.OldPeppytide, new List<Atom>());
        Destroy(this);
    }

    // Connect the molecule to another molecule
    // molecule1 : the molecule that will be connected
    // molecule2 : the molecule that will be connected to
    // slot1 : the slot id of molecule1 that will be connected
    // slot2 : the slot id of molecule2 that will be connected
    public static Connection ConnectOPMolecule(OPMolecule molecule1, OPMolecule molecule2, int slot1, int slot2)
    {
        Slot slotMolecule1 = molecule1.slots[slot1];
        Slot slotMolecule2 = molecule2.slots[slot2];
        slotMolecule1.atom.transform.rotation = Quaternion.identity;

        // Position and rotate the molecule 2 to face the molecule 1 through the slot
        slotMolecule2.atom.transform.position = slotMolecule1.GetPosition();
        Vector3 direction = slotMolecule1.atom.transform.position - slotMolecule2.atom.transform.position;
        slotMolecule2.atom.transform.rotation = Quaternion.FromToRotation(-slotMolecule2.direction, direction);

        Connection connection = Atom.Connect(slotMolecule1.atom, slotMolecule2.atom, "1", false);

        connection.SetMode(Connection.ConnectionMode.Angle);

        return connection;
    }

    // Disconnect the molecule from another molecule
    // molecule1 : the molecule that will be disconnected
    // molecule2 : the molecule that will be disconnected from
    // slot1 : the slot id of molecule1 that will be disconnected
    // slot2 : the slot id of molecule2 that will be disconnected
    public static void DisconnectOPMolecule(OPMolecule molecule1, OPMolecule molecule2, int slot1, int slot2)
    {
        Slot slotMolecule1 = molecule1.slots[slot1];
        Slot slotMolecule2 = molecule2.slots[slot2];
        Atom atom1 = slotMolecule1.atom;
        Atom atom2 = slotMolecule2.atom;

        Atom.Disconnect(atom1, atom2);
    }
}
