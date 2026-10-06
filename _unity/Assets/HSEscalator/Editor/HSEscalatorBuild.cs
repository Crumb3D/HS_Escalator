using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Import settings for the Blender FBX files: 1 unit = 1 m, axis already baked by the exporter.
public class HSEscalatorModelImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Replace('\\', '/').Contains("/HSEscalator/Models/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.bakeAxisConversion = false;
        mi.preserveHierarchy = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importBlendShapes = false;
        mi.addCollider = false;
        mi.isReadable = false;
        mi.importNormals = ModelImporterNormals.Import;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        mi.meshCompression = ModelImporterMeshCompression.Off;
    }
}

public static class HSEscalatorBuild
{
    const string Root = "Assets/HSEscalator";
    const string BundleName = "hsescalator";
    const string BlockTag = "T_Block";

    public static void Build()
    {
        var log = new StringBuilder();
        int code = 0;
        try
        {
            EnsureTag(BlockTag);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            var mats = EnsureMaterials();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            int made = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) continue;
                var go = UnityEngine.Object.Instantiate(src);
                go.name = src.name;
                Process(go, mats);
                var prefabPath = Root + "/Prefabs/" + src.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
                AssetImporter.GetAtPath(prefabPath).SetAssetBundleNameAndVariant(BundleName, "");
                log.AppendLine(src.name);
                made++;
            }
            AssetDatabase.SaveAssets();
            if (made == 0) throw new Exception("No models found in " + Root + "/Models");

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BundleOut"));
            Directory.CreateDirectory(outDir);
            var manifest = BuildPipeline.BuildAssetBundles(outDir,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("BuildAssetBundles returned null");

            var modResources = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Resources"));
            Directory.CreateDirectory(modResources);
            var dest = Path.Combine(modResources, "HSEscalator.unity3d");
            File.Copy(Path.Combine(outDir, BundleName), dest, true);
            log.AppendLine("Bundle: " + dest + " (" + new FileInfo(dest).Length + " bytes, " + made + " prefabs)");
            log.AppendLine("DONE");
        }
        catch (Exception e)
        {
            code = 1;
            log.AppendLine("FAILED: " + e);
        }
        var report = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "build_report.txt"));
        File.WriteAllText(report, log.ToString());
        Debug.Log("[HSEscalatorBuild]\n" + log);
        EditorApplication.Exit(code);
    }

    static int Process(GameObject go, Dictionary<string, Material> mats)
    {
        go.tag = BlockTag;
        go.layer = 16;

        var combined = new Bounds(Vector3.zero, Vector3.one);
        bool anyCol = false;
        var colObjs = new List<GameObject>();

        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 16;
            if (t != go.transform) t.gameObject.tag = "Untagged";

            if (t.name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase))
            {
                var mf = t.GetComponent<MeshFilter>();
                var mb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                for (int i = 0; i < 8; i++)
                {
                    var lp = mb.center + Vector3.Scale(mb.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));
                    var rootLocal = go.transform.InverseTransformPoint(t.TransformPoint(lp));
                    if (!anyCol) { combined = new Bounds(rootLocal, Vector3.zero); anyCol = true; }
                    else combined.Encapsulate(rootLocal);
                }
                colObjs.Add(t.gameObject);
                continue;
            }

            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var shared = mr.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                var key = shared[i] != null ? CleanName(shared[i].name) : "";
                Material m;
                if (mats.TryGetValue(key, out m)) shared[i] = m;
                else Debug.LogWarning("[HSEscalatorBuild] No material for slot '" + key + "' on " + go.name);
            }
            mr.sharedMaterials = shared;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        for (int i = 0; i < colObjs.Count; i++)
            UnityEngine.Object.DestroyImmediate(colObjs[i]);

        var existing = go.GetComponents<BoxCollider>();
        for (int i = 0; i < existing.Length; i++)
            UnityEngine.Object.DestroyImmediate(existing[i]);
        var bc = go.AddComponent<BoxCollider>();
        if (!anyCol)
        {
            bc.center = new Vector3(0.5f, 0.9f, 0.1f);
            bc.size = new Vector3(1f, 1.6f, 0.22f);
        }
        else
        {
            bc.center = combined.center;
            var sz = combined.size;
            if (sz.x < 0.2f) sz.x = 0.2f;
            if (sz.y < 0.2f) sz.y = 0.2f;
            if (sz.z < 0.2f) sz.z = 0.2f;
            bc.size = sz;
        }
        bc.isTrigger = false;
        return 1;
    }

    static string CleanName(string n)
    {
        n = n.Replace(" (Instance)", "");
        int dot = n.IndexOf('.');
        if (dot > 0) n = n.Substring(0, dot);
        return n.Trim();
    }

    static Dictionary<string, Material> EnsureMaterials()
    {
        var d = new Dictionary<string, Material>();
        d["HS_Glass"] = Glass("HS_Glass", new Color(0.72f, 0.86f, 0.9f, 0.28f));
        d["HS_FrameDark"] = Opaque("HS_FrameDark", new Color(0.06f, 0.06f, 0.065f), 0.65f, 0.45f);
        d["HS_FrameSilver"] = Opaque("HS_FrameSilver", new Color(0.78f, 0.79f, 0.8f), 0.85f, 0.55f);
        var light = Opaque("HS_LightGreen", new Color(0.25f, 1f, 0.45f), 0f, 0.6f);
        light.EnableKeyword("_EMISSION");
        light.SetColor("_EmissionColor", new Color(0.15f, 1f, 0.35f) * 2.2f);
        light.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(light);
        d["HS_LightGreen"] = light;
        AssetDatabase.SaveAssets();
        return d;
    }

    static Material Load(string name)
    {
        var path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Standard");
        return m;
    }

    static Material Opaque(string name, Color c, float metallic, float smooth)
    {
        var m = Load(name);
        m.SetFloat("_Mode", 0f);
        m.SetOverrideTag("RenderType", "");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = -1;
        m.color = c;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Glass(string name, Color c)
    {
        var m = Load(name);
        m.SetFloat("_Mode", 3f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.color = c;
        m.SetFloat("_Metallic", 0.1f);
        m.SetFloat("_Glossiness", 0.95f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void EnsureTag(string tag)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;
        var so = new SerializedObject(assets[0]);
        var tags = so.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
