using System.Collections.Generic;
using UnityEngine;

public class OPCarbonAlpha : OPMolecule
{
    private void Awake()
    {
        gameObject.name = "CarbonAlpha";

        Atom atomCarbon = Atom.Create(gameObject.transform.position + new Vector3(0, 0, 0) * scale, gameObject, "Carbon", "C");
        Atom atomHydrogen = Atom.Create(gameObject.transform.position + new Vector3(0, 0.5f, 0) * scale, gameObject, "Hydrogen", "H");

        Atom.Connect(atomCarbon, atomHydrogen, "1", true);

        slots.Add(new Slot(atomCarbon, new Vector3(0.45f, -0.1f, 0.25f) * scale));
        slots.Add(new Slot(atomCarbon, new Vector3(-0.45f, -0.1f, 0.25f) * scale));
        slots.Add(new Slot(atomCarbon, new Vector3(0, -0.1f, -0.5f) * scale));

        atoms.Add(atomCarbon);
        atoms.Add(atomHydrogen);
    }
}
