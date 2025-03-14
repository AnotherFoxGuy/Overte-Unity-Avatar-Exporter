//  AvatarExporter.cs
//
//  Created by David Back on 28 Nov 2018
//  Copyright 2018 High Fidelity, Inc.
//  Copyright 2022 to 2025 Overte e.V.
//
//  Distributed under the Apache License, Version 2.0.
//  See the accompanying file LICENSE or http://www.apache.org/licenses/LICENSE-2.0.html

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityGLTF;
using static Overte.Exporter.Avatar.Constants;
using Object = UnityEngine.Object;

namespace Overte.Exporter.Avatar
{
    internal class AvatarExporter
    {
        private GameObject _avatar;
        private string _name;

        private readonly Dictionary<AvatarRule, string> _failedAvatarRules = new();

        private readonly GLTFSettings _gLTFSettings = ScriptableObject.CreateInstance<GLTFSettings>();

        private HumanDescription _humanDescription;
        private readonly Dictionary<string, string> _humanoidToUserBoneMappings = new();

        private readonly Dictionary<string, UserBoneInformation> _userBoneInfos = new();

        private BoneTreeNode _userBoneTree = new();


        internal AvatarExporter()
        {
            _gLTFSettings.ExportDisabledGameObjects = false;
            _gLTFSettings.BlendShapeExportSparseAccessors = true;
        }

        internal void ExportAvatar(string name, string path, GameObject avatar)
        {
            _name = name;
            _avatar = avatar;
            var animator = _avatar.GetComponent<Animator>();

            if (animator == null || !animator.isHuman)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "Please set model's Animation Type to Humanoid in the Rig section of it's Inspector window.",
                    "Ok"
                );

                return;
            }

            _humanDescription = animator.avatar.humanDescription;

            // Debug.Log(animator.gameObject.name);
            var p = Path.GetDirectoryName(path);

            var avatarCopy = Object.Instantiate(_avatar, Vector3.zero, Quaternion.identity);
            var avatarDescriptor = _avatar.GetComponent<OverteAvatarDescriptor>();

            if (avatarDescriptor.RemapedBlendShapeList.Count > 0 && avatarDescriptor.OptimizeBlendShapes)
            {
                var meshes = avatarCopy.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var mesh in meshes)
                {
                    BakeAndKeepSpecifiedBlendShapes(avatarDescriptor, mesh);
                }
            }

            var exporter = new GLTFSceneExporter(avatarCopy.transform, new ExportContext(_gLTFSettings));
            exporter.SaveGLB(p, _avatar.name);

            SetBoneInformation(avatarCopy);
            // // check if we should be substituting a bone for a missing UpperChest mapping
            AdjustUpperChestMapping();

            WriteFst(path);

            Object.DestroyImmediate(avatarCopy);
        }

        public Dictionary<AvatarRule, string> CheckForErrors(GameObject avatar)
        {
            _avatar = avatar;
            var animator = _avatar.GetComponent<Animator>();
            if (animator == null)
            {
                _failedAvatarRules.Add(AvatarRule.NoAnimator, "Avatar has no Animator");
                return _failedAvatarRules;
            }

            if (!animator.isHuman)
            {
                _failedAvatarRules.Add(AvatarRule.NonHumanoid, "Avatar is not set to Humanoid");
                return _failedAvatarRules;
            }

            _humanDescription = animator.avatar.humanDescription;
            SetBoneInformation(_avatar);
            var bounds = GetAvatarBounds(avatar);
            var height = GetAvatarHeight(avatar);
            // generate the list of avatar rule failure strings for any avatar rules that are not satisfied by this avatar
            SetFailedAvatarRules(bounds, height);
            Debug.Log($"Found {_failedAvatarRules.Count} issues with {avatar.name}");
            return _failedAvatarRules;
        }

        // The Overte FBX Serializer omits the colon based prefixes. This will make the jointnames compatible.
        private string RemoveTypeFromJointname(string jointName)
        {
            return jointName.Substring(jointName.IndexOf(':') + 1);
        }

        private bool WriteFst(string exportFstPath)
        {
            var avatarDescriptor = _avatar.GetComponent<OverteAvatarDescriptor>();

            var fst = new FST();
            fst.name = _name;
            fst.filename = $"{_avatar.name}.glb";

            // write out joint mappings to fst file
            foreach (var userBoneInfo in _userBoneInfos)
                if (userBoneInfo.Value.HasHumanMapping())
                {
                    var jointName = HUMANOID_TO_OVERTE_JOINT_NAME[userBoneInfo.Value.humanName];
                    var userJointName = RemoveTypeFromJointname(userBoneInfo.Key);
                    // Skip joints with the same name
                    if (jointName == userJointName)
                        continue;
                    if (!fst.jointMapList.Exists(x => x.From == jointName))
                        fst.jointMapList.Add(new JointMap(jointName, userJointName));
                    else
                        fst.jointMapList.Find(x => x.From == jointName).To = userJointName;
                }

            // calculate and write out joint rotation offsets to fst file
            var skeletonMap = _humanDescription.skeleton;
            foreach (var userBone in skeletonMap)
            {
                var userBoneName = userBone.name;
                UserBoneInformation userBoneInfo;
                if (!_userBoneInfos.TryGetValue(userBoneName, out userBoneInfo)) continue;

                var userBoneRotation = userBone.rotation;
                var parentName = userBoneInfo.parentName;
                if (parentName == "root")
                    // if the parent is root then use bone's rotation
                    userBoneInfo.rotation = userBoneRotation;
                else
                    // otherwise multiply bone's rotation by parent bone's absolute rotation
                    userBoneInfo.rotation = _userBoneInfos[parentName].rotation * userBoneRotation;

                // generate joint rotation offsets for both humanoid-mapped bones as well as extra unmapped bones
                var jointOffset = new Quaternion();
                if (userBoneInfo.HasHumanMapping())
                {
                    var rotation = REFERENCE_ROTATIONS[userBoneInfo.humanName];
                    jointOffset = Quaternion.Inverse(userBoneInfo.rotation) * rotation;
                }
                else
                {
                    jointOffset = Quaternion.Inverse(userBoneInfo.rotation);
                    var lastRequiredParent = FindLastRequiredAncestorBone(userBoneName);
                    if (lastRequiredParent != "root")
                    {
                        // take the previous offset and multiply it by the current local when we have an extra joint
                        var lastRequiredParentHumanName = _userBoneInfos[lastRequiredParent].humanName;
                        var lastRequiredParentRotation = REFERENCE_ROTATIONS[lastRequiredParentHumanName];
                        jointOffset *= lastRequiredParentRotation;
                    }
                }

                var norBName = RemoveTypeFromJointname(userBoneName);
                if (!fst.jointRotationList.Exists(x => x.BoneName == norBName))
                    // swap from left-handed (Unity) to right-handed (Overte) coordinates and write out joint rotation offset to fst
                    fst.jointRotationList.Add(
                        new JointRotationOffset2(norBName, -jointOffset.x, jointOffset.y, jointOffset.z, -jointOffset.w)
                    );
                else
                    fst.jointRotationList.Find(x => x.BoneName == norBName).offset =
                        new Quaternion(-jointOffset.x, jointOffset.y, jointOffset.z, -jointOffset.w);
            }

            foreach (var blendshape in avatarDescriptor.RemapedBlendShapeList)
            {
                fst.remapBlendShapeList.Add(new RemapBlendShape(blendshape.from, blendshape.to, blendshape.multiplier));
            }

            var res = fst.ExportFile(exportFstPath);

            return res;
        }

        private void SetBoneInformation(GameObject avatar)
        {
            _userBoneInfos.Clear();
            _humanoidToUserBoneMappings.Clear();
            _userBoneTree = new BoneTreeNode();

            // instantiate a game object of the user avatar to traverse the bone tree to gather
            // bone parents and positions as well as build a bone tree, then destroy it
            // GameObject avatarGameObject = (GameObject)Instantiate(avatarResource, Vector3.zero, Quaternion.identity);
            TraverseUserBoneTree(avatar.transform, _userBoneTree);
            // DestroyImmediate(avatarGameObject);

            // iterate over Humanoid bones and update user bone info to increase human mapping counts for each bone
            // as well as set their Humanoid name and build a Humanoid to user bone mapping
            var boneMap = _humanDescription.human;
            foreach (var bone in boneMap)
            {
                var humanName = bone.humanName;
                var userBoneName = bone.boneName;
                if (!_userBoneInfos.TryGetValue(userBoneName, out var info)) 
                    continue;
                ++info.mappingCount;
                if (!HUMANOID_TO_OVERTE_JOINT_NAME.ContainsKey(humanName)) 
                    continue;
                _userBoneInfos[userBoneName].humanName = humanName;
                _humanoidToUserBoneMappings.Add(humanName, userBoneName);
            }
        }

        private void TraverseUserBoneTree(Transform modelBone, BoneTreeNode boneTreeNode)
        {
            var gameObject = modelBone.gameObject;

            // check if this transform is a node containing mesh, light, or camera instead of a bone
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            var skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            var mesh = meshRenderer != null || skinnedMeshRenderer != null;
            var light = gameObject.GetComponent<Light>() != null;
            var camera = gameObject.GetComponent<Camera>() != null;

            // if this is a mesh then store its material data to be exported if the material is mapped to an fbx material name
            if (mesh)
            {
                // ensure branches within the transform hierarchy that contain meshes are removed from the user bone tree
                var ancestorBone = modelBone;
                var previousBoneName = "";
                // find the name of the root child bone that this mesh is underneath
                while (ancestorBone != null)
                {
                    if (ancestorBone.parent == null) break;

                    previousBoneName = ancestorBone.name;
                    ancestorBone = ancestorBone.parent;
                }

                // remove the bone tree node from root's children for the root child bone that has mesh children
                if (!string.IsNullOrEmpty(previousBoneName))
                    foreach (var rootChild in _userBoneTree.children.Where(rootChild =>
                                 rootChild.boneName == previousBoneName))
                    {
                        _userBoneTree.children.Remove(rootChild);
                        break;
                    }
            }
            else if (!light && !camera)
            {
                // if it is in fact a bone, add it to the bone tree as well as user bone infos list with position and parent name
                var boneName = modelBone.name;
                if (modelBone.parent == null)
                {
                    // if no parent then this is actual root bone node of the user avatar, so consider it's parent as "root"
                    boneName =
                        GetRootBoneName(); // ensure we use the root bone name from the skeleton list for consistency
                    boneTreeNode.boneName = boneName;
                    boneTreeNode.parentName = "root";
                }
                else
                {
                    // otherwise add this bone node as a child to it's parent's children list
                    // if its a child of the root bone, use the root bone name from the skeleton list as the parent for consistency
                    var parentName = modelBone.parent.parent == null ? GetRootBoneName() : modelBone.parent.name;
                    var node = new BoneTreeNode(boneName, parentName);
                    boneTreeNode.children.Add(node);
                    boneTreeNode = node;
                }

                var bonePosition = modelBone.position; // bone's absolute position in avatar space
                var userBoneInfo = new UserBoneInformation(boneTreeNode.parentName, boneTreeNode, bonePosition);
                _userBoneInfos.Add(boneName, userBoneInfo);
            }

            // recurse over transform node's children
            for (var i = 0; i < modelBone.childCount; ++i) TraverseUserBoneTree(modelBone.GetChild(i), boneTreeNode);
        }

        private string FindLastRequiredAncestorBone(string currentBone)
        {
            var result = currentBone;
            // iterating upward through user bone info parent names, find the first ancestor bone that is mapped in Humanoid
            while (result != "root" && _userBoneInfos.ContainsKey(result) && !_userBoneInfos[result].HasHumanMapping())
                result = _userBoneInfos[result].parentName;

            return result;
        }

        private void AdjustUpperChestMapping()
        {
            if (_humanoidToUserBoneMappings.ContainsKey("UpperChest"))
                return;
            // if parent of Neck is not Chest then map the parent to UpperChest
            string neckUserBone;
            if (_humanoidToUserBoneMappings.TryGetValue("Neck", out neckUserBone))
            {
                UserBoneInformation neckParentBoneInfo;
                var neckParentUserBone = _userBoneInfos[neckUserBone].parentName;
                if (_userBoneInfos.TryGetValue(neckParentUserBone, out neckParentBoneInfo) &&
                    !neckParentBoneInfo.HasHumanMapping())
                {
                    neckParentBoneInfo.humanName = "UpperChest";
                    _humanoidToUserBoneMappings.Add("UpperChest", neckParentUserBone);
                }
            }

            // if there is still no UpperChest bone but there is a Chest bone then we remap Chest to UpperChest
            string chestUserBone;
            if (!_humanoidToUserBoneMappings.ContainsKey("UpperChest") &&
                _humanoidToUserBoneMappings.TryGetValue("Chest", out chestUserBone))
            {
                _userBoneInfos[chestUserBone].humanName = "UpperChest";
                _humanoidToUserBoneMappings.Remove("Chest");
                _humanoidToUserBoneMappings.Add("UpperChest", chestUserBone);
            }
        }

        private string GetRootBoneName()
        {
            // the "root" bone is the first element in the human skeleton bone list
            if (_humanDescription.skeleton.Length > 0) return _humanDescription.skeleton[0].name;

            return "";
        }

        private void SetFailedAvatarRules(Bounds avatarBounds, float avatarHeight)
        {
            _failedAvatarRules.Clear();

            var hipsUserBone = "";
            var spineUserBone = "";
            var chestUserBone = "";
            var headUserBone = "";

            var hipsPosition = new Vector3();

            // iterate over all avatar rules in order and add any rules that fail
            // to the failed avatar rules map with appropriate error or warning text
            // for (AvatarRule avatarRule = 0; avatarRule < AvatarRule.AvatarRuleEnd; ++avatarRule)
            foreach (var rule in Enum.GetValues(typeof(AvatarRule)))
            {
                var avatarRule = (AvatarRule)rule;
                switch (avatarRule)
                {
                    case AvatarRule.RecommendedUnityVersion:
                        if (Array.IndexOf(RECOMMENDED_UNITY_VERSIONS, Application.unityVersion) == -1)
                            _failedAvatarRules.Add(avatarRule,
                                "The current version of Unity is not one of the recommended Unity versions.");

                        break;
                    case AvatarRule.SingleRoot:
                        // avatar rule fails if the root bone node has more than one child bone
                        if (_userBoneTree.children.Count > 1)
                            _failedAvatarRules.Add(avatarRule,
                                "There is more than one bone at the top level of the selected avatar's " +
                                "bone hierarchy. Please ensure all bones for Humanoid mappings are " +
                                "under the same bone hierarchy.");

                        break;
                    case AvatarRule.NoDuplicateMapping:
                        // avatar rule fails if any user bone is mapped to more than one Humanoid bone
                        foreach (var userBoneInfo in _userBoneInfos)
                        {
                            var boneName = userBoneInfo.Key;
                            var mappingCount = userBoneInfo.Value.mappingCount;
                            if (mappingCount > 1)
                            {
                                var text = $"The {boneName} bone is mapped to more than one bone in Humanoid.";
                                if (_failedAvatarRules.ContainsKey(avatarRule))
                                    _failedAvatarRules[avatarRule] += $"\n{text}";
                                else
                                    _failedAvatarRules.Add(avatarRule, text);
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
                        if (!_humanoidToUserBoneMappings.TryGetValue("Chest", out chestUserBone))
                        {
                            // check to see if there is an unmapped child of Spine that we can suggest to be mapped to Chest
                            var chestMappingCandidate = "";
                            if (!string.IsNullOrEmpty(spineUserBone))
                            {
                                var spineTreeNode = _userBoneInfos[spineUserBone].boneTreeNode;
                                foreach (var spineChildTreeNode in spineTreeNode.children)
                                {
                                    var spineChildBone = spineChildTreeNode.boneName;
                                    if (_userBoneInfos[spineChildBone].HasHumanMapping()) continue;

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

                            _failedAvatarRules.Add(avatarRule,
                                "There is no Chest bone mapped in Humanoid for the selected avatar.");
                            // if the only found child of Spine is not yet mapped then add it as a suggestion for Chest mapping
                            if (!string.IsNullOrEmpty(chestMappingCandidate))
                                _failedAvatarRules[avatarRule] +=
                                    $" It is suggested that you map bone {chestMappingCandidate} to Chest in Humanoid.";
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
                        var leftEyeMapped = _humanoidToUserBoneMappings.ContainsKey("LeftEye");
                        var rightEyeMapped = _humanoidToUserBoneMappings.ContainsKey("RightEye");
                        if (!leftEyeMapped || !rightEyeMapped)
                        {
                            if (leftEyeMapped && !rightEyeMapped)
                                _failedAvatarRules.Add(avatarRule, "There is no RightEye bone mapped in Humanoid " +
                                                                   "for the selected avatar.");
                            else if (!leftEyeMapped && rightEyeMapped)
                                _failedAvatarRules.Add(avatarRule, "There is no LeftEye bone mapped in Humanoid " +
                                                                   "for the selected avatar.");
                            else
                                _failedAvatarRules.Add(avatarRule,
                                    "There is no LeftEye or RightEye bone mapped in Humanoid " +
                                    "for the selected avatar.");
                        }

                        break;
                    case AvatarRule.HipsNotAtBottom:
                        // ensure that Hips is not below a proportional percentage of the avatar's height in avatar space
                        if (!string.IsNullOrEmpty(hipsUserBone))
                        {
                            var hipsBoneInfo = _userBoneInfos[hipsUserBone];
                            hipsPosition = hipsBoneInfo.position;

                            // find the lowest y position of the bones
                            var minBoneYPosition = float.MaxValue;
                            foreach (var userBoneInfo in _userBoneInfos)
                            {
                                var position = userBoneInfo.Value.position;
                                if (position.y < minBoneYPosition) minBoneYPosition = position.y;
                            }

                            // check that Hips is within a percentage of avatar's height from the lowest Y point of the avatar
                            var bottomYRange = HIPS_MIN_Y_PERCENT_OF_HEIGHT * avatarHeight;
                            if (Mathf.Abs(hipsPosition.y - minBoneYPosition) < bottomYRange)
                                _failedAvatarRules.Add(avatarRule,
                                    $"The bone mapped to Hips in Humanoid ({hipsUserBone}) should not be at the bottom of the selected avatar.");
                        }

                        break;
                    case AvatarRule.ExtentsNotBelowGround:
                        // ensure the minimum Y extent of the model's bounds is not below a proportional threshold of avatar's height
                        var belowGroundThreshold = BELOW_GROUND_THRESHOLD_PERCENT_OF_HEIGHT * avatarHeight;
                        if (avatarBounds.min.y < belowGroundThreshold)
                            _failedAvatarRules.Add(avatarRule,
                                "The bottom extents of the selected avatar go below ground level.");

                        break;
                    case AvatarRule.HipsSpineChestNotCoincident:
                        // ensure the bones mapped to Hips, Spine, and Chest are all not in the same position,
                        // check Hips to Spine and Spine to Chest lengths are within HIPS_SPINE_CHEST_MIN_SEPARATION
                        if (!string.IsNullOrEmpty(spineUserBone) && !string.IsNullOrEmpty(chestUserBone) &&
                            !string.IsNullOrEmpty(hipsUserBone))
                        {
                            var spineBoneInfo = _userBoneInfos[spineUserBone];
                            var chestBoneInfo = _userBoneInfos[chestUserBone];
                            var hipsToSpine = hipsPosition - spineBoneInfo.position;
                            var spineToChest = spineBoneInfo.position - chestBoneInfo.position;
                            if (hipsToSpine.magnitude < HIPS_SPINE_CHEST_MIN_SEPARATION &&
                                spineToChest.magnitude < HIPS_SPINE_CHEST_MIN_SEPARATION)
                                _failedAvatarRules.Add(avatarRule,
                                    $"The bone mapped to Hips in Humanoid ({hipsUserBone}), the bone mapped to Spine in Humanoid ({spineUserBone}), and the bone mapped to Chest in Humanoid ({chestUserBone}) should not be coincidental.");
                        }

                        break;
                    case AvatarRule.TotalBoneCountUnderLimit:
                        var userBoneCount = _userBoneInfos.Count;
                        if (userBoneCount > MAXIMUM_USER_BONE_COUNT)
                            _failedAvatarRules.Add(avatarRule,
                                $"The total number of bones in the avatar ({userBoneCount}) exceeds the maximum bone limit ({MAXIMUM_USER_BONE_COUNT}).");

                        break;
                }
            }
        }

        private bool IsHumanBoneInHierarchy(BoneTreeNode boneTreeNode, string humanBoneName)
        {
            UserBoneInformation userBoneInfo;
            if (_userBoneInfos.TryGetValue(boneTreeNode.boneName, out userBoneInfo) &&
                userBoneInfo.humanName == humanBoneName)
                // this bone matches the human bone name being searched for
                return true;

            // recursively check downward through children bones for target human bone
            foreach (var childNode in boneTreeNode.children)
                if (IsHumanBoneInHierarchy(childNode, humanBoneName))
                    return true;

            return false;
        }

        private string CheckHumanBoneMappingRule(AvatarRule avatarRule, string humanBoneName)
        {
            var userBoneName = "";
            // avatar rule fails if bone is not mapped in Humanoid
            if (!_humanoidToUserBoneMappings.TryGetValue(humanBoneName, out userBoneName))
                _failedAvatarRules.Add(avatarRule,
                    $"There is no {humanBoneName} bone mapped in Humanoid for the selected avatar.");

            return userBoneName;
        }

        private void CheckUserBoneDescendantOfHumanRule(AvatarRule avatarRule, string descendantUserBoneName,
            string descendantOfHumanName)
        {
            if (string.IsNullOrEmpty(descendantUserBoneName)) return;

            var descendantOfUserBoneName = "";
            if (!_humanoidToUserBoneMappings.TryGetValue(descendantOfHumanName, out descendantOfUserBoneName)) return;

            var userBoneName = descendantUserBoneName;
            var userBoneInfo = _userBoneInfos[userBoneName];
            var descendantHumanName = userBoneInfo.humanName;
            // iterate upward from user bone through user bone info parent names until root
            // is reached or the ancestor bone name matches the target descendant of name
            while (userBoneName != "root")
            {
                if (userBoneName == descendantOfUserBoneName) return;

                if (_userBoneInfos.TryGetValue(userBoneName, out userBoneInfo))
                    userBoneName = userBoneInfo.parentName;
                else
                    break;
            }

            // avatar rule fails if no ancestor of given user bone matched the descendant of name (no early return)
            _failedAvatarRules.Add(avatarRule,
                $"The bone mapped to {descendantHumanName} in Humanoid ({descendantUserBoneName}) is not a descendant of the bone mapped to {descendantOfHumanName} in Humanoid ({descendantOfUserBoneName}).");
        }

        private void CheckAsymmetricalMappingRule(AvatarRule avatarRule, string[] mappingSuffixes, string appendage)
        {
            var leftCount = 0;
            var rightCount = 0;
            // add Left/Right to each mapping suffix to make Humanoid mapping names,
            // and count the number of bones mapped in Humanoid on each side
            foreach (var mappingSuffix in mappingSuffixes)
            {
                var leftMapping = $"Left{mappingSuffix}";
                var rightMapping = $"Right{mappingSuffix}";
                if (_humanoidToUserBoneMappings.ContainsKey(leftMapping)) ++leftCount;

                if (_humanoidToUserBoneMappings.ContainsKey(rightMapping)) ++rightCount;
            }

            // avatar rule fails if number of left appendage mappings doesn't match number of right appendage mappings
            if (leftCount != rightCount)
                _failedAvatarRules.Add(avatarRule,
                    $"The number of bones mapped in Humanoid for the left {appendage} ({leftCount}) does not match the number of bones mapped in Humanoid for the right {appendage} ({rightCount}).");
        }
        
        /// <summary>
        /// Bakes all blend shapes except those specified to keep
        /// </summary>
        private void BakeAndKeepSpecifiedBlendShapes(OverteAvatarDescriptor descriptor,
            SkinnedMeshRenderer meshRenderer)
        {
            var originalMesh = meshRenderer.sharedMesh;

            var blendshapeNames = descriptor.RemapedBlendShapeList
                .Select(blendshape => blendshape.from)
                .ToList();

            // Extract blend shapes to keep
            var blendShapeIndicesToTransfer = descriptor.RemapedBlendShapeList
                .Select(blendshape => originalMesh.GetBlendShapeIndex(blendshape.from))
                .Where(i => i != -1).ToList();

            var ovBs = Enum.GetNames(typeof(Blendshapes));

            for (var i = 0; i < originalMesh.blendShapeCount; i++)
            {
                var name = originalMesh.GetBlendShapeName(i);
                if (!ovBs.Contains(name)) continue;
                blendshapeNames.Add(name);
                blendShapeIndicesToTransfer.Add(i);
            }

            // Store original blend shape weights
            var originalWeights = new Dictionary<string, float>();

            foreach (var blendshape in blendshapeNames)
            {
                var i = originalMesh.GetBlendShapeIndex(blendshape);
                originalWeights[blendshape] = meshRenderer.GetBlendShapeWeight(i);
            }

            // Bake the mesh
            var bakedMesh = new Mesh();
            meshRenderer.BakeMesh(bakedMesh, true);

            // Copy UV, tangents, and other mesh data
            CopyMeshData(originalMesh, bakedMesh);

            // Transfer bone weights
            TransferBoneWeights(originalMesh, bakedMesh);

            // If we have blend shapes to transfer, do it now
            if (blendShapeIndicesToTransfer.Count > 0)
            {
                // Transfer selected blend shapes to the new mesh
                TransferBlendShapes(originalMesh, bakedMesh, blendShapeIndicesToTransfer);
            }

            // Assign the baked mesh to the target
            meshRenderer.sharedMesh = bakedMesh;

            if (blendShapeIndicesToTransfer.Count > 0)
            {
                // Restore original blend shape weights on source
                foreach (var blendshape in blendshapeNames)
                {
                    var i = meshRenderer.sharedMesh.GetBlendShapeIndex(blendshape);
                    meshRenderer.SetBlendShapeWeight(i, originalWeights[blendshape]);
                }
            }

            Debug.Log(
                $"Mesh {meshRenderer.name} baked with {blendShapeIndicesToTransfer.Count} blend shapes kept.");
        }

        private void TransferBlendShapes(Mesh source, Mesh destination, List<int> blendShapeIndices)
        {
            // For each blend shape to transfer
            foreach (var blendShapeIndex in blendShapeIndices)
            {
                if (blendShapeIndex == -1)
                    continue;

                var blendShapeName = source.GetBlendShapeName(blendShapeIndex);
                var frameCount = source.GetBlendShapeFrameCount(blendShapeIndex);

                // For each frame in the blend shape
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var deltaVertices = new Vector3[source.vertexCount];
                    var deltaNormals = new Vector3[source.vertexCount];
                    var deltaTangents = new Vector3[source.vertexCount];

                    // Get the blend shape data
                    source.GetBlendShapeFrameVertices(blendShapeIndex, frameIndex, deltaVertices, deltaNormals,
                        deltaTangents);
                    var frameWeight = source.GetBlendShapeFrameWeight(blendShapeIndex, frameIndex);

                    // Add the blend shape to the destination mesh
                    destination.AddBlendShapeFrame(blendShapeName, frameWeight, deltaVertices, deltaNormals,
                        deltaTangents);
                }
            }
        }

        private static void TransferBoneWeights(Mesh source, Mesh destination)
        {
            // Check if the source has bone weights
            if (source.boneWeights == null || source.boneWeights.Length == 0)
            {
                Debug.LogWarning("Source mesh does not have bone weights to transfer.");
                return;
            }

            // Check if the vertex counts match
            if (source.vertexCount != destination.vertexCount)
            {
                Debug.LogError(
                    "Source and destination meshes have different vertex counts. Cannot transfer bone weights.");
                return;
            }

            // Copy bone weights
            destination.boneWeights = source.boneWeights;

            // Copy bind poses
            destination.bindposes = source.bindposes;
        }

        private static void CopyMeshData(Mesh source, Mesh destination)
        {
            // Copy UV sets
            if (source.uv is { Length: > 0 })
                destination.uv = source.uv;

            if (source.uv2 is { Length: > 0 })
                destination.uv2 = source.uv2;

            if (source.uv3 is { Length: > 0 })
                destination.uv3 = source.uv3;

            if (source.uv4 is { Length: > 0 })
                destination.uv4 = source.uv4;

            // Copy tangents
            if (source.tangents is { Length: > 0 })
                destination.tangents = source.tangents;

            // Set the same submesh count
            destination.subMeshCount = source.subMeshCount;

            // Copy submesh topology
            for (var i = 0; i < source.subMeshCount; i++)
            {
                destination.SetSubMesh(i, source.GetSubMesh(i));
            }
        }

        private static Bounds GetAvatarBounds(GameObject avatarObject)
        {
            var bounds = new Bounds();
            if (avatarObject == null)
                return bounds;
            var meshRenderers = avatarObject.GetComponentsInChildren<MeshRenderer>();
            var skinnedMeshRenderers = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in meshRenderers)
                bounds.Encapsulate(renderer.bounds);

            foreach (var renderer in skinnedMeshRenderers)
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }

        private static float GetAvatarHeight(GameObject avatarObject)
        {
            // height of an avatar model can be determined to be the max Y extents of the combined bounds for all its mesh renderers
            var avatarBounds = GetAvatarBounds(avatarObject);
            return avatarBounds.max.y - avatarBounds.min.y;
        }
    }
}
#endif