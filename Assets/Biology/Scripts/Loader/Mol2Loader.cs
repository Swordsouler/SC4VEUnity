using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// This script is used to load a mol2 file and generate the atoms and bonds in the scene
public class Mol2Loader : MonoBehaviour
{
    // Name of the mol2 file to load (without extension)
    public string fileName = "Nicotine";
    public string completePath = "";
    // First atom of the molecule (used to find the cycles at initialization)
    private Atom firstAtom;

    public bool isNetworked = false;

    void Awake()
    {
        /*string mol2Path = Application.streamingAssetsPath + "/Molecules/" + fileName + ".mol2";
        string mol2 = File.ReadAllText(mol2Path);
        Mol2ToMolecule(mol2);*/
    }

    void Start()
    {
        string mol2Path = completePath == "" ? Application.streamingAssetsPath + "/Molecules/" + fileName + ".mol2" : completePath;
        string mol2 = File.ReadAllText(mol2Path);
        Mol2ToMolecule(mol2);

        //Molecule.UpdateCycle(firstAtom);
        StartCoroutine(UpdateRepresenation());

        /*List<Atom> molecule1 = new List<Atom>();
        molecule1 = Molecule.ConnectedComponentFromAtom(firstAtom.connections[2].atom1, firstAtom.connections[2].atom2);
        Debug.Log("Molecule 1: " + molecule1.Count);

        List<Atom> molecule2 = new List<Atom>();
        molecule2 = Molecule.ConnectedComponentFromAtom(firstAtom.connections[2].atom2, firstAtom.connections[2].atom1);
        Debug.Log("Molecule 2: " + molecule2.Count);

        Debug.Log(firstAtom.connections[2].atom1.name + " " + firstAtom.connections[2].atom2.name);*/
    }

    private IEnumerator UpdateRepresenation()
    {
        yield return new WaitForEndOfFrame();
        Molecule.SetMoleculeRepresentation(firstAtom, Representation.Type.Normal, new List<Atom>());
        Destroy(this);
    }

    // Load mol2 file and generate atoms and bonds in the scene
    // mol2: the content of the mol2 file
    private Dictionary<string, Atom> Mol2ToMolecule(string mol2)
    {
        // Parse mol2 file
        string[] lines = mol2.Split(new string[] { "\r" }, StringSplitOptions.None);
        string atomName = "";
        int atomCount = 0;
        int bondCount = 0;
        int atomStart = 0;
        int bondStart = 0;
        // Find the start of the different sections of the mol2 file
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("@<TRIPOS>MOLECULE"))
            {
                atomName = lines[i + 1];
                string[] moleculeData = lines[i + 2].Split(' ');
                atomCount = int.Parse(moleculeData[1]);
                bondCount = int.Parse(moleculeData[2]);
            }
            else if (lines[i].Contains("@<TRIPOS>ATOM"))
            {
                atomStart = i + 1;
            }
            else if (lines[i].Contains("@<TRIPOS>BOND"))
            {
                bondStart = i + 1;
            }
        }

        // Calculate the center of the molecule to center the generated atoms
        Vector3 center = Vector3.zero;
        for (int i = atomStart; i < atomStart + atomCount; i++)
        {
            //split this (multiple spaces)
            string[] atomData = lines[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            float x = float.Parse(atomData[3], CultureInfo.InvariantCulture);
            float y = float.Parse(atomData[4], CultureInfo.InvariantCulture);
            float z = float.Parse(atomData[5], CultureInfo.InvariantCulture);
            center += new Vector3(x, y, z);
        }
        center /= atomCount;

        Dictionary<string, Atom> atoms = new();
        // For each atom, create a gameobject and add it to the molecule
        for (int i = atomStart; i < atomStart + atomCount; i++)
        {
            // Split the line into the different information about the atom
            // 1. Atom ID
            // 2. Atom name
            // 3. X coordinate
            // 4. Y coordinate
            // 5. Z coordinate
            // 6. Atom type
            string[] atomData = lines[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            Vector3 position = new Vector3(
                    float.Parse(atomData[3], CultureInfo.InvariantCulture) - center.x + gameObject.transform.position.x,
                    float.Parse(atomData[4], CultureInfo.InvariantCulture) - center.y + gameObject.transform.position.y,
                    float.Parse(atomData[5], CultureInfo.InvariantCulture) - center.z + gameObject.transform.position.z
                ) * Representation.realWorldAtomSize;
            Atom atom = Atom.Create(
                position,
                gameObject,
                atomData[2],
                atomData[6]
            );
            atoms.Add(atomData[1], atom);
            if (firstAtom == null)
                firstAtom = atom;
        }

        // For each bond, perform the connection between the two atoms
        for (int i = bondStart; i < bondStart + bondCount; i++)
        {
            // Split the line into the different information about the bond
            // 1. Bond ID
            // 2. Atom 1
            // 3. Atom 2
            // 4. Bond type
            string[] bondData = lines[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

            // Perform the connection between the two atoms
            Atom.Connect(atoms[bondData[2]], atoms[bondData[3]], bondData[4]);
        }

        return atoms;
    }
}
