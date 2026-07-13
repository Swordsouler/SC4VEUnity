using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("focus sur", "focalise sur", "regarde", "zoom sur", "centre sur",
                       "focus", "focalise", "zoome")]
    [Serializable, CommandDescription("Oriente la caméra principale vers les objets sélectionnés. Paramètres: SelectionParameter.")]
    public class FocusCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            if (objects.Count == 0) return objects;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Focus] Aucune caméra principale (Camera.main) trouvée.");
                return objects;
            }

            // Centroïde des objets sélectionnés
            Vector3 center = Vector3.zero;
            float   radius = 0f;
            foreach (var obj in objects)
            {
                center += obj.transform.position;
                if (obj.TryGetComponent(out Renderer r))
                    radius = Mathf.Max(radius, Vector3.Distance(center / objects.Count, r.bounds.center)
                                                + r.bounds.extents.magnitude);
            }
            center /= objects.Count;
            if (radius < 0.5f) radius = 0.5f;

            Vector3 dir = (cam.transform.position - center).normalized;
            if (dir == Vector3.zero) dir = Vector3.back;

            var prevPos = cam.transform.position;
            var prevRot = cam.transform.rotation;

            cam.transform.position = center + dir * (radius * 3f);
            cam.transform.LookAt(center);

            Debug.Log($"[Focus] Caméra → {center} (distance {radius * 3f:F2})");

            // Note : en VR, Camera.main est contrôlée par le SDK XR ; ce déplacement peut être
            // ignoré selon la configuration du rig. Préférer dans ce cas un FocusTeleport dédié.
            return objects;
        }
    }
}
