using System;
using System.Collections.Generic;
using UnityEngine;

class TangibleData
{
    public int connectionID { get; set; }
    public int atomID { get; set; }
    public int slotID { get; set; }
    public string modelType { get; set; }
    public int atomConnectedTo { get; set; }
    public int slotConnectedTo { get; set; }
    public string modelTypeConnectedTo { get; set; }
    public float angle { get; set; }

    public Type type { get; set; }

    public enum Type
    { CON, DIS, SET, DEL, ADD };
    //public string type { get; set; }
}

public class TangibleGatewaySerial : GatewaySerial
{
    private static Dictionary<int, TangibleAtom> atoms = new();

    //private int action = 0;

    /*private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            switch (action)
            {
                case 0:
                    ConnectAtom(1, 0, "001", 3, 0, "004");
                    ConnectAtom(8, 0, "001", 3, 2, "004");
                    ConnectAtom(4, 0, "006", 3, 1, "004");
                    ConnectAtom(5, 3, "005", 4, 1, "006");
                    ConnectAtom(2, 0, "001", 5, 0, "005");
                    ConnectAtom(6, 0, "001", 5, 1, "005");
                    ConnectAtom(7, 0, "001", 5, 2, "005");
                    break;
                case 1:
                    DisconnectAtom(1, 3);
                    break;
                case 2:
                    DisconnectAtom(8, 3);
                    break;
                case 3:
                    DisconnectAtom(4, 3);
                    break;
                case 4:
                    DisconnectAtom(5, 4);
                    break;
                case 5:
                    DisconnectAtom(2, 5);
                    break;
                case 6:
                    DisconnectAtom(6, 5);
                    break;
                case 7:
                    DisconnectAtom(7, 5);
                    break;
                case 8:
                    DeleteAtom(1);
                    DeleteAtom(2);
                    DeleteAtom(3);
                    DeleteAtom(4);
                    DeleteAtom(5);
                    DeleteAtom(6);
                    DeleteAtom(7);
                    DeleteAtom(8);
                    action = -1;
                    break;
            }
            action++;
        }
    }*/

    // Return the atom corresponding to the id (it instantiate the atom if it doesn't exist)
    private TangibleAtom GetTangibleAtom(int atomID, int slotID, string modelType)
    {
        if (!atoms.ContainsKey(atomID)) AddAtom(atomID, modelType);
        return atoms[atomID];
    }

    // Instantiate an atom with the id and the modelType
    private TangibleAtom AddAtom(int atomID, string modelType)
    {
        // Add atom to the scene with only female type slots except the slotSender
        TangibleAtom atom = TangibleAtom.Create(Vector3.zero, gameObject, modelType);
        atom.LoadTangibleAtom(modelType);
        atoms.Add(atomID, atom);

        return atom;
    }

    //The atomID1 and slotID1 are for the atom that have a male connection, so atomID2 and slotID2 are the female
    private Connection ConnectAtom(int atomID1, int slotID1, string modelType1, int atomID2, int slotID2, string modelType2)
    {
        TangibleAtom atom1 = GetTangibleAtom(atomID1, slotID1, modelType1);
        TangibleAtom atom2 = GetTangibleAtom(atomID2, slotID2, modelType2);

        atom1.slots[slotID1].SetIsMale(true);

        return TangibleAtom.Connect(atom1, atom2, slotID1, slotID2);
    }

    //The atomID1 and slotID1 are for the atom that have a male connection, so atomID2 and slotID2 are the female
    private void DisconnectAtom(int atomID1, int atomID2)
    {
        TangibleAtom.Disconnect(atoms[atomID1], atoms[atomID2]);
    }

    private void DeleteAtom(int id)
    {
        // Delete the atom from the scene
        Destroy(atoms[id].gameObject);
        // Remove the atom from the dictionary
        atoms.Remove(id);
    }

    private void SetAngle(int atomID, int slotID, float angle)
    {
        atoms[atomID].slots[slotID].connection.SetAngle(angle);
    }



    protected override void AnalyzeData(string receivedString)
    {
        try
        {
            Debug.Log(receivedString);
            // Regular expression to remove the \r and \n
            receivedString = System.Text.RegularExpressions.Regex.Replace(receivedString, @"\r\n?|\n", "");

            PerformAction(receivedString.Split(';'));
        }
        catch { }
    }

    // Perform the action corresponding to the data received
    // data : the data received by the tangible molecule
    private void PerformAction(string[] splitStrings)
    {
        TangibleData data = new();
        data.type = (TangibleData.Type)Enum.Parse(typeof(TangibleData.Type), splitStrings[0]);
        //Debug.Log(data.type);
        switch (data.type)
        {
            case TangibleData.Type.CON:
                data.atomID = Int32.Parse(splitStrings[1]);
                data.slotID = Int32.Parse(splitStrings[2]);
                data.modelType = splitStrings[3];
                data.atomConnectedTo = Int32.Parse(splitStrings[4]);
                data.slotConnectedTo = Int32.Parse(splitStrings[5]);
                data.modelTypeConnectedTo = splitStrings[6];
                ConnectAtom(data.atomID, data.slotID, data.modelType, data.atomConnectedTo, data.slotConnectedTo, data.modelTypeConnectedTo);
                break;
            case TangibleData.Type.DIS:
                data.atomID = Int32.Parse(splitStrings[1]);
                data.atomConnectedTo = Int32.Parse(splitStrings[2]);
                DisconnectAtom(data.atomID, data.atomConnectedTo);
                break;
            case TangibleData.Type.SET:
                data.atomID = Int32.Parse(splitStrings[1]);
                data.slotID = Int32.Parse(splitStrings[2]);
                data.angle = float.Parse(splitStrings[3]);
                SetAngle(data.atomID, data.slotID, data.angle);
                break;
            case TangibleData.Type.DEL:
                data.atomID = Int32.Parse(splitStrings[1]);
                DeleteAtom(data.atomID);
                break;
            case TangibleData.Type.ADD:
                data.atomID = Int32.Parse(splitStrings[1]);
                data.modelType = splitStrings[2];
                AddAtom(data.atomID, data.modelType);
                break;
            default:
                Debug.LogError("Unknown data type : " + data.type);
                break;
        }
    }
}