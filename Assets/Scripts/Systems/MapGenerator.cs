using UnityEngine;
using System.Collections.Generic;

namespace NinuNinu.Systems
{
    public class MapGenerator : MonoBehaviour
    {
        public static MapGenerator Instance { get; private set; }

        [Header("Map Settings")]
        public int width = 50;
        public int height = 50;
        public float blockSize = 1f;
        public bool showGizmos = true;
        public Vector3 currentRotation = Vector3.zero;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            PopulateWaterPositions();
        }

        public void PopulateWaterPositions()
        {
            waterPositions.Clear();
            if (mapParent == null) return;

            foreach (Transform child in mapParent)
            {
                if (child.name.ToLower().Contains("air") || child.name.ToLower().Contains("water"))
                {
                    waterPositions.Add(child.position);
                }
            }
        }

        [ContextMenu("Force Update NavMesh Flags")]
        public void ForceUpdateNavMeshFlags()
        {
            if (mapParent == null) return;

            foreach (Transform child in mapParent)
            {
                // Cari block data mana yang cocok dengan nama objek ini
                foreach (var data in blockPrefabs)
                {
                    if (child.name.Contains(data.name))
                    {
                        #if UNITY_EDITOR
                        // Hanya set static jika punya collider (solid) dan memang walkable
                        // Jika air (no collider), jangan set static agar NavMesh jatuh ke bawah
                        bool shouldBeStatic = data.isWalkable && data.hasCollider;
                        UnityEditor.GameObjectUtility.SetStaticEditorFlags(child.gameObject, 
                            shouldBeStatic ? UnityEditor.StaticEditorFlags.NavigationStatic : 0);
                        UnityEditor.GameObjectUtility.SetNavMeshArea(child.gameObject, data.navMeshArea);
                        #endif
                        break;
                    }
                }
            }
            Debug.Log("All blocks NavMesh flags updated! Please Bake again.");
        }
        public BlockData[] blockPrefabs;
        public List<BuildingData> buildingPrefabs = new List<BuildingData>();
        
        [HideInInspector]
        public List<Vector3> waterPositions = new List<Vector3>();

        [System.Serializable]
        public class BlockData
        {
            public string name;
            public GameObject prefab;
            public bool hasCollider = true;
            public bool isWalkable = true;
            public int navMeshArea = 0; // New: 0 = Walkable, 1 = Not Walkable, dsb.
            public float visualYOffset = 0f;        }

        [System.Serializable]
        public class BuildingData
        {
            public string name;
            public GameObject prefab;
            public Vector3Int gridSize = Vector3Int.one;
        }

        [Header("Container")]
        public Transform mapParent;
        public Transform bakedParent; // Container for optimized meshes
        public Transform boundaryParent;

        [Header("Boundaries")]
        public bool autoGenerateBoundaries = true;
        public float boundaryHeight = 10f;
        public float boundaryThickness = 1f;

        void Start()
        {
            // GenerateMap();
        }

        [ContextMenu("Generate Map")]
        public void GenerateMap()
        {
            GenerateMapInternal();
            if (autoGenerateBoundaries) GenerateBoundaries();
        }

        private void GenerateMapInternal()
        {
            waterPositions.Clear();
            // Clear existing map if any (useful for regenerations in editor)
            if (mapParent != null)
            {
                foreach (Transform child in mapParent)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying) {
                        UnityEditor.EditorApplication.delayCall += () => { if(child != null) DestroyImmediate(child.gameObject); };
                    } else {
                        Destroy(child.gameObject);
                    }
                    #else
                    Destroy(child.gameObject);
                    #endif
                }
            }
            else
            {
                GameObject container = new GameObject("GeneratedMap");
                mapParent = container.transform;
            }

            if (blockPrefabs == null || blockPrefabs.Length == 0)
            {
                Debug.LogError("Please assign at least one block prefab!");
                return;
            }

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    // Pick a random block from the array
                    var blockData = blockPrefabs[Random.Range(0, blockPrefabs.Length)];
                    GameObject prefabToSpawn = blockData.prefab;

                    // Calculate position (Calibrate 0 as ground level, adding the visual offset on top)
                    Vector3 pos = new Vector3(x * blockSize, (blockSize * 0.5f) + blockData.visualYOffset, z * blockSize);
                    
                    // Instantiate
                    GameObject block = Instantiate(prefabToSpawn, pos, Quaternion.identity);
                    block.name = blockData.name;
                    block.transform.SetParent(mapParent);

                    // Handle collider in Edit Mode (Always add for raycasting, set trigger if no-collision)
                    BoxCollider col = block.GetComponent<BoxCollider>();
                    if (col == null) 
                    {
                        col = block.AddComponent<BoxCollider>();
                        col.size = Vector3.one * blockSize;
                    }
                    col.isTrigger = !blockData.hasCollider;

                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        bool shouldBeStatic = blockData.isWalkable && blockData.hasCollider;
                        if (shouldBeStatic)
                        {
                            UnityEditor.GameObjectUtility.SetStaticEditorFlags(block, UnityEditor.StaticEditorFlags.NavigationStatic);
                            UnityEditor.GameObjectUtility.SetNavMeshArea(block, blockData.navMeshArea);
                        }
                        else
                        {
                            UnityEditor.GameObjectUtility.SetStaticEditorFlags(block, 0); // Clear if not solid
                        }
                    }
                    #endif

                    if (blockData.name.ToLower().Contains("air") || blockData.name.ToLower().Contains("water"))
                    {
                        waterPositions.Add(pos);
                    }
                }
            }
            
            Debug.Log($"Map {width}x{height} generated with {width * height} blocks.");
        }

        public void PlaceBlock(Vector3 worldPosition, int prefabIndex, Quaternion rotation = default, bool centerPivot = true)
        {
            if (rotation == default) rotation = Quaternion.identity;
            
            if (blockPrefabs == null || blockPrefabs.Length == 0) return;

            // Snap to grid (Floor to find the bottom-left corner of the cell)
            float x = Mathf.Floor(worldPosition.x / blockSize) * blockSize;
            float y = Mathf.Floor(worldPosition.y / blockSize) * blockSize;
            float z = Mathf.Floor(worldPosition.z / blockSize) * blockSize;
            Vector3 snappedPos = new Vector3(x, y, z);

            int finalIndex = prefabIndex >= 0 && prefabIndex < blockPrefabs.Length 
                ? prefabIndex 
                : Random.Range(0, blockPrefabs.Length);

            var blockData = blockPrefabs[finalIndex];

            // Adjust position based on pivot type (Calibrate 0 as ground level, adding the visual offset on top)
            Vector3 finalPos = centerPivot 
                ? snappedPos + new Vector3(blockSize * 0.5f, (blockSize * 0.5f) + blockData.visualYOffset, blockSize * 0.5f) 
                : snappedPos + Vector3.up * blockData.visualYOffset;
            
            // Re-calculate grid reference position for snapping check
            Vector3 gridCheckPos = centerPivot ? snappedPos + Vector3.one * (blockSize * 0.5f) : snappedPos;

            if (!IsWithinBounds(snappedPos)) return;

            // Check if block already exists in this cell
            if (mapParent != null)
            {
                foreach (Transform child in mapParent)
                {
                    // Check distance against grid position (ignoring visual Y offset)
                    float distX = Mathf.Abs(child.position.x - gridCheckPos.x);
                    float distZ = Mathf.Abs(child.position.z - gridCheckPos.z);
                    float distY = Mathf.Abs(child.position.y - (gridCheckPos.y + blockData.visualYOffset)); // Still need to account for its own offset if we compare directly
                    
                    // Actually, more reliable: Check if the grid snapped base positions match
                    Vector3 childSnapped = new Vector3(
                        Mathf.Floor(child.position.x / blockSize) * blockSize,
                        Mathf.Floor(child.position.y / blockSize) * blockSize,
                        Mathf.Floor(child.position.z / blockSize) * blockSize
                    );
                    
                    if (Vector3.Distance(childSnapped, snappedPos) < 0.1f)
                    {
                        return; // Already exists
                    }
                }
            }
            else
            {
                GameObject container = new GameObject("GeneratedMap");
                mapParent = container.transform;
                mapParent.SetParent(this.transform);
            }

            GameObject prefabToSpawn = blockData.prefab;
            GameObject block;

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                block = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabToSpawn);
                block.transform.position = finalPos;
                block.transform.rotation = rotation;
                block.name = blockPrefabs[finalIndex].name; // Keep name for Bake identification
                block.transform.SetParent(mapParent);
                
                // --- Tambahan: Set as Navigation Static agar bisa di-Bake ---
                bool shouldBeStatic = blockData.isWalkable && blockData.hasCollider;
                if (shouldBeStatic)
                {
                    UnityEditor.GameObjectUtility.SetStaticEditorFlags(block, UnityEditor.StaticEditorFlags.NavigationStatic);
                    UnityEditor.GameObjectUtility.SetNavMeshArea(block, blockData.navMeshArea);
                }
                else
                {
                    UnityEditor.GameObjectUtility.SetStaticEditorFlags(block, 0);
                }
                
                UnityEditor.Undo.RegisterCreatedObjectUndo(block, "Place Block");
            }
            else
            {
                block = Instantiate(prefabToSpawn, finalPos, rotation);
                block.name = blockPrefabs[finalIndex].name; // Keep name for Bake identification
                block.transform.SetParent(mapParent);
            }
            #else
            block = Instantiate(prefabToSpawn, finalPos, rotation);
            block.name = blockData.name; // Keep name for Bake identification
            block.transform.SetParent(mapParent);
            #endif
            
            // Handle collider in Edit Mode (Always add for raycasting, set trigger if no-collision)
            BoxCollider col = block.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = block.AddComponent<BoxCollider>();
                col.size = Vector3.one * blockSize;
            }
            col.isTrigger = !blockData.hasCollider;
        }

        public void PlaceBuilding(Vector3 worldPosition, BuildingData building, Quaternion rotation)
        {
            if (building == null || building.prefab == null) return;

            float x = Mathf.Floor(worldPosition.x / blockSize) * blockSize;
            float y = Mathf.Floor(worldPosition.y / blockSize) * blockSize;
            float z = Mathf.Floor(worldPosition.z / blockSize) * blockSize;
            Vector3 cornerPos = new Vector3(x, y, z);

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(building.prefab);
                instance.transform.position = cornerPos;
                instance.transform.rotation = rotation;
                instance.transform.SetParent(mapParent);
                
                // Add Footprint Collider
                BoxCollider col = instance.GetComponent<BoxCollider>();
                if (col == null) col = instance.AddComponent<BoxCollider>();
                
                Vector3 size = new Vector3(building.gridSize.x, building.gridSize.y, building.gridSize.z) * blockSize;
                // Center is half-size relative to the corner pivot
                Vector3 visualOffset = size * 0.5f;
                
                col.center = visualOffset / instance.transform.localScale.x;
                col.size = size / instance.transform.localScale.x;

                UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Place Building");
            }
            #endif
        }

        [ContextMenu("Clear Map")]
        public void ClearMap()
        {
            if (mapParent == null) return;

            // Clear all children
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.SetCurrentGroupName("Clear Map");
                int group = UnityEditor.Undo.GetCurrentGroup();
                while (mapParent.childCount > 0)
                {
                    UnityEditor.Undo.DestroyObjectImmediate(mapParent.GetChild(0).gameObject);
                }
                UnityEditor.Undo.CollapseUndoOperations(group);
            }
            else
            {
                foreach (Transform child in mapParent)
                {
                    Destroy(child.gameObject);
                }
            }
            #else
            foreach (Transform child in mapParent)
            {
                Destroy(child.gameObject);
            }
            #endif
            
            Debug.Log("Map cleared.");
        }

        public void PlaceBlocksArea(Vector3 start, Vector3 end, int prefabIndex, Quaternion rotation = default)
        {
            float minX = Mathf.Min(start.x, end.x);
            float maxX = Mathf.Max(start.x, end.x);
            float minZ = Mathf.Min(start.z, end.z);
            float maxZ = Mathf.Max(start.z, end.z);
            float y = start.y;

            for (float x = minX; x <= maxX; x += blockSize)
            {
                for (float z = minZ; z <= maxZ; z += blockSize)
                {
                    PlaceBlock(new Vector3(x, y, z), prefabIndex, rotation, true);
                }
            }
        }

        public void RemoveBlock(Vector3 worldPosition)
        {
            if (mapParent == null) return;

            float x = Mathf.Floor(worldPosition.x / blockSize) * blockSize;
            float y = Mathf.Floor(worldPosition.y / blockSize) * blockSize;
            float z = Mathf.Floor(worldPosition.z / blockSize) * blockSize;
            
            Vector3 cellCenter = new Vector3(x, y, z) + Vector3.one * (blockSize * 0.5f);
            Vector3 cellCorner = new Vector3(x, y, z);

            foreach (Transform child in mapParent)
            {
                // Check against both center (block) and corner (building)
                if (Vector3.Distance(child.position, cellCenter) < 0.1f || Vector3.Distance(child.position, cellCorner) < 0.1f)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
                    }
                    else
                    {
                        UnityEditor.EditorApplication.delayCall += () => { if(child != null) DestroyImmediate(child.gameObject); };
                    }
                    #else
                    Destroy(child.gameObject);
                    #endif
                    return;
                }
            }
        }

        public void RemoveObject(Transform target)
        {
            if (target == null) return;
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.DestroyObjectImmediate(target.gameObject);
            }
            else
            {
                UnityEditor.EditorApplication.delayCall += () => { if(target != null) DestroyImmediate(target.gameObject); };
            }
            #else
            Destroy(target.gameObject);
            #endif
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            // Draw map boundaries
            Gizmos.color = Color.yellow;
            Vector3 center = new Vector3(width * blockSize / 2f, 0, height * blockSize / 2f);
            Vector3 size = new Vector3(width * blockSize, 0.1f, height * blockSize);
            Gizmos.DrawWireCube(center, size);

            // Draw grid points (optional, subtle)
            Gizmos.color = new Color(1, 1, 1, 0.1f);
            for (int x = 0; x < width; x++)
            {
                Gizmos.DrawLine(new Vector3(x * blockSize - blockSize/2, 0, -blockSize/2), new Vector3(x * blockSize - blockSize/2, 0, height * blockSize - blockSize/2));
            }
            for (int z = 0; z < height; z++)
            {
                Gizmos.DrawLine(new Vector3(-blockSize/2, 0, z * blockSize - blockSize/2), new Vector3(width * blockSize - blockSize/2, 0, z * blockSize - blockSize/2));
            }
        }

        public bool IsWithinBounds(Vector3 worldPosition, Vector3Int gridSize = default)
        {
            if (gridSize == default) gridSize = Vector3Int.one;

            int gridX = Mathf.FloorToInt(worldPosition.x / blockSize);
            int gridY = Mathf.FloorToInt(worldPosition.y / blockSize);
            int gridZ = Mathf.FloorToInt(worldPosition.z / blockSize);
            
            // For corner-pivot objects, check bottom-left corner and top-right extent
            return gridX >= 0 && (gridX + gridSize.x) <= width && 
                   gridY >= 0 && // No upper limit for Y unless specified
                   gridZ >= 0 && (gridZ + gridSize.z) <= height;
        }

        public bool IsAreaOccupied(Vector3 center, Vector3 gridSize, Quaternion rotation = default, List<Transform> exclude = null)
        {
            if (rotation == default) rotation = Quaternion.identity;
            
            // Using Physics.OverlapBox to detect any existing colliders in the area
            // We use a slightly smaller size (0.95f) to avoid self-collision with neighbors
            Vector3 halfExtents = ((Vector3)gridSize * blockSize * 0.95f) / 2f;
            Collider[] colliders = Physics.OverlapBox(center, halfExtents, rotation);
            
            foreach (var col in colliders)
            {
                // Only consider it occupied if the collider is a child of mapParent
                if (mapParent != null && col.transform.IsChildOf(mapParent))
                {
                    // Ignore objects in the exclude list (check hierarchy)
                    if (exclude != null)
                    {
                        bool isExcluded = false;
                        Transform t = col.transform;
                        while (t != null && t != mapParent) // Don't look past mapParent
                        {
                            if (exclude.Contains(t)) { isExcluded = true; break; }
                            t = t.parent;
                        }
                        if (isExcluded) continue;
                    }
                    
                    return true;
                }
            }
            return false;
        }

        [ContextMenu("Bake for Mobile")]
        public void BakeMap()
        {
            if (mapParent == null) return;
            ClearBake();

            GameObject container = new GameObject("BakedMap_Optimized");
            bakedParent = container.transform;
            bakedParent.SetParent(this.transform);

            // 1. Group by Material and Collider Requirement
            // Key: (Material, hasCollider)
            Dictionary<(Material, bool), List<CombineInstance>> groups = new Dictionary<(Material, bool), List<CombineInstance>>();
            
            foreach (Transform child in mapParent)
            {
                if (!child.gameObject.activeSelf) continue;

                // Lookup hasCollider setting from blockPrefabs based on name
                bool blockHasCollider = true;
                foreach(var b in blockPrefabs) {
                    if (child.name == b.name) {
                        blockHasCollider = b.hasCollider;
                        break;
                    }
                }

                MeshFilter[] filters = child.GetComponentsInChildren<MeshFilter>();
                MeshRenderer[] renderers = child.GetComponentsInChildren<MeshRenderer>();
                
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i].sharedMesh == null || renderers[i].sharedMaterial == null) continue;

                    Material mat = renderers[i].sharedMaterial;
                    var key = (mat, blockHasCollider);
                    if (!groups.ContainsKey(key)) groups[key] = new List<CombineInstance>();

                    CombineInstance ci = new CombineInstance();
                    ci.mesh = filters[i].sharedMesh;
                    ci.transform = filters[i].transform.localToWorldMatrix;
                    groups[key].Add(ci);
                }

                // 2. Optimize Colliders (Only if it's supposed to HAVE one)
                if (blockHasCollider)
                {
                    BoxCollider col = child.GetComponent<BoxCollider>();
                    if (col != null)
                    {
                        Vector3 c = col.bounds.center;
                        float d = blockSize * 0.6f; 
                        int neighborCount = 0;
                        Vector3[] dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
                        foreach (var dir in dirs)
                        {
                            if (Physics.Raycast(c, dir, d)) neighborCount++;
                        }
                        if (neighborCount >= 6) col.enabled = false;
                    }
                }
            }

            // 3. Create Merged Mesh Objects
            foreach (var group in groups)
            {
                Material mat = group.Key.Item1;
                bool needsCollider = group.Key.Item2;

                GameObject mergedObj = new GameObject($"MergedChunk_{mat.name}_{(needsCollider ? "WithCol" : "NoCol")}");
                mergedObj.transform.SetParent(bakedParent);
                
                MeshFilter mf = mergedObj.AddComponent<MeshFilter>();
                MeshRenderer mr = mergedObj.AddComponent<MeshRenderer>();
                
                Mesh combinedMesh = new Mesh();
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 
                combinedMesh.CombineMeshes(group.Value.ToArray());
                
                mf.sharedMesh = combinedMesh;
                mr.sharedMaterial = mat;
                
                if (needsCollider)
                {
                    MeshCollider mc = mergedObj.AddComponent<MeshCollider>();
                    mc.sharedMesh = combinedMesh;
                }
                
                mergedObj.isStatic = true;
            }

            toggleOriginalBlocks(false);
            Debug.Log("Map Baked successfully! Reduced to " + groups.Count + " chunks.");
        }

        [ContextMenu("Clear Bake")]
        public void ClearBake()
        {
            if (bakedParent != null)
            {
                if (Application.isPlaying) Destroy(bakedParent.gameObject);
                else DestroyImmediate(bakedParent.gameObject);
            }
            toggleOriginalBlocks(true);
        }

        public void toggleOriginalBlocks(bool visible)
        {
            if (mapParent == null) return;
            foreach (Transform child in mapParent)
            {
                child.gameObject.SetActive(visible);
            }
        }

        [ContextMenu("Generate Boundaries")]
        public void GenerateBoundaries()
        {
            // Clear existing boundaries
            if (boundaryParent != null)
            {
                foreach (Transform child in boundaryParent)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying) {
                        UnityEditor.EditorApplication.delayCall += () => { if(child != null) DestroyImmediate(child.gameObject); };
                    } else {
                        Destroy(child.gameObject);
                    }
                    #else
                    Destroy(child.gameObject);
                    #endif
                }
            }
            else
            {
                GameObject container = new GameObject("MapBoundaries");
                boundaryParent = container.transform;
            }

            float mapWidth = width * blockSize;
            float mapHeight = height * blockSize;
            float centerX = mapWidth * 0.5f - blockSize * 0.5f;
            float centerZ = mapHeight * 0.5f - blockSize * 0.5f;

            // Wall 1: Back (Z = height)
            CreateBoundaryWall("Wall_Back", new Vector3(centerX, boundaryHeight * 0.5f, mapHeight - blockSize * 0.5f), new Vector3(mapWidth, boundaryHeight, boundaryThickness));
            // Wall 2: Front (Z = 0)
            CreateBoundaryWall("Wall_Front", new Vector3(centerX, boundaryHeight * 0.5f, -blockSize * 0.5f), new Vector3(mapWidth, boundaryHeight, boundaryThickness));
            // Wall 3: Left (X = 0)
            CreateBoundaryWall("Wall_Left", new Vector3(-blockSize * 0.5f, boundaryHeight * 0.5f, centerZ), new Vector3(boundaryThickness, boundaryHeight, mapHeight));
            // Wall 4: Right (X = width)
            CreateBoundaryWall("Wall_Right", new Vector3(mapWidth - blockSize * 0.5f, boundaryHeight * 0.5f, centerZ), new Vector3(boundaryThickness, boundaryHeight, mapHeight));
            
            Debug.Log("Map Boundaries Generated!");
        }

        private void CreateBoundaryWall(string name, Vector3 pos, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(boundaryParent);
            wall.transform.position = pos;
            
            BoxCollider col = wall.AddComponent<BoxCollider>();
            col.size = size;
            
            // Opsional: Bisa beri tag/layer khusus jika perlu
        }
    }
}
