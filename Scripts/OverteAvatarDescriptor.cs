using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Overte.Exporter.Avatar
{
    public class OverteAvatarDescriptor : MonoBehaviour
    {
        [SerializeField]
        public string AvatarName;
        
        [SerializeField]
        private string m_exportPath;
    }
}
