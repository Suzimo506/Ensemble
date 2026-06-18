using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Displays
{
    public abstract class LobbyDisplayBase
    {
        private readonly Dictionary<string, Text> _entries = new Dictionary<string, Text>();
        private readonly List<string> _entryOrder = new List<string>();

        protected GameObject Frame { get; private set; }
        protected abstract string FrameParentPath { get; }
        protected abstract Vector2 AnchorPosition { get; }
        protected abstract Vector2 Pivot { get; }
        protected abstract TextAnchor TextAnchor { get; }
        protected abstract int FontSize { get; }
        protected abstract float EntryWidth { get; }
        protected virtual float EntryHeight => FontSize + 8f;
        protected virtual int Direction => -1;

        public bool IsCreated => Frame != null;

        public void Create()
        {
            if (Frame != null) return;

            var parentObj = GameObject.Find(FrameParentPath);
            var parent = parentObj == null ? null : parentObj.transform;
            if (parent == null) return;

            Frame = new GameObject(GetType().Name);
            var rect = Frame.AddComponent<RectTransform>();
            rect.SetParent(parent);
            rect.localScale = Vector3.one;
            rect.anchorMin = Pivot;
            rect.anchorMax = Pivot;
            rect.pivot = Pivot;
            rect.anchoredPosition = AnchorPosition;
            rect.sizeDelta = new Vector2(EntryWidth, EntryHeight);
        }

        public void Destroy()
        {
            ClearEntries();
            if (Frame != null)
            {
                DestroyObject(Frame);
                Frame = null;
            }
        }

        protected Text SetEntry(string key, string value, Action clickAction = null)
        {
            if (Frame == null) return null;

            if (!_entries.TryGetValue(key, out var text) || text == null)
            {
                text = CreateEntryText(key, clickAction);
                _entries[key] = text;
                _entryOrder.Add(key);
            }

            text.text = value;
            PositionEntries();
            return text;
        }

        protected void RemoveMissingEntries(IEnumerable<string> activeKeys)
        {
            var active = new HashSet<string>(activeKeys);
            foreach (var key in _entryOrder.ToArray())
            {
                if (active.Contains(key)) continue;
                RemoveEntry(key);
            }
        }

        protected void ClearEntries()
        {
            foreach (var text in _entries.Values)
            {
                DestroyComponentObject(text);
            }

            _entries.Clear();
            _entryOrder.Clear();
        }

        protected virtual Text CreateEntryText(string key, Action clickAction)
        {
            var entry = new GameObject(key);
            var rect = entry.AddComponent<RectTransform>();
            rect.SetParent(Frame.transform);
            rect.localScale = Vector3.one;
            rect.anchorMin = Pivot;
            rect.anchorMax = Pivot;
            rect.pivot = Pivot;
            rect.sizeDelta = new Vector2(EntryWidth, EntryHeight);

            var text = entry.AddComponent<Text>();
            text.alignment = TextAnchor;
            text.fontSize = FontSize;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = clickAction != null;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.color = Color.white;

            if (clickAction != null)
            {
                var button = entry.AddComponent<Button>();
                button.onClick.AddListener((UnityAction)(() => clickAction.Invoke()));
            }

            return text;
        }

        private void RemoveEntry(string key)
        {
            if (_entries.TryGetValue(key, out var text) && text != null)
            {
                DestroyComponentObject(text);
            }

            _entries.Remove(key);
            _entryOrder.Remove(key);
            PositionEntries();
        }

        private void PositionEntries()
        {
            var y = 0f;
            foreach (var key in _entryOrder)
            {
                if (!_entries.TryGetValue(key, out var text) || text == null) continue;

                var rect = text.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(0f, y);
                y += EntryHeight * Direction;
            }
        }

        private static void DestroyObject(GameObject obj)
        {
            if (obj == null) return;
            UnityEngine.Object.Destroy(obj);
        }

        private static void DestroyComponentObject(Component component)
        {
            if (component == null) return;
            DestroyObject(component.gameObject);
        }
    }
}
