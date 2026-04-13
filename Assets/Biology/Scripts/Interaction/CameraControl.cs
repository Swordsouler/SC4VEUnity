using UnityEngine;

// This script to control the camera with the mouse and the keyboard
// - Zoom in and out with the mouse scroll
// - Move the camera with the arrow keys or zqsd
public class CameraControl : MonoBehaviour
{
    private float minZoom = -5f;
    private float maxZoom = -0.2f;

    private void Start()
    {
        GetComponent<Camera>().nearClipPlane = 0.01f;
    }

    private void Update()
    {
        //Zoom in and out with mouse scroll
        float zoom = Input.GetAxis("Mouse ScrollWheel");
        float zoomScale = Camera.main.transform.position.z / -100;
        if (zoom > 0 && Camera.main.transform.position.z < maxZoom)
            MoveCamera(Vector3.forward, 5 * zoomScale);
        if (zoom < 0 && Camera.main.transform.position.z > minZoom)
            MoveCamera(Vector3.back, 5 * zoomScale);

        //Move camera with zqsd keys or arrow keys
        if (Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow))
            MoveCamera(Vector3.up, Camera.main.transform.position.z / -100);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            MoveCamera(Vector3.down, Camera.main.transform.position.z / -100);
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow))
            MoveCamera(Vector3.left, Camera.main.transform.position.z / -100);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            MoveCamera(Vector3.right, Camera.main.transform.position.z / -100);
    }

    //Move camera with arrow keys
    private void MoveCamera(Vector3 direction, float speed = 1)
    {
        Camera.main.transform.Translate(direction * Time.deltaTime * 100 * speed);
    }
}
