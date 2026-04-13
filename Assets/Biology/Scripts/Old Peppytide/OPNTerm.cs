using System.Collections.Generic;
using UnityEngine;

public class OPNTerm : OPMolecule
{
    private void Awake()
    {
        gameObject.name = "NTerm";

        Atom atomNitrogen = Atom.Create(gameObject.transform.position + new Vector3(0, 0, 0) * scale, gameObject, "Nitrogen", "N");
        Atom atomHydrogen1 = Atom.Create(gameObject.transform.position + new Vector3(0.35f, 0.2f, 0) * scale, gameObject, "Oxygen2", "H");
        Atom atomHydrogen2 = Atom.Create(gameObject.transform.position + new Vector3(-0.35f, 0.2f, 0) * scale, gameObject, "Hydrogen", "H");

        Atom.Connect(atomNitrogen, atomHydrogen1, "1", true);
        Atom.Connect(atomNitrogen, atomHydrogen2, "1", true);

        slots.Add(new Slot(atomNitrogen, new Vector3(0, -0.6f, 0) * scale));

        atoms.Add(atomNitrogen);
        atoms.Add(atomHydrogen1);
        atoms.Add(atomHydrogen2);
    }
}
