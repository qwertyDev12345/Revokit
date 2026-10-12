using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cones
{
    public sealed partial class ConesApp
    {
        private Text editorTitle, coneCount;
        private Button editConeButton, coneModeButton, routeModeButton;
        private RectTransform coneActions, routeActions;
        private WorkoutRing workoutRing;
        private Text[] phaseChips;
        private Sprite heroSprite;
        private static readonly Color Surface = new Color32(248, 249, 250, 255);

        private void Primary(Button button)
        {
            button.targetGraphic.color = ConesVisuals.Orange;
            button.GetComponentInChildren<Text>().color = Color.white;
            button.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            var border = button.GetComponent<Outline>();
            if (border != null) border.enabled = false;
        }

        private void Icon(Transform parent, string kind, Color tint, float size, Vector2 anchor, Vector2 position)
        {
            var go = new GameObject(kind + " icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(ConesIcon));
            go.transform.SetParent(parent, false);
            var icon = go.GetComponent<ConesIcon>(); icon.Kind = kind; icon.color = tint; icon.raycastTarget = false;
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = Vector2.one * size; rect.anchoredPosition = position;
        }

        private void Selection(Button button, bool selected)
        {
            button.targetGraphic.color = selected ? ConesVisuals.Orange : Surface;
            button.GetComponentInChildren<Text>().color = selected ? Color.white : ink;
        }

        private RectTransform Card(string name, Transform parent)
        {
            var card = Panel(name, parent, Color.white);
            var border = card.gameObject.AddComponent<Outline>();
            border.effectColor = ConesVisuals.Border; border.effectDistance = new Vector2(1, -1);
            return card;
        }

        private Text CardText(string value, Transform parent, float top, float height, int size, bool bold = false)
        {
            var text = Label(value, parent, size, TextAnchor.MiddleLeft);
            Top(text.rectTransform, top, height);
            text.rectTransform.offsetMin += new Vector2(24, 0);
            text.rectTransform.offsetMax -= new Vector2(24, 0);
            if (bold) text.fontStyle = FontStyle.Bold;
            return text;
        }

        private void BuildEditorDesign()
        {
            var back = MakeButton("Menu", content, ShowHome);
            Top((RectTransform)back.transform, 0, 86);
            ((RectTransform)back.transform).anchorMax = new Vector2(.19f, 1);
            editorTitle = Label("Zigzag", content, 30, TextAnchor.MiddleCenter);
            editorTitle.fontStyle = FontStyle.Bold;
            editorTitle.resizeTextForBestFit = true; editorTitle.resizeTextMinSize = 18; editorTitle.resizeTextMaxSize = 30;
            Top(editorTitle.rectTransform, 0, 86);
            editorTitle.rectTransform.anchorMin = new Vector2(.21f, 1);
            editorTitle.rectTransform.anchorMax = new Vector2(.78f, 1);
            var save = MakeButton("Save", content, ShowSaveDialog);
            Top((RectTransform)save.transform, 0, 86);
            ((RectTransform)save.transform).anchorMin = new Vector2(.81f, 1);
            Primary(save);

            var chips = Row(100, 86, new[] { "Field", "Cones", "Undo", "Redo" }, new Action[]
                { ShowFieldSettings, () => ShowConeDetails(Board.Selected >= 0 ? Board.Selected : 0), UndoEdit, RedoEdit });
            dimensions = chips[0].GetComponentInChildren<Text>();
            coneCount = chips[1].GetComponentInChildren<Text>();
            undoEditButton = chips[2]; redoEditButton = chips[3];
            hint = Label("", content, 20, TextAnchor.MiddleLeft);
            hint.color = ConesVisuals.Muted;
            Top(hint.rectTransform, 194, 52);
            status = routeLabel = hint;

            var boardObject = new GameObject("Field", typeof(RectTransform), typeof(CanvasRenderer), typeof(FieldBoard));
            boardObject.transform.SetParent(content, false);
            Board = boardObject.GetComponent<FieldBoard>(); Board.Plan = Plan;
            Board.Changed = () => { ResetPlayback(); Refresh(); };
            Board.rectTransform.anchorMin = Vector2.zero; Board.rectTransform.anchorMax = Vector2.one;
            Board.rectTransform.offsetMin = new Vector2(0, 432); Board.rectTransform.offsetMax = new Vector2(0, -252);

            var summaryCard = Panel("Route summary", content, Surface);
            Bottom(summaryCard, 384, 40);
            summary = Label("", summaryCard, 21, TextAnchor.MiddleCenter); Stretch(summary.rectTransform);
            var modes = BottomRow(294, 78, new[] { "Cones", "Route" }, new Action[]
            { () => SetEditorMode(false), () => SetEditorMode(true) });
            coneModeButton = modes[0]; routeModeButton = modes[1];
            var coneButtons = BottomRow(196, 86, new[] { "Add", "Edit", "Delete" }, new Action[]
                { AddCone, () => ShowConeDetails(Board.Selected), RemoveCone });
            coneActions = (RectTransform)coneButtons[0].transform.parent;
            addButton = coneButtons[0]; editConeButton = coneButtons[1]; removeButton = coneButtons[2];
            var routeButtons = BottomRow(196, 86, new[] { "Play", "Restart", "Remove last" }, new Action[]
                { TogglePlayback, ResetAndRefresh, UndoRoute });
            routeActions = (RectTransform)routeButtons[0].transform.parent;
            playButton = routeButtons[0]; playLabel = playButton.GetComponentInChildren<Text>(); undoButton = routeButtons[2];
            var setup = MakeButton("Set up drill", content, ShowSetup);
            Bottom((RectTransform)setup.transform, 98, 86); Primary(setup);
            var more = MakeButton("More tools", content, ShowTools);
            Bottom((RectTransform)more.transform, 0, 86);
            more.GetComponentInChildren<Text>().color = ConesVisuals.Orange;
        }

        private void SetEditorMode(bool route)
        { ResetPlayback(); Board.RouteMode = route; Refresh(); }

        public void ShowFieldSettings()
        {
            BeginPage("Field size", ShowEditor);
            PageText("Resize the field. Cone positions scale with it.", 110, 80, 24);
            var width = PageInput("Width (m)", Plan.width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), 224, 5);
            var length = PageInput("Length (m)", Plan.length.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), 388, 5);
            var error = PageText("Dimensions: 2–40 m", 562, 94, 22);
            var apply = PageButton("Apply size", 706, () =>
            {
                if (!TryNumber(width.text, out float w) || !TryNumber(length.text, out float h) || w < 2 || w > 40 || h < 2 || h > 40)
                { error.text = "Enter dimensions between 2 and 40 m."; return; }
                ResetPlayback(); Plan.Resize(w, h); ShowEditor();
            });
            Primary(apply);
        }

        private void BuildHomeDesign()
        {
            BeginPage("Revokit", null);
            var heading = page.GetComponentInChildren<Text>();
            heading.fontSize = 48; heading.fontStyle = FontStyle.Bold;
            heading.color = ink;
            heading.rectTransform.offsetMin += new Vector2(90, 0);
            Icon(page, "Cone", ConesVisuals.Orange, 70, new Vector2(0, 1), new Vector2(36, -44));
            PageText("Plan. Set up. Train.", 78, 42, 24).color = ConesVisuals.Muted;
            var hero = Card("New drill card", page);
            hero.anchorMin = new Vector2(0, .50f); hero.anchorMax = Vector2.one;
            hero.offsetMin = Vector2.zero; hero.offsetMax = new Vector2(0, -144);
            CardText("Build your next drill", hero, 14, 48, 30, true);
            CardText("Your field. Your rules.", hero, 64, 36, 22).color = ConesVisuals.Muted;
            var artwork = new GameObject("Generated cone artwork", typeof(RectTransform), typeof(Image));
            artwork.transform.SetParent(hero, false);
            var art = artwork.GetComponent<Image>();
            if (heroSprite == null)
            {
                var texture = Resources.Load<Texture2D>("ConesHero");
                if (texture != null) heroSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
            }
            art.sprite = heroSprite; art.preserveAspect = true; art.raycastTarget = false;
            var ar = (RectTransform)artwork.transform;
            ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one;
            ar.offsetMin = new Vector2(28, 108); ar.offsetMax = new Vector2(-28, -104);
            // Fit to a separate bounding box, so aspect fitting cannot cover the heading or button.
            var artBox = new GameObject("Artwork bounds", typeof(RectTransform)).GetComponent<RectTransform>();
            artBox.SetParent(hero, false); Stretch(artBox);
            artBox.offsetMin = new Vector2(28, 108); artBox.offsetMax = new Vector2(-28, -104);
            ar.SetParent(artBox, false); Stretch(ar);
            var create = MakeButton("Create a new drill", hero,
                () => ConfirmReplace(() => UsePlan(new DrillPlan(), "Untitled drill", null), ShowHome));
            create.GetComponentInChildren<Text>().text = "+ New drill";
            Bottom((RectTransform)create.transform, 18, 86);
            ((RectTransform)create.transform).offsetMin += new Vector2(20, 0);
            ((RectTransform)create.transform).offsetMax -= new Vector2(20, 0); Primary(create);

            var current = MakeButton("Continue editing", page, ShowEditor);
            var cr = (RectTransform)current.transform;
            cr.anchorMin = new Vector2(0, .32f); cr.anchorMax = new Vector2(1, .50f);
            cr.offsetMin = new Vector2(0, 16); cr.offsetMax = new Vector2(0, -20);
            current.GetComponentInChildren<Text>().text = "";
            CardText("Continue editing", cr, 12, 34, 24, true);
            var preview = PageBoard(Plan, 54, 14, cr); preview.raycastTarget = false; preview.Compact = true;
            preview.rectTransform.anchorMax = new Vector2(.29f, 1); preview.Refresh();
            var title = CardText(drillTitle + $"\n{Plan.cones.Count} cones · {Plan.width:0.##} × {Plan.length:0.##} m", cr, 54, 82, 23);
            title.rectTransform.anchorMin = new Vector2(.3f, 1);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 16; title.resizeTextMaxSize = 23;

            var quickTitle = PageText("Quick access", 0, 40, 26); quickTitle.fontStyle = FontStyle.Bold;
            quickTitle.rectTransform.anchorMin = quickTitle.rectTransform.anchorMax = new Vector2(0, .32f);
            quickTitle.rectTransform.pivot = new Vector2(0, 1); quickTitle.rectTransform.sizeDelta = new Vector2(600, 40);
            quickTitle.rectTransform.anchoredPosition = new Vector2(0, -6);
            var quick = PageRow(0, new[] { "My drills", "Session plan" }, new Action[] { () => ShowLibrary(), () => ShowSession() });
            var qr = (RectTransform)quick[0].transform.parent;
            qr.anchorMin = new Vector2(0, .14f); qr.anchorMax = new Vector2(1, .32f);
            qr.offsetMin = new Vector2(0, 40); qr.offsetMax = new Vector2(0, -60);
            for (int i = 0; i < quick.Length; i++)
            {
                var label = quick[i].GetComponentInChildren<Text>(); label.text = i == 0 ? "My drills" : "Session plan";
                Bottom(label.rectTransform, 8, 46); label.fontStyle = FontStyle.Bold;
                Icon(quick[i].transform, i == 0 ? "Folder" : "Session", ink, 36, new Vector2(.5f, 1), new Vector2(0, -26));
            }
            var templates = PageButton("Ready-made drills", 0, ShowTemplates, 86);
            Bottom((RectTransform)templates.transform, 98, 86);
            templates.GetComponentInChildren<Text>().color = ConesVisuals.Orange;
            var nav = PageRow(0, new[] { "Home", "Drills", "Session" }, new Action[] { ShowHome, () => ShowLibrary(), () => ShowSession() });
            Bottom((RectTransform)nav[0].transform.parent, 0, 86); Selection(nav[0], true);
            for (int i = 0; i < nav.Length; i++)
            {
                var label = nav[i].GetComponentInChildren<Text>(); Bottom(label.rectTransform, 2, 32); label.fontSize = 21;
                Icon(nav[i].transform, i == 0 ? "Home" : i == 1 ? "Cone" : "Session", i == 0 ? Color.white : ink,
                    36, new Vector2(.5f, 1), new Vector2(0, -26));
            }
            if (!string.IsNullOrEmpty(recoveryNotice))
            {
                // Recovery remains available without placing a notice over navigation.
                var recovery = CardText("Draft recovered", cr, 0, 30, 17);
                recovery.alignment = TextAnchor.MiddleRight; recovery.color = ConesVisuals.Muted;
                recovery.rectTransform.anchorMin = new Vector2(.5f, 1);
            }
        }
    }
}
