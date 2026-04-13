using UnityEngine;

public class Loading : MonoBehaviour
{
    public bool atomTransformMode = false;
    private void Awake()
    {
        Atom.LoadPrefabs();
        TangibleAtom.LoadPrefabs();
        Bond.LoadPrefabs();
        Representation.LoadMeshes();
        Representation.LoadMaterials();
#if ENABLE_WINMD_SUPPORT
        CoreServices.DiagnosticsSystem.ShowProfiler = false;
#endif
        Atom.transformMode = atomTransformMode;
    }
}
