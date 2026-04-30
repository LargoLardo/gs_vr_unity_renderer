using System;
using System.IO;
using System.Reflection;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEngine;

namespace Reminiscence.Editor
{
    public static class BatchGaussianSplatImporter
    {
        const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        const string CreatorTypeName = "GaussianSplatting.Editor.GaussianSplatAssetCreator, GaussianSplattingEditor";

        public static void ImportFromCommandLine()
        {
            try
            {
                string inputFile = RequiredArg("-gsInput");
                string outputFolder = OptionalArg("-gsOutputFolder", "Assets/GaussianAssets");
                string quality = OptionalArg("-gsQuality", "Medium");

                Import(inputFile, outputFolder, quality);
                UnityEngine.Debug.Log($"Imported Gaussian splat asset from {inputFile} into {outputFolder}");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        static void Import(string inputFile, string outputFolder, string quality)
        {
            inputFile = Path.GetFullPath(inputFile).Replace("\\", "/");
            outputFolder = outputFolder.Replace("\\", "/").TrimEnd('/');

            if (!File.Exists(inputFile))
                throw new FileNotFoundException("Gaussian splat input file was not found.", inputFile);

            if (string.IsNullOrWhiteSpace(outputFolder) || (outputFolder != "Assets" && !outputFolder.StartsWith("Assets/", StringComparison.Ordinal)))
                throw new ArgumentException($"Output folder must be inside Assets/: {outputFolder}");

            Type creatorType = Type.GetType(CreatorTypeName);
            if (creatorType == null)
                throw new InvalidOperationException("Gaussian Splatting package editor tools are not available.");

            ScriptableObject creator = ScriptableObject.CreateInstance(creatorType);
            try
            {
                SetField(creatorType, creator, "m_InputFile", inputFile);
                SetField(creatorType, creator, "m_OutputFolder", outputFolder);
                SetField(creatorType, creator, "m_ImportCameras", true);
                SetQuality(creatorType, creator, quality);

                Invoke(creatorType, creator, "ApplyQualityLevel");
                Invoke(creatorType, creator, "CreateAsset");

                string errorMessage = GetField<string>(creatorType, creator, "m_ErrorMessage");
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    throw new InvalidOperationException(errorMessage);

                string assetName = Path.GetFileNameWithoutExtension(inputFile);
                string assetPath = $"{outputFolder}/{assetName}.asset";
                AssetDatabase.Refresh(ImportAssetOptions.ForceUncompressedImport);
                GaussianSplatAsset asset = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(assetPath);
                if (asset == null)
                    throw new FileNotFoundException("Gaussian splat asset was not created.", assetPath);

                CreateRendererPrefab(outputFolder, assetName, asset);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creator);
                EditorUtility.ClearProgressBar();
            }
        }

        static void CreateRendererPrefab(string outputFolder, string assetName, GaussianSplatAsset asset)
        {
            string prefabPath = $"{outputFolder}/{assetName}_Renderer.prefab";
            string latestPrefabPath = $"{outputFolder}/LatestGaussianSplat.prefab";

            GameObject go = new GameObject($"{assetName} Gaussian Splat");
            try
            {
                GaussianSplatRenderer renderer = go.AddComponent<GaussianSplatRenderer>();
                renderer.m_Asset = asset;
                renderer.m_ShaderSplats = LoadRequiredAsset<Shader>("Packages/org.nesnausk.gaussian-splatting/Shaders/RenderGaussianSplats.shader");
                renderer.m_ShaderComposite = LoadRequiredAsset<Shader>("Packages/org.nesnausk.gaussian-splatting/Shaders/GaussianComposite.shader");
                renderer.m_ShaderDebugPoints = LoadRequiredAsset<Shader>("Packages/org.nesnausk.gaussian-splatting/Shaders/GaussianDebugRenderPoints.shader");
                renderer.m_ShaderDebugBoxes = LoadRequiredAsset<Shader>("Packages/org.nesnausk.gaussian-splatting/Shaders/GaussianDebugRenderBoxes.shader");
                renderer.m_CSSplatUtilities = LoadRequiredAsset<ComputeShader>("Packages/org.nesnausk.gaussian-splatting/Shaders/SplatUtilities.compute");

                PlaceSplatInFrontOfCamera placer = go.AddComponent<PlaceSplatInFrontOfCamera>();
                placer.splat = go.transform;
                placer.splatRenderer = renderer;

                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                PrefabUtility.SaveAsPrefabAsset(go, latestPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException($"Could not load required Gaussian Splatting package asset: {path}");
            return asset;
        }

        static void SetQuality(Type creatorType, object creator, string quality)
        {
            Type qualityType = creatorType.GetNestedType("DataQuality", BindingFlags.NonPublic);
            if (qualityType == null)
                throw new InvalidOperationException("Could not find Gaussian splat quality enum.");

            object qualityValue = Enum.Parse(qualityType, quality, ignoreCase: true);
            SetField(creatorType, creator, "m_Quality", qualityValue);
        }

        static void SetField(Type type, object instance, string name, object value)
        {
            FieldInfo field = type.GetField(name, InstancePrivate);
            if (field == null)
                throw new MissingFieldException(type.FullName, name);
            field.SetValue(instance, value);
        }

        static T GetField<T>(Type type, object instance, string name)
        {
            FieldInfo field = type.GetField(name, InstancePrivate);
            if (field == null)
                throw new MissingFieldException(type.FullName, name);
            return (T)field.GetValue(instance);
        }

        static void Invoke(Type type, object instance, string name)
        {
            MethodInfo method = type.GetMethod(name, InstancePrivate);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);

            try
            {
                method.Invoke(instance, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        static string RequiredArg(string name)
        {
            string value = OptionalArg(name, null);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Missing required command line argument: {name}");
            return value;
        }

        static string OptionalArg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return fallback;
        }
    }
}
