using System.Collections;
using UnityEngine;
using static Connection;

//We separate Bond and Connection
//Bond is the visual representation of the connection
//Connection is the data representation of the connection
//So use this to change the visual representation of the connection, and/or the interaction with the bond
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class Bond : MonoBehaviour
{
    private const float CylinderRatio = 2f / 1.5f;
    // The connection between the two atoms
    public Connection connection;
    // The local scale of the bond
    // We need this to make the bond scale depending of the representation
    private Vector3 localScale;

    // bis is the same gameObject as this, but where the parent is the other atom
    // We need this to know where the atom2 is looking at to perform the rotation to target specific angle
    private GameObject bis;

    public Representation.Type representation = Representation.Type.OldPeppytide; // We init with old peppytide representation because we know that the bond are scaled to 0

    public static GameObject prefabBond;
    public static GameObject prefabBondNetwork;
    public static void LoadPrefabs()
    {
        prefabBond = Resources.Load<GameObject>("Prefabs/Bond");
        prefabBondNetwork = Resources.Load<GameObject>("Prefabs/BondNetwork");
    }


    private void Start()
    {
        InitialSetup();
    }

    // Put the bond between the two atoms and make it scale, to make the visual representation of the connection
    // Also, create his bis gameObject to know where the atom2 is looking at
    private void InitialSetup()
    {
        gameObject.name = connection.atom1.name + " <-> " + connection.atom2.name;

        // Set the bond's position, rotation and scale to make the visual representation of the connection
        // a cylinder between the two atoms
        Vector3 dir = connection.atom2.transform.position - connection.atom1.transform.position;
        transform.localPosition = dir * 0.5f;
        localScale = new Vector3(0.25f, dir.magnitude * 0.5f * CylinderRatio, 0.25f);
        transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        UpdateRepresentation(Representation.data[representation]);

        StartCoroutine(SetupBis());
    }

    // If we don't do the wait, the bond bis will not be setup correctly, and then will make weird angle rotation
    private IEnumerator SetupBis()
    {
        yield return new WaitForSeconds(0.1f);
        // Set the bond's color to match the connection mode
        bis = new GameObject
        {
            name = gameObject.name + " bis"
        };
        bis.transform.parent = connection.atom2.transform;
        bis.transform.rotation = gameObject.transform.rotation;
        bis.transform.position = gameObject.transform.position;
    }

    // Update the bond's representation
    public void UpdateRepresentation(Representation.Data data)
    {
        transform.localScale = new Vector3(localScale.x * data.scale["bond"], localScale.y, localScale.z * data.scale["bond"]);
        GetComponent<MeshRenderer>().sharedMaterial = data.material;
        UpdateColor();
    }

    public Color ConnectionMode2Color(ConnectionMode mode)
    {
        return mode switch
        {
            ConnectionMode.Free => new Color(Color.white.r, Color.white.g, Color.white.b, Representation.data[representation].material.color.a),
            ConnectionMode.Locked => new Color(Color.red.r, Color.red.g, Color.red.b, Representation.data[representation].material.color.a),
            ConnectionMode.Angle => new Color(Color.green.r, Color.green.g, Color.green.b, Representation.data[representation].material.color.a),
            _ => new Color(Color.white.r, Color.white.g, Color.white.b, Representation.data[representation].material.color.a),
        };
    }

    // Update the bond's color to match the connection mode
    public void UpdateColor()
    {
        GetComponent<Renderer>().material.color = ConnectionMode2Color(connection.mode);
    }

    // We destroy the bis gameObject when we destroy the bond
    private void OnDestroy()
    {
        Destroy(bis);
    }

    // Return the direction where the bond is looking at
    public Vector3 GetDirection()
    {
        return transform.forward;
    }

    // Return the direction where the bis bond is looking at
    public Vector3 GetBisDirection()
    {
        if (bis == null)
        {
            return Vector3.zero;
        }
        return bis.transform.forward;
    }
}
