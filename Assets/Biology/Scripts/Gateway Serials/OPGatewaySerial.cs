using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;

class OPData
{
    public enum DataType
    {
        POSITION = 49,
        CONNECT = 25,
        DISCONNECT = 24,
        AVAILABLE = 26,
        IGNORE = 2,
        EMPTY = 0
    };
    public int nodeID { get; set; }
    public int childSensorID { get; set; }
    public int command { get; set; }
    public int ack { get; set; }
    public DataType type { get; set; }
    public int payload { get; set; }
}
public class OPGatewaySerial : GatewaySerial
{
    public static Dictionary<int, OPMolecule> molecules = new Dictionary<int, OPMolecule>();
    public static Dictionary<int, Connection> connections = new Dictionary<int, Connection>();
    public static Dictionary<int, int> connectedTo = new Dictionary<int, int>();

    protected override void AnalyzeData(string receivedString)
    {
        try
        {
            string[] SplitStrings = receivedString.Split(';');
            OPData data = new OPData();
            data.nodeID = Int32.Parse(SplitStrings[0]);
            data.childSensorID = Int32.Parse(SplitStrings[1]);
            data.command = Int32.Parse(SplitStrings[2]);
            data.ack = Int32.Parse(SplitStrings[3]);
            data.type = (OPData.DataType)Int32.Parse(SplitStrings[4]);
            data.payload = Int32.Parse(SplitStrings[5]);
            PerformAction(data);
        }
        catch { }
    }

    // Map the angle received from the tangible molecule to degrees
    // angle : the angle received from the tangible molecule
    private float MapAngle(int angle) // 14 bits to Degrees (0 to 360)
    {
        return -angle * 360f / (16384f);
    }

    // Perform the action corresponding to the data received
    // data : the data received by the tangible molecule
    private void PerformAction(OPData data)
    {
        switch (data.type)
        {
            case OPData.DataType.POSITION:
                float angle = MapAngle(data.payload);
                float offsetAngle = OPMolecule.MOLECULEOFFSET[data.nodeID] + OPMolecule.MOLECULEOFFSET[connectedTo[data.nodeID]] + FixOffset(data.nodeID, connectedTo[data.nodeID]);
                //Debug.Log("(" + data.nodeID + "," + connectedTo[data.nodeID] + ") : " + OPMolecule.MOLECULEOFFSET[data.nodeID] + " + " + OPMolecule.MOLECULEOFFSET[connectedTo[data.nodeID]] + " = " + (angle));
                connections[data.nodeID].SetAngle(angle + offsetAngle);
                //Debug.Log("Set angle of " + data.nodeID + " to " + (angle + offsetAngle));
                break;
            case OPData.DataType.CONNECT:
                //Debug.Log("Connect " + data.nodeID + " to " + data.payload);
                StartCoroutine(Connect(data.nodeID, data.payload));
                break;
            case OPData.DataType.DISCONNECT:
                //Debug.Log("Disconnect " + data.nodeID + " to " + data.payload);
                Disconnect(data.nodeID, data.payload);
                break;
            case OPData.DataType.AVAILABLE:
                getOPMolecule(data.nodeID);
                break;
            case OPData.DataType.IGNORE:
                break;
            case OPData.DataType.EMPTY:
                break;
            default:
                break;
        }
    }

    // Return the molecule corresponding to the id
    // id : the id of connection sended by the tangible molecule
    // return : the molecule corresponding to the id
    // If the molecule doesn't exist, create it
    private OPMolecule getOPMolecule(int id)
    {
        if (molecules.ContainsKey(id)) return molecules[id];

        //Debug.Log("Create molecule " + id);

        GameObject go = new GameObject();
        go.transform.parent = transform;
        if (id < 40)
        {
            OPMolecule molecule = go.AddComponent<OPMethyl>();
            molecules.Add(id, molecule);
        }
        else if (id < 128)
        {
            OPMolecule molecule = go.AddComponent<OPAmide>();
            molecules.Add(id - ((id - 39) % 2), molecule);
            molecules.Add(id - ((id - 39) % 2) + 1, molecule);
        }
        else if (id < 253)
        {
            OPMolecule molecule = go.AddComponent<OPCarbonAlpha>();
            molecules.Add(id - ((id - 128) % 3), molecule);
            molecules.Add(id - ((id - 128) % 3) + 1, molecule);
            molecules.Add(id - ((id - 128) % 3) + 2, molecule);
        }
        else if (id == 253)
        {
            OPMolecule molecule = go.AddComponent<OPNTerm>();
            molecules.Add(id, molecule);
        }
        else if (id == 254)
        {
            OPMolecule molecule = go.AddComponent<OPCTerm>();
            molecules.Add(id, molecule);
        }
        else
        {
            Debug.Log("Unknown molecule id : " + id);
        }

        return molecules[id];
    }

    // Attribute the right slot to each id
    // id : the id of the node of the molecule
    private int getSlot(int id)
    {
        if (id < 40)
        {
            return 0;
        }
        else if (id < 128)
        {
            return ((id - 39) % 2);
        }
        else if (id < 253)
        {
            return ((id - 128) % 3);
        }
        else
        {
            return 0;
        }
    }

    // Disconnect the molecule corresponding to the id
    // nodeID : the id of the molecule to disconnect
    // payload : the id of the other molecule to disconnect
    private void Disconnect(int nodeID, int payload)
    {
        connections.Remove(nodeID);
        OPMolecule.DisconnectOPMolecule(getOPMolecule(nodeID), getOPMolecule(payload), getSlot(nodeID), getSlot(payload));
        connectedTo.Remove(nodeID);
        connectedTo.Remove(payload);
    }

    // We need to wait for the molecule to be created before connecting it
    // That why we connect the molecule in a coroutine after a frame
    // getOPMolecule instantiates the molecule if it doesn't exist
    // nodeID : the id of the molecule to connect
    // payload : the id of the other molecule to connect
    private IEnumerator Connect(int nodeID, int payload)
    {
        OPMolecule molecule1 = getOPMolecule(nodeID);
        OPMolecule molecule2 = getOPMolecule(payload);
        yield return new WaitForEndOfFrame();
        connections.Add(nodeID, OPMolecule.ConnectOPMolecule(molecule1, molecule2, getSlot(nodeID), getSlot(payload)));
        connectedTo.Add(nodeID, payload);
        connectedTo.Add(payload, nodeID);
    }

    // Fix the offset rotation of molecule connection
    // It's important for the 2nd connection of amide
    private float FixOffset(int nodeID, int payload)
    {
        if (nodeID < 40)
        {
            return 0;
        }
        else if (nodeID < 128)
        {
            if (getSlot(nodeID) == 1)
            {
                return 0;
            }
            int slot = getSlot(payload);
            switch (slot)
            {
                case 0:
                    return 120;
                case 1:
                    return 0;
                case 2:
                    return -120;
                default:
                    return 0;
            }
        }
        else
        {
            return 0;
        }
    }
}