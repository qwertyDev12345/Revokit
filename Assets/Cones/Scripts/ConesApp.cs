using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cones
{
    public sealed partial class ConesApp : MonoBehaviour
    {
        public DrillPlan Plan { get; private set; }
        public FieldBoard Board { get; private set; }
        public SafeAreaPanel SafePanel { get; private set; }
        private Font font;
        private Text dimensions, summary, hint, status, playLabel, routeLabel;
        private Button removeButton, playButton, addButton, undoButton;
        private RectTransform content;
        private float travelled;
        private bool paused;
        private string savePath;
        private readonly Color ink = new Color32(23, 28, 34, 255);
        private readonly Color buttonColor = new Color32(248, 249, 250, 255);

        private void Awake()
        {
            gameObject.name = "Cones App";
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            savePath = Path.Combine(Application.persistentDataPath, "cones-plan.json");
            Plan = DrillPlan.Example();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUI();
            Refresh();
            savedSnapshot = JsonUtility.ToJson(Plan);
            InitializeWorkspace();
            ShowHome();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-conesSmokeTest") >= 0)
                gameObject.AddComponent<ConesSmokeCheck>();
#endif
        }

        private void BuildUI()
        {
            var canvasObject = new GameObject("Cones Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Panel("Background", canvasObject.transform, new Color32(255, 255, 255, 255));
            Stretch(background);
            var safe = new GameObject("Safe Area", typeof(RectTransform), typeof(SafeAreaPanel));
            safe.transform.SetParent(canvasObject.transform, false);
            SafePanel = safe.GetComponent<SafeAreaPanel>();
            SafePanel.Apply();
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(safe.transform, false);
            content.anchorMin = new Vector2(0.5f, 0);
            content.anchorMax = new Vector2(0.5f, 1);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(672, -24);
            content.anchoredPosition = Vector2.zero;

            BuildEditorDesign();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
        }

        private void Update()
        {
            // Fit content to the safe area's actual width, including side insets.
            var parent = (RectTransform)content.parent;
            content.sizeDelta = new Vector2(Mathf.Min(672, Mathf.Max(1, parent.rect.width - 32)), -24);
            if (page != null) page.sizeDelta = content.sizeDelta;
            UpdateFeatures();
            if (!Board.Playing || paused) return;
            travelled += Time.deltaTime * Plan.speed;
            float total = Plan.Distance * Plan.rounds;
            Board.Runner = Plan.PositionAt(travelled >= total ? Plan.Distance : travelled % Plan.Distance);
            Board.SetVerticesDirty();
            routeLabel.text = $"Round {Mathf.Min(Plan.rounds, (int)(travelled / Plan.Distance) + 1)}/{Plan.rounds} · {Plan.speed:0.0} m/s";
            if (travelled >= total)
            {
                paused = true;
                playLabel.text = "Replay";
                Say($"Completed {Plan.rounds} round(s). Total distance: {total:0.0} m.");
            }
        }

        private void Resize(int width, int length)
        {
            ResetPlayback();
            Plan.Resize(Plan.width + width, Plan.length + length);
            Refresh();
            Say("Field resized. Cone positions were scaled proportionally.");
        }

        public void AddCone()
        {
            if (Plan.cones.Count >= 24) { Say("A drill can contain up to 24 cones."); return; }
            ResetPlayback();
            // Pick the most open point on a half-metre grid, avoiding stacked new cones.
            Vector2 candidate = new Vector2(Plan.width / 2, Plan.length / 2);
            float best = -1;
            for (float x = 0.5f; x < Plan.width; x += 0.5f)
                for (float y = 0.5f; y < Plan.length; y += 0.5f)
                {
                    var point = new Vector2(x, y);
                    float nearest = 10000;
                    foreach (Vector2 cone in Plan.cones) nearest = Mathf.Min(nearest, (point - cone).sqrMagnitude);
                    if (nearest > best) { best = nearest; candidate = point; }
                }
            Plan.cones.Add(candidate);
            Board.Selected = Plan.cones.Count - 1;
            Board.RouteMode = false;
            Refresh();
            Say("Cone added. Drag it to a new position.");
        }

        public void RemoveCone()
        {
            if (Board.Selected < 0) return;
            ResetPlayback();
            Plan.RemoveCone(Board.Selected);
            Board.Selected = -1;
            Refresh();
            Say("Cone and its route steps removed.");
        }

        public void ToggleMode()
        {
            ResetPlayback();
            Board.RouteMode = !Board.RouteMode;
            Refresh();
        }

        public void ClearRoute()
        {
            ResetPlayback();
            Plan.route.Clear();
            Board.RouteMode = true;
            Refresh();
            Say("Tap cones in order. You can revisit an earlier cone.");
        }

        public void UndoRoute()
        {
            ResetPlayback();
            if (Plan.route.Count > 0) Plan.route.RemoveAt(Plan.route.Count - 1);
            Refresh();
        }

        public void TogglePlayback()
        {
            if (Plan.Distance < 0.01f) { Say("Add at least two different positions to preview a route."); return; }
            if (travelled >= Plan.Distance * Plan.rounds) { travelled = 0; Board.Playing = false; }
            if (!Board.Playing)
            {
                Board.Playing = true; paused = false;
                Board.Runner = Plan.PositionAt(travelled);
            }
            else paused = !paused;
            Refresh();
            Say(paused ? "Preview paused." : $"Preview: {Plan.speed:0.0} m/s · {Plan.rounds} round(s).");
        }

        private void ResetPlayback() { Board.Playing = false; paused = false; travelled = 0; }
        private void ResetAndRefresh() { ResetPlayback(); Refresh(); Say("Preview reset to the start."); }

        public void SavePlan()
        {
            try
            {
                var entry = Library.Save(activeId, drillTitle, Plan);
                activeId = entry.id;
                savedSnapshot = JsonUtility.ToJson(Plan);
                MarkDraftDirty();
                ShowEditor();
                Say("Drill saved to My drills on this device.");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { ShowMessage("Could not save", "Check the drill name and available storage.", ShowSaveDialog); }
        }

        public void LoadPlan()
        {
            if (!File.Exists(savePath)) { ShowMessage("No previous save", "There is no old single-slot save on this device.", () => ShowLibrary()); return; }
            try
            {
                if (new FileInfo(savePath).Length > 131072) throw new InvalidDataException("Plan file is too large.");
                var loaded = JsonUtility.FromJson<DrillPlan>(File.ReadAllText(savePath));
                if (loaded == null || !loaded.IsValid()) throw new InvalidDataException("Invalid plan.");
                ResetPlayback();
                UsePlan(loaded, "Imported drill", null);
                Say("Old single-slot save imported. Save it to My drills.");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { ShowMessage("Import failed", "The old save could not be read. Your current drill was kept.", () => ShowLibrary()); }
        }

        private void OnApplicationPause(bool value)
        {
            if (value && Board != null && Board.Playing) { paused = true; Refresh(); }
            if (value) { PauseTraining(false); PauseAdvanced(false); SaveDraftNow(); }
        }

        private void OnApplicationQuit() => SaveDraftNow();

        public void Refresh()
        {
            Plan.ReconcileLegs();
            TrackEdit();
            dimensions.text = $"{Plan.width:0.##} × {Plan.length:0.##} m";
            coneCount.text = $"{Plan.cones.Count} cones";
            editorTitle.text = drillTitle;
            summary.text = $"{Plan.Distance:0.0} m route · {Plan.rounds} round(s)" + (Plan.ObstacleWarnings > 0 ? $" · {Plan.ObstacleWarnings} conflicts" : "");
            Selection(coneModeButton, !Board.RouteMode); Selection(routeModeButton, Board.RouteMode);
            coneActions.gameObject.SetActive(!Board.RouteMode);
            routeActions.gameObject.SetActive(Board.RouteMode);
            editConeButton.interactable = Board.Selected >= 0;
            hint.text = Board.RouteMode ? "Tap cones in movement order." : "Tap to select a cone. Drag to move it.";
            removeButton.interactable = Board.Selected >= 0;
            addButton.interactable = Plan.cones.Count < 24;
            playButton.interactable = Plan.Distance > 0.01f;
            undoButton.interactable = Plan.route.Count > 0;
            playLabel.text = Board.Playing ? (paused ? "Resume" : "Pause") : "Play";
            Board.Refresh();
        }

        private void Say(string message) => status.text = message;

        private RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var panelImage = go.GetComponent<Image>();
            panelImage.color = color;
            if (name != "Background") { panelImage.sprite = ConesVisuals.Rounded; panelImage.type = Image.Type.Sliced; }
            go.GetComponent<Image>().raycastTarget = false;
            return (RectTransform)go.transform;
        }

        private Text Label(string value, Transform parent, int size, TextAnchor alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.fontSize = size; text.text = value; text.color = ink;
            text.supportRichText = false;
            text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Button MakeButton(string caption, Transform parent, Action action)
        {
            var rect = Panel(caption, parent, buttonColor);
            var image = rect.GetComponent<Image>(); image.raycastTarget = true;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = ConesVisuals.Border; outline.effectDistance = new Vector2(1, -1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.pressedColor = new Color(.85f, .87f, .89f);
            colors.disabledColor = new Color(.85f, .86f, .88f, .6f); colors.fadeDuration = .1f; button.colors = colors;
            button.onClick.AddListener(() => action());
            var label = Label(caption, rect, 28, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(5, 3); label.rectTransform.offsetMax = new Vector2(-5, -3);
            return button;
        }

        private Button[] Row(float top, float height, string[] labels, Action[] actions) => MakeRow(top, height, labels, actions, false);
        private Button[] BottomRow(float bottom, float height, string[] labels, Action[] actions) => MakeRow(bottom, height, labels, actions, true);

        private Button[] MakeRow(float position, float height, string[] labels, Action[] actions, bool bottom)
        {
            var row = new GameObject("Button row", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            if (bottom) Bottom(row, position, height); else Top(row, position, height);
            var result = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                result[i] = MakeButton(labels[i], row, actions[i]);
                var rect = (RectTransform)result[i].transform;
                rect.anchorMin = new Vector2((float)i / labels.Length, 0);
                rect.anchorMax = new Vector2((float)(i + 1) / labels.Length, 1);
                rect.offsetMin = new Vector2(i == 0 ? 0 : 5, 0);
                rect.offsetMax = new Vector2(i == labels.Length - 1 ? 0 : -5, 0);
            }
            return result;
        }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private static void Top(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(0, -top - height); rect.offsetMax = new Vector2(0, -top);
        }

        private static void Bottom(RectTransform rect, float bottom, float height)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(0.5f, 0);
            rect.offsetMin = new Vector2(0, bottom); rect.offsetMax = new Vector2(0, bottom + height);
        }
    }
}
