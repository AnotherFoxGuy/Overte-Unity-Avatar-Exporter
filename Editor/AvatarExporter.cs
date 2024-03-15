//  AvatarExporter.cs
//
//  Created by David Back on 28 Nov 2018
//  Copyright 2018 High Fidelity, Inc.
//  Copyright 2022 to 2023 Overte e.V.
//
//  Distributed under the Apache License, Version 2.0.
//  See the accompanying file LICENSE or http://www.apache.org/licenses/LICENSE-2.0.html
#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityGLTF;
using static Overte.Exporter.Avatar.Constants;

namespace Overte.Exporter.Avatar
{
    class AvatarExporter
    {
        // // ExportSettings provides generic export settings
        // private static readonly ExportSettings GltfExportSettings = new ExportSettings
        // {
        //     Format = GltfFormat.Json,
        //     FileConflictResolution = FileConflictResolution.Overwrite,
        // };
        // // private FST currentFst;

        GLTFSettings _gLTFSettings = new GLTFSettings
        {
            ExportDisabledGameObjects = false,
        };

        private HumanDescription humanDescription;

        private Dictionary<string, UserBoneInformation> userBoneInfos = new Dictionary<string, UserBoneInformation>();
        private Dictionary<string, string> humanoidToUserBoneMappings = new Dictionary<string, string>();
        private BoneTreeNode userBoneTree = new BoneTreeNode();
        private Dictionary<AvatarRule, string> failedAvatarRules = new Dictionary<AvatarRule, string>();
        private List<string> warnings = new List<string>();

        private GameObject _avatar;


        internal AvatarExporter(GameObject avatar)
        {
            _avatar = avatar;
        }

        internal void ExportAvatar(string path)
        {
            var _animator = _avatar.GetComponent<Animator>();

            if (!_animator.isHuman)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "Please set model's Animation Type to Humanoid in the Rig section of it's Inspector window.",
                    "Ok"
                );

                return;
            }
            humanDescription = _animator.avatar.humanDescription;

            Debug.Log(_animator.gameObject.name);
            var p = Path.GetDirectoryName(path);
            ExportGltf(p, _avatar.name);

            SetBoneInformation();
            // // check if we should be substituting a bone for a missing UpperChest mapping
            AdjustUpperChestMapping();

            WriteFST(path);
        }

        void ExportGltf(string ExportPath, string fileName)
        {
            var tr = _avatar.transform.position;
            _avatar.transform.position = Vector3.zero;
            var exporter = new GLTFSceneExporter(_avatar.transform, new ExportContext(_gLTFSettings));
            // exporter.SaveGLTFandBin(ExportPath, fileName);
            exporter.SaveGLB(ExportPath, fileName);
            _avatar.transform.position = tr;
        }

        // The Overte FBX Serializer omits the colon based prefixes. This will make the jointnames compatible.
        string removeTypeFromJointname(string jointName) => jointName.Substring(jointName.IndexOf(':') + 1);

        bool WriteFST(string exportFstPath)
        {
            var fst = new FST();
            fst.name = _avatar.name;
            fst.filename = $"{_avatar.name}.glb";

            // write out joint mappings to fst file
            foreach (var userBoneInfo in userBoneInfos)
            {
                if (userBoneInfo.Value.HasHumanMapping())
                {
                    var jointName = HUMANOID_TO_OVERTE_JOINT_NAME[userBoneInfo.Value.humanName];
                    var userJointName = removeTypeFromJointname(userBoneInfo.Key);
                    // Skip joints with the same name
                    if (jointName == userJointName)
                        continue;
                    if (!fst.jointMapList.Exists(x => x.From == jointName))
                        fst.jointMapList.Add(new JointMap(jointName, userJointName));
                    else
                        fst.jointMapList.Find(x => x.From == jointName).To = userJointName;
                }
            }

            // calculate and write out joint rotation offsets to fst file
            SkeletonBone[] skeletonMap = humanDescription.skeleton;
            foreach (SkeletonBone userBone in skeletonMap)
            {
                string userBoneName = userBone.name;
                UserBoneInformation userBoneInfo;
                if (!userBoneInfos.TryGetValue(userBoneName, out userBoneInfo))
                {
                    continue;
                }

                Quaternion userBoneRotation = userBone.rotation;
                string parentName = userBoneInfo.parentName;
                if (parentName == "root")
                {
                    // if the parent is root then use bone's rotation
                    userBoneInfo.rotation = userBoneRotation;
                }
                else
                {
                    // otherwise multiply bone's rotation by parent bone's absolute rotation
                    userBoneInfo.rotation = userBoneInfos[parentName].rotation * userBoneRotation;
                }

                // generate joint rotation offsets for both humanoid-mapped bones as well as extra unmapped bones
                Quaternion jointOffset = new Quaternion();
                if (userBoneInfo.HasHumanMapping())
                {
                    Quaternion rotation = REFERENCE_ROTATIONS[userBoneInfo.humanName];
                    jointOffset = Quaternion.Inverse(userBoneInfo.rotation) * rotation;
                }
                else
                {
                    jointOffset = Quaternion.Inverse(userBoneInfo.rotation);
                    string lastRequiredParent = FindLastRequiredAncestorBone(userBoneName);
                    if (lastRequiredParent != "root")
                    {
                        // take the previous offset and multiply it by the current local when we have an extra joint
                        string lastRequiredParentHumanName = userBoneInfos[lastRequiredParent].humanName;
                        Quaternion lastRequiredParentRotation = REFERENCE_ROTATIONS[lastRequiredParentHumanName];
                        jointOffset *= lastRequiredParentRotation;
                    }
                }

                var norBName = removeTypeFromJointname(userBoneName);
                if (!fst.jointRotationList.Exists(x => x.BoneName == norBName))
                    // swap from left-handed (Unity) to right-handed (Overte) coordinates and write out joint rotation offset to fst
                    fst.jointRotationList.Add(
                        new JointRotationOffset2(norBName, -jointOffset.x, jointOffset.y, jointOffset.z, -jointOffset.w)
                    );
                else
                    fst.jointRotationList.Find(x => x.BoneName == norBName).offset =
                        new Quaternion(-jointOffset.x, jointOffset.y, jointOffset.z, -jointOffset.w);
            }

            var res = fst.ExportFile(exportFstPath);

            return res;
        }

        void SetBoneInformation()
        {
            userBoneInfos.Clear();
            humanoidToUserBoneMappings.Clear();
            userBoneTree = new BoneTreeNode();

            // instantiate a game object of the user avatar to traverse the bone tree to gather
            // bone parents and positions as well as build a bone tree, then destroy it
            // GameObject avatarGameObject = (GameObject)Instantiate(avatarResource, Vector3.zero, Quaternion.identity);
            TraverseUserBoneTree(_avatar.transform, userBoneTree);
            Bounds bounds = AvatarUtilities.GetAvatarBounds(_avatar);
            float height = AvatarUtilities.GetAvatarHeight(_avatar);
            // DestroyImmediate(avatarGameObject);

            // iterate over Humanoid bones and update user bone info to increase human mapping counts for each bone
            // as well as set their Humanoid name and build a Humanoid to user bone mapping
            HumanBone[] boneMap = humanDescription.human;
            foreach (HumanBone bone in boneMap)
            {
                string humanName = bone.humanName;
                string userBoneName = bone.boneName;
                if (userBoneInfos.ContainsKey(userBoneName))
                {
                    ++userBoneInfos[userBoneName].mappingCount;
                    if (HUMANOID_TO_OVERTE_JOINT_NAME.ContainsKey(humanName))
                    {
                        userBoneInfos[userBoneName].humanName = humanName;
                        humanoidToUserBoneMappings.Add(humanName, userBoneName);
                    }
                }
            }

            // generate the list of avatar rule failure strings for any avatar rules that are not satisfied by this avatar
            SetFailedAvatarRules(bounds, height);
        }

        void TraverseUserBoneTree(Transform modelBone, BoneTreeNode boneTreeNode)
        {
            GameObject gameObject = modelBone.gameObject;

            // check if this transform is a node containing mesh, light, or camera instead of a bone
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            bool mesh = meshRenderer != null || skinnedMeshRenderer != null;
            bool light = gameObject.GetComponent<Light>() != null;
            bool camera = gameObject.GetComponent<Camera>() != null;

            // if this is a mesh then store its material data to be exported if the material is mapped to an fbx material name
            if (mesh)
            {
                // ensure branches within the transform hierarchy that contain meshes are removed from the user bone tree
                Transform ancestorBone = modelBone;
                string previousBoneName = "";
                // find the name of the root child bone that this mesh is underneath
                while (ancestorBone != null)
                {
                    if (ancestorBone.parent == null)
                    {
                        break;
                    }
                    previousBoneName = ancestorBone.name;
                    ancestorBone = ancestorBone.parent;
                }
                // remove the bone tree node from root's children for the root child bone that has mesh children
                if (!string.IsNullOrEmpty(previousBoneName))
                {
                    foreach (BoneTreeNode rootChild in userBoneTree.children)
                    {
                        if (rootChild.boneName == previousBoneName)
                        {
                            userBoneTree.children.Remove(rootChild);
                            break;
                        }
                    }
                }
            }
            else if (!light && !camera)
            {
                // if it is in fact a bone, add it to the bone tree as well as user bone infos list with position and parent name
                string boneName = modelBone.name;
                if (modelBone.parent == null)
                {
                    // if no parent then this is actual root bone node of the user avatar, so consider it's parent as "root"
                    boneName = GetRootBoneName(); // ensure we use the root bone name from the skeleton list for consistency
                    boneTreeNode.boneName = boneName;
                    boneTreeNode.parentName = "root";
                }
                else
                {
                    // otherwise add this bone node as a child to it's parent's children list
                    // if its a child of the root bone, use the root bone name from the skeleton list as the parent for consistency
                    string parentName = modelBone.parent.parent == null ? GetRootBoneName() : modelBone.parent.name;
                    BoneTreeNode node = new BoneTreeNode(boneName, parentName);
                    boneTreeNode.children.Add(node);
                    boneTreeNode = node;
                }

                Vector3 bonePosition = modelBone.position; // bone's absolute position in avatar space
                UserBoneInformation userBoneInfo = new UserBoneInformation(boneTreeNode.parentName, boneTreeNode, bonePosition);
                userBoneInfos.Add(boneName, userBoneInfo);
            }

            // recurse over transform node's children
            for (int i = 0; i < modelBone.childCount; ++i)
            {
                TraverseUserBoneTree(modelBone.GetChild(i), boneTreeNode);
            }
        }

        string FindLastRequiredAncestorBone(string currentBone)
        {
            string result = currentBone;
            // iterating upward through user bone info parent names, find the first ancestor bone that is mapped in Humanoid
            while (result != "root" && userBoneInfos.ContainsKey(result) && !userBoneInfos[result].HasHumanMapping())
            {
                result = userBoneInfos[result].parentName;
            }
            return result;
        }

        void AdjustUpperChestMapping()
        {
            if (!humanoidToUserBoneMappings.ContainsKey("UpperChest"))
            {
                // if parent of Neck is not Chest then map the parent to UpperChest
                string neckUserBone;
                if (humanoidToUserBoneMappings.TryGetValue("Neck", out neckUserBone))
                {
                    UserBoneInformation neckParentBoneInfo;
                    string neckParentUserBone = userBoneInfos[neckUserBone].parentName;
                    if (userBoneInfos.TryGetValue(neckParentUserBone, out neckParentBoneInfo) && !neckParentBoneInfo.HasHumanMapping())
                    {
                        neckParentBoneInfo.humanName = "UpperChest";
                        humanoidToUserBoneMappings.Add("UpperChest", neckParentUserBone);
                    }
                }
                // if there is still no UpperChest bone but there is a Chest bone then we remap Chest to UpperChest
                string chestUserBone;
                if (!humanoidToUserBoneMappings.ContainsKey("UpperChest") &&
                    humanoidToUserBoneMappings.TryGetValue("Chest", out chestUserBone))
                {
                    userBoneInfos[chestUserBone].humanName = "UpperChest";
                    humanoidToUserBoneMappings.Remove("Chest");
                    humanoidToUserBoneMappings.Add("UpperChest", chestUserBone);
                }
            }
        }

        string GetRootBoneName()
        {
            // the "root" bone is the first element in the human skeleton bone list
            if (humanDescription.skeleton.Length > 0)
            {
                return humanDescription.skeleton[0].name;
            }
            return "";
        }

        void SetFailedAvatarRules(Bounds avatarBounds, float avatarHeight)
        {
            failedAvatarRules.Clear();

            string hipsUserBone = "";
            string spineUserBone = "";
            string chestUserBone = "";
            string headUserBone = "";

            Vector3 hipsPosition = new Vector3();

            // iterate over all avatar rules in order and add any rules that fail
            // to the failed avatar rules map with appropriate error or warning text
            for (AvatarRule avatarRule = 0; avatarRule < AvatarRule.AvatarRuleEnd; ++avatarRule)
            {
                switch (avatarRule)
                {
                    case AvatarRule.RecommendedUnityVersion:
                        if (Array.IndexOf(RECOMMENDED_UNITY_VERSIONS, Application.unityVersion) == -1)
                        {
                            failedAvatarRules.Add(avatarRule, "The current version of Unity is not one of the recommended Unity versions.");
                        }
                        break;
                    case AvatarRule.SingleRoot:
                        // avatar rule fails if the root bone node has more than one child bone
                        if (userBoneTree.children.Count > 1)
                        {
                            failedAvatarRules.Add(avatarRule, "There is more than one bone at the top level of the selected avatar's " +
                                                              "bone hierarchy. Please ensure all bones for Humanoid mappings are " +
                                                              "under the same bone hierarchy.");
                        }
                        break;
                    case AvatarRule.NoDuplicateMapping:
                        // avatar rule fails if any user bone is mapped to more than one Humanoid bone
                        foreach (var userBoneInfo in userBoneInfos)
                        {
                            string boneName = userBoneInfo.Key;
                            int mappingCount = userBoneInfo.Value.mappingCount;
                            if (mappingCount > 1)
                            {
                                string text = "The " + boneName + " bone is mapped to more than one bone in Humanoid.";
                                if (failedAvatarRules.ContainsKey(avatarRule))
                                {
                                    failedAvatarRules[avatarRule] += "\n" + text;
                                }
                                else
                                {
                                    failedAvatarRules.Add(avatarRule, text);
                                }
                            }
                        }
                        break;
                    case AvatarRule.NoAsymmetricalLegMapping:
                        CheckAsymmetricalMappingRule(avatarRule, LEG_MAPPING_SUFFIXES, "leg");
                        break;
                    case AvatarRule.NoAsymmetricalArmMapping:
                        CheckAsymmetricalMappingRule(avatarRule, ARM_MAPPING_SUFFIXES, "arm");
                        break;
                    case AvatarRule.NoAsymmetricalHandMapping:
                        CheckAsymmetricalMappingRule(avatarRule, HAND_MAPPING_SUFFIXES, "hand");
                        break;
                    case AvatarRule.HipsMapped:
                        hipsUserBone = CheckHumanBoneMappingRule(avatarRule, "Hips");
                        break;
                    case AvatarRule.SpineMapped:
                        spineUserBone = CheckHumanBoneMappingRule(avatarRule, "Spine");
                        break;
                    case AvatarRule.SpineDescendantOfHips:
                        CheckUserBoneDescendantOfHumanRule(avatarRule, spineUserBone, "Hips");
                        break;
                    case AvatarRule.ChestMapped:
                        if (!humanoidToUserBoneMappings.TryGetValue("Chest", out chestUserBone))
                        {
                            // check to see if there is an unmapped child of Spine that we can suggest to be mapped to Chest
                            string chestMappingCandidate = "";
                            if (!string.IsNullOrEmpty(spineUserBone))
                            {
                                BoneTreeNode spineTreeNode = userBoneInfos[spineUserBone].boneTreeNode;
                                foreach (BoneTreeNode spineChildTreeNode in spineTreeNode.children)
                                {
                                    string spineChildBone = spineChildTreeNode.boneName;
                                    if (userBoneInfos[spineChildBone].HasHumanMapping())
                                    {
                                        continue;
                                    }
                                    // a suitable candidate for Chest should have Neck/Head or Shoulder mappings in its descendants
                                    if (IsHumanBoneInHierarchy(spineChildTreeNode, "Neck") ||
                                        IsHumanBoneInHierarchy(spineChildTreeNode, "Head") ||
                                        IsHumanBoneInHierarchy(spineChildTreeNode, "LeftShoulder") ||
                                        IsHumanBoneInHierarchy(spineChildTreeNode, "RightShoulder"))
                                    {
                                        chestMappingCandidate = spineChildBone;
                                        break;
                                    }
                                }
                            }
                            failedAvatarRules.Add(avatarRule, "There is no Chest bone mapped in Humanoid for the selected avatar.");
                            // if the only found child of Spine is not yet mapped then add it as a suggestion for Chest mapping
                            if (!string.IsNullOrEmpty(chestMappingCandidate))
                            {
                                failedAvatarRules[avatarRule] += " It is suggested that you map bone " + chestMappingCandidate +
                                                                 " to Chest in Humanoid.";
                            }
                        }
                        break;
                    case AvatarRule.ChestDescendantOfSpine:
                        CheckUserBoneDescendantOfHumanRule(avatarRule, chestUserBone, "Spine");
                        break;
                    case AvatarRule.NeckMapped:
                        CheckHumanBoneMappingRule(avatarRule, "Neck");
                        break;
                    case AvatarRule.HeadMapped:
                        headUserBone = CheckHumanBoneMappingRule(avatarRule, "Head");
                        break;
                    case AvatarRule.HeadDescendantOfChest:
                        CheckUserBoneDescendantOfHumanRule(avatarRule, headUserBone, "Chest");
                        break;
                    case AvatarRule.EyesMapped:
                        bool leftEyeMapped = humanoidToUserBoneMappings.ContainsKey("LeftEye");
                        bool rightEyeMapped = humanoidToUserBoneMappings.ContainsKey("RightEye");
                        if (!leftEyeMapped || !rightEyeMapped)
                        {
                            if (leftEyeMapped && !rightEyeMapped)
                            {
                                failedAvatarRules.Add(avatarRule, "There is no RightEye bone mapped in Humanoid " +
                                                                  "for the selected avatar.");
                            }
                            else if (!leftEyeMapped && rightEyeMapped)
                            {
                                failedAvatarRules.Add(avatarRule, "There is no LeftEye bone mapped in Humanoid " +
                                                                  "for the selected avatar.");
                            }
                            else
                            {
                                failedAvatarRules.Add(avatarRule, "There is no LeftEye or RightEye bone mapped in Humanoid " +
                                                                  "for the selected avatar.");
                            }
                        }
                        break;
                    case AvatarRule.HipsNotAtBottom:
                        // ensure that Hips is not below a proportional percentage of the avatar's height in avatar space
                        if (!string.IsNullOrEmpty(hipsUserBone))
                        {
                            UserBoneInformation hipsBoneInfo = userBoneInfos[hipsUserBone];
                            hipsPosition = hipsBoneInfo.position;

                            // find the lowest y position of the bones
                            float minBoneYPosition = float.MaxValue;
                            foreach (var userBoneInfo in userBoneInfos)
                            {
                                Vector3 position = userBoneInfo.Value.position;
                                if (position.y < minBoneYPosition)
                                {
                                    minBoneYPosition = position.y;
                                }
                            }

                            // check that Hips is within a percentage of avatar's height from the lowest Y point of the avatar
                            float bottomYRange = HIPS_MIN_Y_PERCENT_OF_HEIGHT * avatarHeight;
                            if (Mathf.Abs(hipsPosition.y - minBoneYPosition) < bottomYRange)
                            {
                                failedAvatarRules.Add(avatarRule, "The bone mapped to Hips in Humanoid (" + hipsUserBone +
                                                                  ") should not be at the bottom of the selected avatar.");
                            }
                        }
                        break;
                    case AvatarRule.ExtentsNotBelowGround:
                        // ensure the minimum Y extent of the model's bounds is not below a proportional threshold of avatar's height
                        float belowGroundThreshold = BELOW_GROUND_THRESHOLD_PERCENT_OF_HEIGHT * avatarHeight;
                        if (avatarBounds.min.y < belowGroundThreshold)
                        {
                            failedAvatarRules.Add(avatarRule, "The bottom extents of the selected avatar go below ground level.");
                        }
                        break;
                    case AvatarRule.HipsSpineChestNotCoincident:
                        // ensure the bones mapped to Hips, Spine, and Chest are all not in the same position,
                        // check Hips to Spine and Spine to Chest lengths are within HIPS_SPINE_CHEST_MIN_SEPARATION
                        if (!string.IsNullOrEmpty(spineUserBone) && !string.IsNullOrEmpty(chestUserBone) &&
                            !string.IsNullOrEmpty(hipsUserBone))
                        {
                            UserBoneInformation spineBoneInfo = userBoneInfos[spineUserBone];
                            UserBoneInformation chestBoneInfo = userBoneInfos[chestUserBone];
                            Vector3 hipsToSpine = hipsPosition - spineBoneInfo.position;
                            Vector3 spineToChest = spineBoneInfo.position - chestBoneInfo.position;
                            if (hipsToSpine.magnitude < HIPS_SPINE_CHEST_MIN_SEPARATION &&
                                spineToChest.magnitude < HIPS_SPINE_CHEST_MIN_SEPARATION)
                            {
                                failedAvatarRules.Add(avatarRule, "The bone mapped to Hips in Humanoid (" + hipsUserBone +
                                                                  "), the bone mapped to Spine in Humanoid (" + spineUserBone +
                                                                  "), and the bone mapped to Chest in Humanoid (" + chestUserBone +
                                                                  ") should not be coincidental.");
                            }
                        }
                        break;
                    case AvatarRule.TotalBoneCountUnderLimit:
                        int userBoneCount = userBoneInfos.Count;
                        if (userBoneCount > MAXIMUM_USER_BONE_COUNT)
                        {
                            failedAvatarRules.Add(avatarRule, "The total number of bones in the avatar (" + userBoneCount +
                                                              ") exceeds the maximum bone limit (" + MAXIMUM_USER_BONE_COUNT + ").");
                        }
                        break;
                }
            }
        }

        bool IsHumanBoneInHierarchy(BoneTreeNode boneTreeNode, string humanBoneName)
        {
            UserBoneInformation userBoneInfo;
            if (userBoneInfos.TryGetValue(boneTreeNode.boneName, out userBoneInfo) && userBoneInfo.humanName == humanBoneName)
            {
                // this bone matches the human bone name being searched for
                return true;
            }

            // recursively check downward through children bones for target human bone
            foreach (BoneTreeNode childNode in boneTreeNode.children)
            {
                if (IsHumanBoneInHierarchy(childNode, humanBoneName))
                {
                    return true;
                }
            }

            return false;
        }

        string CheckHumanBoneMappingRule(AvatarRule avatarRule, string humanBoneName)
        {
            string userBoneName = "";
            // avatar rule fails if bone is not mapped in Humanoid
            if (!humanoidToUserBoneMappings.TryGetValue(humanBoneName, out userBoneName))
            {
                failedAvatarRules.Add(avatarRule, "There is no " + humanBoneName +
                                                  " bone mapped in Humanoid for the selected avatar.");
            }
            return userBoneName;
        }

        void CheckUserBoneDescendantOfHumanRule(AvatarRule avatarRule, string descendantUserBoneName, string descendantOfHumanName)
        {
            if (string.IsNullOrEmpty(descendantUserBoneName))
            {
                return;
            }

            string descendantOfUserBoneName = "";
            if (!humanoidToUserBoneMappings.TryGetValue(descendantOfHumanName, out descendantOfUserBoneName))
            {
                return;
            }

            string userBoneName = descendantUserBoneName;
            UserBoneInformation userBoneInfo = userBoneInfos[userBoneName];
            string descendantHumanName = userBoneInfo.humanName;
            // iterate upward from user bone through user bone info parent names until root
            // is reached or the ancestor bone name matches the target descendant of name
            while (userBoneName != "root")
            {
                if (userBoneName == descendantOfUserBoneName)
                {
                    return;
                }
                if (userBoneInfos.TryGetValue(userBoneName, out userBoneInfo))
                {
                    userBoneName = userBoneInfo.parentName;
                }
                else
                {
                    break;
                }
            }

            // avatar rule fails if no ancestor of given user bone matched the descendant of name (no early return)
            failedAvatarRules.Add(avatarRule, "The bone mapped to " + descendantHumanName + " in Humanoid (" +
                                              descendantUserBoneName + ") is not a descendant of the bone mapped to " +
                                              descendantOfHumanName + " in Humanoid (" + descendantOfUserBoneName + ").");
        }

        void CheckAsymmetricalMappingRule(AvatarRule avatarRule, string[] mappingSuffixes, string appendage)
        {
            int leftCount = 0;
            int rightCount = 0;
            // add Left/Right to each mapping suffix to make Humanoid mapping names,
            // and count the number of bones mapped in Humanoid on each side
            foreach (string mappingSuffix in mappingSuffixes)
            {
                string leftMapping = "Left" + mappingSuffix;
                string rightMapping = "Right" + mappingSuffix;
                if (humanoidToUserBoneMappings.ContainsKey(leftMapping))
                {
                    ++leftCount;
                }
                if (humanoidToUserBoneMappings.ContainsKey(rightMapping))
                {
                    ++rightCount;
                }
            }
            // avatar rule fails if number of left appendage mappings doesn't match number of right appendage mappings
            if (leftCount != rightCount)
            {
                failedAvatarRules.Add(avatarRule, "The number of bones mapped in Humanoid for the left " + appendage + " (" +
                                                  leftCount + ") does not match the number of bones mapped in Humanoid for the right " +
                                                  appendage + " (" + rightCount + ").");
            }
        }

        string GetTextureDirectory(string basePath)
        {
            string textureDirectory = Path.GetDirectoryName(basePath) + "/" + TEXTURES_DIRECTORY;
            textureDirectory = textureDirectory.Replace("//", "/");
            return textureDirectory;
        }

        /*string SetTextureDependencies()
        {
            string textureWarnings = "";
            textureDependencies.Clear();

            // build the list of all local asset paths for textures that Unity considers dependencies of the model
            // for any textures that have duplicate names, return a string of duplicate name warnings
            string[] dependencies = AssetDatabase.GetDependencies(assetPath);
            foreach (string dependencyPath in dependencies)
            {
                UnityEngine.Object textureObject = AssetDatabase.LoadAssetAtPath(dependencyPath, typeof(Texture2D));
                if (textureObject != null)
                {
                    string textureName = Path.GetFileName(dependencyPath);
                    if (textureDependencies.ContainsKey(textureName))
                    {
                        textureWarnings += "There is more than one texture with the name " + textureName +
                                           " referenced in the selected avatar.\n\n";
                    }
                    else
                    {
                        textureDependencies.Add(textureName, dependencyPath);
                    }
                }
            }

            return textureWarnings;
        }*/

        /*bool CopyExternalTextures(string texturesDirectory)
        {
            // copy the found dependency textures from the local asset folder to the textures folder in the target export project
            foreach (var texture in textureDependencies)
            {
                string targetPath = texturesDirectory + "/" + texture.Key;
                try
                {
                    File.Copy(texture.Value, targetPath, true);
                }
                catch
                {
                    EditorUtility.DisplayDialog("Error", "Failed to copy texture file " + texture.Value + " to " + targetPath +
                                                ". Please check the location and try again.", "Ok");
                    return false;
                }
            }
            return true;
        }*/

    }

    static class AvatarUtilities
    {
        public const float DEFAULT_AVATAR_HEIGHT = 1.755f;

        static public Bounds GetAvatarBounds(GameObject avatarObject)
        {
            Bounds bounds = new Bounds();
            if (avatarObject != null)
            {
                var meshRenderers = avatarObject.GetComponentsInChildren<MeshRenderer>();
                var skinnedMeshRenderers = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var renderer in meshRenderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                foreach (var renderer in skinnedMeshRenderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        static public float GetAvatarHeight(GameObject avatarObject)
        {
            // height of an avatar model can be determined to be the max Y extents of the combined bounds for all its mesh renderers
            Bounds avatarBounds = GetAvatarBounds(avatarObject);
            return avatarBounds.max.y - avatarBounds.min.y;
        }
    }
}
#endif
