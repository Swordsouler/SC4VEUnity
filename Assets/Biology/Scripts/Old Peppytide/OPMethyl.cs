using System.Collections.Generic;
using UnityEngine;

public class OPMethyl : OPMolecule
{
    private void Awake()
    {
        gameObject.name = "Methyl";

        Atom atomCarbon = Atom.Create(gameObject.transform.position + new Vector3(0, 0, 0) * scale, gameObject, "Carbon", "C");
        Atom atomHydrogen1 = Atom.Create(gameObject.transform.position + new Vector3(0, 0.2f, 0.4f) * scale, gameObject, "Hydrogen1", "H");
        Atom atomHydrogen2 = Atom.Create(gameObject.transform.position + new Vector3(-0.35f, 0.2f, -0.2f) * scale, gameObject, "Hydrogen2", "H");
        Atom atomHydrogen3 = Atom.Create(gameObject.transform.position + new Vector3(0.35f, 0.2f, -0.2f) * scale, gameObject, "Hydrogen3", "H");

        Atom.Connect(atomCarbon, atomHydrogen1, "1", true);
        Atom.Connect(atomCarbon, atomHydrogen2, "1", true);
        Atom.Connect(atomCarbon, atomHydrogen3, "1", true);

        slots.Add(new Slot(atomCarbon, new Vector3(0, -0.5f, 0) * scale));

        atoms.Add(atomCarbon);
        atoms.Add(atomHydrogen1);
        atoms.Add(atomHydrogen2);
        atoms.Add(atomHydrogen3);
    }
}
