using UnityEngine;

public class OPAmide : OPMolecule
{
    private void Awake()
    {
        gameObject.name = "Amide";

        Atom atomCarbon = Atom.Create(gameObject.transform.position + new Vector3(0, 0, 0) * scale, gameObject, "Carbon", "C");
        Atom atomOxygen = Atom.Create(gameObject.transform.position + new Vector3(0.4f, 0.25f, 0) * scale, gameObject, "Oxygen", "O");
        Atom atomHydrogen = Atom.Create(gameObject.transform.position + new Vector3(-0.8f, 0.05f, 0) * scale, gameObject, "Hydrogen", "H");
        Atom atomNitrogen = Atom.Create(gameObject.transform.position + new Vector3(-0.4f, 0.25f, 0) * scale, gameObject, "Nitrogen", "N");

        Atom.Connect(atomCarbon, atomOxygen, "1", true);
        Atom.Connect(atomCarbon, atomNitrogen, "1", true);
        Atom.Connect(atomNitrogen, atomHydrogen, "1", true);

        slots.Add(new Slot(atomNitrogen, new Vector3(-0.05f, 0.5f, 0) * scale));
        slots.Add(new Slot(atomCarbon, new Vector3(0, -0.5f, 0) * scale));

        atoms.Add(atomCarbon);
        atoms.Add(atomOxygen);
        atoms.Add(atomHydrogen);
        atoms.Add(atomNitrogen);
    }
}
