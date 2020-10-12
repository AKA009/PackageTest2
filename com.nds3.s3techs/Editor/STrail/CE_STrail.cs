using System.Collections;
using System.Collections.Generic;
using S3Unity.STrail;
using UnityEngine;
using UnityEditor;

namespace EditorExt
{
    [CustomEditor(typeof(STrail))]
    [CanEditMultipleObjects]
    public class CE_STrail: Editor
    {
        private STrail[] targetComponents;

        void OnEnable()
        {
            targetComponents = new STrail[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                targetComponents[i] = targets[i] as STrail;
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (targetComponents == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targetComponents[i] == null)
                {
                    return;
                }
            }

            if (GUI.changed)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    targetComponents[i].InspectorChange();
                }
            }
        }
    }
}

