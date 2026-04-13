using B83.Win32;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class FileDragAndDrop : MonoBehaviour
{
    List<string> log = new();
    void OnEnable()
    {
        // must be installed on the main thread to get the right thread id.
        UnityDragAndDropHook.InstallHook();
        UnityDragAndDropHook.OnDroppedFiles += OnFiles;
    }
    void OnDisable()
    {
        UnityDragAndDropHook.UninstallHook();
    }

    void OnFiles(List<string> aFiles, POINT aPos)
    {
        // do something with the dropped file names. aPos will contain the 
        // mouse position within the window where the files has been dropped.
        string str = "Dropped " + aFiles.Count + " files at: " + aPos + "\n\t" +
            aFiles.Aggregate((a, b) => a + "\n\t" + b);
        Debug.Log(str);
        foreach (var file in aFiles)
        {
            // ignore if extension is not .mol2
            if (!file.EndsWith(".mol2"))
                continue;

            GameObject go = new();
            go.AddComponent<Mol2Loader>();
            go.GetComponent<Mol2Loader>().completePath = file;

            // find gameobject called "Interactable Area" and make it as parent
            GameObject interactableArea = GameObject.Find("Interactable Area");
            if (interactableArea != null)
                go.transform.parent = interactableArea.transform;
        }
        log.Add(str);
    }
}
