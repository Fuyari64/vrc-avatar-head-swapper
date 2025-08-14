using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditorInternal;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Debug = UnityEngine.Debug;

namespace Fuyari64.VrcAvatarHeadSwapper
{
    public class VrcAvatarHeadSwapperEditorWindow : EditorWindow
    {
        private GameObject headAvatar;
        private GameObject bodyAvatar;

        private GameObject headPreviewInstance;

        private string blenderPath;

        [MenuItem("Tools/HeadSwapper")]
        public static void ShowWindow()
        {
            var window = GetWindow<VrcAvatarHeadSwapperEditorWindow>();
            window.minSize = new Vector2(300, 300);
        }

        private void OnEnable()
        {
            blenderPath = TryToGetBlenderPath();
        }

        private void OnGUI()
        {
            GUILayout.Label("VrcAvatarHeadSwapper - Select your avatars", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "Instructions:\n" +
                "0. Make sure you have blender installed and available on system PATH\n" +
                "1. Delete/Hide meshes from head or body avatars that you want to remove (e.g. the Head of the target avatar, the Body of the source avatar)\n" +
                "2. Position head avatar in unity space where you would like the head to be on the final merge\n" +
                "3. Execute script\n" +
                "4. Validate that the physbones/constraints/colliders were migrated successfully. Extra cleanup may be necessary depending on armature complexity",
                MessageType.Info
            );
            EditorGUILayout.Space();
            
            headAvatar = (GameObject)EditorGUILayout.ObjectField("Head Avatar Prefab", headAvatar, typeof(GameObject), true);
            bodyAvatar = (GameObject)EditorGUILayout.ObjectField("Body Avatar Prefab", bodyAvatar, typeof(GameObject), true);
            string? headFbxPath = null;
            string? bodyFbxPath = null;

            if (headAvatar != null)
            {
                headFbxPath = GetFbxPathFromGameObject(headAvatar);
                EditorGUILayout.LabelField("Head FBX Path:", headFbxPath);
            }

            if (bodyAvatar != null)
            {
                bodyFbxPath = GetFbxPathFromGameObject(bodyAvatar);
                EditorGUILayout.LabelField("Body FBX Path:", bodyFbxPath);
            }

            if (!string.IsNullOrEmpty(this.blenderPath) && File.Exists(this.blenderPath))
            {
                if (GUILayout.Button("Test launch blender"))
                {
                    Process.Start(blenderPath);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Blender not found on system PATH. Please select it manually", MessageType.Warning);
                if (GUILayout.Button("Browse..."))
                {
                    string selected = EditorUtility.OpenFilePanel("Select Blender Executable", "", "exe");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        blenderPath = selected;
                    }
                }
            }

            if (!string.IsNullOrEmpty(blenderPath) && headAvatar != null && bodyAvatar != null)
            {
                if (GUILayout.Button("Execute Head Swap"))
                {
                    WriteTransformData();

                    ExecuteHeadSwap(headFbxPath, bodyFbxPath);

                    GameObject mergedFbx = GetMostRecentFbx(Path.Combine(Application.dataPath, "Fuyari64", "Temp"));

                    if (mergedFbx != null)
                    {
                        ProcessMergedAvatar(mergedFbx);
                    }
                }
            }
        }

        private void ProcessMergedAvatar(GameObject mergedFbx)
        {
            ApplyHumanoidSettings(mergedFbx);

            GameObject sceneInstance = PrefabUtility.InstantiatePrefab(mergedFbx, SceneManager.GetActiveScene()) as GameObject;

            CopyVrcAvatarDescriptor(headAvatar, sceneInstance);

            CopyMaterialsFromAvatars(headAvatar, bodyAvatar, sceneInstance);

            CopyBonesAndModifiers(headAvatar, bodyAvatar, sceneInstance);

            CopyBlendshapes(headAvatar, bodyAvatar, sceneInstance);

            ApplyBoundingBoxesAndLightAnchors(sceneInstance);

            Selection.activeObject = sceneInstance;
            Debug.Log("Merged avatar processed and added to scene!");
        }

        private void ApplyHumanoidSettings(GameObject mergedFbx)
        {
            string assetPath = AssetDatabase.GetAssetPath(mergedFbx);
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (modelImporter != null)
            {
                modelImporter.animationType = ModelImporterAnimationType.Human;
                modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                Debug.Log($"Applied humanoid settings to {assetPath}");
            }
        }

        private void CopyBonesAndModifiers(GameObject headAvatar, GameObject bodyAvatar, GameObject mergedFbx)
        {
            // Process existing GameObjects in mergedFbx
            var nonSkinnedMeshRenderer = mergedFbx.GetComponentsInChildren<Transform>()
                .Where(t => t.GetComponent<SkinnedMeshRenderer>() == null)
                .ToArray();

            // First pass: Create all colliders and copy rotation constraints
            foreach (var child in nonSkinnedMeshRenderer)
            {
                var bodySourceChild = bodyAvatar.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == child.name);
                if (bodySourceChild != null)
                {
                    var bodyComponents = bodySourceChild.GetComponents<Component>();
                    foreach (var bodyComponent in bodyComponents)
                    {
                        if (bodyComponent is VRCPhysBoneCollider)
                        {
                            UpdatePhysBoneCollider(bodyComponent, child.gameObject, mergedFbx);
                        }
                        else if (bodyComponent is VRCRotationConstraint)
                        {
                            UpdateRotationConstraint(bodyComponent, child.gameObject, mergedFbx); 
                        }
                    }
                }

                var headSourceChild = headAvatar.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == child.name);
                if (headSourceChild != null)
                {
                    var headComponents = headSourceChild.GetComponents<Component>();
                    foreach (var headComponent in headComponents)
                    {
                        if (headComponent is VRCPhysBoneCollider)
                        {
                            UpdatePhysBoneCollider(headComponent, child.gameObject, mergedFbx);
                        }
                        else if (headComponent is VRCRotationConstraint)
                        {
                            UpdateRotationConstraint(headComponent, child.gameObject, mergedFbx);
                        }
                    }
                }
            }

            // Second pass: Create all PhysBones and link them to colliders
            foreach (var child in nonSkinnedMeshRenderer)
            {
                var bodySourceChild = bodyAvatar.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == child.name);
                if (bodySourceChild != null)
                {
                    var bodyComponents = bodySourceChild.GetComponents<Component>();
                    foreach (var bodyComponent in bodyComponents)
                    {
                        if (bodyComponent is VRCPhysBone)
                        {
                            UpdatePhysBone(bodyComponent, child.gameObject, mergedFbx);
                        }
                    }
                }

                var headSourceChild = headAvatar.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == child.name);
                if (headSourceChild != null)
                {
                    var headComponents = headSourceChild.GetComponents<Component>();
                    foreach (var headComponent in headComponents)
                    {
                        if (headComponent is VRCPhysBone)
                        {
                            UpdatePhysBone(headComponent, child.gameObject, mergedFbx);
                        }
                    }
                }
            }

            CreateMissingNeededGameObjects(bodyAvatar, mergedFbx);
            CreateMissingNeededGameObjects(headAvatar, mergedFbx);
        }

        private void CreateMissingNeededGameObjects(GameObject sourceAvatar, GameObject mergedFbx)
        {
            var sourceEmptyTransforms = sourceAvatar.GetComponentsInChildren<Transform>(true)
                .Where(t =>
                    t.GetComponent<SkinnedMeshRenderer>() == null &&
                    t.GetComponent<MeshRenderer>() == null &&
                    t.GetComponent<Animator>() == null &&
                    t.GetComponent<Camera>() == null &&
                    t.GetComponent<Light>() == null &&
                    !IsBone(t)) // Exclude bones
                .ToArray();

            // First pass: Create all colliders
            foreach (var sourceChild in sourceEmptyTransforms)
            {
                var existingTarget = mergedFbx.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == sourceChild.name);
                if (existingTarget == null)
                {
                    if (IsGameObjectNeeded(sourceChild.gameObject, mergedFbx))
                    {
                        if (!WouldModifyAlreadyModifiedBone(sourceChild.gameObject, mergedFbx))
                        {
                            var newGameObject = new GameObject(sourceChild.name);
                            newGameObject.transform.SetParent(mergedFbx.transform);

                            var components = sourceChild.GetComponents<Component>();
                            foreach (var component in components)
                            {
                                if (component is VRCPhysBoneCollider)
                                {
                                    UpdatePhysBoneCollider(component, newGameObject, mergedFbx);
                                }
                            }
                        }
                    }
                }
            }

            // Second pass: Create all PhysBones and link them to colliders. Assuming all empty game objects are already created.
            foreach (var sourceChild in sourceEmptyTransforms)
            {
                var existingTarget = mergedFbx.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == sourceChild.name);
                    if (IsGameObjectNeeded(sourceChild.gameObject, mergedFbx))
                    {
                        if (!WouldModifyAlreadyModifiedBone(sourceChild.gameObject, mergedFbx))
                        {
                            var newGameObject = mergedFbx.transform.Find(sourceChild.name); // this transform.find probably doesnt work, although maybe it works for empty objects
                            if (newGameObject != null)
                            {
                                var components = sourceChild.GetComponents<Component>();
                                foreach (var component in components)
                                {
                                    if (component is VRCPhysBone)
                                    {
                                        UpdatePhysBone(component, newGameObject.gameObject, mergedFbx);
                                    }
                                }
                            }
                        }
                }
            }
        }

        private void UpdatePhysBoneCollider(Component sourceComponent, GameObject targetGameObject, GameObject mergedFbx)
        {
            var sourceCollider = sourceComponent as VRCPhysBoneCollider;
            ComponentUtility.CopyComponent(sourceComponent);
            ComponentUtility.PasteComponentAsNew(targetGameObject);
            var targetCollider = targetGameObject.GetComponent<VRCPhysBoneCollider>();

            if (sourceCollider.rootTransform != null)
            {
                var targetRootTransform = FindTransformByName(mergedFbx, sourceCollider.rootTransform.name);
                targetCollider.rootTransform = targetRootTransform;
            }
        }

        private void UpdatePhysBone(Component sourceComponent, GameObject targetGameObject, GameObject mergedFbx)
        {
            var sourcePhysBone = sourceComponent as VRCPhysBone;
            ComponentUtility.CopyComponent(sourceComponent);
            ComponentUtility.PasteComponentAsNew(targetGameObject);
            var targetPhysBone = targetGameObject.GetComponent<VRCPhysBone>();

            if (sourcePhysBone.rootTransform != null)
            {
                var targetRootTransform = FindTransformByName(mergedFbx, sourcePhysBone.rootTransform.name);
                targetPhysBone.rootTransform = targetRootTransform;
            }

            // Get all colliders from the source PhysBone and find their equivalents by name
            if (sourcePhysBone.colliders != null && sourcePhysBone.colliders.Count > 0)
            {
                var targetColliders = new List<VRCPhysBoneColliderBase>();

                foreach (var sourceCollider in sourcePhysBone.colliders)
                {
                    if (sourceCollider != null)
                    {
                        string colliderName = sourceCollider.name;
                        var targetColliderGameObject = FindTransformByName(mergedFbx, colliderName);
                        if (targetColliderGameObject != null)
                        {
                            var targetCollider = targetColliderGameObject.GetComponent<VRCPhysBoneCollider>();
                            if (targetCollider != null)
                            {
                                targetColliders.Add(targetCollider);
                            }
                        }
                    }
                }

                targetPhysBone.colliders = targetColliders;
            }
        }

        
        private void UpdateRotationConstraint(Component sourceComponent, GameObject targetGameObject, GameObject mergedFbx)
        {
            var sourceConstraint = sourceComponent as VRCRotationConstraint;
            ComponentUtility.CopyComponent(sourceConstraint);
            ComponentUtility.PasteComponentAsNew(targetGameObject);

            var targetConstraint = targetGameObject.GetComponent<VRCRotationConstraint>();
            targetConstraint.Sources.Clear();
            if (targetConstraint != null && sourceConstraint.Sources.Count > 0)
            {
                foreach (var source in sourceConstraint.Sources)
                {
                    if (source.SourceTransform != null)
                    {
                        string boneName = source.SourceTransform.name;
                        var targetBone = FindTransformByName(mergedFbx, boneName);

                        if (targetBone != null)
                        {
                            var newSource = new VRCConstraintSource
                            {
                                SourceTransform = targetBone,
                                Weight = source.Weight,
                            };

                            targetConstraint.Sources.Add(newSource);
                        }
                    }
                }
            }
        }
        
        private bool IsGameObjectNeeded(GameObject sourceGameObject, GameObject mergedFbx)
        {
            var components = sourceGameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component is VRCPhysBone physBone)
                {
                    if (physBone.rootTransform != null &&
                        FindTransformByName(mergedFbx, physBone.rootTransform.name) != null)
                    {
                        return true;
                    }
                }
                if (component is VRCPhysBoneCollider collider)
                {
                    if (collider.rootTransform != null &&
                        FindTransformByName(mergedFbx, collider.rootTransform.name) != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool WouldModifyAlreadyModifiedBone(GameObject sourceGameObject, GameObject mergedFbx)
        {
            // Check if any bone this GameObject would modify has already being modified
            var components = sourceGameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component is VRCPhysBone physBone)
                {
                    if (physBone.rootTransform != null)
                    {
                        var targetBone = FindTransformByName(mergedFbx, physBone.rootTransform.name);
                        if (targetBone != null)
                        {
                            var existingPhysBones = targetBone.GetComponentsInChildren<VRCPhysBone>();
                            if (existingPhysBones.Length > 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool IsBone(Transform transform)
        {
            // Check if this transform is part of the humanoid bone hierarchy
            var animator = transform.GetComponentInParent<Animator>();
            if (animator != null && animator.avatar != null)
            {
                var humanBones = animator.avatar.humanDescription.human;
                foreach (var humanBone in humanBones)
                {
                    if (humanBone.boneName == transform.name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Transform FindTransformByName(GameObject root, string transformName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == transformName);
        }

        private void CopyBlendshapes(GameObject headAvatar, GameObject bodyAvatar, GameObject targetAvatar)
        {
            var headMeshes = headAvatar.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bodyMeshes = bodyAvatar.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var mesh in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                SkinnedMeshRenderer importMesh = headMeshes.FirstOrDefault(smr => smr.name == mesh.name) ?? bodyMeshes.FirstOrDefault(smr => smr.name == mesh.name);
                if (importMesh != null)
                {
                    for (var i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                    {
                        mesh.SetBlendShapeWeight(i, importMesh.GetBlendShapeWeight(i));
                    }
                }
            }
        }

        private void ApplyBoundingBoxesAndLightAnchors(GameObject targetAvatar)
        {
            var anchorBone = targetAvatar.GetComponentsInChildren<Transform>(true)
                                 .FirstOrDefault(t => t.name == "Chest") ??
                             targetAvatar.GetComponentsInChildren<Transform>(true)
                                 .FirstOrDefault(t => t.name == "Hips");

            SkinnedMeshRenderer[] meshes = targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var mesh in meshes)
            {
                if (anchorBone != null)
                {
                    mesh.probeAnchor = anchorBone;
                }

                // Has to be multiplied by 2 for whatever reason in order to be 1,1,1
                mesh.localBounds = new Bounds(Vector3.zero, Vector3.one * 2);
            }
        }

        private void CopyMaterialsFromAvatars(GameObject headAvatar, GameObject bodyAvatar, GameObject targetAvatar)
        {
            var headMeshes = headAvatar.GetComponentsInChildren<SkinnedMeshRenderer>()
                .ToDictionary(m => m.name, m => m.sharedMaterials);
            var bodyMeshes = bodyAvatar.GetComponentsInChildren<SkinnedMeshRenderer>()
                .ToDictionary(m => m.name, m => m.sharedMaterials);

            foreach (var mesh in targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (headMeshes.TryGetValue(mesh.name, out var headMaterials))
                {
                    // mesh.materials causes reference errors on export
                    mesh.sharedMaterials = headMaterials;
                }
                else if (bodyMeshes.TryGetValue(mesh.name, out var bodyMaterials))
                {
                    mesh.sharedMaterials = bodyMaterials;
                }
            }
        }

        private void CopyVrcAvatarDescriptor(GameObject sourceAvatar, GameObject targetAvatar)
        {
            var sourceDescriptor = sourceAvatar.GetComponent<VRCAvatarDescriptor>();
            if (sourceDescriptor == null) return;

            ComponentUtility.CopyComponent(sourceDescriptor);
            ComponentUtility.PasteComponentAsNew(targetAvatar);

            var targetDescriptor = targetAvatar.GetComponent<VRCAvatarDescriptor>();

            string headVisemeBodyMeshName = sourceDescriptor.VisemeSkinnedMesh.name;

            // assign new Body
            var mergedBodyMesh = targetAvatar.GetComponentsInChildren<SkinnedMeshRenderer>()
                .FirstOrDefault(smr => smr.name == headVisemeBodyMeshName);

            if (mergedBodyMesh != null)
            {
                Debug.Log(mergedBodyMesh.name);
                targetDescriptor.VisemeSkinnedMesh = mergedBodyMesh;
            }

            // assign new eye bones
            var mergedAvatarLeftEye = targetAvatar.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(tr => tr.name == sourceDescriptor.customEyeLookSettings.leftEye.name);
            var mergedAvatarRightEye = targetAvatar.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(tr => tr.name == sourceDescriptor.customEyeLookSettings.rightEye.name);
            if (mergedAvatarLeftEye != null || mergedAvatarRightEye != null)
            {
                Debug.Log("About to assign eye name");
                targetDescriptor.customEyeLookSettings.leftEye = mergedAvatarLeftEye;
                targetDescriptor.customEyeLookSettings.rightEye = mergedAvatarRightEye;
            }
            ;
        }

        private void ExecuteHeadSwap(string headAvatarFbxPath, string bodyAvatarFbxPath)
        {
            // Convert Unity asset paths to absolute file system paths
            string headAbsolutePath = Path.GetFullPath(headAvatarFbxPath);
            string bodyAbsolutePath = Path.GetFullPath(bodyAvatarFbxPath);

            // Get the transform.json path
            string transformJsonPath = Path.Combine(Application.dataPath, "Fuyari64", "Temp", "transform.json");

            // Build the Blender command
            string arguments = $"--background --python \"{Path.Combine(Application.dataPath, "Fuyari64", "PythonFiles", "main.py")}\" -- \"{headAbsolutePath}\" \"{bodyAbsolutePath}\" \"{transformJsonPath}\"";

            Debug.Log($"Executing Blender command: {blenderPath} {arguments}");

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = blenderPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.Combine(Application.dataPath, "Fuyari64")
                };

                using (Process process = Process.Start(psi))
                {
                    // Read output in real-time
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Debug.Log($"Blender: {e.Data}");
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Debug.LogError($"Blender Error: {e.Data}");
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for completion
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Debug.Log("Blender scripts executed successsfully!");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", $"Blender process failed with exit code: {process.ExitCode}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to execute Blender: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to execute Blender: {ex.Message}", "OK");
            }
        }

        private string GetFbxPathFromGameObject(GameObject avatar)
        {
            if (avatar == null) return "No avatar selected";

            var animator = avatar.GetComponent<Animator>();
            if (animator == null || animator.avatar == null)
            {
                return "No Animator or Avatar found";
            }

            string avatarPath = AssetDatabase.GetAssetPath(animator.avatar);

            if (string.IsNullOrEmpty(avatarPath))
            {
                return "Avatar path not found";
            }

            return avatarPath;
        }

        private string TryToGetBlenderPath()
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "blender",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadLine();
                process.WaitForExit();
                if (!string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    return output.Trim();
                }
            }

            return null;
        }

        private List<string> GetMeshNames(GameObject avatar)
        {
            var skinnedMeshes = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            List<string> meshNames = new List<string>();

            foreach (var smr in skinnedMeshes)
            {
                if (smr.sharedMesh != null)
                {
                    meshNames.Add(smr.sharedMesh.name);
                }
            }

            return meshNames;
        }

        private GameObject GetMostRecentFbx(string tempFolder)
        {
            var fbxFiles = Directory.GetFiles(tempFolder, "*.fbx");
            if (fbxFiles.Length == 0)
                return null;

            var mostRecentFbx = fbxFiles
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .First();

            // Unity asset operators expect relative paths
            string fullPath = mostRecentFbx.FullName;
            int assetsIndex = fullPath.IndexOf("Assets");
            string assetPath = fullPath.Substring(assetsIndex);

            assetPath = assetPath.Replace('\\', '/');

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        private void WriteTransformData()
        {
            var data = new AvatarsData
            {
                headAvatarMetadata = new TransformData
                {
                    position = headAvatar.transform.position,
                    rotation = headAvatar.transform.eulerAngles,
                    scale = headAvatar.transform.localScale,
                    meshes = GetMeshNames(headAvatar),
                },
                bodyAvatarMetadata = new TransformData
                {
                    position = bodyAvatar.transform.position,
                    rotation = bodyAvatar.transform.eulerAngles,
                    scale = bodyAvatar.transform.localScale,
                    meshes = GetMeshNames(bodyAvatar),
                }
            };

            File.WriteAllText(Application.dataPath + "/Fuyari64/Temp/transform.json", JsonUtility.ToJson(data, true));
        }

        [System.Serializable]
        public class TransformData
        {
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 scale;
            public List<string> meshes;
        }

        [System.Serializable]
        public class AvatarsData
        {
            public TransformData headAvatarMetadata;
            public TransformData bodyAvatarMetadata;
        }
    }
}