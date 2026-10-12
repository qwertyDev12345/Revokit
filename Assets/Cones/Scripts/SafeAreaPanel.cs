using UnityEngine;

namespace Cones
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private Rect lastArea;
        private Vector2Int lastSize;
        // Also used by the desktop smoke check; never enabled in an iOS build.
        [System.NonSerialized] public bool simulatePhoneInsets;

        private void OnEnable() => Apply();
        private void Update() => Apply();

        public void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (simulatePhoneInsets)
                area = new Rect(0, Screen.height * 0.037f, Screen.width, Screen.height * 0.9f);
#endif
            var size = new Vector2Int(Screen.width, Screen.height);
            if (area == lastArea && size == lastSize) return;
            lastArea = area;
            lastSize = size;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
