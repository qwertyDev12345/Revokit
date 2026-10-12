using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Cones
{
    public sealed partial class ConesApp
    {
        public void ShowVariations()
        {
            BeginPage("Route variations", ShowTools);
            PageText("Make a variation of the current drill. All changes support Undo.", 112, 96, 25);
            PageButton("Mirror left / right", 240, () => ApplyVariation(0));
            PageButton("Mirror top / bottom", 350, () => ApplyVariation(1));
            PageButton("Reverse route", 460, () => ApplyVariation(2));
            var start = PageInput("Start at route step", "1", 594, 3);
            var error = PageText($"Choose 1–{Plan.route.Count}. Rotating an open route changes its connections.", 748, 94, 22);
            PageButton("Change starting step", 884, () =>
            {
                if (!int.TryParse(start.text, out int n) || n < 1 || n > Plan.route.Count) { error.text = "Choose an existing route step."; return; }
                ResetPlayback(); Plan.StartAt(n - 1); ShowEditor();
            }).interactable = Plan.route.Count > 1;
        }
        public void ApplyVariation(int kind)
        {
            ResetPlayback();
            if (kind == 2) Plan.Reverse(); else Plan.Mirror(kind == 0);
            ShowEditor();
        }
        public void ShowMovements(int pageNumber = 0)
        {
            Plan.ReconcileLegs();
            int pages = Mathf.Max(1, (Plan.legs.Count + 3) / 4);
            pageNumber = Mathf.Clamp(pageNumber, 0, pages - 1);
            BeginPage("Movement types", ShowTools);
            PageText($"Assign movement to each route segment.\nPage {pageNumber + 1}/{pages}", 110, 90, 23);
            if (Plan.legs.Count == 0) PageText("Add route steps in the editor first.", 250, 90, 26);
            for (int i = 0; i < 4 && pageNumber * 4 + i < Plan.legs.Count; i++)
            {
                int step = pageNumber * 4 + i, current = pageNumber;
                var leg = Plan.legs[step];
                PageText($"{step + 1}. Cone {leg.from + 1} → {leg.to + 1}", 220 + i * 164, 40, 25);
                PageButton(DrillPlan.MovementName(leg.movement) + "  ›", 272 + i * 164, () =>
                { SetMovement(step, (MovementKind)(((int)Plan.legs[step].movement + 1) % 4)); ShowMovements(current); });
            }
            int selected = pageNumber;
            var pager = PageRow(906, new[] { "Previous", "Next" }, new Action[] { () => ShowMovements(selected - 1), () => ShowMovements(selected + 1) });
            pager[0].interactable = selected > 0; pager[1].interactable = selected + 1 < pages;
            PageText("Tap a movement to cycle: Run, Side step, Backpedal, Dribble.", 1008, 58, 19);
        }
        public void SetMovement(int segment, MovementKind movement)
        {
            Plan.ReconcileLegs();
            if (segment < 0 || segment >= Plan.legs.Count || (int)movement < 0 || (int)movement > 3) return;
            Plan.legs[segment].movement = movement; Refresh();
        }
        public void ShowObstacles(int pageNumber = 0)
        {
            if (Plan.obstacles == null) Plan.obstacles = new List<FieldObstacle>();
            int pages = Mathf.Max(1, (Plan.obstacles.Count + 2) / 3);
            pageNumber = Mathf.Clamp(pageNumber, 0, pages - 1);
            BeginPage("Obstacles", ShowTools);
            PageText($"{Plan.obstacles.Count}/20 zones · {Plan.ObstacleWarnings} conflict(s)\nWalls, goals and no-go areas are marked on the field.", 110, 100, 23);
            PageButton("Add obstacle", 232, () => ShowObstacleEditor(-1)).interactable = Plan.obstacles.Count < 20;
            if (Plan.obstacles.Count == 0) PageText("No obstacles yet. Add a rectangle using metre coordinates.", 388, 120, 25);
            for (int i = 0; i < 3 && pageNumber * 3 + i < Plan.obstacles.Count; i++)
            {
                int index = pageNumber * 3 + i;
                var o = Plan.obstacles[index];
                PageButton($"{o.name}\n{o.width:0.##} × {o.height:0.##} m at {o.x:0.##}, {o.y:0.##}", 354 + i * 160, () => ShowObstacleEditor(index), 136);
            }
            int selected = pageNumber;
            var pager = PageRow(874, new[] { "Previous", "Next" }, new Action[] { () => ShowObstacles(selected - 1), () => ShowObstacles(selected + 1) });
            pager[0].interactable = selected > 0; pager[1].interactable = selected + 1 < pages;
            PageText("Red segments cross a marked zone. Move cones or adjust the route to resolve conflicts.", 982, 84, 20);
        }
        public void ShowObstacleEditor(int index)
        {
            var original = index >= 0 ? Plan.obstacles[index] : new FieldObstacle();
            var draft = LocalWorkspace.Clone(original);
            BeginPage(index >= 0 ? "Edit obstacle" : "Add obstacle", () => ShowObstacles());
            var kind = PageButton("Type: " + ObstacleName(draft.kind), 110, () => { });
            kind.onClick.RemoveAllListeners(); kind.onClick.AddListener(() =>
            { draft.kind = (ObstacleKind)(((int)draft.kind + 1) % 3); kind.GetComponentInChildren<UnityEngine.UI.Text>().text = "Type: " + ObstacleName(draft.kind); });
            var x = PageInput("X from left (m)", draft.x.ToString("0.##", CultureInfo.InvariantCulture), 218, 7);
            var y = PageInput("Y from bottom (m)", draft.y.ToString("0.##", CultureInfo.InvariantCulture), 356, 7);
            var width = PageInput("Width (m)", draft.width.ToString("0.##", CultureInfo.InvariantCulture), 494, 7);
            var height = PageInput("Height (m)", draft.height.ToString("0.##", CultureInfo.InvariantCulture), 632, 7);
            var error = PageText("Minimum size: 0.1 m. Keep the zone inside the field.", 780, 74, 22);
            PageButton("Apply obstacle", 868, () =>
            {
                if (!TryNumber(x.text, out draft.x) || !TryNumber(y.text, out draft.y) || !TryNumber(width.text, out draft.width) || !TryNumber(height.text, out draft.height))
                { error.text = "Enter valid metre coordinates."; return; }
                draft.name = ObstacleName(draft.kind) + " " + (index >= 0 ? index + 1 : (Plan.obstacles?.Count ?? 0) + 1);
                if (!ApplyObstacle(index, draft)) { error.text = "The zone must fit inside the field and be at least 0.1 m wide and high."; return; }
                ShowObstacles();
            });
            if (index >= 0) PageButton("Delete obstacle", 976, () => ShowConfirm("Delete obstacle?", "Remove this marked zone? You can undo the change in the editor.",
                () => { Plan.obstacles.RemoveAt(index); Refresh(); ShowObstacles(); }, () => ShowObstacleEditor(index)), 86);
        }
        public bool ApplyObstacle(int index, FieldObstacle obstacle)
        {
            if (obstacle == null || !obstacle.IsValid(Plan.width, Plan.length)) return false;
            if (Plan.obstacles == null) Plan.obstacles = new List<FieldObstacle>();
            if (index >= Plan.obstacles.Count || index < -1 || (index == -1 && Plan.obstacles.Count >= 20)) return false;
            ResetPlayback();
            if (index == -1) Plan.obstacles.Add(LocalWorkspace.Clone(obstacle)); else Plan.obstacles[index] = LocalWorkspace.Clone(obstacle);
            Refresh(); return true;
        }
        private static string ObstacleName(ObstacleKind kind) => kind == ObstacleKind.NoGo ? "No-go area" : kind.ToString();
    }
}
