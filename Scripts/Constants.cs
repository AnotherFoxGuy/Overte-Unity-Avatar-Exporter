//  Constants.cs
//
//  Created by Edgar on 21-2-2024
//  Copyright 2024 Overte e.V.
//
//  Distributed under the Apache License, Version 2.0.
//  See the accompanying file LICENSE or http://www.apache.org/licenses/LICENSE-2.0.html

using System.Collections.Generic;
using UnityEngine;

namespace Overte.Exporter.Avatar
{
    public static class Constants
    {
        // update version number for every PR that changes this file, also set updated version in README file
        public const string EXPORTER_VERSION = "1.0.0";

        public const float HIPS_MIN_Y_PERCENT_OF_HEIGHT = 0.03f;
        public const float BELOW_GROUND_THRESHOLD_PERCENT_OF_HEIGHT = -0.15f;
        public const float HIPS_SPINE_CHEST_MIN_SEPARATION = 0.001f;
        public const int MAXIMUM_USER_BONE_COUNT = 256;
        public const string EMPTY_WARNING_TEXT = "None";
        public const string TEXTURES_DIRECTORY = "textures";
        public const string DEFAULT_MATERIAL_NAME = "No Name";
        public const string HEIGHT_REFERENCE_PREFAB_GUID = "60f00688eb8a9dd55b72d8a31d283a6b";
        public static readonly Vector3 PREVIEW_CAMERA_PIVOT = new Vector3(0.0f, 1.755f, 0.0f);
        public static readonly Vector3 PREVIEW_CAMERA_DIRECTION = new Vector3(0.0f, 0.0f, -1.0f);

        // TODO: use regex
        public static readonly string[] RECOMMENDED_UNITY_VERSIONS = new string[] {
            "2019.4.31f1", //Version currently used by VRChat
            "2021.3.23f1" //Version currently used by ChilloutVR
        };

        public static readonly Dictionary<string, string> HUMANOID_TO_OVERTE_JOINT_NAME = new Dictionary<string, string> {
            {"Chest", "Spine1"},
            {"Head", "Head"},
            {"Hips", "Hips"},
            {"Left Index Distal", "LeftHandIndex3"},
            {"Left Index Intermediate", "LeftHandIndex2"},
            {"Left Index Proximal", "LeftHandIndex1"},
            {"Left Little Distal", "LeftHandPinky3"},
            {"Left Little Intermediate", "LeftHandPinky2"},
            {"Left Little Proximal", "LeftHandPinky1"},
            {"Left Middle Distal", "LeftHandMiddle3"},
            {"Left Middle Intermediate", "LeftHandMiddle2"},
            {"Left Middle Proximal", "LeftHandMiddle1"},
            {"Left Ring Distal", "LeftHandRing3"},
            {"Left Ring Intermediate", "LeftHandRing2"},
            {"Left Ring Proximal", "LeftHandRing1"},
            {"Left Thumb Distal", "LeftHandThumb3"},
            {"Left Thumb Intermediate", "LeftHandThumb2"},
            {"Left Thumb Proximal", "LeftHandThumb1"},
            {"LeftEye", "LeftEye"},
            {"LeftFoot", "LeftFoot"},
            {"LeftHand", "LeftHand"},
            {"LeftLowerArm", "LeftForeArm"},
            {"LeftLowerLeg", "LeftLeg"},
            {"LeftShoulder", "LeftShoulder"},
            {"LeftToes", "LeftToeBase"},
            {"LeftUpperArm", "LeftArm"},
            {"LeftUpperLeg", "LeftUpLeg"},
            {"Neck", "Neck"},
            {"Right Index Distal", "RightHandIndex3"},
            {"Right Index Intermediate", "RightHandIndex2"},
            {"Right Index Proximal", "RightHandIndex1"},
            {"Right Little Distal", "RightHandPinky3"},
            {"Right Little Intermediate", "RightHandPinky2"},
            {"Right Little Proximal", "RightHandPinky1"},
            {"Right Middle Distal", "RightHandMiddle3"},
            {"Right Middle Intermediate", "RightHandMiddle2"},
            {"Right Middle Proximal", "RightHandMiddle1"},
            {"Right Ring Distal", "RightHandRing3"},
            {"Right Ring Intermediate", "RightHandRing2"},
            {"Right Ring Proximal", "RightHandRing1"},
            {"Right Thumb Distal", "RightHandThumb3"},
            {"Right Thumb Intermediate", "RightHandThumb2"},
            {"Right Thumb Proximal", "RightHandThumb1"},
            {"RightEye", "RightEye"},
            {"RightFoot", "RightFoot"},
            {"RightHand", "RightHand"},
            {"RightLowerArm", "RightForeArm"},
            {"RightLowerLeg", "RightLeg"},
            {"RightShoulder", "RightShoulder"},
            {"RightToes", "RightToeBase"},
            {"RightUpperArm", "RightArm"},
            {"RightUpperLeg", "RightUpLeg"},
            {"Spine", "Spine"},
            {"UpperChest", "Spine2"},
        };

        // absolute reference rotations for each Humanoid bone using Artemis fbx in Unity 2018.2.12f1
        public static readonly Dictionary<string, Quaternion> REFERENCE_ROTATIONS = new Dictionary<string, Quaternion> {
            {"Chest", new Quaternion(-0.0824653f, 1.25274e-7f, -6.75759e-6f, 0.996594f)},
            {"Head", new Quaternion(-2.509889e-9f, -3.379446e-12f, 2.306033e-13f, 1f)},
            {"Hips", new Quaternion(-3.043941e-10f, -1.573706e-7f, 5.112975e-6f, 1f)},
            {"Left Index Distal", new Quaternion(-0.5086057f, 0.4908088f, -0.4912299f, -0.5090388f)},
            {"Left Index Intermediate", new Quaternion(-0.4934928f, 0.5062312f, -0.5064303f, -0.4936835f)},
            {"Left Index Proximal", new Quaternion(-0.4986293f, 0.5017503f, -0.5013659f, -0.4982448f)},
            {"Left Little Distal", new Quaternion(-0.490056f, 0.5143053f, -0.5095307f, -0.4855038f)},
            {"Left Little Intermediate", new Quaternion(-0.5083722f, 0.4954255f, -0.4915887f, -0.5044324f)},
            {"Left Little Proximal", new Quaternion(-0.5062528f, 0.497324f, -0.4937346f, -0.5025966f)},
            {"Left Middle Distal", new Quaternion(-0.4871885f, 0.5123404f, -0.5125002f, -0.4873383f)},
            {"Left Middle Intermediate", new Quaternion(-0.5171652f, 0.4827828f, -0.4822642f, -0.5166069f)},
            {"Left Middle Proximal", new Quaternion(-0.4955998f, 0.5041052f, -0.5043675f, -0.4958555f)},
            {"Left Ring Distal", new Quaternion(-0.4936301f, 0.5097645f, -0.5061787f, -0.4901562f)},
            {"Left Ring Intermediate", new Quaternion(-0.5089865f, 0.4943658f, -0.4909532f, -0.5054707f)},
            {"Left Ring Proximal", new Quaternion(-0.5020972f, 0.5005084f, -0.4979034f, -0.4994819f)},
            {"Left Thumb Distal", new Quaternion(-0.6617184f, 0.2884935f, -0.3604706f, -0.5907297f)},
            {"Left Thumb Intermediate", new Quaternion(-0.6935627f, 0.1995147f, -0.2805665f, -0.6328092f)},
            {"Left Thumb Proximal", new Quaternion(-0.6663674f, 0.278572f, -0.3507071f, -0.5961183f)},
            {"LeftEye", new Quaternion(-2.509889e-9f, -3.379446e-12f, 2.306033e-13f, 1f)},
            {"LeftFoot", new Quaternion(0.009215056f, 0.3612514f, 0.9323555f, -0.01121602f)},
            {"LeftHand", new Quaternion(-0.4797408f, 0.5195366f, -0.5279632f, -0.4703038f)},
            {"LeftLowerArm", new Quaternion(-0.4594738f, 0.4594729f, -0.5374805f, -0.5374788f)},
            {"LeftLowerLeg", new Quaternion(-0.0005380471f, -0.03154583f, 0.9994993f, 0.002378627f)},
            {"LeftShoulder", new Quaternion(-0.3840606f, 0.525857f, -0.5957767f, -0.47013f)},
            {"LeftToes", new Quaternion(-0.0002536641f, 0.7113448f, 0.7027079f, -0.01379319f)},
            {"LeftUpperArm", new Quaternion(-0.4591927f, 0.4591916f, -0.5377204f, -0.5377193f)},
            {"LeftUpperLeg", new Quaternion(-0.0006682819f, 0.0006864658f, 0.9999968f, -0.002333928f)},
            {"Neck", new Quaternion(-2.509889e-9f, -3.379446e-12f, 2.306033e-13f, 1f)},
            {"Right Index Distal", new Quaternion(0.5083892f, 0.4911618f, -0.4914584f, 0.5086939f)},
            {"Right Index Intermediate", new Quaternion(0.4931984f, 0.5065879f, -0.5067145f, 0.4933202f)},
            {"Right Index Proximal", new Quaternion(0.4991491f, 0.5012957f, -0.5008481f, 0.4987026f)},
            {"Right Little Distal", new Quaternion(0.4890696f, 0.5154139f, -0.5104482f, 0.4843578f)},
            {"Right Little Intermediate", new Quaternion(0.5084175f, 0.495413f, -0.4915423f, 0.5044444f)},
            {"Right Little Proximal", new Quaternion(0.5069782f, 0.4965974f, -0.4930001f, 0.5033045f)},
            {"Right Middle Distal", new Quaternion(0.4867662f, 0.5129694f, -0.5128888f, 0.4866894f)},
            {"Right Middle Intermediate", new Quaternion(0.5167004f, 0.4833596f, -0.4827653f, 0.5160643f)},
            {"Right Middle Proximal", new Quaternion(0.4965845f, 0.5031784f, -0.5033959f, 0.4967981f)},
            {"Right Ring Distal", new Quaternion(0.4933217f, 0.5102056f, -0.5064691f, 0.4897075f)},
            {"Right Ring Intermediate", new Quaternion(0.5085972f, 0.494844f, -0.4913519f, 0.505007f)},
            {"Right Ring Proximal", new Quaternion(0.502959f, 0.4996676f, -0.4970418f, 0.5003144f)},
            {"Right Thumb Distal", new Quaternion(0.6611374f, 0.2896575f, -0.3616535f, 0.5900872f)},
            {"Right Thumb Intermediate", new Quaternion(0.6937408f, 0.1986776f, -0.279922f, 0.6331626f)},
            {"Right Thumb Proximal", new Quaternion(0.6664271f, 0.2783172f, -0.3505667f, 0.596253f)},
            {"RightEye", new Quaternion(-2.509889e-9f, -3.379446e-12f, 2.306033e-13f, 1f)},
            {"RightFoot", new Quaternion(-0.009482829f, 0.3612484f, 0.9323512f, 0.01144584f)},
            {"RightHand", new Quaternion(0.4797273f, 0.5195542f, -0.5279628f, 0.4702987f)},
            {"RightLowerArm", new Quaternion(0.4594217f, 0.4594215f, -0.5375242f, 0.5375237f)},
            {"RightLowerLeg", new Quaternion(0.0005446263f, -0.03177159f, 0.9994922f, -0.002395923f)},
            {"RightShoulder", new Quaternion(0.3841222f, 0.5257177f, -0.5957286f, 0.4702966f)},
            {"RightToes", new Quaternion(0.0001034f, 0.7113398f, 0.7027067f, 0.01411251f)},
            {"RightUpperArm", new Quaternion(0.4591419f, 0.4591423f, -0.537763f, 0.5377624f)},
            {"RightUpperLeg", new Quaternion(0.0006750703f, 0.0008973633f, 0.9999966f, 0.002352045f)},
            {"Spine", new Quaternion(-0.05427956f, 1.508558e-7f, -2.775203e-6f, 0.9985258f)},
            {"UpperChest", new Quaternion(-0.0824653f, 1.25274e-7f, -6.75759e-6f, 0.996594f)},
        };

        // Humanoid mapping name suffixes for each set of appendages
        public static readonly string[] LEG_MAPPING_SUFFIXES = new string[] {
            "UpperLeg",
            "LowerLeg",
            "Foot",
            "Toes",
        };
        public static readonly string[] ARM_MAPPING_SUFFIXES = new string[] {
            "Shoulder",
            "UpperArm",
            "LowerArm",
            "Hand",
        };
        public static readonly string[] HAND_MAPPING_SUFFIXES = new string[] {
            " Index Distal",
            " Index Intermediate",
            " Index Proximal",
            " Little Distal",
            " Little Intermediate",
            " Little Proximal",
            " Middle Distal",
            " Middle Intermediate",
            " Middle Proximal",
            " Ring Distal",
            " Ring Intermediate",
            " Ring Proximal",
            " Thumb Distal",
            " Thumb Intermediate",
            " Thumb Proximal",
        };

        public const string STANDARD_SHADER = "Standard";
        public const string STANDARD_ROUGHNESS_SHADER = "Autodesk Interactive"; // "Standard (Roughness setup)" Has been renamed in unity 2018.03
        public const string STANDARD_SPECULAR_SHADER = "Standard (Specular setup)";
        public static readonly string[] SUPPORTED_SHADERS = new string[] {
            STANDARD_SHADER,
            STANDARD_ROUGHNESS_SHADER,
            STANDARD_SPECULAR_SHADER,
        };

        public enum AvatarRule
        {
            RecommendedUnityVersion,
            SingleRoot,
            NoDuplicateMapping,
            NoAsymmetricalLegMapping,
            NoAsymmetricalArmMapping,
            NoAsymmetricalHandMapping,
            HipsMapped,
            SpineMapped,
            SpineDescendantOfHips,
            ChestMapped,
            ChestDescendantOfSpine,
            NeckMapped,
            HeadMapped,
            HeadDescendantOfChest,
            EyesMapped,
            HipsNotAtBottom,
            ExtentsNotBelowGround,
            HipsSpineChestNotCoincident,
            TotalBoneCountUnderLimit,
            AvatarRuleEnd,
        };
        // rules that are treated as errors and prevent exporting, otherwise rules will show as warnings
        public static readonly AvatarRule[] EXPORT_BLOCKING_AVATAR_RULES = new AvatarRule[] {
            AvatarRule.HipsMapped,
            AvatarRule.SpineMapped,
            AvatarRule.ChestMapped,
            AvatarRule.HeadMapped,
        };
    }
}

