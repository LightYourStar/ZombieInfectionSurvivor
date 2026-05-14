#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Game.Config;
using Game.Core;
using Game.Gameplay.Player;
using Game.Gameplay.Wave;
using Game.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class WhiteboxMapSetupTool
    {
        private const string PixelSpritePath = "Assets/Game/Art/Whitebox/WhiteboxPixel.png";
        private static readonly Vector2 MapMin = new Vector2(-20f, -20f);
        private static readonly Vector2 MapMax = new Vector2(20f, 20f);

        [MenuItem("Tools/Game/Build Whitebox Map")]
        public static void BuildCurrentSceneFromMenu()
        {
            BuildCurrentScene(true);
        }

        public static void BuildCurrentScene(bool saveScene)
        {
            Sprite pixelSprite = EnsurePixelSprite();

            GameObject mapRoot = GetOrCreateRoot("MapRoot");
            mapRoot.transform.position = Vector3.zero;

            Transform backgroundRoot = GetOrCreateChild(mapRoot.transform, "Background");
            Transform blockersRoot = GetOrCreateChild(mapRoot.transform, "Blockers");
            Transform hotspotsRoot = GetOrCreateChild(mapRoot.transform, "SpawnHotspots");
            Transform routeHintsRoot = GetOrCreateChild(mapRoot.transform, "RouteHints");
            Transform spawnPointsRoot = GetOrCreateChild(mapRoot.transform, "SpawnPoints");
            Transform systemsRoot = GetOrCreateChild(mapRoot.transform, "Systems");

            ClearChildren(backgroundRoot);
            ClearChildren(blockersRoot);
            ClearChildren(hotspotsRoot);
            ClearChildren(routeHintsRoot);
            ClearChildren(spawnPointsRoot);

            CreateBackground(backgroundRoot, pixelSprite);

            List<Collider2D> blockerColliders = new List<Collider2D>();
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "BorderBlocker_Top", new Vector2(0f, 20.75f), new Vector2(43f, 1.5f), new Color(0.72f, 0.25f, 0.2f, 0.85f)));
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "BorderBlocker_Bottom", new Vector2(0f, -20.75f), new Vector2(43f, 1.5f), new Color(0.72f, 0.25f, 0.2f, 0.85f)));
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "BorderBlocker_Left", new Vector2(-20.75f, 0f), new Vector2(1.5f, 43f), new Color(0.72f, 0.25f, 0.2f, 0.85f)));
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "BorderBlocker_Right", new Vector2(20.75f, 0f), new Vector2(1.5f, 43f), new Color(0.72f, 0.25f, 0.2f, 0.85f)));
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "BuildingBlocker_01", new Vector2(-5.5f, 7f), new Vector2(6f, 5f), new Color(0.35f, 0.38f, 0.43f, 0.9f)));
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "BuildingBlocker_02", new Vector2(10.5f, 4.5f), new Vector2(5.5f, 8f), new Color(0.35f, 0.38f, 0.43f, 0.9f)));
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "BuildingBlocker_03", new Vector2(-12f, -1.5f), new Vector2(5f, 7f), new Color(0.35f, 0.38f, 0.43f, 0.9f)));
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "FountainBlocker", new Vector2(0f, 0f), new Vector2(3f, 3f), new Color(0.18f, 0.48f, 0.72f, 0.9f)));
            blockerColliders.Add(CreateBoxBlocker(blockersRoot, pixelSprite, "ParkFenceBlocker", new Vector2(-12f, -11f), new Vector2(8f, 1f), new Color(0.24f, 0.5f, 0.28f, 0.9f)));

            List<SpawnHotspot> hotspots = new List<SpawnHotspot>();
            hotspots.Add(CreateHotspot(hotspotsRoot, "Hotspot_Park", new Vector2(-15f, -7.5f), HumanClusterType.Small, 3, 5, 2.8f, 4f, 0f, false, true, true));
            hotspots.Add(CreateHotspot(hotspotsRoot, "Hotspot_Plaza", new Vector2(0f, 3.5f), HumanClusterType.Large, 8, 25, 4.2f, 2.2f, 90f, true, false, true));
            hotspots.Add(CreateHotspot(hotspotsRoot, "Hotspot_CommercialStreet", new Vector2(12.5f, -5f), HumanClusterType.Medium, 8, 12, 3.8f, 0.9f, 15f, true, false, true, 45f, 2.6f));
            hotspots.Add(CreateHotspot(hotspotsRoot, "Hotspot_Residential", new Vector2(-15f, 6f), HumanClusterType.Small, 3, 5, 2.6f, 3f, 0f, false, true, true));
            hotspots.Add(CreateHotspot(hotspotsRoot, "Hotspot_BusStop", new Vector2(13.5f, 10.5f), HumanClusterType.Medium, 6, 10, 3f, 2.2f, 60f, false, false, false));

            CreatePoint(routeHintsRoot, "RoutePoint_01", new Vector2(-15f, -7.5f));
            CreatePoint(routeHintsRoot, "RoutePoint_02", new Vector2(0f, 3.5f));
            CreatePoint(routeHintsRoot, "RoutePoint_03", new Vector2(12.5f, -5f));
            CreatePoint(routeHintsRoot, "RoutePoint_04", new Vector2(-4f, -14f));

            Transform playerSpawnPoint = CreatePoint(spawnPointsRoot, "PlayerSpawnPoint", new Vector2(-16.5f, -8.5f));
            ConfigureRuntimeSystems(systemsRoot, hotspotsRoot, hotspots, blockerColliders, playerSpawnPoint);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (saveScene)
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }

        private static void ConfigureRuntimeSystems(
            Transform systemsRoot,
            Transform hotspotsRoot,
            List<SpawnHotspot> hotspots,
            List<Collider2D> blockerColliders,
            Transform playerSpawnPoint)
        {
            GameSystemRunner runner = Object.FindObjectOfType<GameSystemRunner>();
            SpawnSystem spawnSystem = Object.FindObjectOfType<SpawnSystem>();
            ObjectPoolManager poolManager = Object.FindObjectOfType<ObjectPoolManager>();
            PlayerController playerController = Object.FindObjectOfType<PlayerController>();
            Transform playerTransform = playerController != null ? playerController.transform : null;
            GameConfig gameConfig = runner != null ? runner.GameConfig : null;

            HumanClusterSpawner sourceSpawner = Object.FindObjectOfType<HumanClusterSpawner>(true);
            Transform spawnerTransform = GetOrCreateChild(systemsRoot, "HumanClusterSpawner");
            HumanClusterSpawner clusterSpawner = spawnerTransform.GetComponent<HumanClusterSpawner>();
            if (clusterSpawner == null)
            {
                clusterSpawner = spawnerTransform.gameObject.AddComponent<HumanClusterSpawner>();
            }

            if (sourceSpawner != null && sourceSpawner != clusterSpawner)
            {
                EditorUtility.CopySerialized(sourceSpawner, clusterSpawner);
            }

            ConfigureClusterSpawner(clusterSpawner, poolManager, spawnSystem, gameConfig, playerTransform, hotspotsRoot, hotspots);
            RemoveExtraClusterSpawners(clusterSpawner);

            Transform mapRuntimeTransform = GetOrCreateChild(systemsRoot, "MapRuntimeController");
            MapRuntimeController mapRuntimeController = mapRuntimeTransform.GetComponent<MapRuntimeController>();
            if (mapRuntimeController == null)
            {
                mapRuntimeController = mapRuntimeTransform.gameObject.AddComponent<MapRuntimeController>();
            }
            mapRuntimeController.Configure(spawnSystem, playerTransform, playerSpawnPoint, blockerColliders.ToArray(), MapMin, MapMax);
            SerializedObject clusterObject = new SerializedObject(clusterSpawner);
            SetObjectReference(clusterObject, "m_mapRuntimeController", mapRuntimeController);
            clusterObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clusterSpawner);

            if (playerTransform != null)
            {
                playerTransform.position = new Vector3(playerSpawnPoint.position.x, playerSpawnPoint.position.y, playerTransform.position.z);
            }

            ConfigureBounds(spawnSystem, playerController);

            if (runner != null)
            {
                SerializedObject runnerObject = new SerializedObject(runner);
                SetObjectReference(runnerObject, "m_clusterSpawner", clusterSpawner);
                runnerObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(runner);
            }
        }

        private static void ConfigureClusterSpawner(
            HumanClusterSpawner clusterSpawner,
            ObjectPoolManager poolManager,
            SpawnSystem spawnSystem,
            GameConfig gameConfig,
            Transform playerTransform,
            Transform hotspotsRoot,
            List<SpawnHotspot> hotspots)
        {
            SerializedObject serializedObject = new SerializedObject(clusterSpawner);
            SetObjectReference(serializedObject, "m_poolManager", poolManager);
            SetObjectReference(serializedObject, "m_spawnSystem", spawnSystem);
            SetObjectReference(serializedObject, "m_config", gameConfig);
            SetObjectReference(serializedObject, "m_playerTransform", playerTransform);
            SetObjectReference(serializedObject, "m_hotspotRoot", hotspotsRoot);

            SerializedProperty hotspotList = serializedObject.FindProperty("m_hotspots");
            if (hotspotList != null)
            {
                hotspotList.arraySize = hotspots.Count;
                for (int i = 0; i < hotspots.Count; i++)
                {
                    hotspotList.GetArrayElementAtIndex(i).objectReferenceValue = hotspots[i];
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clusterSpawner);
        }

        private static void ConfigureBounds(SpawnSystem spawnSystem, PlayerController playerController)
        {
            if (spawnSystem != null)
            {
                SerializedObject spawnSystemObject = new SerializedObject(spawnSystem);
                SetVector2(spawnSystemObject, "m_mapMin", MapMin);
                SetVector2(spawnSystemObject, "m_mapMax", MapMax);
                spawnSystemObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(spawnSystem);
            }

            if (playerController != null)
            {
                SerializedObject playerObject = new SerializedObject(playerController);
                SetVector2(playerObject, "m_mapMin", MapMin);
                SetVector2(playerObject, "m_mapMax", MapMax);
                playerObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(playerController);
            }
        }

        private static void RemoveExtraClusterSpawners(HumanClusterSpawner clusterSpawner)
        {
            HumanClusterSpawner[] allSpawners = Object.FindObjectsOfType<HumanClusterSpawner>(true);
            for (int i = 0; i < allSpawners.Length; i++)
            {
                HumanClusterSpawner candidate = allSpawners[i];
                if (candidate != null && candidate != clusterSpawner)
                {
                    Object.DestroyImmediate(candidate, true);
                }
            }
        }

        private static void CreateBackground(Transform parent, Sprite pixelSprite)
        {
            GameObject background = new GameObject("BackgroundSprite");
            background.transform.SetParent(parent, false);
            background.transform.localPosition = Vector3.zero;
            background.transform.localScale = new Vector3(MapMax.x - MapMin.x, MapMax.y - MapMin.y, 1f);

            SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = pixelSprite;
            renderer.color = new Color(0.16f, 0.18f, 0.18f, 1f);
            renderer.sortingOrder = -100;
        }

        private static BoxCollider2D CreateBoxBlocker(
            Transform parent,
            Sprite pixelSprite,
            string objectName,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject blocker = new GameObject(objectName);
            blocker.transform.SetParent(parent, false);
            blocker.transform.localPosition = new Vector3(position.x, position.y, 0f);
            blocker.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = blocker.AddComponent<SpriteRenderer>();
            renderer.sprite = pixelSprite;
            renderer.color = color;
            renderer.sortingOrder = -10;

            BoxCollider2D collider = blocker.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            return collider;
        }

        private static SpawnHotspot CreateHotspot(
            Transform parent,
            string objectName,
            Vector2 position,
            HumanClusterType clusterType,
            int minCount,
            int maxCount,
            float spawnRadius,
            float weight,
            float enableTime,
            bool boosted,
            bool openingPriority,
            bool flowFallback,
            float weightRampTime = -1f,
            float weightAfterRamp = -1f)
        {
            GameObject hotspotObject = new GameObject(objectName);
            hotspotObject.transform.SetParent(parent, false);
            hotspotObject.transform.localPosition = new Vector3(position.x, position.y, 0f);

            SpawnHotspot hotspot = hotspotObject.AddComponent<SpawnHotspot>();
            hotspot.Configure(objectName, clusterType, minCount, maxCount, spawnRadius, weight, enableTime, boosted, openingPriority, flowFallback, weightRampTime, weightAfterRamp);
            return hotspot;
        }

        private static Transform CreatePoint(Transform parent, string objectName, Vector2 position)
        {
            GameObject point = new GameObject(objectName);
            point.transform.SetParent(parent, false);
            point.transform.localPosition = new Vector3(position.x, position.y, 0f);
            return point.transform;
        }

        private static GameObject GetOrCreateRoot(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            return existing != null ? existing : new GameObject(objectName);
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetVector2(SerializedObject serializedObject, string propertyName, Vector2 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector2Value = value;
            }
        }

        private static Sprite EnsurePixelSprite()
        {
            EnsureFolder("Assets/Game/Art");
            EnsureFolder("Assets/Game/Art/Whitebox");

            if (!File.Exists(PixelSpritePath))
            {
                Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[64];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.white;
                }

                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(PixelSpritePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(PixelSpritePath);
            }

            TextureImporter importer = AssetImporter.GetAtPath(PixelSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 8f;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(PixelSpritePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
#endif
