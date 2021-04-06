using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Devdog.General;
using Devdog.General.Editors;
using UnityEditor;
using UnityEngine;
using EditorUtility = UnityEditor.EditorUtility;
using Object = UnityEngine.Object;
using EditorStyles = Devdog.General.Editors.EditorStyles;

namespace Devdog.InventoryPro.Editors {

	public class ItemEditor : InventoryEditorCrudBase<InventoryItemBase> {
		protected class TypeFilter {
			public System.Type type;
			public bool enabled;

			public TypeFilter(System.Type type, bool enabled) {
				this.type = type;
				this.enabled = enabled;
			}
		}

		protected override List<InventoryItemBase> crudList {
			get { return new List<InventoryItemBase>(ItemManager.database.items); }
			set {
				ItemManager.database.items = value.ToArray();
				//Resize(itemEditorInspectorList, ItemManager.database.items.Length, null);
			}
		}

		public Editor itemEditorInspector { get; set; }

		private static InventoryItemBase _previousItem;
		private static InventoryItemBase _isDraggingPrefab;
		private string _previouslySelectedGUIItemName;
		private bool updatingPrefabs;
		protected TypeFilter[] allItemTypes;

		private UnityEngine.SceneManagement.Scene previewScene;
		//private List<Editor> itemEditorInspectorList;
		private int selectedIndex = -1;

		public ItemEditor(string singleName, string pluralName, EditorWindow window, UnityEngine.SceneManagement.Scene previewScene) : base(singleName, pluralName, window) {
			/*
            if (selectedItem != null)
            {
                itemEditorInspector = Editor.CreateEditor(selectedItem);
            }
			*/

			this.previewScene = previewScene;
			window.autoRepaintOnSceneChange = false;
			allItemTypes = GetAllItemTypes();
			//itemEditorInspectorList = new List<Editor>();
			//Resize(itemEditorInspectorList, ItemManager.database.items.Length, null);
		}

		protected TypeFilter[] GetAllItemTypes() {
			return ReflectionUtility.GetAllTypesThatImplement(typeof(InventoryItemBase), true)
				.Select(o => new TypeFilter(o, false)).ToArray();
		}

		protected override bool MatchesSearch(InventoryItemBase item, string searchQuery) {
			searchQuery = searchQuery ?? "";

			string search = searchQuery.ToLower();
			return (item.name.ToLower().Contains(search) ||
				item.description.ToLower().Contains(search) ||
				item.ID.ToString().Contains(search) ||
				item.GetType().Name.ToLower().Contains(search));
		}

		protected override void CreateNewItem() {
			var picker = CreateNewItemEditor.Get((System.Type type, GameObject obj, EditorWindow thisWindow) => {
				//InventoryScriptableObjectUtility.SetPrefabSaveFolderIfNotSet();
				//string prefabPath = InventoryScriptableObjectUtility.GetSaveFolderForFolderName("Items") + "/item_" + System.DateTime.Now.ToFileTimeUtc() + "_PFB.prefab";

				var instanceObj = UnityEngine.Object.Instantiate<GameObject>(obj); // For unity 5.3+ - Source needs to be instance object.
				if (InventorySettingsManager.instance != null && InventorySettingsManager.instance.settings != null) {
					instanceObj.layer = InventorySettingsManager.instance.settings.itemWorldLayer;
				} else {
					Debug.LogWarning("Couldn't set item layer because there's no InventorySettingsManager in the scene");
				}

				var comp = (InventoryItemBase)instanceObj.AddComponent(type);
				comp.ID = (crudList.Count > 0) ? crudList.Max(o => o.ID) + 1 : 0;
				EditorUtility.SetDirty(comp); // To save it.

				instanceObj.GetOrAddComponent<ItemTrigger>();
				instanceObj.GetOrAddComponent<ItemTriggerInputHandler>();
				if (instanceObj.GetComponent<SpriteRenderer>() == null) {
					// This is not a 2D object
					if (instanceObj.GetComponent<Collider>() == null)
						instanceObj.AddComponent<BoxCollider>();

					var sphereCollider = instanceObj.GetOrAddComponent<SphereCollider>();
					sphereCollider.isTrigger = true;
					sphereCollider.radius = 1f;

					instanceObj.GetOrAddComponent<Rigidbody>();
				}

				/*
                //var prefab = PrefabUtility.CreatePrefab(prefabPath, instanceObj);
				var prefab = PrefabUtility.SaveAsPrefabAsset(instanceObj, prefabPath);
                UnityEngine.Object.DestroyImmediate(instanceObj);
				
                AssetDatabase.SetLabels(prefab, new string[] { "InventoryProPrefab" });
				*/
				AssetDatabase.SetLabels(instanceObj, new string[] { "InventoryProPrefab" });

				// Avoid deleting the actual prefab / model, only the cube / internal models without an asset path.
				if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj))) {
					UnityEngine.Object.DestroyImmediate(obj);
				}

				//AddItem(prefab.GetComponent<InventoryItemBase>(), true);
				InventoryScriptableObjectUtility.SetPrefabSaveFolderIfNotSet();
				var prefabPath = InventoryScriptableObjectUtility.GetSaveFolderForFolderName("Items") + Path.AltDirectorySeparatorChar + GetAssetName(comp);

				// var prefab = PrefabUtility.CreatePrefab(prefabPath, instanceObj);
				var prefab = PrefabUtility.SaveAsPrefabAsset(instanceObj, prefabPath);
				UnityEngine.Object.DestroyImmediate(instanceObj);
				AddItem(prefab.gameObject.GetComponent<InventoryItemBase>(), true);

				thisWindow.Close();
			});
			picker.Show();
		}

		public override void DuplicateItem(int index) {
			var source = crudList[index];

			var item = UnityEngine.Object.Instantiate<InventoryItemBase>(source);
			item.ID = (crudList.Count > 0) ? crudList.Max(o => o.ID) + 1 : 0;
			item.name += "(duplicate)";

			//string prefabPath = InventoryScriptableObjectUtility.GetSaveFolderForFolderName("Items") + "/item_" + System.DateTime.Now.ToFileTimeUtc() + "_PFB.prefab";

			//var prefab = PrefabUtility.CreatePrefab(prefabPath, item.gameObject);
			//prefab.layer = InventorySettingsManager.instance.settings.itemWorldLayer;

			//AssetDatabase.SetLabels(prefab, new string[] { "InventoryProPrefab" });
			item.gameObject.layer = InventorySettingsManager.instance.settings.itemWorldLayer;

			//AddItem(prefab.gameObject.GetComponent<InventoryItemBase>());

			//EditorUtility.SetDirty(prefab); // To save it.

			EditorUtility.SetDirty(item.gameObject); // To save it.

			var prefabPath = InventoryScriptableObjectUtility.GetSaveFolderForFolderName("Items") + Path.AltDirectorySeparatorChar + GetAssetName(item);
			//var prefab = PrefabUtility.CreatePrefab(prefabPath, item.gameObject);
			var prefab = PrefabUtility.SaveAsPrefabAsset(item.gameObject, prefabPath);
			UnityEngine.Object.DestroyImmediate(item.gameObject, false); // Destroy the instance created
			AddItem(prefab.gameObject.GetComponent<InventoryItemBase>());

			window.Repaint();
		}

		public override void AddItem(InventoryItemBase item, bool editOnceAdded = true) {
			base.AddItem(item, editOnceAdded);
			//UpdateAssetName(item);

			EditorUtility.SetDirty(ItemManager.database); // To save it.
		}

		public override void RemoveItem(int i) {
			AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(ItemManager.database.items[i]));
			//UnityEngine.Object.DestroyImmediate(((InventoryItemBase)itemEditorInspectorList[i].target).gameObject);
			//itemEditorInspectorList.RemoveAt(i);
			base.RemoveItem(i);

			EditorUtility.SetDirty(ItemManager.database); // To save it.
		}

		public override void EditItem(InventoryItemBase item, int i) {
			base.EditItem(item, i);

			Undo.ClearUndo(_previousItem);
			Undo.RecordObject(item, "INV_PRO_item");

			itemEditorInspector = GetEditor(item, i);
			GUI.changed = false;

			selectedIndex = i;
			selectedItem = ItemManager.database.items[selectedIndex];

			_previousItem = item;
		}

		protected override void DrawSidebar() {
			GUILayout.BeginHorizontal();

			GUILayout.BeginVertical();

			int i = 0;
			foreach (var type in allItemTypes) {
				if (i % 3 == 0)
					GUILayout.BeginHorizontal();

				type.enabled = GUILayout.Toggle(type.enabled, type.type.Name.Replace("InventoryItem", ""), "OL Toggle");

				if (i % 3 == 2 || i == allItemTypes.Length - 1)
					GUILayout.EndHorizontal();

				i++;
			}
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();

			base.DrawSidebar();

		}

		protected override void DrawSidebarRow(InventoryItemBase item, int i) {
			int checkedCount = 0;
			foreach (var type in allItemTypes) {
				if (type.enabled)
					checkedCount++;
			}

			if (checkedCount > 0) {
				if (allItemTypes.FirstOrDefault(o => o.type == item.GetType() && o.enabled) == null) {
					return;
				}
			}

			BeginSidebarRow(item, i);

			DrawSidebarRowElement("#" + item.ID.ToString(), 40);
			DrawSidebarRowElement(item.name, 130);
			DrawSidebarRowElement(item.GetType().Name.Replace("InventoryItem", ""), 125);
			DrawSidebarValidation(item, i);

			sidebarRowElementOffset.x -= 20; // To compensate for visibility toggle

			//TODO check this
			//bool t = DrawSidebarRowElementButton("V", UnityEditor.EditorStyles.miniButton, 20, 18);
			bool t = DrawSidebarRowElementToggle(true, "", "AssetLabel Icon", 20);
			if (!t) // User clicked view icon
				AssetDatabase.OpenAsset(selectedItem);

			EndSidebarRow(item, i);
		}

		protected override void ClickedSidebarRowElement(InventoryItemBase item, int i) {

			base.ClickedSidebarRowElement(item, i);
		}

		protected override void DrawDetail(InventoryItemBase item, int index) {
			EditorGUIUtility.labelWidth = EditorStyles.labelWidth;

			if (InventoryScriptableObjectUtility.isPrefabsSaveFolderSet == false) {
				EditorGUILayout.HelpBox("Prefab save folder is not set.", MessageType.Error);
				if (GUILayout.Button("Set prefab save folder")) {
					InventoryScriptableObjectUtility.SetPrefabSaveFolder();
				}

				EditorGUIUtility.labelWidth = 0;
				return;
			}

			GUILayout.Label("Use the inspector if you want to add custom components.", EditorStyles.titleStyle);
			EditorGUILayout.Space();
			EditorGUILayout.Space();

			if (GUILayout.Button("Convert type")) {
				var typePicker = ScriptPickerEditor.Get(typeof(InventoryItemBase));
				typePicker.Show();
				typePicker.OnPickObject += type => {
					ConvertThisToNewType(item, type);
				};

				return;
			}

			//EditorGUI.BeginChangeCheck();
			var instance = (InventoryItemBase)itemEditorInspector.target;
			itemEditorInspector.OnInspectorGUI();

			if (_previouslySelectedGUIItemName == "ItemEditor_itemName" && GUI.GetNameOfFocusedControl() != _previouslySelectedGUIItemName) {
				//UpdateAssetName(item);
				if (itemEditorInspector.target is InventoryItemBase) {
					// TODO Remove this
				}
			}

			//if (EditorGUI.EndChangeCheck() && selectedItem != null)
			if (GUI.changed || !EditorIsSaved()) {
				selectedItem = ItemManager.database.items[index] = UpdatePrefab(instance, item);
				UnityEditor.EditorUtility.SetDirty(selectedItem);

				SetEditorIsSaved();
			}

			_previouslySelectedGUIItemName = GUI.GetNameOfFocusedControl();

			ValidateItemFromCache(item);
			EditorGUIUtility.labelWidth = 0;
		}

		private bool EditorIsSaved() {
			var editor = (InventoryItemBaseEditor)itemEditorInspector;

			return editor.saved;
		}

		private void SetEditorIsSaved() {
			var editor = (InventoryItemBaseEditor)itemEditorInspector;

			editor.saved = true;
		}

		public static string GetAssetName(InventoryItemBase item) {
			uint categoryID = item.category != null ? item.category.ID : 0;
			return $"Item_{categoryID}_" + (string.IsNullOrEmpty(item.name) ? string.Empty : item.name.ToLower().Replace(" ", "_")) + "_#" + item.ID + "_" + ItemManager.database.name + "_PFB.prefab";
		}

		private InventoryItemBase UpdatePrefab(InventoryItemBase instance, InventoryItemBase oldPrefab) {
			// Debug.Log("Update " + oldPrefab.name + "  " + oldPrefab.ID + "  to " + instance.name + " " + instance.ID);

			var assetPath = AssetDatabase.GetAssetPath(oldPrefab);
			var newName = GetAssetName(instance);
			PrefabUtility.ApplyPrefabInstance(instance.gameObject, InteractionMode.AutomatedAction);
			//AssetDatabase.SaveAssets();

			// if (assetPath.EndsWith(newName) == false) {
			// 	// Debug.Log("Do rename prefab: {assetPath} to: {newName}");
			// 	var s = AssetDatabase.RenameAsset(assetPath, newName);
			// 	Debug.Log($"Do rename prefab: {assetPath} to: {newName}    {s}");
			// }
			UpdateAssetName(instance);

			return PrefabUtility.GetCorrespondingObjectFromSource(instance);
		}

		public void ConvertThisToNewType(InventoryItemBase currentItem, Type type) {
			var comp = (InventoryItemBase)currentItem.gameObject.AddComponent(type);
			ReflectionUtility.CopySerializableValues(currentItem, comp);

			// Set in database
			for (int i = 0; i < ItemManager.database.items.Length; i++) {
				if (ItemManager.database.items[i].ID == currentItem.ID) {
					ItemManager.database.items[i] = comp;

					selectedItem = comp;
					//UnityEngine.Object.DestroyImmediate(((InventoryItemBase)itemEditorInspectorList[i].target).gameObject);
					//itemEditorInspectorList[i] = null;
					itemEditorInspector = GetEditor(selectedItem, i);

					break;
				}
			}

			//selectedItem = comp;
			//itemEditorInspector = Editor.CreateEditor(selectedItem);
			//EditorUtility.SetDirty(selectedItem);
			GUI.changed = true;

			Object.DestroyImmediate(currentItem, true); // Get rid of the old object
			window.Repaint();
		}

		protected override bool IDsOutOfSync() {
			if (updatingPrefabs) {
				return true;
			}

			uint next = 0;
			foreach (var item in crudList) {
				if (item == null || item.ID != next)
					return true;

				next++;
			}

			return false;
		}

		protected override void SyncIDs() {
			if (updatingPrefabs) {
				return;
			}
			updatingPrefabs = true;
			Debug.Log("Item ID's out of sync, force updating...");

			crudList = crudList.Where(o => o != null).Distinct().ToList();

			List<GameObject> brokenPrefabs = new List<GameObject>();
			foreach (var o in crudList) {
				if (IsMissingComponent(o.gameObject)) {
					brokenPrefabs.Add(o.gameObject);
				}
			}

			if (brokenPrefabs.Count > 0) {
				Selection.objects = brokenPrefabs.ToArray();
				Debug.LogError("Fix Broken Prefabs First");
				return;
			}

			uint id = 0;
			foreach (var item in crudList) {
				UpdatePrefab(item, id);

				id++;
			}

			selectedItem = null;
			AssetDatabase.SaveAssets();
			EditorUtility.SetDirty(ItemManager.database);
			updatingPrefabs = false;
		}

		private void UpdatePrefab(InventoryItemBase item, uint id) {
			string assetPath = AssetDatabase.GetAssetPath(item.gameObject);

			using(var editScope = new EditPrefabAssetScope(assetPath)) {
				editScope.prefabRoot.GetComponent<InventoryItemBase>().ID = id;
			}

			UpdateAssetName(item);
		}

		private void UpdateAssetName(InventoryItemBase item) {
			string assetPath = AssetDatabase.GetAssetPath(item.gameObject);

			var newName = GetAssetName(item);
			if (assetPath.EndsWith(newName) == false) {
				Debug.Log("Rename " + assetPath + " to " + newName);
				AssetDatabase.RenameAsset(assetPath, newName);

				AssetDatabase.SaveAssets();
			}
		}

		private bool IsMissingComponent(GameObject go) {
			Component[] components = go.GetComponentsInChildren<Component>(true);
			for (int i = 0; i < components.Length; i++) {
				if (components[i] == null) {
					return true;
				}
			}
			return false;
		}

		private Editor GetEditor(InventoryItemBase item, int i) {
			Editor result = null;
			if (item != null) {
				//if (itemEditorInspectorList[i] == null)
				{
					var instance = UnityEditor.PrefabUtility.InstantiatePrefab(item, previewScene);
					return Editor.CreateEditor(instance);

				}
				//result = itemEditorInspectorList[i];
			}
			return result;
		}

		private static void Resize<T>(List<T> list, int sz, T c) {
			int cur = list.Count;
			if (sz < cur)
				list.RemoveRange(sz, cur - sz);
			else if (sz > cur) {
				if (sz > list.Capacity) //this bit is purely an optimisation, to avoid multiple automatic capacity changes.
					list.Capacity = sz;
				list.AddRange(Enumerable.Repeat(c, sz - cur));
			}
		}

	}

	public class EditPrefabAssetScope : IDisposable {

		public readonly string assetPath;
		public readonly GameObject prefabRoot;

		public EditPrefabAssetScope(string assetPath) {
			this.assetPath = assetPath;
			prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
		}

		public void Dispose() {
			PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
			PrefabUtility.UnloadPrefabContents(prefabRoot);
		}
	}

}