using System.Collections.Generic;
using UnityEngine;

public class Connection
{
    // Current mode of the connection
    // Free: the connection is free to rotate
    // Locked: the connection is locked and cannot rotate
    // Angle: the connection is free to rotate but atoms will do there best to keep the connection at the specified angle
    public enum ConnectionMode { Free, Locked, Angle }
    public ConnectionMode mode { get; set; } = ConnectionMode.Free;

    // atom1: the first atom of the connection
    // atom2: the second atom of the connection
    // bond: the bond between the two atoms (visual representation of the connection)
    // joint: the joint between the two atoms (physical representation of the connection)
    public Atom atom1;
    public Atom atom2;
    public Bond bond;
    public Joint joint;

    // Current angle of the connection applied to the joint
    // This is only used when the connection is in Angle mode and is used to keep the connection at the specified angle
    public float angle { get; set; } = 0;

    // True if the connection is part of a cycle. Connections in cycles are not allowed to rotate and are Locked
    private bool partOfCycle { get; set; } = false;

    // Create a connection between two atoms
    public Connection(Atom atom1, Atom atom2, Bond bond)
    {
        this.atom1 = atom1;
        this.atom2 = atom2;
        this.bond = bond;
        atom1.connections.Add(this);
        atom2.connections.Add(this);
        bond.connection = this;
        bond.representation = atom1.GetRepresentation().type;
    }

    // Remove the connection to make both atoms not connected anymore
    public void Remove()
    {
        // Remove the connection from the atoms
        atom1.connections.Remove(this);
        atom2.connections.Remove(this);

        // Destroy the representation of the connection (physical and visual)
        GameObject.Destroy(bond.gameObject);
        GameObject.Destroy(joint);

        // Emit force to push the atoms away from each other
        atom1.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        atom2.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        atom1.GetComponent<Rigidbody>().AddForce((atom1.transform.position - atom2.transform.position).normalized * 3 * Molecule.GetMoleculeSize(atom1, new List<Atom>()));
        atom2.GetComponent<Rigidbody>().AddForce((atom2.transform.position - atom1.transform.position).normalized * 3 * Molecule.GetMoleculeSize(atom2, new List<Atom>()));
    }

    // Set the angle of the connection
    // angle: the new angle of the connection
    // the angle stored is always between 0 and 360
    public void SetAngle(float angle)
    {
        //Debug.Log("Connnection::SetAngle");
        // Clamp the angle between 0 and 360 to match the limits of the joint
        if (angle >= 360)
        {
            angle = angle % 360;
        }
        else if (angle < 0)
        {
            angle = angle % 360 + 360;
        }

        //get angle difference
        float difference = angle - this.angle;
        this.angle = angle;
        if (Atom.transformMode) UpdateAnglePosition(difference);
    }

    // Set the mode of the connection (Free, Locked, Angle)
    // mode: the new mode of the connection
    // If the connection is part of a cycle, the mode is forced to Locked
    // We also update the color of the bond
    public void SetMode(ConnectionMode mode)
    {
        this.mode = partOfCycle ? ConnectionMode.Locked : mode;
        bond.UpdateColor();
        joint = joint == null ? AddJoint() : UpdateJoint();
    }

    // Set the connection as part of a cycle (to force it to be Locked)
    public void SetPartOfCycle(bool partOfCycle)
    {
        this.partOfCycle = partOfCycle;
        SetMode(mode);
    }
    public bool IsPartOfCycle()
    {
        return partOfCycle;
    }

    // Perform the rotation in the case :
    // - The connection is in Angle mode
    // - The sender is atom1 or atom2
    public void UpdateAnglePosition(float differenceAngle)
    {

        List<Atom> molecule1 = Molecule.ConnectedComponentFromAtom(atom1, atom2);

        List<Atom> molecule2 = Molecule.ConnectedComponentFromAtom(atom2, atom1);

        List<Atom> smallestMolecule = molecule1.Count < molecule2.Count ? molecule1 : molecule2;
        Atom origin = molecule1.Count < molecule2.Count ? atom2 : atom1;
        Atom destination = molecule1.Count < molecule2.Count ? atom1 : atom2;

        if (mode != ConnectionMode.Angle) return;

        //Molecule.SetMoleculeKinematic();

        GameObject go = new GameObject();
        go.transform.position = origin.transform.position;
        go.transform.parent = origin.transform.parent;
        foreach (Atom atom in smallestMolecule)
        {
            atom.transform.parent = go.transform;
        }
        go.transform.rotation = ComputeRotation(origin, destination, differenceAngle);
        foreach (Atom atom in smallestMolecule)
        {
            atom.transform.parent = go.transform.parent;
        }
        GameObject.Destroy(go);



        /*

        Vector3 direction = (destination.transform.position - origin.transform.position).normalized;
        Quaternion rotation = ComputeRotation(origin, destination, differenceAngle);
        foreach (Atom atom in smallestMolecule)
        {
            atom.gameObject.transform.Rotate(direction, differenceAngle, Space.World);
        }
        //origin.gameObject.transform.rotation = ComputeRotation(origin, destination, angle);*/
    }

    // Perform the rotation of the connection
    private Quaternion ComputeRotation(Atom origin, Atom destination, float differenceAngle)
    {
        //get the direction from origin to destination
        Vector3 direction = (destination.transform.position - origin.transform.position).normalized;

        //get the rotation axis
        return Quaternion.AngleAxis(differenceAngle, direction);
    }

    // Perform the rotation in the case :
    // - The connection is in Angle mode
    // - The sender is atom1 or atom2
    public void UpdateAngleTorque(Atom sender)
    {
        //    Debug.Log("Connection::UpdateAngle");
        if (mode != ConnectionMode.Angle) return;
        if (sender != atom1 && sender != atom2) return;
        PerformTorque(sender, sender == atom1);
    }

    // Perform the rotation of the connection
    // The sender is the atom that is about to be rotated
    // isAtom1 is true if the sender is atom1. We need this to make the rotation work in the other direction
    private void PerformTorque(Atom sender, bool isAtom1)
    {
        //Debug.Log("Connection::PerformRotation " + sender.name + " " + isAtom1);
        // DirectionToAtom is the direction from the sender to the other atom
        // DirectionBond is the direction of the bond
        // DirectionBondBis is the direction of the bond from the other atom
        // Both bond are attached to their parent atom (bond = atom1, bond.bis = atom2)
        // There is maybe a solution more optimised to make this work without bond.bis 
        // but I didn't find it because it need several calculation about direction and rotation of atoms and bonds
        Vector3 directionToAtom = bond.transform.up;
        Vector3 directionBond = bond.GetDirection();
        Vector3 directionBondBis = bond.GetBisDirection();
        // If atom1 is the sender, we need to invert the direction of the bond to make the rotation work in the other direction
        if (isAtom1)
        {
            //Debug.Log("Testing avoid rotation for atom1");
            //return;
            Vector3 temp = directionBond;
            directionBond = directionBondBis;
            directionBondBis = temp;
        }

        // Get the target direction where we want the new angle to be applied
        Quaternion rotation = Quaternion.AngleAxis(angle * (isAtom1 ? -1 : 1), directionToAtom);
        Vector3 targetedDirection = rotation * directionBond;

        // Get the angle to target to make the speed of the rotation more smooth
        float angleToTarget = Vector3.SignedAngle(directionBondBis, targetedDirection, directionToAtom);

        float ratioForces = 1.0f;
        if (sender.connections.Count == 1) ratioForces = 0.1f;
        // Apply torque to the rigidbody to make the directionBondBis == targetedDirection
        sender.GetComponent<Rigidbody>().AddTorque(Vector3.Cross(directionBondBis, targetedDirection) * Mathf.Abs(angleToTarget) * 10f * ratioForces, ForceMode.Acceleration);

        // DEBUG : Draw angle's direction
        Debug.DrawRay(sender.transform.position, directionBondBis * 3, Color.blue);
        Debug.DrawRay(sender.transform.position, targetedDirection * 3, Color.red);
    }

    // Add the configural joint to the atom to represent the connection
    // atom: the atom that the joint will be connected to
    // mode: the type of the connection (Free, Limited, Locked)
    // angle: the angle of the joint (only applied if the mode is Limited)
    private ConfigurableJoint AddJoint()
    {
        ConfigurableJoint configurableJoint = atom1.gameObject.AddComponent<ConfigurableJoint>();
        configurableJoint.connectedBody = atom2.gameObject.GetComponent<Rigidbody>();
        configurableJoint.xMotion = ConfigurableJointMotion.Locked;
        configurableJoint.yMotion = ConfigurableJointMotion.Locked;
        configurableJoint.zMotion = ConfigurableJointMotion.Locked;
        configurableJoint.angularXMotion = mode == Connection.ConnectionMode.Locked ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
        configurableJoint.angularYMotion = ConfigurableJointMotion.Locked;
        configurableJoint.angularZMotion = ConfigurableJointMotion.Locked;
        configurableJoint.lowAngularXLimit = new SoftJointLimit() { limit = 0 };
        configurableJoint.highAngularXLimit = new SoftJointLimit() { limit = 0 };
        configurableJoint.autoConfigureConnectedAnchor = false;

        // We need this otherwise the joint will be different between build and editor
        // I have really no idea why this is happening, and it's seems like a problem on the build side
        // because the logic way is the one used in the editor
        float distanceRatio = 342f * Representation.realWorldAtomSize;
#if UNITY_EDITOR
        distanceRatio = 1.0f;
#endif

        // Setup the anchor of the joint to be at the center of the other atoms
        Vector3 direction = (atom2.transform.position - atom1.transform.position);
        float distance = Vector3.Distance(atom2.transform.position, atom1.transform.position) / distanceRatio;
        Vector3 anchor = direction.normalized * distance;
        anchor = Quaternion.Inverse(atom1.transform.rotation) * anchor;
        configurableJoint.anchor = anchor;

        // Make axis face the parent atom depending on the rotation of the current atom
        // to make the angularXMotion perpendicular to the connection
        configurableJoint.axis = anchor;

        // Make the joint more efficient to reach the target position
        configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        configurableJoint.projectionDistance = 0.01f;
        configurableJoint.projectionAngle = 0.01f;
        configurableJoint.enableCollision = false;

        return configurableJoint;
    }

    // Update the configural joint to the atom to represent the connection
    // atom: the atom that the joint will be connected to
    // mode: the type of the connection (Free, Limited, Locked)
    // angle: the angle of the joint (only applied if the mode is Limited)
    private ConfigurableJoint UpdateJoint()
    {
        // joint instance of ConfigurableJoint
        ConfigurableJoint configurableJoint = joint as ConfigurableJoint;
        configurableJoint.angularXMotion = mode == Connection.ConnectionMode.Locked ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
        return configurableJoint;
    }
}