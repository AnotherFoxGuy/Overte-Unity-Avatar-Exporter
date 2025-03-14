using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Overte.Exporter.Avatar
{
    public class OverteAvatarDescriptor : MonoBehaviour
    {
        [SerializeField] public string AvatarName;
        
        [SerializeField] public bool OptimizeBlendShapes = true;

        // [SerializeField] private string m_exportPath;

        [SerializeField] public List<OvBlendshape> RemapedBlendShapeList = new();
        [Serializable]
        public class OvBlendshape
        {
            public string from;
            public string to;
            public float multiplier;
        }
    }
}