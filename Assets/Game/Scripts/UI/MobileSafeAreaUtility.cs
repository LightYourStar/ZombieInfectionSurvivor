using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 轻量移动端安全区工具。用于运行时创建的 UI 根节点，避免贴边和刘海屏遮挡。
    /// </summary>
    public static class MobileSafeAreaUtility
    {
        public static void ApplySafeArea(RectTransform rectTransform, float minPadding = 24f)
        {
            if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            safeArea.xMin = Mathf.Max(safeArea.xMin, minPadding);
            safeArea.yMin = Mathf.Max(safeArea.yMin, minPadding);
            safeArea.xMax = Mathf.Min(safeArea.xMax, Screen.width - minPadding);
            safeArea.yMax = Mathf.Min(safeArea.yMax, Screen.height - minPadding);

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
