#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace NinuNinu.Systems
{
    [CustomEditor(typeof(MapGenerator))]
    public class MapPainter : Editor
    {
        public enum ToolType { Pen, Rectangle, Select, Building }
        private ToolType currentTool = ToolType.Pen;
        
        private MapGenerator generator;
        private int selectedPrefabIndex = 0;
        private int selectedBuildingIndex = 0;
        private Tool lastTool = Tool.Move;
        
        private bool isPainting = false;
        private bool isDraggingRect = false;
        private bool isMovingObjects = false;
        private Vector3 dragStartPos;
        private Vector3 initialCenter;
        private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
        private Transform selectedBlock; // Legacy for single selection display
        private List<Transform> selectedBlocks = new List<Transform>();

        private void OnEnable()
        {
            generator = (MapGenerator)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty currentRotationProp = serializedObject.FindProperty("currentRotation");

            DrawDefaultInspector();

            generator = (MapGenerator)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("--- Painting Tool Settings ---", EditorStyles.boldLabel);

            currentTool = (ToolType)EditorGUILayout.EnumPopup("Current Tool", currentTool);
            
            generator.showGizmos = EditorGUILayout.Toggle("Show Boundary Gizmos", generator.showGizmos);
            
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Rotation: {currentRotationProp.vector3Value}", EditorStyles.boldLabel);
            if (GUILayout.Button("Reset Rot"))
            {
                Undo.RecordObject(generator, "Reset Rotation");
                currentRotationProp.vector3Value = Vector3.zero;
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("HORIZ. ROT (R)"))
            {
                RotateHorizontal();
            }
            if (GUILayout.Button("VERT. ROT (V)"))
            {
                RotateVertical();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            // Tool Tabs
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(currentTool == ToolType.Pen, "PEN", EditorStyles.miniButtonLeft)) currentTool = ToolType.Pen;
            if (GUILayout.Toggle(currentTool == ToolType.Rectangle, "RECT", EditorStyles.miniButtonMid)) currentTool = ToolType.Rectangle;
            if (GUILayout.Toggle(currentTool == ToolType.Select, "SELECT", EditorStyles.miniButtonMid)) currentTool = ToolType.Select;
            if (GUILayout.Toggle(currentTool == ToolType.Building, "BUILDING", EditorStyles.miniButtonRight)) currentTool = ToolType.Building;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (currentTool == ToolType.Building)
            {
                EditorGUILayout.LabelField("--- Building Library ---", EditorStyles.boldLabel);
                if (generator.buildingPrefabs != null && generator.buildingPrefabs.Count > 0)
                {
                    string[] buildingOptions = new string[generator.buildingPrefabs.Count];
                    for (int i = 0; i < generator.buildingPrefabs.Count; i++)
                    {
                        buildingOptions[i] = !string.IsNullOrEmpty(generator.buildingPrefabs[i].name) 
                            ? generator.buildingPrefabs[i].name 
                            : (generator.buildingPrefabs[i].prefab != null ? generator.buildingPrefabs[i].prefab.name : "Unnamed");
                    }
                    selectedBuildingIndex = EditorGUILayout.Popup("Select Building", selectedBuildingIndex, buildingOptions);

                    var bData = generator.buildingPrefabs[selectedBuildingIndex];
                    bData.gridSize = EditorGUILayout.Vector3IntField("Grid Size (W,H,L)", bData.gridSize);
                    
                    if (GUILayout.Button("Auto-Calculate Size"))
                    {
                        if (bData.prefab != null)
                        {
                            Undo.RecordObject(generator, "Auto Calc Size");
                            MeshFilter[] filters = bData.prefab.GetComponentsInChildren<MeshFilter>();
                            if (filters.Length > 0)
                            {
                                Bounds bounds = filters[0].sharedMesh.bounds;
                                for (int i = 1; i < filters.Length; i++) bounds.Encapsulate(filters[i].sharedMesh.bounds);
                                
                                bData.gridSize.x = Mathf.Max(1, Mathf.RoundToInt(bounds.size.x / generator.blockSize));
                                bData.gridSize.y = Mathf.Max(1, Mathf.RoundToInt(bounds.size.y / generator.blockSize));
                                bData.gridSize.z = Mathf.Max(1, Mathf.RoundToInt(bounds.size.z / generator.blockSize));
                            }
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Belum ada prefab bangunan. Tambahkan di MapGenerator component.", MessageType.Warning);
                }
            }
            else
            {
                // Prefab Selection Grid
                if (generator.blockPrefabs != null && generator.blockPrefabs.Length > 0)
                {
                    EditorGUILayout.LabelField("Pilih Blok Untuk Dilukis:");
                    string[] options = new string[generator.blockPrefabs.Length];
                    for (int i = 0; i < generator.blockPrefabs.Length; i++)
                    {
                        options[i] = generator.blockPrefabs[i] != null ? generator.blockPrefabs[i].name : "Empty";
                    }
                    selectedPrefabIndex = GUILayout.SelectionGrid(selectedPrefabIndex, options, 3);
                }
            }

            EditorGUILayout.Space();

            GUI.color = isPainting ? Color.green : Color.white;
            if (GUILayout.Button(isPainting ? "PAINTING MODE: AKTIF (Klik Untuk Stop)" : "MULAI MELUKIS (START PAINTING)"))
            {
                isPainting = !isPainting;
                if (isPainting)
                {
                    lastTool = Tools.current;
                    Tools.current = Tool.None;
                }
                else
                {
                    Tools.current = lastTool;
                }
            }
            GUI.color = Color.white;

            EditorGUILayout.Space();

            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("HAPUS SEMUA (CLEAR MAP)"))
            {
                if (EditorUtility.DisplayDialog("Clear Map", "Are you sure you want to delete ALL blocks?", "Yes", "No"))
                {
                    generator.GenerateMap();
                }
            }
            GUI.color = Color.white;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("--- Mobile Optimization (Bake) ---", EditorStyles.boldLabel);
            EditorStyles.helpBox.richText = true;
            EditorGUILayout.HelpBox("<b>Bake for Mobile</b> akan menggabungkan ribuah mesh menjadi satu untuk performa HP 60fps. \n<i>Catatan: Bersihkan (Clear) sebelum mengedit map lagi.</i>", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUI.color = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("BAKE FOR MOBILE"))
            {
                generator.BakeMap();
                isPainting = false; // Disable painting when baking
                Tools.current = lastTool;
            }
            GUI.color = new Color(1f, 1f, 0.5f);
            if (GUILayout.Button("CLEAR OPTIMIZATION"))
            {
                generator.ClearBake();
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Instruksi:\n1. Klik tombol di atas sampai berwarna Hijau.\n2. Di SCENE VIEW: Klik & Tahan mouse kiri untuk buat blok.\n3. R/V: Rotasi.\n4. Delete: Hapus seleksi.", MessageType.Info);
            
            serializedObject.ApplyModifiedProperties();
            if (GUI.changed) EditorUtility.SetDirty(target);
        }

        private void OnSceneGUI()
        {
            Event e = Event.current;

            // Toggle painting with 'P' key
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.P)
            {
                isPainting = !isPainting;
                Debug.Log("Painting Mode: " + (isPainting ? "ON" : "OFF") + " (Press 'P' to Toggle)");
                e.Use();
            }

            if (!isPainting) return;

            // Prevent selecting other objects while painting
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            // Handle Rotation Shortcut
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.R) // Horizontal
                {
                    RotateHorizontal();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.V) // Vertical
                {
                    RotateVertical();
                    e.Use();
                }
                else if (currentTool == ToolType.Select && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
                {
                    // Batch Deletion for current selection
                    if (selectedBlocks.Count > 0)
                    {
                        Undo.SetCurrentGroupName("Batch Delete Blocks");
                        int group = Undo.GetCurrentGroup();
                        
                        foreach (var block in selectedBlocks)
                        {
                            if (block != null) generator.RemoveBlock(block.position);
                        }
                        selectedBlocks.Clear();
                        
                        Undo.CollapseUndoOperations(group);
                        e.Use();
                    }
                }
            }

            // Raycast logic
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 placementPos = Vector3.zero;
            bool canPlace = false;

            // 1. Raycast against existing blocks (intelligent stacking/face-based)
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, Physics.AllLayers, QueryTriggerInteraction.Collide))
            {
                // Check if we hit a block that is a child of mapParent
                if (generator.mapParent != null && hit.transform.IsChildOf(generator.mapParent))
                {
                    // Calculate the position of the new block cell
                    Vector3 cellPos = hit.transform.position + hit.normal * generator.blockSize;
                    float snapX = Mathf.Floor(cellPos.x / generator.blockSize) * generator.blockSize;
                    float snapY = Mathf.Floor(cellPos.y / generator.blockSize) * generator.blockSize;
                    float snapZ = Mathf.Floor(cellPos.z / generator.blockSize) * generator.blockSize;
                    
                    placementPos = new Vector3(snapX, snapY, snapZ);
                    canPlace = true;
                }
            }
            
            // 2. If no block hit, raycast against ground plane (Y=0)
            if (!canPlace)
            {
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float distance))
                {
                    Vector3 worldPos = ray.GetPoint(distance);
                    float x = Mathf.Floor(worldPos.x / generator.blockSize) * generator.blockSize;
                    float z = Mathf.Floor(worldPos.z / generator.blockSize) * generator.blockSize;
                    placementPos = new Vector3(x, 0, z);
                    canPlace = true;
                }
            }

            // 3. Final Boundary Check
            Vector3Int currentSize = Vector3Int.one;
            if (currentTool == ToolType.Building && generator.buildingPrefabs != null && selectedBuildingIndex < generator.buildingPrefabs.Count)
            {
                currentSize = generator.buildingPrefabs[selectedBuildingIndex].gridSize;
            }

            if (canPlace && !generator.IsWithinBounds(placementPos, currentSize))
            {
                canPlace = false; 
                
                // Show red preview even if invalid
                Handles.color = Color.red;
                if (currentTool == ToolType.Building)
                {
                    // Show wireframe for the full footprint that's outside
                    Vector3 visualOffset = new Vector3(currentSize.x * 0.5f, currentSize.y * 0.5f, currentSize.z * 0.5f) * generator.blockSize;
                    Handles.DrawWireCube(placementPos + visualOffset, (Vector3)currentSize * generator.blockSize * 1.05f);
                }
                else
                {
                    Handles.DrawWireCube(placementPos + Vector3.one * generator.blockSize * 0.5f, Vector3.one * generator.blockSize * 1.05f);
                }
                Handles.Label(placementPos + Vector3.up * currentSize.y, "OUTSIDE BOUNDARY!");
            }

            // Handle Input
            if (currentTool == ToolType.Pen)
            {
                if (canPlace && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown) && e.button == 0)
                {
                    if (e.shift)
                    {
                        if (Physics.Raycast(ray, out RaycastHit hitRemove, 1000f, Physics.AllLayers, QueryTriggerInteraction.Collide))
                        {
                            if (generator.mapParent != null && hitRemove.transform.IsChildOf(generator.mapParent))
                            {
                                generator.RemoveObject(hitRemove.transform);
                            }
                        }
                    }
                    else
                    {
                        // Check for overlap before placing block
                        if (generator.IsAreaOccupied(placementPos + Vector3.one * generator.blockSize * 0.5f, Vector3.one))
                        {
                            // Already handled by visual feedback
                        }
                        else
                        {
                            generator.PlaceBlock(placementPos, selectedPrefabIndex, Quaternion.Euler(generator.currentRotation), true);
                        }
                    }
                    e.Use();
                }

                // Draw a preview cursor
                if (canPlace)
                {
                    bool isOccupied = !e.shift && generator.IsAreaOccupied(placementPos + Vector3.one * generator.blockSize * 0.5f, Vector3.one);
                    Handles.color = (e.shift || isOccupied) ? Color.red : Color.cyan;
                    
                    // Offset center by 0.5 to align wireframe with the grid cell (corner pivot)
                    Vector3 previewCenter = placementPos + new Vector3(0.5f, 0.5f, 0.5f) * generator.blockSize;
                    Handles.DrawWireCube(previewCenter, Vector3.one * generator.blockSize * 1.1f);
                    
                    if (isOccupied) Handles.Label(previewCenter + Vector3.up, "OCCUPIED");
                    
                    Matrix4x4 rotationMatrix = Matrix4x4.TRS(previewCenter, Quaternion.Euler(generator.currentRotation), Vector3.one);
                    using (new Handles.DrawingScope(rotationMatrix))
                    {
                        Handles.DrawWireCube(Vector3.zero, Vector3.one * generator.blockSize * 1.05f);
                        // Draw an arrow for orientation (Forward)
                        Handles.color = Color.blue;
                        Handles.DrawLine(Vector3.zero, Vector3.forward * generator.blockSize * 0.7f);
                        // Draw an arrow for orientation (Up)
                        Handles.color = Color.green;
                        Handles.DrawLine(Vector3.zero, Vector3.up * generator.blockSize * 0.7f);
                    }

                    string label = e.shift ? "HAPUS (Shift Aktif)" : $"TARUH (H: {placementPos.y} | Rot: {generator.currentRotation})";
                    Handles.Label(placementPos + Vector3.up, label);
                }
            }
            else if (currentTool == ToolType.Rectangle)
            {
                if (canPlace && e.type == EventType.MouseDown && e.button == 0)
                {
                    dragStartPos = placementPos;
                    isDraggingRect = true;
                    e.Use();
                }

                if (isDraggingRect)
                {
                    Vector3 currentPos = placementPos;
                    
                    // Draw visual rectangle feedback
                    DrawVisualRect(dragStartPos, currentPos);

                    if (e.type == EventType.MouseUp && e.button == 0)
                    {
                        Undo.SetCurrentGroupName(e.shift ? "Area Remove" : "Area Place");
                        int group = Undo.GetCurrentGroup();

                        if (e.shift)
                        {
                            RemoveBlocksArea(dragStartPos, currentPos);
                        }
                        else
                        {
                            generator.PlaceBlocksArea(dragStartPos, currentPos, selectedPrefabIndex, Quaternion.Euler(generator.currentRotation));
                        }

                        Undo.CollapseUndoOperations(group);
                        isDraggingRect = false;
                        e.Use();
                    }
                    
                    if (e.type == EventType.MouseDrag && e.button == 0)
                    {
                        e.Use();
                    }
                }
            }
            else if (currentTool == ToolType.Select)
            {
                // 1. Unity-Style Move Handles
                if (selectedBlocks.Count > 0)
                {
                    Vector3 currentCenter = Vector3.zero;
                    int validCount = 0;
                    foreach (var b in selectedBlocks) { if (b != null) { currentCenter += b.position; validCount++; } }
                    
                    if (validCount > 0)
                    {
                        currentCenter /= validCount;

                        // Track the start of a drag
                        if (e.type == EventType.MouseDown && GUIUtility.hotControl == 0)
                        {
                            // We hit a handle? Hard to tell, but we can store original positions anyway
                            // Actually, better to store them when a change is detected.
                        }

                        EditorGUI.BeginChangeCheck();
                        Vector3 newCenter = Handles.PositionHandle(currentCenter, Quaternion.identity);
                        bool changed = EditorGUI.EndChangeCheck();

                        // If currently dragging (hotControl is set)
                        if (GUIUtility.hotControl != 0 && (changed || isMovingObjects))
                        {
                            if (!isMovingObjects)
                            {
                                isMovingObjects = true;
                                initialCenter = currentCenter;
                                originalPositions.Clear();
                                foreach (var b in selectedBlocks) { if (b != null) originalPositions[b] = b.position; }
                                Undo.RecordObjects(selectedBlocks.ToArray(), "Move Selection");
                            }

                            Vector3 rawDelta = newCenter - initialCenter;
                            Vector3 snappedDelta = new Vector3(
                                Mathf.Round(rawDelta.x / generator.blockSize) * generator.blockSize,
                                Mathf.Round(rawDelta.y / generator.blockSize) * generator.blockSize,
                                Mathf.Round(rawDelta.z / generator.blockSize) * generator.blockSize
                            );

                            // IMPORTANT: Force physics sync so OverlapBox is accurate
                            Physics.SyncTransforms();

                            // Validate the move group
                            bool allValid = true;
                            string blockReason = "";

                            foreach (var block in selectedBlocks)
                            {
                                if (block == null) continue;
                                Vector3 originalPos = originalPositions.ContainsKey(block) ? originalPositions[block] : block.position;
                                Vector3 targetPos = originalPos + snappedDelta;

                                BoxCollider col = block.GetComponent<BoxCollider>();
                                if (col != null)
                                {
                                    // 1. Calculate OBB Corners at target position for precise boundary check
                                    Vector3[] localCorners = {
                                        col.center + new Vector3(-col.size.x, -col.size.y, -col.size.z) * 0.5f,
                                        col.center + new Vector3(col.size.x, -col.size.y, -col.size.z) * 0.5f,
                                        col.center + new Vector3(-col.size.x, -col.size.y, col.size.z) * 0.5f,
                                        col.center + new Vector3(col.size.x, -col.size.y, col.size.z) * 0.5f,
                                        col.center + new Vector3(-col.size.x, col.size.y, -col.size.z) * 0.5f,
                                        col.center + new Vector3(col.size.x, col.size.y, -col.size.z) * 0.5f,
                                        col.center + new Vector3(-col.size.x, col.size.y, col.size.z) * 0.5f,
                                        col.center + new Vector3(col.size.x, col.size.y, col.size.z) * 0.5f
                                    };

                                    bool outOfBounds = false;
                                    float minY = float.MaxValue;
                                    foreach (var lc in localCorners)
                                    {
                                        Vector3 worldCorner = targetPos + (block.rotation * Vector3.Scale(lc, block.localScale));
                                        minY = Mathf.Min(minY, worldCorner.y);
                                        if (worldCorner.x < -0.01f || worldCorner.x > (generator.width * generator.blockSize) + 0.01f ||
                                            worldCorner.y < -0.01f || 
                                            worldCorner.z < -0.01f || worldCorner.z > (generator.height * generator.blockSize) + 0.01f)
                                        {
                                            outOfBounds = true;
                                            break;
                                        }
                                    }

                                    Vector3 scaledOffset = Vector3.Scale(col.center, block.localScale);
                                    Vector3 targetWorldCenter = targetPos + (block.rotation * scaledOffset);
                                    
                                    if (outOfBounds)
                                    {
                                        allValid = false;
                                        blockReason = $"OUTBOUND: {block.name} (MinY: {minY:F2})";
                                    }
                                    else if (generator.IsAreaOccupied(targetWorldCenter, col.size, block.rotation, selectedBlocks))
                                    {
                                        allValid = false;
                                        blockReason = $"COLLISION: {block.name}";
                                    }
                                }
                                else
                                {
                                    if (!generator.IsWithinBounds(targetPos, Vector3Int.one))
                                    {
                                        allValid = false;
                                        blockReason = $"OUTBOUND: {block.name}";
                                    }
                                    else if (generator.IsAreaOccupied(targetPos + Vector3.one * (generator.blockSize * 0.5f), Vector3.one, block.rotation, selectedBlocks))
                                    {
                                        allValid = false;
                                        blockReason = $"COLLISION: {block.name}";
                                    }
                                }
                                if (!allValid) break;
                            }

                            // VISUAL FEEDBACK: Draw Ghosts
                            Handles.color = allValid ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);
                            if (!allValid) Handles.Label(newCenter + Vector3.up * 2, blockReason, EditorStyles.boldLabel);
                            foreach (var block in selectedBlocks)
                            {
                                if (block == null) continue;
                                Vector3 originalPos = originalPositions.ContainsKey(block) ? originalPositions[block] : block.position;
                                Vector3 ghostPos = originalPos + snappedDelta;
                                
                                BoxCollider col = block.GetComponent<BoxCollider>();
                                if (col != null)
                                {
                                    Matrix4x4 ghostMatrix = Matrix4x4.TRS(ghostPos, block.rotation, Vector3.one);
                                    using (new Handles.DrawingScope(ghostMatrix))
                                    {
                                        Handles.DrawWireCube(col.center, col.size * 1.05f);
                                    }
                                }
                                else
                                {
                                    Handles.DrawWireCube(ghostPos, Vector3.one * generator.blockSize * 1.1f);
                                }
                            }

                            // Only move actual objects if valid
                            if (allValid && snappedDelta.sqrMagnitude > 0.001f)
                            {
                                foreach (var b in selectedBlocks)
                                {
                                    if (b != null && originalPositions.ContainsKey(b))
                                    {
                                        b.position = originalPositions[b] + snappedDelta;
                                    }
                                }
                                Physics.SyncTransforms();
                            }
                        }
                        else if (GUIUtility.hotControl == 0 && isMovingObjects)
                        {
                            // Drag dropped
                            isMovingObjects = false;
                            originalPositions.Clear();
                        }
                    }
                }

                // 2. Selection Interaction
                if (GUIUtility.hotControl == 0)
                {
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        bool hitSomething = Physics.Raycast(ray, out RaycastHit firstHit, 1000f, Physics.AllLayers, QueryTriggerInteraction.Collide);
                        if (hitSomething)
                        {
                            if (generator.mapParent != null && firstHit.transform.IsChildOf(generator.mapParent))
                            {
                                if (e.shift || e.control)
                                {
                                    if (selectedBlocks.Contains(firstHit.transform)) selectedBlocks.Remove(firstHit.transform);
                                    else selectedBlocks.Add(firstHit.transform);
                                }
                                else
                                {
                                    if (!selectedBlocks.Contains(firstHit.transform))
                                    {
                                        selectedBlocks.Clear();
                                        selectedBlocks.Add(firstHit.transform);
                                    }
                                }
                                dragStartPos = firstHit.transform.position;
                            }
                            else
                            {
                                if (!e.shift && !e.control) selectedBlocks.Clear();
                                dragStartPos = firstHit.point;
                                dragStartPos = new Vector3(
                                    Mathf.Floor(dragStartPos.x / generator.blockSize) * generator.blockSize,
                                    Mathf.Floor(dragStartPos.y / generator.blockSize) * generator.blockSize,
                                    Mathf.Floor(dragStartPos.z / generator.blockSize) * generator.blockSize
                                );
                            }
                            isDraggingRect = true;
                            e.Use();
                        }
                        else
                        {
                            if (!e.shift && !e.control) 
                            { 
                                selectedBlocks.Clear(); 
                                e.Use(); 
                            }
                        }
                    }
                }

                if (isDraggingRect)
                {
                    bool hitCurrent = Physics.Raycast(ray, out RaycastHit currentHit);
                    Vector3 currentPos;

                    if (hitCurrent)
                    {
                        // FIX: If hitting a block, use its center position
                        if (generator.mapParent != null && currentHit.transform.IsChildOf(generator.mapParent))
                        {
                            currentPos = currentHit.transform.position;
                        }
                        else
                        {
                            currentPos = currentHit.point;
                            currentPos = new Vector3(
                                Mathf.Floor(currentPos.x / generator.blockSize) * generator.blockSize,
                                Mathf.Floor(currentPos.y / generator.blockSize) * generator.blockSize,
                                Mathf.Floor(currentPos.z / generator.blockSize) * generator.blockSize
                            );
                        }
                    }
                    else
                    {
                        currentPos = dragStartPos;
                    }

                    // Visual feedback for selection area (3D)
                    Vector3 center = (dragStartPos + currentPos) / 2f;
                    Vector3 size = new Vector3(
                        Mathf.Abs(dragStartPos.x - currentPos.x) + generator.blockSize,
                        Mathf.Abs(dragStartPos.y - currentPos.y) + generator.blockSize,
                        Mathf.Abs(dragStartPos.z - currentPos.z) + generator.blockSize
                    );
                    
                    Handles.color = new Color(1f, 1f, 0f, 0.4f);
                    Handles.DrawWireCube(center, size);
                    Handles.color = Color.yellow;
                    Handles.DrawWireCube(center, size * 1.01f);

                    if (e.type == EventType.MouseUp && e.button == 0)
                    {
                        if (!e.shift && !e.control) selectedBlocks.Clear();

                        float minX = Mathf.Min(dragStartPos.x, currentPos.x) - 0.1f;
                        float maxX = Mathf.Max(dragStartPos.x, currentPos.x) + 0.1f;
                        float minY = Mathf.Min(dragStartPos.y, currentPos.y) - 0.1f;
                        float maxY = Mathf.Max(dragStartPos.y, currentPos.y) + 0.1f;
                        float minZ = Mathf.Min(dragStartPos.z, currentPos.z) - 0.1f;
                        float maxZ = Mathf.Max(dragStartPos.z, currentPos.z) + 0.1f;

                        if (generator.mapParent != null)
                        {
                            foreach (Transform child in generator.mapParent)
                            {
                                if (child.position.x >= minX && child.position.x <= maxX &&
                                    child.position.y >= minY && child.position.y <= maxY &&
                                    child.position.z >= minZ && child.position.z <= maxZ)
                                {
                                    if (!selectedBlocks.Contains(child))
                                        selectedBlocks.Add(child);
                                }
                            }
                        }

                        isDraggingRect = false;
                        e.Use();
                    }
                    
                    if (e.type == EventType.MouseDrag && e.button == 0) e.Use();
                }



                // Visual feedback for persistent selection (inside Select tool)
                if (selectedBlocks.Count > 0)
                {
                    Handles.color = Color.yellow;
                    foreach (var block in selectedBlocks)
                    {
                        if (block != null)
                        {
                            BoxCollider col = block.GetComponent<BoxCollider>();
                            if (col != null)
                            {
                                // Draw highlight based on collider bounds (for buildings)
                                Handles.DrawWireCube(col.bounds.center, col.bounds.size * 1.05f);
                            }
                            else
                            {
                                // Default block highlight
                                Handles.DrawWireCube(block.position, Vector3.one * generator.blockSize * 1.1f);
                            }
                        }
                    }
                    Vector3 labelPos = selectedBlocks[0] != null ? selectedBlocks[0].position : Vector3.zero;
                    Handles.Label(labelPos + Vector3.up * 1.5f, $"{selectedBlocks.Count} BLOCKS SELECTED - R: Horiz | V: Vert | Shift: Add | Del: Remove");
                }
            }
            else if (currentTool == ToolType.Building)
            {
                if (generator.buildingPrefabs != null && selectedBuildingIndex < generator.buildingPrefabs.Count)
                {
                    var bData = generator.buildingPrefabs[selectedBuildingIndex];
                    if (canPlace && bData.prefab != null)
                    {
                        // Visual Preview for Building
                        Vector3 size = new Vector3(bData.gridSize.x, bData.gridSize.y, bData.gridSize.z) * generator.blockSize;
                        Quaternion rot = Quaternion.Euler(generator.currentRotation);
                        
                        // Local center relative to corner pivot
                        Vector3 localCenter = new Vector3(bData.gridSize.x * 0.5f, bData.gridSize.y * 0.5f, bData.gridSize.z * 0.5f) * generator.blockSize;
                        // Rotated center in world space
                        Vector3 worldCenter = placementPos + (rot * localCenter);
                        
                        bool isOccupied = generator.IsAreaOccupied(worldCenter, (Vector3)bData.gridSize, rot);

                        Matrix4x4 buildingMatrix = Matrix4x4.TRS(placementPos, rot, Vector3.one);
                        using (new Handles.DrawingScope(buildingMatrix))
                        {
                            Handles.color = isOccupied ? new Color(1, 0, 0, 0.4f) : new Color(0, 1, 1, 0.3f);
                            Handles.DrawWireCube(localCenter, size * 1.05f);
                            Handles.color = isOccupied ? Color.red : Color.cyan;
                            Handles.DrawWireCube(localCenter, size * 1.01f);
                        }

                        // FIX: Precise Boundary Check for Rotated Building Footprint
                        worldCenter = placementPos + (rot * localCenter);
                        
                        // Calculate AABB/OBB of the rotated footprint for labels and validation
                        Bounds b = new Bounds(worldCenter, Vector3.zero);
                        Vector3[] rotatedCorners = {
                            rot * new Vector3(0, 0, 0),
                            rot * new Vector3(size.x, 0, 0),
                            rot * new Vector3(0, 0, size.z),
                            rot * new Vector3(size.x, 0, size.z),
                            rot * new Vector3(0, size.y, 0),
                            rot * new Vector3(size.x, size.y, 0),
                            rot * new Vector3(0, size.y, size.z),
                            rot * new Vector3(size.x, size.y, size.z)
                        };
                        
                        bool isWithinBounds = true;
                        float minY = float.MaxValue;
                        foreach (var c in rotatedCorners)
                        {
                            Vector3 worldCorner = placementPos + c;
                            minY = Mathf.Min(minY, worldCorner.y);
                            if (worldCorner.x < -0.01f || worldCorner.x > (generator.width * generator.blockSize) + 0.01f ||
                                worldCorner.y < -0.01f || 
                                worldCorner.z < -0.01f || worldCorner.z > (generator.height * generator.blockSize) + 0.01f)
                            {
                                isWithinBounds = false;
                                break;
                            }
                        }

                        isOccupied = generator.IsAreaOccupied(worldCenter, (Vector3)bData.gridSize, rot);

                        if (isOccupied || !isWithinBounds)
                        {
                            string reason = isOccupied ? "AREA OCCUPIED" : $"OUTBOUND (MinY: {minY:F2})";
                            Handles.Label(placementPos + Vector3.up * (bData.gridSize.y + 1), reason + " - CANNOT PLACE", EditorStyles.boldLabel);
                        }
                        else
                        {
                            Handles.Label(placementPos + Vector3.up * bData.gridSize.y, $"PLACE BUILDING: {bData.name}");
                        }

                        if (!isOccupied && isWithinBounds && e.type == EventType.MouseDown && e.button == 0)
                        {
                            generator.PlaceBuilding(placementPos, bData, rot);
                            e.Use();
                        }
                    }
                }
                else
                {
                    Handles.Label(placementPos + Vector3.up, "NO BUILDING SELECTED IN INSPECTOR");
                }
            }

            // Force repaint to keep the preview cursor smooth
            HandleUtility.Repaint();
        }

        private void DrawVisualRect(Vector3 start, Vector3 end)
        {
            float minX = Mathf.Min(start.x, end.x);
            float maxX = Mathf.Max(start.x, end.x);
            float minZ = Mathf.Min(start.z, end.z);
            float maxZ = Mathf.Max(start.z, end.z);
            float y = start.y;

            Vector3 center = new Vector3((minX + maxX) / 2f, y, (minZ + maxZ) / 2f) + Vector3.one * (generator.blockSize * 0.5f);
            center.y = y + generator.blockSize * 0.5f; // Center of the block vertically too
            Vector3 size = new Vector3(maxX - minX + generator.blockSize, generator.blockSize, maxZ - minZ + generator.blockSize);

            Handles.color = Event.current.shift ? Color.red : Color.green;
            Handles.DrawWireCube(center, size);
            Handles.Label(center + Vector3.up, $"RECT: {Mathf.RoundToInt(size.x/generator.blockSize)}x{Mathf.RoundToInt(size.z/generator.blockSize)}");
        }

        private void RemoveBlocksArea(Vector3 start, Vector3 end)
        {
            float minX = Mathf.Min(start.x, end.x);
            float maxX = Mathf.Max(start.x, end.x);
            float minZ = Mathf.Min(start.z, end.z);
            float maxZ = Mathf.Max(start.z, end.z);
            float y = start.y;

            for (float x = minX; x <= maxX; x += generator.blockSize)
            {
                for (float z = minZ; z <= maxZ; z += generator.blockSize)
                {
                    generator.RemoveBlock(new Vector3(x, y, z));
                }
            }
        }

        private void RotateHorizontal()
        {
            serializedObject.Update();
            SerializedProperty rotProp = serializedObject.FindProperty("currentRotation");
            
            if (currentTool == ToolType.Select && selectedBlocks.Count > 0)
            {
                // Validation Phase
                bool canRotate = true;
                string blockReason = "";
                Vector3 centerOfSelection = Vector3.zero;

                foreach (var block in selectedBlocks)
                {
                    if (block == null) continue;
                    centerOfSelection += block.position;
                    
                    Quaternion oldRot = block.rotation;
                    block.Rotate(Vector3.up, 90f, Space.World);
                    Physics.SyncTransforms();
                    
                    BoxCollider col = block.GetComponent<BoxCollider>();
                    if (col != null)
                    {
                        // Precise OBB corner check for boundary
                        Vector3[] localCorners = {
                            col.center + new Vector3(-col.size.x, -col.size.y, -col.size.z) * 0.5f,
                            col.center + new Vector3(col.size.x, -col.size.y, -col.size.z) * 0.5f,
                            col.center + new Vector3(-col.size.x, -col.size.y, col.size.z) * 0.5f,
                            col.center + new Vector3(col.size.x, -col.size.y, col.size.z) * 0.5f,
                            col.center + new Vector3(-col.size.x, col.size.y, -col.size.z) * 0.5f,
                            col.center + new Vector3(col.size.x, col.size.y, -col.size.z) * 0.5f,
                            col.center + new Vector3(-col.size.x, col.size.y, col.size.z) * 0.5f,
                            col.center + new Vector3(col.size.x, col.size.y, col.size.z) * 0.5f
                        };

                        foreach (var lc in localCorners)
                        {
                            Vector3 worldCorner = block.position + (block.rotation * Vector3.Scale(lc, block.localScale));
                            if (worldCorner.x < -0.01f || worldCorner.x > (generator.width * generator.blockSize) + 0.01f ||
                                worldCorner.y < -0.01f || 
                                worldCorner.z < -0.01f || worldCorner.z > (generator.height * generator.blockSize) + 0.01f)
                            {
                                canRotate = false;
                                blockReason = $"OUTBOUND: {block.name}";
                                break;
                            }
                        }

                        if (canRotate)
                        {
                            Vector3 scaledOffset = Vector3.Scale(col.center, block.localScale);
                            Vector3 targetWorldCenter = block.position + (block.rotation * scaledOffset);
                            if (generator.IsAreaOccupied(targetWorldCenter, col.size, block.rotation, selectedBlocks))
                            {
                                canRotate = false;
                                blockReason = $"COLLISION: {block.name}";
                            }
                        }
                    }
                    else if (!generator.IsWithinBounds(block.position, Vector3Int.one) || 
                             generator.IsAreaOccupied(block.position + Vector3.one * (generator.blockSize * 0.5f), Vector3.one, block.rotation, selectedBlocks))
                    {
                        canRotate = false;
                        blockReason = $"BLOCKED: {block.name}";
                    }
                    
                    block.rotation = oldRot; // Revert simulation
                    Physics.SyncTransforms();
                    if (!canRotate) break;
                }

                if (canRotate)
                {
                    Undo.RecordObjects(selectedBlocks.ToArray(), "Rotate Blocks Y");
                    foreach (var block in selectedBlocks)
                    {
                        if (block != null) block.Rotate(Vector3.up, 90f, Space.World);
                    }
                }
                else
                {
                    centerOfSelection /= selectedBlocks.Count;
                    Debug.LogWarning($"Rotation Blocked: {blockReason}");
                }
            }

            // Always update global brush rotation as well
            Vector3 rot = rotProp.vector3Value;
            rot.y = (rot.y + 90f) % 360f;
            rotProp.vector3Value = rot;
            
            serializedObject.ApplyModifiedProperties();
            this.Repaint();
            SceneView.RepaintAll();
        }

        private void RotateVertical()
        {
            serializedObject.Update();
            SerializedProperty rotProp = serializedObject.FindProperty("currentRotation");

            if (currentTool == ToolType.Select && selectedBlocks.Count > 0)
            {
                // Validation Phase
                bool canRotate = true;
                string blockReason = "";
                Vector3 centerOfSelection = Vector3.zero;

                foreach (var block in selectedBlocks)
                {
                    if (block == null) continue;
                    centerOfSelection += block.position;
                    
                    Quaternion oldRot = block.rotation;
                    block.Rotate(Vector3.right, 90f, Space.World);
                    Physics.SyncTransforms();

                    BoxCollider col = block.GetComponent<BoxCollider>();
                    if (col != null)
                    {
                        // Precise OBB corner check
                        Vector3[] localCorners = {
                            col.center + new Vector3(-col.size.x, -col.size.y, -col.size.z) * 0.5f,
                            col.center + new Vector3(col.size.x, -col.size.y, -col.size.z) * 0.5f,
                            col.center + new Vector3(-col.size.x, -col.size.y, col.size.z) * 0.5f,
                            col.center + new Vector3(col.size.x, -col.size.y, col.size.z) * 0.5f,
                            col.center + new Vector3(-col.size.x, col.size.y, -col.size.z) * 0.5f,
                            col.center + new Vector3(col.size.x, col.size.y, -col.size.z) * 0.5f,
                            col.center + new Vector3(-col.size.x, col.size.y, col.size.z) * 0.5f,
                            col.center + new Vector3(col.size.x, col.size.y, col.size.z) * 0.5f
                        };

                        foreach (var lc in localCorners)
                        {
                            Vector3 worldCorner = block.position + (block.rotation * Vector3.Scale(lc, block.localScale));
                            if (worldCorner.x < -0.01f || worldCorner.x > (generator.width * generator.blockSize) + 0.01f ||
                                worldCorner.y < -0.01f || 
                                worldCorner.z < -0.01f || worldCorner.z > (generator.height * generator.blockSize) + 0.01f)
                            {
                                canRotate = false;
                                blockReason = $"OUTBOUND: {block.name}";
                                break;
                            }
                        }

                        if (canRotate)
                        {
                            Vector3 scaledOffset = Vector3.Scale(col.center, block.localScale);
                            Vector3 targetWorldCenter = block.position + (block.rotation * scaledOffset);
                            if (generator.IsAreaOccupied(targetWorldCenter, col.size, block.rotation, selectedBlocks))
                            {
                                canRotate = false;
                                blockReason = $"COLLISION: {block.name}";
                            }
                        }
                    }
                    else if (!generator.IsWithinBounds(block.position, Vector3Int.one) || 
                             generator.IsAreaOccupied(block.position + Vector3.one * (generator.blockSize * 0.5f), Vector3.one, block.rotation, selectedBlocks))
                    {
                        canRotate = false;
                        blockReason = $"BLOCKED: {block.name}";
                    }
                    
                    block.rotation = oldRot; 
                    Physics.SyncTransforms();
                    if (!canRotate) break;
                }

                if (canRotate)
                {
                    Undo.RecordObjects(selectedBlocks.ToArray(), "Rotate Blocks X");
                    foreach (var block in selectedBlocks)
                    {
                        if (block != null) block.Rotate(Vector3.right, 90f, Space.World);
                    }
                }
                else
                {
                    centerOfSelection /= selectedBlocks.Count;
                    Debug.LogWarning($"Rotation Blocked: {blockReason}");
                }
            }

            // Always update global brush rotation as well
            Vector3 rot = rotProp.vector3Value;
            rot.x = (rot.x + 90f) % 360f;
            rotProp.vector3Value = rot;

            serializedObject.ApplyModifiedProperties();
            this.Repaint();
            SceneView.RepaintAll();
        }
    }
}
#endif
