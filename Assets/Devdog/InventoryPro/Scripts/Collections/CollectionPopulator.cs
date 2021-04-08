using Devdog.General;
using UnityEngine;

namespace Devdog.InventoryPro {
    public partial class CollectionPopulator : MonoBehaviour {
        public ItemCollectionBase targetCollection;
        public ItemAmountRow[] items = new ItemAmountRow[0];

        /// <summary>
        /// This will ignore layout sizes, and force the items into the slots.
        /// </summary>
        public bool useForceSet = false;

        private InventoryItemBase[] _items = new InventoryItemBase[0];

        protected void Awake() {
            _items = InventoryItemUtility.RowsToItems(items, true);
            for (int i = 0; i < _items.Length; i++) {
                _items[i].transform.SetParent(transform);
                _items[i].gameObject.SetActive(false);
            }
        }

        protected void Start() {
            if (targetCollection == null)
                targetCollection = GetComponent<ItemCollectionBase>();
            if (targetCollection == null) {
                DevdogLogger.LogError("CollectionPopulator can only be used on a collection.", transform);
                return;
            }

            if (useForceSet) {
                for (uint i = 0; i < _items.Length; i++) {
                    targetCollection.SetItem(i, _items[i], true);
                }
            } else {
                targetCollection.AddItems(_items);
            }
        }
    }
}