using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TD.PushBox;

namespace JobTest.TacticsVoxel
{
    public sealed partial class VoxelLevelWindow : EditorWindow
    {
        // Keep the serialized erase value; 5 belonged to the removed duplicate tool.
        private enum Mode { Select,Paint,Rectangle,Pick,Move,Erase=6 }
        [SerializeField] private VoxelTacticsLevel level;
        [SerializeField] private VoxelLevelPalette palette;
        [SerializeField] private VoxelPaletteCategory category;
        [SerializeField] private Mode mode=Mode.Paint;
        [SerializeField] private int entryIndex,height;
        [SerializeField] private int statueDirection;
        [SerializeField] private bool editing,preserveFlatHeight,showGrid=true;
        [SerializeField] private GameObject selected;
        private Vector2 scroll;
        private string search="",status="";
        private VoxelLevelEditOperations context;
        private Vector2Int hover,rectangleStart,coordinate;
        private bool hasHover,dragging;
        private int undoGroup=-1;
        private int strokeControl;
        private readonly HashSet<Vector2Int> painted=new();
        private UnityEditor.Editor sharedEditor;
        private bool editShared;
        private static readonly string[] Categories={"地形","角色","敌方","机关","环境"};
        private static readonly Mode[] ToolModes={Mode.Select,Mode.Paint,Mode.Rectangle,Mode.Pick,Mode.Move,Mode.Erase};
        private static readonly string[] Icons={"d_RectTool","d_Grid.PaintTool","d_Grid.BoxTool","d_Grid.PickingTool","d_MoveTool","TreeEditor.Trash"};
        private static readonly string[] ModeNames={"选择","绘制","矩形绘制","吸取","移动所选对象","擦除"};

        [MenuItem("TD推箱子/关卡编辑")]
        public static void Open()
        {
            var window=GetWindow<VoxelLevelWindow>("关卡编辑");window.minSize=new Vector2(330,600);
            if(window.level==null) window.level=FindLevel();
            if(window.palette==null||window.palette.puzzlePalette!=(window.level!=null&&window.level.GetComponent<PushBoxLevel>()!=null)) window.palette=VoxelLevelPalette.For(window.level);
            window.Show();
        }
        public static void Open(VoxelTacticsLevel target,VoxelLevelPalette resources)
        {
            Open();
            var window=GetWindow<VoxelLevelWindow>();
            window.ClearTerrainPreview();window.SetEditing(false);window.afterSummon=false;
            window.level=target;window.palette=resources;window.selected=null;
            window.SetCategory(VoxelPaletteCategory.Terrain);window.Invalidate();window.FrameBoard();
        }
        private static VoxelTacticsLevel FindLevel()
        {
            var go=Selection.activeGameObject;
            if(go!=null&&go.GetComponentInParent<VoxelTacticsLevel>()!=null) return go.GetComponentInParent<VoxelTacticsLevel>();
            return UnityEngine.Object.FindObjectsByType<VoxelTacticsLevel>(FindObjectsSortMode.None)
                .FirstOrDefault(l=>l.gameObject.scene==SceneManager.GetActiveScene());
        }
        private void OnEnable()
        {
            titleContent=new GUIContent("关卡编辑");
            if(!Enum.IsDefined(typeof(Mode),mode)) {mode=Mode.Select;editing=false;}
            SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=Invalidate;
            EditorApplication.hierarchyChanged+=HierarchyChanged;Selection.selectionChanged+=SelectionChanged;
            EditorApplication.projectChanged+=Invalidate;AssemblyReloadEvents.beforeAssemblyReload+=ClearTerrainPreview;
            EditorSceneManager.sceneSaving+=BeforeSceneSave;
            EditorSceneManager.activeSceneChangedInEditMode+=SceneChanged;
            EditorApplication.playModeStateChanged+=PlayStateChanged;
        }
        private void OnDisable()
        {
            FinishStroke();ClearTerrainPreview();SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=Invalidate;
            EditorApplication.hierarchyChanged-=HierarchyChanged;Selection.selectionChanged-=SelectionChanged;
            EditorApplication.projectChanged-=Invalidate;AssemblyReloadEvents.beforeAssemblyReload-=ClearTerrainPreview;
            EditorSceneManager.sceneSaving-=BeforeSceneSave;
            EditorSceneManager.activeSceneChangedInEditMode-=SceneChanged;
            EditorApplication.playModeStateChanged-=PlayStateChanged;
            if(sharedEditor!=null) DestroyImmediate(sharedEditor);SceneView.RepaintAll();
        }
        private void PlayStateChanged(PlayModeStateChange change) { ClearTerrainPreview();afterSummon=false;SetEditing(false);context=null;Repaint(); }
        private void SceneChanged(Scene oldScene,Scene newScene)
        { ClearTerrainPreview();afterSummon=false;SetEditing(false);level=FindLevel();palette=VoxelLevelPalette.For(level);selected=null;Invalidate(); }
        private void Invalidate() { context=null;terrainPreviewDirty=true;Repaint();SceneView.RepaintAll(); }
        private void SelectionChanged()
        {
            var candidate=Selection.activeGameObject;
            if(candidate!=null&&level!=null&&candidate.transform.IsChildOf(level.transform))
            {
                var root=Resolve(candidate);
                if(root!=null) { selected=root;UpdateCoordinate();editShared=false; }
            }
            Repaint();
        }
        private bool EnsureContext()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||level==null||palette==null) return false;
            bool puzzle=level.GetComponent<PushBoxLevel>()!=null;
            if(palette.puzzlePalette!=puzzle) {palette=VoxelLevelPalette.For(level);context=null;}
            if(PrefabStageUtility.GetCurrentPrefabStage()!=null) { status="请退出 Prefab 编辑模式。";return false; }
            try { context??=new VoxelLevelEditOperations(level,palette);return true; }
            catch(Exception e) { status=e.Message;return false; }
        }
        private VoxelLevelPalette.Entry Entry=>palette!=null&&entryIndex>=0&&entryIndex<palette.entries.Count?palette.entries[entryIndex]:null;
        private bool RectangleAllowed=>category==VoxelPaletteCategory.Terrain||afterSummon&&category==VoxelPaletteCategory.Objects&&Entry?.prefab?.GetComponent<PushBoxObject>()?.Kind==PushBoxKind.Goal;
        private void Choose(int index)
        {
            entryIndex=index;var tile=Entry?.prefab!=null?Entry.prefab.GetComponent<VoxelTacticsTile>():null;
            if(tile!=null) height=tile.HeightLevel;
            SceneView.RepaintAll();Repaint();
        }
        private void SetEditing(bool active)
        {
            FinishStroke();editing=active;hasHover=false;status="";
            SceneView.RepaintAll();Repaint();
        }
        private void ActivateTool(Mode next)
        { placingBoss=false;mode=next;SetEditing(true); }
        private void ChooseForPainting(int index)
        {
            Choose(index);
            ActivateTool(mode==Mode.Rectangle&&RectangleAllowed?Mode.Rectangle:Mode.Paint);
        }
        private void SetCategory(VoxelPaletteCategory next)
        {
            FinishStroke();hasHover=false;status="";placingBoss=false;
            category=next;int index=palette.entries.FindIndex(e=>!e.surfaceDetail&&e.category==category&&(!afterSummon||IsChangeEntry(e)));Choose(index);
            if(mode==Mode.Rectangle&&!RectangleAllowed) mode=Mode.Paint;
        }

        private void OnGUI()
        {
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                level=(VoxelTacticsLevel)EditorGUILayout.ObjectField("关卡",level,typeof(VoxelTacticsLevel),true);
                palette=(VoxelLevelPalette)EditorGUILayout.ObjectField("资源目录",palette,typeof(VoxelLevelPalette),false);
                if(EditorGUI.EndChangeCheck()) { ClearTerrainPreview();afterSummon=false;SetEditing(false);Invalidate(); }
                if(palette==null)
                { if(GUILayout.Button("加载默认资源目录")) palette=VoxelLevelPalette.Default();return; }
                if(level==null)
                {
                    if(GUILayout.Button("使用当前场景关卡")) {level=FindLevel();Invalidate();}
                    if(GUILayout.Button("创建关卡")) TDLevelCreateWindow.Open();
                    return;
                }
                if(!EnsureContext()) { EditorGUILayout.HelpBox(status,MessageType.Warning);return; }
                DrawTerrainChangeControls();
                var editingButtonStyle=new GUIStyle(EditorStyles.toolbarButton)
                {fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
                if(editing)
                    foreach(var state in new[]{editingButtonStyle.onNormal,editingButtonStyle.onHover,editingButtonStyle.onActive,editingButtonStyle.onFocused})
                    {state.background=Texture2D.whiteTexture;state.scaledBackgrounds=null;state.textColor=Color.white;}
                using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar,GUILayout.Height(34)))
                {
                    Color oldBackground=GUI.backgroundColor;
                    if(editing) GUI.backgroundColor=new Color(0.20f,0.56f,0.36f);
                    EditorGUI.BeginChangeCheck();
                    bool nextEditing=GUILayout.Toggle(editing,new GUIContent(editing?"编辑中":"已暂停",EditorGUIUtility.IconContent("d_EditCollider").image,"启用或暂停场景编辑；选择资源或工具也会启用编辑"),editingButtonStyle,GUILayout.Width(116),GUILayout.Height(32));
                    GUI.backgroundColor=oldBackground;
                    if(EditorGUI.EndChangeCheck()) SetEditing(nextEditing);
                    showGrid=GUILayout.Toggle(showGrid,new GUIContent(EditorGUIUtility.IconContent("Overlays/ViewOptions").image,"网格显示"),EditorStyles.toolbarButton,GUILayout.Width(30),GUILayout.Height(32));
                    GUILayout.Label($"{context.Size.x} × {context.Size.y}",EditorStyles.miniLabel,GUILayout.Height(32));
                    GUILayout.FlexibleSpace();
                    if(GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_ViewToolZoom").image,"聚焦棋盘"),EditorStyles.toolbarButton,GUILayout.Width(28),GUILayout.Height(32))) FrameBoard();
                    if(GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_SaveAs").image,"保存当前场景"),EditorStyles.toolbarButton,GUILayout.Width(28),GUILayout.Height(32)))
                    {AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(level.gameObject.scene);}
                }
                float categoryHeight=EditorStyles.miniButton.CalcSize(new GUIContent(Categories[0])).y+6;
                Rect categoryRect=EditorGUILayout.GetControlRect(false,categoryHeight);
                int categoryCount=afterSummon?3:Categories.Length;
                for(int i=0;i<categoryCount;i++)
                {
                    // Toolbar substitutes its own segmented styles, so size each segment directly.
                    var categoryButtonStyle=new GUIStyle(i==0?EditorStyles.miniButtonLeft:
                        i==categoryCount-1?EditorStyles.miniButtonRight:EditorStyles.miniButtonMid)
                    {fixedHeight=categoryHeight,alignment=TextAnchor.MiddleCenter};
                    float left=categoryRect.x+categoryRect.width*i/categoryCount;
                    float right=categoryRect.x+categoryRect.width*(i+1)/categoryCount;
                    var target=afterSummon?(i==0?VoxelPaletteCategory.Terrain:i==1?VoxelPaletteCategory.Enemies:VoxelPaletteCategory.Objects):(VoxelPaletteCategory)i;
                    string label=afterSummon?(i==0?"地形":i==1?"敌方":"机关"):Categories[i];
                    if(GUI.Toggle(new Rect(left,categoryRect.y,right-left,categoryHeight),target==category,label,categoryButtonStyle)
                        &&target!=category) SetCategory(target);
                }
                var toolButtonStyle=new GUIStyle(EditorStyles.miniButton)
                {fixedHeight=36,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(4,4,10,10)};
                using(new EditorGUILayout.HorizontalScope())
                    for(int i=0;i<ToolModes.Length;i++)
                    {
                        var tool=ToolModes[i];
                        bool allowed=tool!=Mode.Rectangle||RectangleAllowed;
                        using(new EditorGUI.DisabledScope(!allowed))
                        {
                            var icon=EditorGUIUtility.IconContent(Icons[i]);
                            EditorGUI.BeginChangeCheck();
                            GUILayout.Toggle(editing&&mode==tool,new GUIContent(icon.image,ModeNames[i]),toolButtonStyle,GUILayout.Height(36));
                            if(EditorGUI.EndChangeCheck()) ActivateTool(tool);
                        }
                    }
                GUILayout.Space(8);
                using(new EditorGUILayout.HorizontalScope())
                {
                    height=Mathf.Clamp(EditorGUILayout.IntField("画笔高度层",height),-32,32);
                    if(category==VoxelPaletteCategory.Terrain) preserveFlatHeight=GUILayout.Toggle(preserveFlatHeight,new GUIContent("平地换色保留高度","默认按画笔高度绘制；仅平地之间换色时保留高度，高地和斜坡刷平不受影响"));
                }
                if(!afterSummon&&category==VoxelPaletteCategory.Decoration)
                {
                    DrawEnvironmentControls();
                    if(!EnsureContext()) {EditorGUILayout.HelpBox(status,MessageType.Warning);return;}
                }
                if(DirectionStatue!=null)
                {
                    int direction=CurrentStatueDirection;
                    EditorGUI.BeginChangeCheck();
                    direction=EditorGUILayout.Popup("模型朝向",direction,new[]{"南（-Z）","西（-X）","北（+Z）","东（+X）"});
                    if(EditorGUI.EndChangeCheck()) SetStatueDirection(direction);
                }
                search=EditorGUILayout.TextField("搜索",search);
                scroll=EditorGUILayout.BeginScrollView(scroll);
                DrawPalette();if(afterSummon) DrawChangeSelection();else DrawSelection();
                EditorGUILayout.EndScrollView();
                if(hasHover) GUILayout.Label($"格点 {hover.x}, {hover.y}    高度层 {height}",EditorStyles.miniLabel);
                if(!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status,MessageType.Info);
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode) EditorGUILayout.HelpBox("运行期间关卡编辑已锁定。",MessageType.Info);
        }

        private void DrawPalette()
        {
            var entries=palette.entries.Select((e,i)=>(e,i)).Where(pair=>!pair.e.surfaceDetail&&pair.e.category==category&&(!afterSummon||IsChangeEntry(pair.e))
                &&(string.IsNullOrEmpty(search)||(pair.e.label??"").IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
            if(category==VoxelPaletteCategory.Terrain)
            {
                bool Obstacle(VoxelLevelPalette.Entry entry)
                {
                    var tile=entry.prefab!=null?entry.prefab.GetComponent<VoxelTacticsTile>():null;
                    return entry.IsTerrainObject||tile!=null&&tile.CreateState().kind!=VoxelGroundKind.Void&&(!tile.Walkable||tile.HeightLevel>0);
                }
                var ground=entries.Where(pair=>!Obstacle(pair.e)).ToArray();
                var obstacles=entries.Where(pair=>Obstacle(pair.e)).ToArray();
                if(ground.Length>0) {EditorGUILayout.LabelField("地面",EditorStyles.boldLabel);DrawPaletteEntries(ground,3);}
                if(obstacles.Length>0) {EditorGUILayout.Space(6);EditorGUILayout.LabelField("障碍",EditorStyles.boldLabel);DrawPaletteEntries(obstacles,3);}
            }
            else DrawPaletteEntries(entries,Mathf.Max(2,Mathf.FloorToInt((position.width-28)/92)));
            if(Entry!=null)
            {
                string error=VoxelLevelPalette.Check(Entry);
                if(error!=null) EditorGUILayout.HelpBox(error,MessageType.Warning);
            }
        }

        private void DrawPaletteEntries((VoxelLevelPalette.Entry e,int i)[] entries,int columns)
        {
            for(int row=0;row<entries.Length;row+=columns)
                using(new EditorGUILayout.HorizontalScope())
                    for(int column=0;column<columns;column++)
                    {
                        int index=row+column;
                        if(index>=entries.Length) { GUILayout.Space(86);continue; }
                        var pair=entries[index];string error=VoxelLevelPalette.Check(pair.e);
                        Texture image=pair.e.prefab!=null?AssetPreview.GetAssetPreview(pair.e.prefab)??AssetPreview.GetMiniThumbnail(pair.e.prefab):null;
                        Color old=GUI.backgroundColor;if(entryIndex==pair.i) GUI.backgroundColor=new Color(0.55f,0.83f,0.70f);
                        using(new EditorGUI.DisabledScope(error!=null))
                            if(GUILayout.Button(new GUIContent(pair.e.label,image,error??pair.e.prefab.name),GUILayout.Height(72),GUILayout.MinWidth(80))) ChooseForPainting(pair.i);
                        GUI.backgroundColor=old;
                    }
        }

        private void DrawSelection()
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("当前对象",EditorStyles.boldLabel);
            var next=(GameObject)EditorGUILayout.ObjectField(selected,typeof(GameObject),true);
            if(next!=selected) {selected=next;UpdateCoordinate();editShared=false;}
            if(selected==null||!context.IsOwned(selected)) return;
            Component component=selected.GetComponent<PushBoxActor>();
            if(component==null) component=selected.GetComponent<PushBoxObject>();
            if(component==null) component=selected.GetComponent<VoxelTacticsTile>();
            bool movable=component is PushBoxActor||component is PushBoxObject||context.IsDecoration(selected)||context.IsModelOnlyObject(selected);
            if(movable)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    coordinate=EditorGUILayout.Vector2IntField("格点",coordinate);
                    if(GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_MoveTool").image,"移动到填写的格点"),GUILayout.Width(28)))
                        OneAction(()=>context.Move(selected,coordinate,height,out status));
                }
            }
            if(component==null) return;
            if(selected.TryGetComponent<PushBoxEntrance>(out var entrance))
            {
                if(PushBoxEntranceEditor.Draw(entrance))
                {VoxelLevelEditOperations.Record(entrance);EditorSceneManager.MarkSceneDirty(level.gameObject.scene);Invalidate();}
                return;
            }
            var serialized=new SerializedObject(component);serialized.Update();
            using(new EditorGUI.DisabledScope(true))
            {
                var id=serialized.FindProperty("stableId");if(id!=null) EditorGUILayout.TextField("唯一标识",id.stringValue);
            }
            EditorGUI.BeginChangeCheck();
            var property=serialized.GetIterator();bool enter=true;
            var excluded=new HashSet<string>{"m_Script","stableId","gridPosition","visualRoot","selectionMarker","healthAnchor","movingPart","indicator","surfaceOffset","highlightMaterial"};
            while(property.NextVisible(enter))
            {
                enter=false;if(excluded.Contains(property.name)) continue;
                if(component is PushBoxObject door&&property.name=="inputs") {PushBoxObjectEditor.DrawInputs(property,door);continue;}
                if(component is PushBoxObject obj)
                {
                    if(obj.Kind!=PushBoxKind.Crate&&property.name=="crushable") continue;
                    if(obj.Kind!=PushBoxKind.Enemy&&property.name=="attack") continue;
                    if(obj.Kind!=PushBoxKind.Door&&property.name=="doorRule") continue;
                    if(obj.Kind==PushBoxKind.Door&&obj.DoorRule==PushBoxDoorRule.AllEnemiesDefeated&&(property.name=="inputs"||property.name=="startsOpen"||property.name=="toggleOnPress")) continue;
                    if(obj.Kind==PushBoxKind.Enemy&&(property.name=="inputs"||property.name=="startsOpen"||property.name=="toggleOnPress"||property.name=="lowerDistance")) continue;
                }
                // Tile shape/height is changed through the brush, keeping the matching mesh and rule direction together.
                if(component is VoxelTacticsTile&&property.name!="definition") continue;
                EditorGUILayout.PropertyField(property,true);
            }
            bool changed=EditorGUI.EndChangeCheck();
            if(serialized.ApplyModifiedProperties()||changed) {VoxelLevelEditOperations.Record(component);EditorSceneManager.MarkSceneDirty(level.gameObject.scene);Invalidate();}
            serialized.Update();var definition=serialized.FindProperty("definition");
            if(definition?.objectReferenceValue is ScriptableObject asset)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    editShared=GUILayout.Toggle(editShared,"编辑共享配置",EditorStyles.miniButton);
                    if(GUILayout.Button("复制配置给当前对象",EditorStyles.miniButton)) DuplicateDefinition(component,asset);
                }
                if(editShared)
                {
                    EditorGUILayout.HelpBox("共享配置的修改会影响所有引用它的对象。",MessageType.Warning);
                    UnityEditor.Editor.CreateCachedEditor(asset,null,ref sharedEditor);sharedEditor.OnInspectorGUI();
                }
            }
        }
        private void DuplicateDefinition(Component owner,ScriptableObject asset)
        {
            string path=EditorUtility.SaveFilePanelInProject("另存配置",asset.name+"_独立","asset","选择新配置的位置");
            if(string.IsNullOrEmpty(path)) return;
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(asset),path)) {status="配置复制失败。";return;}
            var serialized=new SerializedObject(owner);serialized.FindProperty("definition").objectReferenceValue=TDProjectPaths.Load<ScriptableObject>(path);
            serialized.ApplyModifiedProperties();VoxelLevelEditOperations.Record(owner);EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            editShared=false;status="当前对象已使用独立配置；场景撤销不会删除新配置文件。";
        }
        private void FrameBoard()
        {
            if(!EnsureContext()) return;
            var view=SceneView.lastActiveSceneView;if(view==null) return;
            Vector3 center=level.Surface(new Vector2Int(context.Size.x/2,context.Size.y/2),0);
            view.Frame(new Bounds(center,new Vector3(context.Size.x*level.CellSize,2,context.Size.y*level.CellSize)),false);
        }
        private GameObject Resolve(GameObject candidate)
        {
            if(candidate==null||level==null) return null;
            var puzzleActor=candidate.GetComponentInParent<PushBoxActor>();if(puzzleActor!=null&&puzzleActor.GetComponentInParent<VoxelTacticsLevel>()==level) return puzzleActor.gameObject;
            var puzzleItem=candidate.GetComponentInParent<PushBoxObject>();if(puzzleItem!=null&&puzzleItem.GetComponentInParent<VoxelTacticsLevel>()==level) return puzzleItem.gameObject;
            var prefab=PrefabUtility.GetNearestPrefabInstanceRoot(candidate);
            if(prefab!=null&&context!=null&&context.IsOwned(prefab)&&(context.IsSurfaceDetail(prefab)||context.IsModelOnlyObject(prefab))) return prefab;
            var tile=candidate.GetComponentInParent<VoxelTacticsTile>();if(tile!=null&&tile.GetComponentInParent<VoxelTacticsLevel>()==level) return tile.gameObject;
            var parent=level.DetailsContainer;var objects=level.ObjectsContainer;
            for(Transform t=candidate.transform;t!=null&&t!=level.transform;t=t.parent) if(t.parent==parent||t.parent==objects) return t.gameObject;
            return null;
        }
        private void UpdateCoordinate()
        {
            if(selected==null||level==null) return;
            if(PushBoxEditOperations.IsEntity(selected)) {coordinate=PushBoxEditOperations.Position(selected);return;}
            coordinate=
                Vector2Int.RoundToInt(new Vector2((level.transform.InverseTransformPoint(selected.transform.position).x-level.GridOrigin.x)/level.CellSize,
                    (level.transform.InverseTransformPoint(selected.transform.position).z-level.GridOrigin.z)/level.CellSize));
        }
        private void Select(GameObject go) {selected=go;Selection.activeGameObject=go;UpdateCoordinate();Repaint();}
        private void StartStroke()
        {
            Undo.IncrementCurrentGroup();undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("关卡编辑："+ModeNames[Array.IndexOf(ToolModes,mode)]);painted.Clear();
        }
        private void FinishStroke()
        {
            if(undoGroup>=0) {Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(undoGroup);undoGroup=-1;}
            if(strokeControl!=0&&GUIUtility.hotControl==strokeControl) GUIUtility.hotControl=0;
            strokeControl=0;
            dragging=false;painted.Clear();
        }
        private void OneAction(Action action,string label=null)
        {StartStroke();if(label!=null) Undo.SetCurrentGroupName(label);try{action();}catch(Exception e){status=e.Message;}finally{FinishStroke();Invalidate();}}
        private GameObject At(Vector2Int p,Vector2 mouse)
        {
            var picked=Resolve(HandleUtility.PickGameObject(mouse,false));
            if(category==VoxelPaletteCategory.Terrain)
            {
                if(context.IsTerrainObject(picked)&&PushBoxEditOperations.Position(picked)==p) return picked;
                var obstacle=level.GetComponentsInChildren<PushBoxObject>().FirstOrDefault(item=>item.Covers(p)&&context.IsTerrainObject(item.gameObject));
                return obstacle!=null?obstacle.gameObject:context.tiles.TryGetValue(p,out var tile)?tile.gameObject:null;
            }
            if(picked==null) return null;
            if(category==VoxelPaletteCategory.Objects&&(picked.GetComponent<PushBoxObject>() is PushBoxObject obj&&obj.Kind!=PushBoxKind.Enemy&&!context.IsDecoration(picked)&&!context.IsTerrainObject(picked)||context.IsModelOnlyObject(picked))) return picked;
            if(category==VoxelPaletteCategory.Players&&picked.GetComponent<PushBoxActor>()!=null) return picked;
            if(category==VoxelPaletteCategory.Decoration&&context.IsDecoration(picked)) return picked;
            if(category==VoxelPaletteCategory.Enemies&&picked.GetComponent<PushBoxObject>()?.Kind==PushBoxKind.Enemy) return picked;
            return null;
        }
        private void Apply(Vector2Int p,Vector2 mouse)
        {
            if(!painted.Add(p)) return;
            if(afterSummon) {ApplyTerrainChange(p);return;}
            switch(mode)
            {
                case Mode.Paint:
                case Mode.Rectangle:
                    if(Entry==null||Entry.category!=category||Entry.surfaceDetail) {status="请选择当前分类的资源。";return;}
                    if(category==VoxelPaletteCategory.Terrain&&!Entry.IsTerrainObject) context.Paint(p,Entry,height,preserveFlatHeight,out status);
                    else {var go=context.Place(p,Entry,height,out status,statueDirection*90);if(go!=null) Select(go);}
                    break;
                case Mode.Erase:
                    var erased=At(p,mouse);if(erased!=null) context.Erase(erased,out status);
                    break;
                case Mode.Select: Select(At(p,mouse));break;
                case Mode.Move:
                    context.Move(selected,p,height,out status);UpdateCoordinate();break;
                case Mode.Pick:
                    var source=At(p,mouse);
                    if(source!=null)
                    {
                        var prefab=PrefabUtility.GetCorrespondingObjectFromSource(source);
                        int index=palette.entries.FindIndex(e=>!e.surfaceDetail&&e.prefab==prefab);
                        if(index>=0) {category=palette.entries[index].category;Choose(index);mode=Mode.Paint;}
                        if(source.TryGetComponent<VoxelTacticsTile>(out var tile)) height=tile.HeightLevel;
                    }
                    break;
            }
        }
        private void SafeApply(Vector2Int p,Vector2 mouse)
        {try{Apply(p,mouse);}catch(Exception error){status=error.Message;FinishStroke();}}
        private void SceneGUI(SceneView view)
        {
            if(afterSummon&&EnsureContext()&&Event.current.type==EventType.Repaint) RefreshTerrainPreview();
            if(!editing||!EnsureContext()) return;
            Event e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape) {SetEditing(false);e.Use();return;}
            if(e.type==EventType.KeyDown&&!e.alt&&!e.control&&(e.keyCode==KeyCode.Q||e.keyCode==KeyCode.E)&&DirectionStatue!=null)
            {SetStatueDirection((CurrentStatueDirection+(e.keyCode==KeyCode.E?1:3))%4);e.Use();return;}
            if(e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Q||e.keyCode==KeyCode.E)&&category==VoxelPaletteCategory.Terrain)
            {
                var current=Entry?.prefab!=null?Entry.prefab.GetComponent<VoxelTacticsTile>():null;
                if(current!=null&&current.Slope!=VoxelSlopeDirection.None)
                {
                    int slope=((int)current.Slope-1+(e.keyCode==KeyCode.E?1:3))%4+1;
                    int index=palette.entries.FindIndex(entry=>entry.prefab!=null&&entry.prefab.TryGetComponent<VoxelTacticsTile>(out var tile)&&tile.Slope==(VoxelSlopeDirection)slope);
                    if(index>=0) {int oldHeight=height;Choose(index);height=oldHeight;}e.Use();
                }
            }
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout&&!e.alt) HandleUtility.AddDefaultControl(control);
            Ray ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane=new Plane(level.transform.up,level.Surface(Vector2Int.zero,height));
            hasHover=plane.Raycast(ray,out float distance);
            if(hasHover)
            {
                Vector3 point=level.transform.InverseTransformPoint(ray.GetPoint(distance))-level.GridOrigin;
                hover=new Vector2Int(Mathf.FloorToInt(point.x/level.CellSize+0.5f),Mathf.FloorToInt(point.z/level.CellSize+0.5f));
            }
            if(e.type==EventType.Repaint) {if(showGrid) DrawGrid();if(hasHover) DrawPreview();}
            if(e.alt||e.type==EventType.Ignore||e.type==EventType.MouseLeaveWindow)
            {if(dragging) FinishStroke();return;}
            if(e.type==EventType.MouseDown&&e.button==0&&hasHover&&HandleUtility.nearestControl==control)
            {
                StartStroke();dragging=true;rectangleStart=hover;strokeControl=control;GUIUtility.hotControl=control;
                if(mode!=Mode.Rectangle) SafeApply(hover,e.mousePosition);e.Use();
            }
            else if(e.type==EventType.MouseDrag&&e.button==0&&dragging&&hasHover)
            {
                if(mode==Mode.Paint||mode==Mode.Erase)
                {
                    Vector2Int from=painted.Count==0?hover:rectangleStart;
                    int steps=Mathf.Max(Mathf.Abs(hover.x-from.x),Mathf.Abs(hover.y-from.y));
                    for(int i=0;i<=Mathf.Min(steps,2048);i++) SafeApply(Vector2Int.RoundToInt(Vector2.Lerp(from,hover,steps==0?0:i/(float)steps)),e.mousePosition);
                    rectangleStart=hover;
                }
                e.Use();
            }
            else if(e.type==EventType.MouseUp&&e.button==0&&dragging)
            {
                if(mode==Mode.Rectangle&&hasHover)
                {
                    try {if(afterSummon) ApplyChangeRectangle(rectangleStart,hover);else context.PaintRectangle(rectangleStart,hover,Entry,height,preserveFlatHeight,out status);}
                    catch(Exception error) {status=error.Message;}
                }
                FinishStroke();GUIUtility.hotControl=0;Invalidate();e.Use();
            }
            if(e.type==EventType.MouseMove||dragging) {view.Repaint();Repaint();}
        }
        private PushBoxObject DirectionStatue
        {
            get
            {
                var item=afterSummon&&ChangePlan!=null&&(mode==Mode.Select||mode==Mode.Move)?(category==VoxelPaletteCategory.Enemies?EffectiveEnemies().FirstOrDefault(obj=>obj.key==changeEnemyKey)?.targetPrefab:EffectiveObjects().FirstOrDefault(obj=>obj.key==changeObjectKey)?.targetPrefab):
                    (mode==Mode.Select||mode==Mode.Move?selected:Entry?.prefab)?.GetComponent<PushBoxObject>();
                return item!=null&&(item.Kind==PushBoxKind.Blocker||item.Attack==PushBoxAttack.DragonFire)?item:null;
            }
        }
        private int CurrentStatueDirection=>afterSummon&&(mode==Mode.Select||mode==Mode.Move)
            ?((category==VoxelPaletteCategory.Enemies?EffectiveEnemies().FirstOrDefault(obj=>obj.key==changeEnemyKey)?.yaw??0:EffectiveObjects().FirstOrDefault(obj=>obj.key==changeObjectKey)?.yaw??0)/90)%4:mode==Mode.Select||mode==Mode.Move
                ?Mathf.RoundToInt(Mathf.Repeat((Quaternion.Inverse(level.transform.rotation)*DirectionStatue.transform.rotation).eulerAngles.y,360)/90)%4:statueDirection;
        private void SetStatueDirection(int direction)
        {
            statueDirection=direction;
            if(afterSummon)
            {
                if(mode==Mode.Select||mode==Mode.Move)
                {
                    if(category==VoxelPaletteCategory.Enemies)
                    {var item=EffectiveEnemies().FirstOrDefault(obj=>obj.key==changeEnemyKey);if(item!=null) {item.yaw=direction*90;SaveEnemyChange(item);}}
                    else {var item=EffectiveObjects().FirstOrDefault(obj=>obj.key==changeObjectKey);if(item!=null) {item.yaw=direction*90;SaveObjectChange(item);}}
                }
                Repaint();SceneView.RepaintAll();return;
            }
            if(mode==Mode.Select||mode==Mode.Move) context.RotateStatue(selected,direction*90);
            Repaint();SceneView.RepaintAll();
        }
        private void DrawGrid()
        {
            Handles.color=new Color(0.75f,0.88f,0.9f,0.25f);
            Vector3 Point(float x,float z)=>level.transform.TransformPoint(level.GridOrigin+new Vector3(x*level.CellSize,height*level.HeightStep+0.015f,z*level.CellSize));
            for(int x=0;x<=context.Size.x;x++) Handles.DrawLine(Point(x-0.5f,-0.5f),Point(x-0.5f,context.Size.y-0.5f));
            for(int z=0;z<=context.Size.y;z++) Handles.DrawLine(Point(-0.5f,z-0.5f),Point(context.Size.x-0.5f,z-0.5f));
        }
        private void DrawPreview()
        {
            bool valid=category==VoxelPaletteCategory.Decoration||context.Contains(hover);
            if((mode==Mode.Paint||mode==Mode.Rectangle)&&Entry!=null)
                valid&=(afterSummon?CheckChange(hover):category==VoxelPaletteCategory.Terrain?context.CheckTerrain(hover,Entry,false,context.ResolveTerrainHeight(hover,Entry,height,preserveFlatHeight)):
                    VoxelLevelPalette.Check(Entry)??context.CheckPlacement(Entry.prefab,hover,null,category==VoxelPaletteCategory.Decoration,Entry.surfaceDetail,Entry.modelOnly))==null;
            bool painting=mode==Mode.Paint||mode==Mode.Rectangle;
            var template=Entry?.prefab!=null?Entry.prefab.GetComponent<VoxelTacticsTile>():null;
            Vector2Int uphill=painting&&template!=null?template.CreateState().Uphill:
                context.tiles.TryGetValue(hover,out var hovered)?hovered.CreateState().Uphill:Vector2Int.zero;
            int effectiveHeight=painting&&template!=null?(afterSummon?ChangeHeight(hover):context.ResolveTerrainHeight(hover,Entry,height,preserveFlatHeight)):height;
            Vector3 center=(painting&&template!=null?level.Surface(hover,effectiveHeight)
                +level.transform.up*(uphill==Vector2Int.zero?0:level.HeightStep/2):afterSummon?ChangeSurface(hover):context.Surface(hover,height))+level.transform.up*0.03f;
            Vector3 Corner(float x,float z)=>center+level.transform.TransformVector(new Vector3(x,(x*uphill.x+z*uphill.y)/1.08f*level.HeightStep,z));
            var item=(mode==Mode.Move?selected:painting?Entry?.prefab:null)?.GetComponent<PushBoxObject>();
            if(afterSummon&&mode==Mode.Move&&ChangePlan!=null) item=EffectiveObjects().FirstOrDefault(obj=>obj.key==changeObjectKey)?.targetPrefab;
            if(afterSummon&&placingBoss) item=null;
            var footprint=item!=null?item.Footprint:Vector2Int.one;
            center+=level.transform.TransformVector(new Vector3((footprint.x-1)*level.CellSize/2,0,(footprint.y-1)*level.CellSize/2));
            float edgeX=level.CellSize*(footprint.x/2f-0.04f),edgeZ=level.CellSize*(footprint.y/2f-0.04f);
            Handles.DrawSolidRectangleWithOutline(new[]{Corner(-edgeX,-edgeZ),Corner(-edgeX,edgeZ),Corner(edgeX,edgeZ),Corner(edgeX,-edgeZ)},
                valid?new Color(0.2f,0.9f,0.6f,0.15f):new Color(1,0.2f,0.15f,0.2f),valid?Color.green:Color.red);
            Handles.Label(center+level.transform.up*0.12f,$"{(afterSummon&&placingBoss?"Boss 出生格 ":"")}{hover.x}, {hover.y}");
            if(mode==Mode.Rectangle&&dragging)
            {
                Handles.color=new Color(0.2f,0.9f,0.6f,0.8f);
                Vector3 a=level.Surface(rectangleStart,height),b=level.Surface(hover,height);
                Handles.DrawWireCube((a+b)/2,new Vector3((Mathf.Abs(hover.x-rectangleStart.x)+1)*level.CellSize,0.04f,(Mathf.Abs(hover.y-rectangleStart.y)+1)*level.CellSize));
            }
        }
    }
}
