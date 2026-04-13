using System.Collections.Generic;
using UnityEngine;

public class OPCTerm : OPMolecule
{
    private void Awake()
    {
        gameObject.name = "CTerm";

        Atom atomCarbon = Atom.Create(gameObject.transform.position + new Vector3(0, 0, 0) * scale, gameObject, "Carbon", "C");
        Atom atomOxygen1 = Atom.Create(gameObject.transform.position + new Vector3(-0.4f, 0.28f, 0) * scale, gameObject, "Oxygen1", "O");
        Atom atomOxygen2 = Atom.Create(gameObject.transform.position + new Vector3(0.4f, 0.28f, 0) * scale, gameObject, "Oxygen2", "O");
        Atom atomHydrogen = Atom.Create(gameObject.transform.position + new Vector3(0.7f, 0.49f, 0) * scale, gameObject, "Hydrogen", "H");

        Atom.Connect(atomCarbon, atomOxygen1, "1", true);
        Atom.Connect(atomCarbon, atomOxygen2, "1", true);
        Atom.Connect(atomOxygen2, atomHydrogen, "1", true);

        slots.Add(new Slot(atomCarbon, new Vector3(0, -0.5f, 0) * scale));

        atoms.Add(atomCarbon);
        atoms.Add(atomOxygen1);
        atoms.Add(atomOxygen2);
        atoms.Add(atomHydrogen);
    }
}
