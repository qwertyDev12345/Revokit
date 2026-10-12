using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cones
{
    public sealed partial class ConesApp
    {
        public string LastExportPath { get; private set; }
        private RectTransform exportPanel;
        private bool exporting;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void ConesShareImage(string path);
#endif

        public void ShowExport()
        {
            BeginPage("Export image", ShowTools);
            exportPanel = Panel("Export artwork", page, Color.white);
            exportPanel.anchorMin = Vector2.zero; exportPanel.anchorMax = Vector2.one;
            exportPanel.offsetMin = new Vector2(0, 228); exportPanel.offsetMax = new Vector2(0, -110);
            var title = Label(drillTitle, exportPanel, 30, TextAnchor.MiddleLeft);
            Top(title.rectTransform, 0, 74);
            var dimensions = Label($"{Plan.width:0.##} × {Plan.length:0.##} m · {Plan.cones.Count} cones", exportPanel, 23, TextAnchor.MiddleLeft);
            Top(dimensions.rectTransform, 80, 38);
            PageBoard(Plan, 130, 100, exportPanel);
            var distance = Label($"Route: {Plan.Distance:0.0} m · {Plan.rounds} round(s)\nGrid: 1 m · Revokit", exportPanel, 21, TextAnchor.MiddleLeft);
            Bottom(distance.rectTransform, 8, 82);
            var save = PageButton("Save PNG", 0, () => ExportCurrentImage(false));
            Bottom((RectTransform)save.transform, 120, 86);
#if UNITY_IOS && !UNITY_EDITOR
            var share = PageButton("Save and share", 0, () => ExportCurrentImage(true));
            Bottom((RectTransform)share.transform, 20, 86);
#else
            var note = Label("PNG files are saved locally. Sharing is available on iPhone.", page, 20, TextAnchor.MiddleLeft);
            Bottom(note.rectTransform, 18, 86);
#endif
        }

        public void ExportCurrentImage(bool share)
        {
            if (exporting || exportPanel == null) return;
            exporting = true;
            StartCoroutine(CaptureExport(exportPanel, share));
        }

        private IEnumerator CaptureExport(RectTransform panel, bool share)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Texture2D screen = null, cropped = null;
            string failure = null;
            try
            {
                if (panel == null || !panel.gameObject.activeInHierarchy) yield break;
                var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                Vector2 bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 top = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                int x = Mathf.Clamp(Mathf.FloorToInt(bottom.x), 0, Screen.width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(bottom.y), 0, Screen.height - 1);
                int width = Mathf.Min(Screen.width - x, Mathf.RoundToInt(top.x - bottom.x));
                int height = Mathf.Min(Screen.height - y, Mathf.RoundToInt(top.y - bottom.y));
                if (width <= 0 || height <= 0) throw new InvalidOperationException("Export area is unavailable.");
                screen = ScreenCapture.CaptureScreenshotAsTexture();
                cropped = new Texture2D(width, height, TextureFormat.RGB24, false);
                cropped.SetPixels(screen.GetPixels(x, y, width, height)); cropped.Apply();
                string folder = WorkspacePath("Exports"); Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "cones-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".png");
                File.WriteAllBytes(path, cropped.EncodeToPNG());
                LastExportPath = path;
            }
            catch (Exception e) { failure = "The image could not be saved. Check available storage and try again."; Debug.LogWarning(e.Message); }
            finally
            {
                exporting = false;
                if (screen != null) Destroy(screen);
                if (cropped != null) Destroy(cropped);
            }
            if (failure != null) { ShowMessage("Export failed", failure, ShowExport); yield break; }
            BeginPage("Image saved", ShowExport);
            PageText("Your PNG includes the field dimensions, cone numbers, labels, and route distances.", 150, 140, 25);
            PageText(Path.GetFileName(LastExportPath), 328, 90, 22);
#if UNITY_IOS && !UNITY_EDITOR
            PageButton("Share image", 478, () => ConesShareImage(LastExportPath));
            if (share) ConesShareImage(LastExportPath);
#else
            PageButton("Open export folder", 478, () => Application.OpenURL(new Uri(Path.GetDirectoryName(LastExportPath)).AbsoluteUri));
#endif
            PageButton("Back to editor", 594, ShowEditor);
        }
    }
}
