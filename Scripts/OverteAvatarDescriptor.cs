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

        [SerializeField] private string m_exportPath;

        [SerializeField] public List<OvBlendshape> RemapedBlendShapeList = new();
        [Serializable]
        public class OvBlendshape
        {
            public string from;
            public string to;
            public float multiplier;
            public OvBlendshapes test;
        }

        public enum OvBlendshapes
        {
            //Eye Blendshapes
            EyeBlink_L,
            EyeBlink_R,
            EyeSquint_L,
            EyeSquint_R,
            EyeDown_L,
            EyeDown_R,
            EyeIn_L,
            EyeIn_R,
            EyeOpen_L,
            EyeOpen_R,
            EyeOut_L,
            EyeOut_R,
            EyeUp_L,
            EyeUp_R,
            BrowsD_L,
            BrowsD_R,
            BrowsU_C,
            BrowsU_L,
            BrowsU_R,

            // Jaw Blendshapes
            JawFwd,
            JawLeft,
            JawOpen,
            JawRight,
            MouthLeft,
            MouthRight,
            MouthFrown_L,
            MouthFrown_R,
            MouthSmile_L,
            MouthSmile_R,
            MouthDimple_L,
            MouthDimple_R,

            // Lip Blendshapes
            LipsStretch_L,
            LipsStretch_R,
            LipsUpperClose,
            LipsLowerClose,
            LipsFunnel,
            LipsPucker,
            Puff,

            //Mouth, Cheek and User Blendshapes
            CheekSquint_L,
            CheekSquint_R,
            MouthClose,
            MouthUpperUp_L,
            MouthUpperUp_R,
            MouthLowerDown_L,
            MouthLowerDown_R,
            MouthPress_L,
            MouthPress_R,
            MouthShrugLower,
            MouthShrugUpper,
            NoseSneer_L,
            NoseSneer_R,
            TongueOut,

            // User Defined
            UserBlendshape0,
            UserBlendshape1,
            UserBlendshape2,
            UserBlendshape3,
            UserBlendshape4,
            UserBlendshape5,
            UserBlendshape6,
            UserBlendshape7,
            UserBlendshape8,
            UserBlendshape9,
        }
    }
}