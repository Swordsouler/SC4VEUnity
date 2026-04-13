using Sven.Context;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

//This script is used to perform every mouse interaction with the molecule :
// - Move the molecule
// - Rotate the molecule
// - Toggle a connection mode
// - Rotate a connection 

//To use this script, you need to add physics raycaster to the camera + eventSystem to the scene.
[RequireComponent(typeof(Rigidbody))]
public class MouseInteraction : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private Pointer _pointer;

    //The different interaction type that can be performed with the molecule
    private enum InteractionType { None, MoveAtom, RotateAtom, RotateConnection }
    private InteractionType type { get; set; } = InteractionType.None;

    //The mouse offset between the mouse position and the molecule position when the mouse is clicked
    private Vector3 mouseOffset { get; set; }

    //The movement speed when the atom is moved
    private const float ATOM_MOVE_SPEED = 20.0f;
    //The rotation speed when the atom is rotated
    private const float ATOM_ROTATION_SPEED = 20.0f;
    //The rotation speed when the connection is rotated
    private const float CONNECTION_ROTATION_SPEED = 2.0f;

    //The mouse movement
    private float yaw = 0;
    private float pitch = 0;

    private static MouseInteraction instance;

    //The GameObject that is currently selected (the one which has been clicked)
    private GameObject currentSelection;
    //The GameObject of the atom that is currently locked
    private GameObject currentAtom;
    private Connection currentConnection;


    private GameObject tempParent;

    //Setup the rigidbody
    private void Awake()
    {
        instance = this;
        GetComponent<Rigidbody>().maxAngularVelocity = 1000;
        GetComponent<Rigidbody>().angularDamping = 5f;
        GetComponent<Rigidbody>().linearDamping = 5f;
        GetComponent<Rigidbody>().useGravity = false;
        QualitySettings.vSyncCount = 0;  // VSync must be disabled or the physics will be weird
        Application.targetFrameRate = 60;
    }

    private void Update()
    {
        // if you press R reload the scene
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // Rotate the molecule depends to the mouse position
        // by applying mouse offset and rotation speed to the selected atom
        yaw = Input.GetAxis("Mouse X");
        pitch = Input.GetAxis("Mouse Y");

        if (type == InteractionType.None) return;

        // If all the mouse buttons are released, reset the type of interaction
        if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
        {
            DisableSelection(false);
        }
        else
        {
            if (Input.GetMouseButton(1))
            {
                // center the mouse cursor
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }

    public static void DisableSelection(bool stop)
    {
        if (stop)
        {
            instance.gameObject.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            instance.gameObject.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        }
        instance.type = InteractionType.None;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (instance.currentAtom != null)
        {
            instance.currentAtom.GetComponent<Rigidbody>().isKinematic = Atom.transformMode;
            instance.currentAtom = null;
        }
        if (instance.currentSelection != null)
        {
            instance.currentSelection = null;
        }
        if (instance.currentConnection != null)
        {
            instance.currentConnection = null;
        }

        instance.DestroyTempParent();
    }

    private void FixedUpdate()
    {
        switch (type)
        {
            case InteractionType.MoveAtom:
                MoveAtom();
                break;
            case InteractionType.RotateAtom:
                RotateAtom();
                break;
            case InteractionType.RotateConnection:
                RotateConnection(currentConnection);
                break;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        currentSelection = eventData.pointerCurrentRaycast.gameObject;
        switch (eventData.button)
        {
            case PointerEventData.InputButton.Left:
                if (currentSelection.tag == "Atom" ||
                    (currentSelection.tag == "Connection" && currentSelection.GetComponent<Bond>().connection.mode == Connection.ConnectionMode.Locked))
                {
                    ChangeInteractionType(InteractionType.MoveAtom);
                    FocusSelection(currentSelection.transform.parent.gameObject);
                    SetupMouseOffset();
                }
                else if (currentSelection.tag == "Connection")
                {
                    currentConnection = currentSelection.GetComponent<Bond>().connection;
                    ToggleConnectionMode(currentSelection.GetComponent<Bond>().connection);
                }
                break;
            case PointerEventData.InputButton.Right:
                if (currentSelection.tag == "Atom" ||
                    (currentSelection.tag == "Connection" && currentSelection.GetComponent<Bond>().connection.mode != Connection.ConnectionMode.Angle))
                {
                    ChangeInteractionType(InteractionType.RotateAtom);
                    FocusSelection(currentSelection.transform.parent.gameObject);
                }
                else if (currentSelection.tag == "Connection")
                {
                    currentConnection = currentSelection.GetComponent<Bond>().connection;
                    ChangeInteractionType(InteractionType.RotateConnection);
                }
                break;
            case PointerEventData.InputButton.Middle:
                if (currentSelection.tag == "Atom" || currentSelection.tag == "Connection")
                {
                    if (Input.GetKey(KeyCode.Space) && currentSelection.tag == "Atom")
                    {
                        Atom atom = currentSelection.transform.parent.GetComponent<Atom>();
                        atom.ToggleType();
                    }
                    else
                    {
                        Atom atom = currentSelection.transform.parent.GetComponent<Atom>();
                        Molecule.SetMoleculeRepresentation(atom, atom.NextRepresentation(), new List<Atom>());
                    }
                }
                break;
        }
    }

    private void ChangeInteractionType(InteractionType newType)
    {
        type = newType;
        Debug.Log("type = " + type.ToString());
        Cursor.visible = false;
    }

    // Setup the mouse offset between the mouse position and the molecule position
    private void SetupMouseOffset()
    {
        // Get the offset between the mouse position and the molecule position
        Vector3 newMouseOffset = gameObject.transform.position - Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        newMouseOffset.z = 0;
        mouseOffset = newMouseOffset;
    }

    private void CreateTempParent()
    {
        if (tempParent == null)
        {
            tempParent = new GameObject("tempParent");
            tempParent.transform.position = currentAtom.transform.position;
            tempParent.transform.rotation = currentAtom.transform.rotation;
            tempParent.transform.parent = currentAtom.transform.parent;
            List<Atom> molecule = Molecule.GetMolecule(currentAtom.GetComponent<Atom>());
            foreach (Atom atom in molecule)
            {
                atom.transform.parent = tempParent.transform;
            }
        }
    }

    private void DestroyTempParent()
    {
        if (instance.tempParent != null)
        {
            //each child of tempParent
            List<Transform> childrens = new();
            foreach (Transform child in instance.tempParent.transform)
            {
                childrens.Add(child);
            }
            foreach (Transform child in childrens)
            {
                child.parent = instance.tempParent.transform.parent;
            }
            Destroy(instance.tempParent);
        }
    }

    // Move the molecule to the mouse position
    private void MoveAtom()
    {
        Rigidbody rb = gameObject.GetComponent<Rigidbody>();

        // Move the molecule to the mouse position 
        // by applying mouse offset and movement speed to the selected atom
        Vector3 pos = Input.mousePosition;
        pos.z = -Camera.main.transform.position.z;
        pos = Camera.main.ScreenToWorldPoint(pos);
        if (Atom.transformMode)
        {
            //make the movement of the atom using transform
            CreateTempParent();
            tempParent.transform.position = pos + mouseOffset;
        }
        else
        {
            Vector3 dir = pos - transform.position + mouseOffset;
            rb.linearVelocity = dir * ATOM_MOVE_SPEED;
        }
    }

    // Rotate the molecule around the selected atom
    private void RotateAtom()
    {
        Rigidbody rb = gameObject.GetComponent<Rigidbody>();

        if (Atom.transformMode)
        {
            //make the movement of the atom using transform
            CreateTempParent();
            tempParent.transform.Rotate(new Vector3(pitch, -yaw, 0) * ATOM_ROTATION_SPEED, Space.World);
        }
        else
        {
            Vector3 newRotation = Vector3.zero;
            newRotation.x = pitch;
            newRotation.y = -yaw;
            rb.AddTorque(newRotation * ATOM_ROTATION_SPEED, ForceMode.Force);
        }
    }

    // Toggle the connection mode
    private void ToggleConnectionMode(Connection connection)
    {
        //Get the connection from the selected GameObject and toggle the mode (Free <-> Angle)
        Connection.ConnectionMode mode;
        switch (connection.mode)
        {
            case Connection.ConnectionMode.Free:
                mode = Connection.ConnectionMode.Angle;
                break;
            case Connection.ConnectionMode.Angle:
                mode = Connection.ConnectionMode.Free;
                break;
            default:
                return;
        }
        connection.SetMode(mode);
    }

    // Rotate the connection
    private void RotateConnection(Connection connection)
    {
        Debug.Log("rotating connection between atoms 1: " + connection.atom1 + " and 2: " + connection.atom2);
        connection.SetAngle(connection.angle + yaw * CONNECTION_ROTATION_SPEED);
    }

    // Update the offset of interactable area to make the selected atom the center of it
    // atom: The atom that has to be the center of the interactable area
    private void FocusSelection(GameObject atom)
    {
        currentAtom = atom;

        //Make the selected atom kinematic to make the molecule able to move depending to the selected atom
        currentAtom.GetComponent<Rigidbody>().isKinematic = true;

        //Get the offset between the selected atom and the molecule position
        Vector3 offset = gameObject.transform.position - currentAtom.transform.position;

        transform.position = currentAtom.transform.position;
        // Foreach children add the offset to the position
        foreach (Transform child in gameObject.transform)
        {
            child.position += offset;
        }
    }
}
