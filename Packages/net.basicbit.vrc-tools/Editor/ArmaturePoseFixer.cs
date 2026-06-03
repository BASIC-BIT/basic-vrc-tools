using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BasicBit.VrcTools.Editor
{
    internal static class ArmaturePoseFixer
    {
        private const string MenuPath = "BASIC/BASIC VRC Tools/Fix Bones on AVI";

        [MenuItem(MenuPath, true)]
        private static bool ValidateFixBonesOnAvi()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(MenuPath)]
        private static void FixBonesOnAvi()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("BASIC VRC Tools", "Select an avatar root or avatar child first.", "OK");
                return;
            }

            var avatarRoot = FindAvatarRoot(selected.transform);
            var pairs = new List<BonePair>();

            pairs.AddRange(GetModularAvatarPairs(avatarRoot));
            pairs.AddRange(GetVrcFuryPairs(avatarRoot));

            if (pairs.Count == 0)
            {
                pairs.AddRange(GetGenericSkinnedMeshPairs(avatarRoot));
            }

            var result = ApplyPairs(pairs);
            var message = result.Moved == 0
                ? $"No bones needed changes. Checked {result.Checked} matching bone pairs."
                : $"Moved {result.Moved} bones. Checked {result.Checked} matching bone pairs.";

            Debug.Log($"[BASIC VRC Tools] {message} Selected root: {avatarRoot.name}");
            EditorUtility.DisplayDialog("BASIC VRC Tools", message, "OK");
        }

        private static Transform FindAvatarRoot(Transform selected)
        {
            var descriptor = FindAncestorComponent(selected, "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (descriptor != null) return descriptor.transform;

            var animator = selected.GetComponentInParent<Animator>();
            if (animator != null) return animator.transform;

            return selected;
        }

        private static Component FindAncestorComponent(Transform selected, string fullTypeName)
        {
            for (var current = selected; current != null; current = current.parent)
            {
                foreach (var component in current.GetComponents<Component>())
                {
                    if (component != null && component.GetType().FullName == fullTypeName)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<BonePair> GetModularAvatarPairs(Transform avatarRoot)
        {
            foreach (var component in avatarRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != "nadena.dev.modular_avatar.core.ModularAvatarMergeArmature")
                {
                    continue;
                }

                var getBonesMapping = component.GetType().GetMethod("GetBonesMapping", BindingFlags.Instance | BindingFlags.Public);
                if (getBonesMapping == null) continue;

                if (!(getBonesMapping.Invoke(component, Array.Empty<object>()) is IEnumerable mapping)) continue;

                foreach (var item in mapping)
                {
                    var itemType = item.GetType();
                    var baseBone = itemType.GetField("Item1")?.GetValue(item) as Transform;
                    var mergeBone = itemType.GetField("Item2")?.GetValue(item) as Transform;
                    if (baseBone != null && mergeBone != null && baseBone != mergeBone)
                    {
                        yield return new BonePair(baseBone, mergeBone, "Modular Avatar");
                    }
                }
            }
        }

        private static IEnumerable<BonePair> GetVrcFuryPairs(Transform avatarRoot)
        {
            foreach (var component in avatarRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != "VF.Model.VRCFury") continue;

                var content = component.GetType().GetField("content", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(component);
                if (content == null || content.GetType().FullName != "VF.Model.Feature.ArmatureLink") continue;

                var propBone = ReadField<GameObject>(content, "propBone");
                var linkTo = ReadField<IEnumerable>(content, "linkTo");
                if (propBone == null || linkTo == null) continue;

                foreach (var target in ReadExplicitVrcFuryTargets(linkTo))
                {
                    foreach (var pair in MatchByRelativeNames(target.transform, propBone.transform, "VRCFury"))
                    {
                        yield return pair;
                    }
                }
            }
        }

        private static IEnumerable<GameObject> ReadExplicitVrcFuryTargets(IEnumerable linkTo)
        {
            foreach (var entry in linkTo)
            {
                if (entry == null) continue;

                var useObj = ReadField<bool>(entry, "useObj");
                var obj = ReadField<GameObject>(entry, "obj");
                if (useObj && obj != null)
                {
                    yield return obj;
                }
            }
        }

        private static T ReadField<T>(object source, string fieldName)
        {
            var field = source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return default;
            var value = field.GetValue(source);
            return value is T typed ? typed : default;
        }

        private static IEnumerable<BonePair> GetGenericSkinnedMeshPairs(Transform avatarRoot)
        {
            var baseRoot = FindHumanoidArmatureRoot(avatarRoot);
            if (baseRoot == null) yield break;

            var baseByName = baseRoot.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => t.name)
                .ToDictionary(g => g.Key, g => g.First());

            var seen = new HashSet<Transform>();
            foreach (var skin in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var bone in skin.bones)
                {
                    if (bone == null || bone.IsChildOf(baseRoot) || !seen.Add(bone)) continue;
                    if (baseByName.TryGetValue(bone.name, out var baseBone) && baseBone != bone)
                    {
                        yield return new BonePair(baseBone, bone, "Generic skin bone");
                    }
                }
            }
        }

        private static Transform FindHumanoidArmatureRoot(Transform avatarRoot)
        {
            var animator = avatarRoot.GetComponentInChildren<Animator>(true);
            var hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips == null) return null;

            var current = hips;
            while (current.parent != null && current.parent != avatarRoot)
            {
                current = current.parent;
            }

            return current;
        }

        private static IEnumerable<BonePair> MatchByRelativeNames(Transform baseRoot, Transform mergeRoot, string source)
        {
            var baseByName = baseRoot.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => t.name)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var mergeBone in mergeRoot.GetComponentsInChildren<Transform>(true))
            {
                if (mergeBone == mergeRoot) continue;
                if (baseByName.TryGetValue(mergeBone.name, out var baseBone) && baseBone != mergeBone)
                {
                    yield return new BonePair(baseBone, mergeBone, source);
                }
            }
        }

        private static ApplyResult ApplyPairs(IEnumerable<BonePair> pairs)
        {
            var checkedCount = 0;
            var movedCount = 0;
            var moved = new HashSet<Transform>();

            foreach (var pair in pairs)
            {
                if (pair.BaseBone == null || pair.MergeBone == null || !moved.Add(pair.MergeBone)) continue;
                checkedCount++;

                if (Vector3.Distance(pair.BaseBone.localPosition, pair.MergeBone.localPosition) < 0.00001f) continue;

                Undo.RecordObject(pair.MergeBone, "Fix avatar merge bone positions");
                pair.MergeBone.localPosition = pair.BaseBone.localPosition;
                EditorUtility.SetDirty(pair.MergeBone);
                movedCount++;

                Debug.Log($"[BASIC VRC Tools] {pair.Source}: {pair.MergeBone.name} localPosition <- {pair.BaseBone.name}");
            }

            return new ApplyResult(checkedCount, movedCount);
        }

        private readonly struct BonePair
        {
            public readonly Transform BaseBone;
            public readonly Transform MergeBone;
            public readonly string Source;

            public BonePair(Transform baseBone, Transform mergeBone, string source)
            {
                BaseBone = baseBone;
                MergeBone = mergeBone;
                Source = source;
            }
        }

        private readonly struct ApplyResult
        {
            public readonly int Checked;
            public readonly int Moved;

            public ApplyResult(int checkedCount, int moved)
            {
                Checked = checkedCount;
                Moved = moved;
            }
        }
    }
}
