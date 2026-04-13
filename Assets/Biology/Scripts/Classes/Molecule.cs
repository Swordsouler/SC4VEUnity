using System.Collections.Generic;
using UnityEngine;

// This script is used to perform action on a complete molecule
public class Molecule
{
    public static Dictionary<int, Atom> atoms = new Dictionary<int, Atom>();

    public static void SetMoleculeKinematic()
    {
        foreach (Atom atom in atoms.Values)
        {
            atom.GetComponent<Rigidbody>().isKinematic = true;
        }
    }

    public static void ResetVisited()
    {
        foreach (Atom atom in atoms.Values)
        {
            atom.visited = false;
        }
    }

    public static List<Atom> ConnectedComponentFromAtom(Atom atomDroit, Atom atomGauche)
    {
        foreach (Connection connection in atomDroit.connections)
        {
            if (atomGauche == connection.atom1 || atomGauche == connection.atom2)
            {
                if (connection.IsPartOfCycle())
                {
                    Debug.LogError("Trying to get connected component from atom that is part of a cycle");
                    return new List<Atom>();
                }

            }
        }
        ResetVisited();
        atomDroit.visited = true;
        List<Atom> result = new List<Atom>();
        ConnectedComponentFromAtomRecursive(atomGauche, result);
        return result;
    }

    public static List<Atom> GetMolecule(Atom atom)
    {
        ResetVisited();
        List<Atom> result = new List<Atom>();
        ConnectedComponentFromAtomRecursive(atom, result);
        return result;
    }

    private static void ConnectedComponentFromAtomRecursive(Atom atom, List<Atom> connectedComponent)
    {
        if (atom.visited) return;
        connectedComponent.Add(atom);
        atom.visited = true;
        //Debug.Log(atomGauche.name);
        foreach (Connection connection in atom.connections)
        {
            if (connection.atom1 != atom)
                ConnectedComponentFromAtomRecursive(connection.atom1, connectedComponent);
            if (connection.atom2 != atom)
                ConnectedComponentFromAtomRecursive(connection.atom2, connectedComponent);
        }
    }

    // Return the number of atoms in the molecule that contains the atom
    // atom: the atom that will start the search for the molecule
    // visited: list of all the atoms that have already been visited
    public static int GetMoleculeSize(Atom atom, List<Atom> visited)
    {
        if (visited.Contains(atom)) return 0;
        visited.Add(atom);
        int size = 1;
        foreach (Connection connection in atom.connections)
        {
            if (connection.atom1 != atom)
                size += GetMoleculeSize(connection.atom1, visited);
            if (connection.atom2 != atom)
                size += GetMoleculeSize(connection.atom2, visited);
        }
        return size;
    }

    // Find all the cycles in the molecule and Lock the connections that are part of a cycle
    // atomToCheck: the atom that will start the search for cycles (it will check only atoms which are in the same molecule than this atom)
    public static void UpdateCycle(Atom atomToCheck)
    {
        List<Connection> cycle = Molecule.FindCycles(atomToCheck, null, new List<Atom>(), new List<Connection>());
        // We unlock all the connections of the molecule that contains the atomToCheck
        // Then we lock the connections that are part of a cycle
        UnlockConnections(atomToCheck, new List<Atom>());
        foreach (Connection connection in cycle)
        {
            if (cycle.Contains(connection))
            {
                connection.SetPartOfCycle(true);
            }
        }
    }

    // Update the representation of all the atoms in the molecule that contains the atom
    // atom: the atom that will start the search for the molecule
    // representation: the representation to apply to all the atoms of the molecule
    // visited: list of all the atoms that have already been visited
    public static void SetMoleculeRepresentation(Atom atom, Representation.Type representation, List<Atom> visited)
    {
        if (visited.Contains(atom)) return;
        visited.Add(atom);
        foreach (Connection connection in atom.connections)
        {
            if (connection.atom1 != atom)
                SetMoleculeRepresentation(connection.atom1, representation, visited);
            if (connection.atom2 != atom)
                SetMoleculeRepresentation(connection.atom2, representation, visited);
        }
        atom.SetRepresentation(representation);
    }

    // Unlock all the connections of the molecule that contains the atom
    // atom: the atom that will start the search for the molecule
    // visited: list of all the atoms that have already been visited
    private static void UnlockConnections(Atom atom, List<Atom> visited)
    {
        if (visited.Contains(atom)) return;
        visited.Add(atom);
        foreach (Connection connection in atom.connections)
        {
            connection.SetPartOfCycle(false);
            if (connection.atom1 != atom)
                UnlockConnections(connection.atom1, visited);
            if (connection.atom2 != atom)
                UnlockConnections(connection.atom2, visited);
        }
    }

    // Return the list of connection that are part of a cycle in the molecule of the selected atom
    // currentAtom: the current atom we are visiting
    // parentAtom: the atom we came from
    // visited: the list of visited atoms
    // cycle: the list of connections that are part of a cycle
    private static List<Connection> FindCycles(
        Atom currentAtom,
        Atom parentAtom,
        List<Atom> visited,
        List<Connection> cycle
    )
    {
        //Store the visited atom
        visited = new List<Atom>(visited);
        visited.Add(currentAtom);

        foreach (Connection connection in currentAtom.connections)
        {
            Atom atom = connection.atom1 == currentAtom ? connection.atom2 : connection.atom1;
            //Don't go back to the parent atom
            if (atom == parentAtom) continue;
            //If we find an atom that has already been visited, we have found a cycle
            if (visited.Contains(atom))
            {
                //Store all connections that are part of the cycle, by making them a unique in the final list
                int index = visited.IndexOf(atom);
                for (int i = index; i < visited.Count - 1; i++)
                {
                    Connection visitedConnection = visited[i].connections.Find(c => c.atom1 == visited[i] && c.atom2 == visited[i + 1] || c.atom1 == visited[i + 1] && c.atom2 == visited[i]);
                    if (!cycle.Contains(visitedConnection))
                    {
                        cycle.Add(visitedConnection);
                    }
                }
            }
            else
            {
                //Continue the search in the next atom
                cycle = FindCycles(atom, currentAtom, visited, cycle);
            }
        }
        return cycle;
    }
}
