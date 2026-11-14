using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// All editors below live inside the game's existing chooseUI. Button clicks
// are polled on the Unity thread, without IL2CPP managed UnityAction delegates.
internal static class NativeKungfuFields
{
    private sealed record Click(RectTransform Rect, Action Action, Action? Right = null);
    private static readonly List<Click> Clicks = new();
    private static int _openedFrame;
    private static Text? _visualStatus;
    private static IntPtr _draft;
    private static float? _mana;
    private static int _effectPage;
    private static int _ratioPage;
    private static int _slot;
    private static bool _customLayout;
    private static GameObject? _fieldsRoot;
    private static Transform Fields => _fieldsRoot!.transform;
    private static readonly List<Click> MainClicks = new();
    private static readonly List<GameObject> MainButtons = new();
    private static readonly List<GameObject> EffectPager = new();
    private static readonly List<GameObject> RatioPager = new();
    private static readonly List<GameObject> RatioToolbar = new();
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    internal static void Install(Harmony h)
    {
        void Patch(Type type, string name, string? pre = null, string? post = null) => h.Patch(
            AccessTools.Method(type, name) ?? throw new MissingMethodException(type.Name, name),
            pre == null ? null : new HarmonyMethod(typeof(NativeKungfuFields), pre),
            post == null ? null : new HarmonyMethod(typeof(NativeKungfuFields), post));
        Patch(typeof(CustomSkillDamageRatioController), "RefreshAttriRatioUI", nameof(SafeRatioLabel));
        Patch(typeof(GameDataController), "Update", post: nameof(Tick));
        Patch(typeof(UIGrid), "Reposition", nameof(AllowLayout));
        Patch(typeof(CustomSkillCreateController), "ShowCreatePanel", nameof(Reset));
        Patch(typeof(CustomSkillCreateController), "ChangeBaseDamageButtonClicked", post: nameof(ClearManaOverride));
        Patch(typeof(CustomSkillCreateController), "ChooseDamageRangeButtonClicked", nameof(Range));
        Patch(typeof(CustomSkillCreateController), "ChoosePostureButtonClicked", nameof(Posture));
        Patch(typeof(CustomSkillCreateController), "RandomSkillIcon", nameof(Icon));
        Patch(typeof(CustomSkillCreateController), "RefreshManaCost", post: nameof(Mana));
        Patch(typeof(CustomSkillCreateController), "RefreshSpeAddDataGrid", post: nameof(PageEffects));
        Patch(typeof(CustomSkillCreateController), "RefreshChooseSpeAddDataUI", nameof(RestoreLayout));
    }

    private static CustomSkillCreateController Owner => CustomSkillCreateController.Instance;
    private static bool Active => NativeKungfuEditor.Owns(Owner);
    private static void ClearManaOverride() { if (Active) { _mana = null; Owner.RefreshManaCost(); } }
    private static bool AllowLayout(UIGrid __instance) => !(_customLayout && Active && __instance.gameObject.Pointer == Owner.chooseGrid.gameObject.Pointer);
    private static void Reset() { if (_fieldsRoot != null) Object.Destroy(_fieldsRoot); _fieldsRoot = null; _customLayout = false; if (Owner != null) Owner.chooseGrid.SetActive(true); foreach (var go in MainButtons) if (go != null) Object.Destroy(go); MainButtons.Clear(); EffectPager.Clear(); RatioPager.Clear(); RatioToolbar.Clear(); MainClicks.Clear(); Clicks.Clear(); _draft = IntPtr.Zero; _mana = null; _effectPage = 0; _ratioPage = 0; }
    private static void Begin(string title = "自定义设置")
    {
        _visualStatus = null;
        NativeVisualStage.Close();
        TrainerBehaviour.StopNativeVisualPreview();
        _slot = 0; _customLayout = true;
        Clicks.Clear(); _openedFrame = Time.frameCount;
        if (_fieldsRoot != null) { _fieldsRoot.SetActive(false); Object.Destroy(_fieldsRoot); }
        // Keep native layout components entirely out of custom forms. Reuse the native
        // popup and control prefabs, with a plain content RectTransform as their parent.
        _fieldsRoot = new GameObject("NativeEditorFields");
        var rect = _fieldsRoot.AddComponent<RectTransform>();
        var native = Owner.chooseGrid.GetComponent<RectTransform>();
        rect.SetParent(native.parent, false);
        rect.anchorMin = native.anchorMin; rect.anchorMax = native.anchorMax;
        rect.pivot = native.pivot; rect.sizeDelta = native.sizeDelta;
        rect.anchoredPosition = native.anchoredPosition; rect.localScale = native.localScale;
        Owner.chooseGrid.SetActive(false);
        Owner.chooseUI.SetActive(true);
        Owner.chooseUI.transform.SetAsLastSibling();
        SyncToolbar();
        var heading = Button(title, () => { });
        heading.GetComponent<Button>().interactable = false;
        var hr = heading.GetComponent<RectTransform>();
        hr.anchoredPosition = new Vector2(20, -6); hr.sizeDelta = new Vector2(650, 32);
        Clicks.RemoveAt(Clicks.Count - 1); _slot = 0;
    }
    private static void RestoreLayout() {
        _customLayout = false;
        if (_fieldsRoot != null) { _fieldsRoot.SetActive(false); Object.Destroy(_fieldsRoot); _fieldsRoot = null; }
        Owner.chooseGrid.SetActive(true);
        var grid = Owner.chooseGrid.GetComponent<UIGrid>(); if (grid != null) grid.enabled = true;
        Clicks.Clear();
    }
    private static void Place(GameObject go, float x, float y, float width)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.sizeDelta = new Vector2(width, 40); r.localScale = Vector3.one;
        r.anchoredPosition = new Vector2(x + (_fieldsRoot != null && go.transform.parent == _fieldsRoot.transform ? 20 : 0), -y - (_fieldsRoot != null && go.transform.parent == _fieldsRoot.transform ? 48 : 0));
        var text = go.GetComponentInChildren<Text>(true);
        if (text != null) {
            text.fontSize = 18; text.resizeTextForBestFit = true; text.resizeTextMinSize = 13; text.resizeTextMaxSize = 18;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var t = text.GetComponent<RectTransform>(); t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = new Vector2(8, 2); t.offsetMax = new Vector2(-8, -2);
        }
    }
    private static GameObject Button(string label, Action action)
    {
        var go = Object.Instantiate(Owner.chooseButtonPrefab, Fields);
        go.name = "NativeCustomField";
        var b = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        if (b == null) throw new InvalidOperationException("原生选项模板没有 Button");
        b.onClick = new Button.ButtonClickedEvent();
        var text = go.GetComponentInChildren<Text>(true);
        if (text != null) text.text = label;
        go.SetActive(true);
        Place(go, (_slot % 3) * 225, (_slot / 3) * 48, 210); _slot++;
        Clicks.Add(new Click(b.GetComponent<RectTransform>(), action));
        return go;
    }
    private static InputField Field(string label, string value)
    {
        _slot = ((_slot + 2) / 3) * 3;
        var y = (_slot / 3) * 48;
        var title = Button(label, () => { });
        Place(title, 0, y, 400);
        var titleButton = title.GetComponent<Button>() ?? title.GetComponentInChildren<Button>(true);
        titleButton.interactable = false;
        var go = Object.Instantiate(Owner.skillName.gameObject, Fields);
        var input = go.GetComponent<InputField>();
        input.onValueChanged = new InputField.OnChangeEvent();
        input.onEndEdit = new InputField.SubmitEvent();
        input.contentType = InputField.ContentType.Standard;
        input.characterLimit = 24;
        input.text = value;
        var rect = go.GetComponent<RectTransform>();
        var template = Owner.chooseButtonPrefab.GetComponent<RectTransform>();
        if (rect != null && template != null) {
            rect.anchorMin = template.anchorMin; rect.anchorMax = template.anchorMax;
            rect.pivot = template.pivot; rect.sizeDelta = template.sizeDelta;
            rect.localScale = template.localScale;
            var textRect = input.textComponent.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 2); textRect.offsetMax = new Vector2(-8, -2);
        }
        go.SetActive(true);
        Place(go, 425, y, 190); _slot = ((_slot + 2) / 3) * 3;
        return input;
    }
    private static float Value(InputField field, float min = 0, float max = 1000000)
    {
        if (!float.TryParse(field.text.Trim(), NumberStyles.Float, C, out var v) || !float.IsFinite(v) || v < min || v > max)
            throw new ArgumentException($"请输入 {min}～{max} 的有限数值");
        return v;
    }
    private static void End(Action apply, float? footerY = null)
    {
        var applyButton = Button("应用", () => { apply(); Refresh(); Owner.CloseChooseUI(); Clicks.Clear(); });
        var cancelButton = Button("取消", () => { Owner.CloseChooseUI(); Clicks.Clear(); });
        Place(applyButton, 225, 330, 210); Place(cancelButton, 450, 330, 210);
    }
    private static void Refresh()
    {
        Owner.RefreshBaseDamageUI(); Owner.RefreshManaCost(); Owner.RefreshSkillPointText();
        Owner.RefreshDamageRangeUI(); Owner.RefreshPostureUI(); Owner.RefreshSpeAddDataGrid();
    }
    private static void Tick()
    {
        try { NativeVisualStage.Tick(); } catch (Exception ex) { NativeVisualStage.Close(); LongYinTrainerPlugin.Logger.LogWarning("Preview update stopped: " + ex); }
        if (_visualStatus != null && !string.IsNullOrEmpty(NativeVisualStage.PlaybackStatus)) _visualStatus.text = NativeVisualStage.PlaybackStatus;
        if (Active && !Owner.chooseUI.activeInHierarchy)
        {
            // Paging remains available even if this native UIGrid has no RectTransform.
            var direction = Input.GetKeyDown(KeyCode.PageDown) ? 1 : Input.GetKeyDown(KeyCode.PageUp) ? -1 : 0;
            var rect = Owner.speAddDataGrid.GetComponent<RectTransform>();
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null))
            { var wheel = Input.mouseScrollDelta.y; if (wheel != 0) direction = wheel < 0 ? 1 : -1; }
            if (direction != 0) { _effectPage += direction; PageEffects(); }
        }
        if (!Active) return;
        if (NativeKungfuEditor.ChoosingBase && !Owner.chooseUI.activeInHierarchy) { ChooseBaseCategory(); return; }
        EnsureMainButtons();
        SyncToolbar();
        if (!Owner.chooseUI.activeInHierarchy && Input.GetMouseButtonUp(0) && EditClickedText()) return;
        if (Time.frameCount == _openedFrame || !(Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))) return;
        foreach (var item in (Owner.chooseUI.activeInHierarchy ? Clicks : MainClicks).ToArray())
        {
            if (item.Rect == null || !item.Rect.gameObject.activeInHierarchy) continue;
            var canvas = item.Rect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(item.Rect, Input.mousePosition, camera)) continue;
            try { if (Input.GetMouseButtonUp(1)) item.Right?.Invoke(); else item.Action(); }
            catch (Exception ex) { LongYinTrainerPlugin.Logger.LogError("Native custom field: " + ex); Owner.ShowSubmitError(ex.Message); }
            break;
        }
    }
    private static bool Hit(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        var canvas = rect.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
    }
    private static bool RowTextHit(Component row)
    {
        // Never steal +/- or delete clicks, even if the Text rect stretches across a row.
        foreach (var b in row.GetComponentsInChildren<Button>(true))
            if ((b.name == "Left" || b.name == "Right" || b.name.Contains("Delete")) && Hit(b.GetComponent<RectTransform>())) return false;
        foreach (var t in row.GetComponentsInChildren<Text>(true))
            if (Hit(t.GetComponent<RectTransform>())) return true;
        return false;
    }
    private static bool EditClickedText()
    {
        try {
            foreach (var row in Owner.externalDamageUI.GetComponentsInChildren<CustomSkillDamageRatioController>(true))
                if (row.gameObject.activeInHierarchy && RowTextHit(row)) { RatioEdit(row.ratioIndex); return true; }
            foreach (var row in Owner.speAddDataGrid.GetComponentsInChildren<CustomSkillSpeAddDataController>(true))
                if (row.gameObject.activeInHierarchy && RowTextHit(row)) { Effect(row); return true; }
            if (Hit(Owner.manaCostText.GetComponent<RectTransform>())) { Numbers(); return true; }
            foreach (var text in Owner.externalDamageUI.GetComponentsInChildren<Text>(true))
                if (text.text.StartsWith("基础伤害") && Hit(text.GetComponent<RectTransform>())) {
                    var overButton = false;
                    foreach (var button in text.transform.parent.GetComponentsInChildren<Button>(true))
                        if (Hit(button.GetComponent<RectTransform>())) overButton = true;
                    if (!overButton) { Numbers(); return true; }
                }
            if (Hit(Owner.skillIcon.GetComponent<RectTransform>())) { Icons(0); return true; }
        } catch (Exception ex) { Owner.ShowSubmitError(ex.Message); LongYinTrainerPlugin.Logger.LogError(ex); }
        return false;
    }
    private static void RatioEdit(int index)
    {
        Begin(); var d = Owner.draftSkillData;
        var name = CustomSkillDamageRatioController.GetRatioName(index);
        var step = index >= 24 && index <= 26 ? .1f : 1f;
        var input = Field(name + "加成系数（可自定义）", CustomSkillDamageRatioController.GetRatioValue(d.addDamageRatio, index).ToString(C));
        for (var i = 1; i < GlobalData.AttriRatioString.Count; i++) {
            var value = i * step;
            Button(GlobalData.AttriRatioString[i], () => input.text = value.ToString(C));
        }
        Button("移除此加成", () => input.text = "0");
        End(() => { CustomSkillDamageRatioController.SetRatioValue(d.addDamageRatio, index, Value(input)); RebuildRatios(); });
    }
    private static bool Numbers()
    {
        if (!Active) return true;
        Begin(); var d = Owner.draftSkillData;
        var damage = Field("基础伤害", d.baseDamage.ToString(C));
        var mana = Field("内力消耗", d.manaCost.ToString(C));
        End(() => {
            var a = Value(damage); var b = Value(mana);
            d.baseDamage = a; d.manaCost = b; _draft = d.Pointer; _mana = b;
        });
        return false;
    }
    private static void Mana(CustomSkillCreateController __instance)
    {
        if (!Active || !_mana.HasValue || __instance.draftSkillData.Pointer != _draft) return;
        __instance.draftSkillData.manaCost = _mana.Value;
        __instance.manaCostText.text = "内力消耗 " + _mana.Value.ToString(C);
    }
    private static void Ratios()
    {
        Begin("加成管理 · 左键升档，右键退档，未启用表示移除"); var d = Owner.draftSkillData;
        // Native indices: six attributes, nine combat, nine life skills, HP/power/mana/charm.
        for (var i = 0; i < 28; i++) {
            var index = i;
            var step = index >= 24 && index <= 26 ? .1f : 1f;
            GameObject? button = null;
            string Label() {
                var v = CustomSkillDamageRatioController.GetRatioValue(d.addDamageRatio, index);
                var grade = Math.Clamp(Mathf.RoundToInt(v / step), 0, GlobalData.AttriRatioString.Count - 1);
                return CustomSkillDamageRatioController.GetRatioName(index) + " · " + (v <= 0 ? "未启用" : GlobalData.AttriRatioString[grade]);
            }
            void Change(int direction) {
                var old = CustomSkillDamageRatioController.GetRatioValue(d.addDamageRatio, index);
                var count = GlobalData.AttriRatioString.Count;
                var grade = (Mathf.RoundToInt(old / step) + direction + count) % count;
                CustomSkillDamageRatioController.SetRatioValue(d.addDamageRatio, index, grade * step);
                button!.GetComponentInChildren<Text>(true).text = Label();
                RebuildRatios();
            }
            button = Button(Label(), () => Change(1));
            Clicks[^1] = Clicks[^1] with { Right = () => Change(-1) };
            Place(button, (i % 4) * 165, (i / 4) * 43, 155);
        }
        FooterButton("完成", 3, () => { Refresh(); Owner.CloseChooseUI(); Clicks.Clear(); });
    }
    private static bool SafeRatioLabel(CustomSkillDamageRatioController __instance)
    {
        if (!Active) return true;
        var index = __instance.ratioIndex;
        var value = CustomSkillDamageRatioController.GetRatioValue(Owner.draftSkillData.addDamageRatio, index);
        var step = index >= 24 && index <= 26 ? .1f : 1f;
        var grade = Mathf.RoundToInt(value / step);
        if (grade >= 0 && grade < GlobalData.AttriRatioString.Count) return true;
        var text = __instance.transform.Find("Text")?.GetComponent<Text>();
        if (text != null) text.text = CustomSkillDamageRatioController.GetRatioName(index) + "加成 ×" + value.ToString(C);
        return false;
    }
    private static void RebuildRatios()
    {
        // InitDamageRatioUI only appends. Remove old rows before rebuilding.
        foreach (var row in Owner.externalDamageUI.GetComponentsInChildren<CustomSkillDamageRatioController>(true)) {
            row.gameObject.SetActive(false); row.transform.SetParent(null); Object.Destroy(row.gameObject);
        }
        Owner.InitDamageRatioUI(); PageRatios();
    }
    private static void PageRatios()
    {
        var rows = Owner.externalDamageUI.GetComponentsInChildren<CustomSkillDamageRatioController>(true);
        _ratioPage = Math.Clamp(_ratioPage, 0, Math.Max(0, (rows.Length - 1) / 3));
        for (var i = 0; i < rows.Length; i++) rows[i].gameObject.SetActive(i / 3 == _ratioPage);
        if (rows.Length > 0) {
            var grid = rows[0].transform.parent.GetComponent<UIGrid>();
            if (grid != null) { grid.hideInactive = true; grid.Reposition(); }
        }
    }
    private static bool Effect(CustomSkillSpeAddDataController __instance)
    {
        if (!Active) return true;
        BatchEffects(); return false;
    }
    internal static void AllBonuses()
    {
        var draft = Owner.draftSkillData;
        var pending = new Dictionary<string, float>();
        string Key(int group, int id) => group + ":" + id;
        HeroSpeAddData? Data(int group) => group == 0 ? draft.useAddData : group == 1 ? draft.equipAddData : draft.upgradeAddData;
        void Show(int group, string query, int page)
        {
            var defs = GameDataController.Instance.speAddDataBase;
            var ids = new List<int>();
            for (var id = 0; id < defs.Count; id++) {
                if (defs[id] == null || string.IsNullOrWhiteSpace(defs[id].name)) continue;
                if (query.Length == 0 || defs[id].name.Contains(query, StringComparison.OrdinalIgnoreCase) || id.ToString(C) == query) ids.Add(id);
            }
            const int size = 10;
            page = Math.Clamp(page, 0, Math.Max(0, (ids.Count - 1) / size));
            Begin("全部加成 · 0=移除 · %填50即50% · " + ids.Count + "项 · " + (page + 1) + "/" + Math.Max(1, (ids.Count + size - 1) / size));
            var fields = new List<(int Id, InputField Input)>();
            void Capture() {
                foreach (var item in fields) pending[Key(group, item.Id)] = Value(item.Input, -1000000) / (TrainerBehaviour.NativeEffectIsPercent(item.Id) ? 100f : 1f);
            }
            var labels = new[] { "使用效果", "装备加成", "每级成长" };
            for (var i = 0; i < 3; i++) {
                var selected = i;
                var tab = Button((i == group ? "✓ " : "") + labels[i], () => { Capture(); Show(selected, query, 0); });
                Place(tab, i * 220, 0, 210);
            }
            var search = CompactField("名称 / 编号", query, 0, 45, 490);
            var find = Button("搜索", () => { Capture(); Show(group, search.text.Trim(), 0); }); Place(find, 500, 45, 150);
            for (var i = page * size; i < Math.Min(ids.Count, (page + 1) * size); i++) {
                var id = ids[i]; var slot = i - page * size;
                var value = pending.TryGetValue(Key(group, id), out var saved) ? saved : Data(group)?.Get(id) ?? 0;
                var percent = TrainerBehaviour.NativeEffectIsPercent(id);
                fields.Add((id, CompactField(defs[id].name + (percent ? " (%)" : ""), (value * (percent ? 100 : 1)).ToString(C), slot % 2 * 335, 95 + slot / 2 * 43, 315)));
            }
            FooterButton("上一页", 0, () => { Capture(); Show(group, search.text.Trim(), page - 1); }, page > 0);
            FooterButton("下一页", 1, () => { Capture(); Show(group, search.text.Trim(), page + 1); }, (page + 1) * size < ids.Count);
            FooterButton("应用全部", 2, () => {
                Capture();
                foreach (var item in pending) {
                    var parts = item.Key.Split(':'); var g = int.Parse(parts[0], C); var id = int.Parse(parts[1], C);
                    var data = Data(g);
                    if (data == null) {
                        data = new HeroSpeAddData();
                        if (g == 0) draft.useAddData = data; else if (g == 1) draft.equipAddData = data; else draft.upgradeAddData = data;
                    }
                    data.Set(id, item.Value);
                }
                Owner.RefreshSpeAddDataGrid(); Refresh(); Owner.CloseChooseUI(); Clicks.Clear();
            });
            FooterButton("取消", 3, () => { Owner.CloseChooseUI(); Clicks.Clear(); });
        }
        Show(0, "", 0);
    }
    private static void BatchEffects()
    {
        var rows = Owner.speAddDataGrid.GetComponentsInChildren<CustomSkillSpeAddDataController>(true);
        var values = new string[rows.Length];
        for (var i = 0; i < rows.Length; i++) values[i] = (rows[i].GetTargetSpeAdd().Get(rows[i].speAddType) * (TrainerBehaviour.NativeEffectIsPercent(rows[i].speAddType) ? 100f : 1f)).ToString(C);
        void Show(int page) {
            Begin("特效批量编辑 · 百分比填 50 表示 50% · " + (page + 1) + "/" + Math.Max(1, (rows.Length + 13) / 14));
            var inputs = new List<(int Index, InputField Input)>();
            var defs = GameDataController.Instance.speAddDataBase;
            for (var i = page * 14; i < Math.Min(rows.Length, (page + 1) * 14); i++) {
                var slot = i - page * 14; var id = rows[i].speAddType;
                inputs.Add((i, CompactField(defs[id].name + (TrainerBehaviour.NativeEffectIsPercent(id) ? "（%）" : ""), values[i], (slot % 2) * 335, (slot / 2) * 43, 315)));
            }
            void Capture() { foreach (var input in inputs) { Value(input.Input, -1000000); values[input.Index] = input.Input.text; } }
            FooterButton("上一页", 0, () => { Capture(); Show(page - 1); }, page > 0);
            FooterButton("下一页", 1, () => { Capture(); Show(page + 1); }, (page + 1) * 14 < rows.Length);
            FooterButton("应用全部", 2, () => {
                Capture(); var parsed = new float[values.Length];
                for (var i = 0; i < values.Length; i++) parsed[i] = float.Parse(values[i], C) / (TrainerBehaviour.NativeEffectIsPercent(rows[i].speAddType) ? 100f : 1f);
                for (var i = 0; i < rows.Length; i++) { rows[i].GetTargetSpeAdd().Set(rows[i].speAddType, parsed[i]); rows[i].RefreshSpeAddUI(); }
                Refresh(); Owner.CloseChooseUI(); Clicks.Clear();
            });
            FooterButton("取消", 3, () => { Owner.CloseChooseUI(); Clicks.Clear(); });
        }
        Show(0);
    }
    private static void FooterButton(string label, int column, Action action, bool enabled = true)
    {
        var button = Button(label, action); Place(button, column * 165, 330, 155);
        if (!enabled) { button.GetComponent<Button>().interactable = false; Clicks.RemoveAt(Clicks.Count - 1); }
    }
    private static readonly string[] AttackShapes = { "距离环（菱形格）", "方形", "十字直线", "对角斜线", "朝向区域" };
    private static readonly string[] DamageShapes = { "距离环（菱形格）", "方形", "十字直线", "对角斜线", "朝向直线", "朝向扇形", "朝向斜线", "全场", "朝向十字" };
    private sealed class RangeRow
    {
        internal int Type;
        internal InputField Min = null!, Max = null!;
        internal bool Removed;
    }
    private static InputField CompactField(string label, string value, float x, float y, float width)
    {
        var saved = _slot;
        var input = Field(label, value);
        // Field creates the label immediately before the input.
        var parent = Fields;
        Place(parent.GetChild(parent.childCount - 2).gameObject, x, y, width * .55f);
        Place(input.gameObject, x + width * .57f, y, width * .4f);
        _slot = saved;
        return input;
    }
    private static bool Range()
    {
        if (!Active) return true;
        Begin("范围设置 · 每行独立编辑，距离单位为格"); var d = Owner.draftSkillData;
        var rows = new List<RangeRow>();
        var count = d.attackRangeData?.Count ?? 0;
        void AddRow(bool damage, int type, int min, int max, int line) {
            var childStart = Fields.childCount;
            var row = new RangeRow { Type = type }; rows.Add(row);
            var names = damage ? DamageShapes : AttackShapes;
            row.Type = Math.Clamp(row.Type, 0, names.Length - 1);
            GameObject? shape = null;
            string Label() => (damage ? "命中" : "释放" + line) + " · " + (row.Removed ? "已移除" : names[row.Type]);
            shape = Button(Label(), () => { row.Type = (row.Type + 1) % names.Length; shape!.GetComponentInChildren<Text>(true).text = Label(); });
            Place(shape, 0, line * 35, 225);
            row.Min = CompactField("最近格", min.ToString(C), 230, line * 35, 165);
            row.Max = CompactField("最远格", max.ToString(C), 400, line * 35, 165);
            if (!damage) {
                GameObject? remove = null;
                remove = Button("移除", () => { row.Removed = !row.Removed; remove!.GetComponentInChildren<Text>(true).text = row.Removed ? "恢复" : "移除"; shape!.GetComponentInChildren<Text>(true).text = Label(); });
                Place(remove, 575, line * 35, 75);
            }
            for (var child = childStart; child < Fields.childCount; child++) { var rect = Fields.GetChild(child).GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(rect.sizeDelta.x, 32); }
        }
        AddRow(true, (int)d.damageRangeData.rangeType, d.damageRangeData.minRange, d.damageRangeData.maxRange, 0);
        for (var i = 0; i < count; i++) { var r = d.attackRangeData![i]; AddRow(false, (int)r.rangeType, r.minRange, r.maxRange, i + 1); }
        // Reserve one row for adding a new layer directly, without navigating away.
        var add = Button("＋释放范围", () => {
            if (rows.Count >= 9) { Owner.ShowSubmitError("最多八层释放范围"); return; }
            AddRow(false, 0, 1, 1, rows.Count);
        });
        Place(add, 0, 330, 210);
        _slot = 28;
        End(() => {
            var copy = new Il2CppSystem.Collections.Generic.List<SkillAttackRangeData>();
            SkillDamageRangeData? hit = null;
            for (var i = 0; i < rows.Count; i++) {
                var r = rows[i]; if (r.Removed) continue;
                var lo = Value(r.Min, 0, 32); var hi = Value(r.Max, 0, 32);
                if (lo != MathF.Truncate(lo) || hi != MathF.Truncate(hi) || lo > hi) throw new ArgumentException("距离须为整数，最近距离不能大于最远距离");
                if (i == 0) hit = new SkillDamageRangeData(r.Type, (int)lo, (int)hi);
                else copy.Add(new SkillAttackRangeData(r.Type, (int)lo, (int)hi));
            }
            if (copy.Count == 0) throw new ArgumentException("至少保留一层释放范围");
            d.damageRangeData = hit; d.attackRangeData = copy; d.RefreshMaxAttackRange();
        }, 390);
        return false;
    }
    private static bool Posture()
    {
        if (!Active) return true;
        Begin("架势设置 · 左侧进攻，右侧防御"); var d = Owner.draftSkillData;
        var attack = new List<InputField>(); var defense = new List<InputField>();
        for (var i = 0; i < 6; i++) {
            var name = GlobalData.PartPostureName[i];
            attack.Add(CompactField("进攻·" + name, (d.atkPartPosture?.partPosture?[i] ?? 0).ToString(C), 0, i * 48, 315));
            defense.Add(CompactField("防御·" + name, (d.defPartPosture?.partPosture?[i] ?? 0).ToString(C), 335, i * 48, 315));
        }
        _slot = 18;
        End(() => {
            var a = new Il2CppSystem.Collections.Generic.List<float>();
            var b = new Il2CppSystem.Collections.Generic.List<float>();
            foreach (var f in attack) a.Add(Value(f, -1000000));
            foreach (var f in defense) b.Add(Value(f, -1000000));
            d.atkPartPosture = new PartPostureData { partPosture = a };
            d.defPartPosture = new PartPostureData { partPosture = b };
        });
        return false;
    }
    private static bool Icon() => true; // Dice keeps the original random-icon handler.
    private static void Icons(int page) => VisualPicker("icon", page);
    internal static string? AutomaticDescription;
    private static GameObject StageArea(float x, float y, float width, float height)
    {
        var stage = new GameObject("CombinedVisualPreview"); var rect = stage.AddComponent<RectTransform>(); rect.SetParent(Fields, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x + 20, -y - 48); rect.sizeDelta = new Vector2(width, height);
        return stage;
    }
    private static void VisualMenu()
    {
        Begin("动作与特效搭配 · 更换后在右侧预览");
        var fields = new[] { "animation", "weapon", "bullet", "visual", "trail", "all" };
        var names = new[] { "使用动作", "更换武器", "弹道", "场景特效", "武器拖尾", "整套视觉同步" };
        for (var i = 0; i < fields.Length; i++) {
            var field = fields[i]; var button = Button(names[i], () => VisualPicker(field, 0));
            Place(button, (i % 2) * 155, (i / 2) * 48, 145);
        }
        var outfit = Button("预览服装", () => OutfitPicker(0)); Place(outfit, 0, 144, 145);
        var move = Button("角色位移：" + (Owner.draftSkillData.autoHeroMove ? "开" : "关"), () => { Owner.draftSkillData.autoHeroMove = !Owner.draftSkillData.autoHeroMove; VisualMenu(); }); Place(move, 155, 144, 145);
        var showRange = Button("范围格：" + (NativeVisualStage.ShowRanges ? "显示" : "隐藏"), () => { NativeVisualStage.ShowRanges = !NativeVisualStage.ShowRanges; VisualMenu(); }); Place(showRange, 0, 192, 300);
        var note = Button("服装仅用于预览；蓝格释放范围，红格作用范围。", () => { });
        Clicks.RemoveAt(Clicks.Count - 1); Place(note, 0, 236, 300); note.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 34); note.GetComponent<Button>().interactable = false;
        var stage = StageArea(320, 0, 330, 265);
        var status = Button("正在准备人物与效果…", () => { }); Clicks.RemoveAt(Clicks.Count - 1); Place(status, 0, 275, 650); status.GetComponent<Button>().interactable = false;
        _visualStatus = status.GetComponentInChildren<Text>(true);
        void Play() { status.GetComponentInChildren<Text>(true).text = NativeVisualStage.Open(Owner.draftSkillData, stage.transform); }
        FooterButton("重新播放", 0, Play);
        FooterButton("停止", 1, NativeVisualStage.Close);
        FooterButton("主角服装", 2, () => { NativeVisualStage.Outfit = null; Play(); });
        FooterButton("完成", 3, () => { NativeVisualStage.Close(); Owner.CloseChooseUI(); Clicks.Clear(); });
        Play();
    }
    private static void OutfitPicker(int page)
    {
        var choices = NativeVisualStage.Outfits.ToArray();
        Begin("预览服装 · 选择后返回人物预览，不修改主角装备");
        for (var i = page * 24; i < Math.Min(choices.Length, (page + 1) * 24); i++) {
            var skin = choices[i]; var slot = i - page * 24;
            var button = Button("服装 " + (i + 1), () => { NativeVisualStage.Outfit = skin; VisualMenu(); });
            Place(button, (slot % 4) * 165, (slot / 4) * 48, 155);
        }
        FooterButton("上一页", 0, () => OutfitPicker(page - 1), page > 0);
        FooterButton("下一页", 1, () => OutfitPicker(page + 1), (page + 1) * 24 < choices.Length);
        FooterButton("主角服装", 2, () => { NativeVisualStage.Outfit = null; VisualMenu(); });
        FooterButton("返回", 3, VisualMenu);
    }
    private static void ChooseRarity()
    {
        Begin("自定义稀有度 · 当前：" + TrainerBehaviour.NativeRarityName(Owner.draftSkillData.rareLv));
        var levels = new SortedSet<int>();
        foreach (var pair in GameDataController.Instance.kungfuSkillDataBase)
            if (pair.Value != null && pair.Key >= 0 && pair.Key < 900000) levels.Add(pair.Value.rareLv);
        var index = 0;
        foreach (var level in levels) {
            var selected = level;
            var button = Button(TrainerBehaviour.NativeRarityName(level), () => {
                var words = new[] { "基础", "进阶", "上乘", "秘传", "顶极", "绝世" };
                var old = Owner.draftSkillData.rareLv;
                if (AutomaticDescription != null && Owner.skillDescribe.text == AutomaticDescription && old >= 0 && old < words.Length && selected >= 0 && selected < words.Length) {
                    AutomaticDescription = AutomaticDescription.Replace(words[old], words[selected]);
                    Owner.skillDescribe.text = AutomaticDescription;
                }
                Owner.draftSkillData.rareLv = selected;
                Owner.RefreshSkillIcon(); Owner.RefreshSkillPointText();
                Owner.RefreshManaCost(); Owner.RefreshBaseDamageUI();
                Owner.CloseChooseUI(); Clicks.Clear();
            });
            Place(button, (index % 3) * 220, 40 + (index / 3) * 65, 210); index++;
        }
        FooterButton("取消", 3, () => { Owner.CloseChooseUI(); Clicks.Clear(); });
    }
    private static void ExitEditor()
    {
        NativeVisualStage.Close(); TrainerBehaviour.StopNativeVisualPreview();
        NativeKungfuEditor.ChoosingBase = false;
        Owner.CloseChooseUI(); Reset(); Owner.customSkillCreateUI.SetActive(false);
    }
    private static void ConfirmEditorAction(bool changeBase)
    {
        Begin(changeBase ? "更换基础功法会重置当前草稿，是否继续？" : "退出将丢弃未保存草稿，是否继续？");
        FooterButton(changeBase ? "选择新基础" : "退出编辑", 2, () => {
            if (changeBase) ChooseBaseCategory(); else ExitEditor();
        });
        FooterButton("继续编辑", 3, () => { Owner.CloseChooseUI(); Clicks.Clear(); });
    }
    internal static void ChooseBaseCategory()
    {
        Begin("新建功法 · 先选择类别，再选择基础功法");
        for (var i = 0; i < 9; i++) {
            var category = i;
            var button = Button(TrainerBehaviour.NativeCategoryName(i), () => {
                SourceFilters["base"] = new SourceFilter { Type = category };
                VisualPicker("base", 0);
            });
            Place(button, (i % 3) * 220, 40 + (i / 3) * 65, 210);
        }
        FooterButton(NativeKungfuEditor.ChoosingBase ? "取消新建" : "返回编辑", 3, () => {
            if (NativeKungfuEditor.ChoosingBase) ExitEditor();
            else { Owner.CloseChooseUI(); Clicks.Clear(); }
        });
    }
    private sealed class SourceFilter { internal string Query = ""; internal int Type = -1, Rarity = -1; }
    private static readonly Dictionary<string, SourceFilter> SourceFilters = new();
    private static void VisualPicker(string field, int page)
    {
        var names = new Dictionary<string, string> { ["icon"] = "图标", ["base"] = "基础功法", ["animation"] = "使用动作", ["weapon"] = "显示武器", ["bullet"] = "弹道", ["visual"] = "场景特效", ["all"] = "整套视觉", ["trail"] = "武器拖尾" };
        if (!SourceFilters.TryGetValue(field, out var filter)) SourceFilters[field] = filter = new SourceFilter();
        var choices = new List<KungfuSkillData>(); var rarities = new SortedSet<int>();
        foreach (var pair in GameDataController.Instance.kungfuSkillDataBase) {
            var d = pair.Value; if (d == null || pair.Key < 0 || pair.Key >= 900000) continue;
            if (field == "base" && (d.hide || d.summonSkill)) continue;
            rarities.Add(d.rareLv);
            if (field == "animation" && string.IsNullOrEmpty(d.animationName)) continue;
            if (field == "weapon" && string.IsNullOrEmpty(d.weaponName)) continue;
            if (field == "bullet" && d.skillBullet == null) continue;
            if (field == "visual" && (d.skillSpeEffects == null || d.skillSpeEffects.Count == 0)) continue;
            if (filter.Type >= 0 && d.type != filter.Type || filter.Rarity >= 0 && d.rareLv != filter.Rarity) continue;
            if (filter.Query.Length > 0 && !(d.name ?? "").Contains(filter.Query, StringComparison.OrdinalIgnoreCase) && !d.skillID.ToString(C).Contains(filter.Query, StringComparison.OrdinalIgnoreCase)) continue;
            choices.Add(d);
        }
        const int size = 25;
        choices.Sort((a,b) => a.skillID.CompareTo(b.skillID));
        var pages = Math.Max(1, (choices.Count + size - 1) / size); page = Math.Clamp(page, 0, pages - 1);
        Begin(names[field] + "来源 · " + choices.Count + "项 · " + (page + 1) + "/" + pages);
        var search = CompactField("名称 / 编号", filter.Query, 0, 0, 430); search.characterLimit = 80;
        var find = Button("搜索", () => { filter.Query = search.text.Trim(); VisualPicker(field, 0); }); Place(find, 440, 0, 100);
        var reset = Button("重置", () => { filter.Query = ""; filter.Type = filter.Rarity = -1; VisualPicker(field, 0); }); Place(reset, 550, 0, 100);
        void FilterMenu(bool category)
        {
            filter.Query = search.text.Trim();
            Begin(category ? "选择类别" : "选择品级");
            var values = new List<int> { -1 };
            if (category) for (var i = 0; i < 9; i++) values.Add(i);
            else values.AddRange(rarities);
            for (var i = 0; i < values.Count; i++) {
                var value = values[i];
                var label = value < 0 ? "全部" : category ? TrainerBehaviour.NativeCategoryName(value) : TrainerBehaviour.NativeRarityName(value);
                var selected = category ? filter.Type : filter.Rarity;
                var option = Button((selected == value ? "✓ " : "") + label, () => {
                    if (category) filter.Type = value; else filter.Rarity = value;
                    VisualPicker(field, 0);
                });
                Place(option, (i % 3) * 220, 35 + (i / 3) * 55, 210);
            }
            FooterButton("取消", 3, () => VisualPicker(field, page));
        }
        var type = Button("类别：" + (filter.Type < 0 ? "全部" : TrainerBehaviour.NativeCategoryName(filter.Type)) + " ▾", () => FilterMenu(true));
        Place(type, 0, 45, 315);
        var rarity = Button("品级：" + (filter.Rarity < 0 ? "全部" : TrainerBehaviour.NativeRarityName(filter.Rarity)) + " ▾", () => FilterMenu(false));
        Place(rarity, 335, 45, 315);
        for (var i = page * size; i < Math.Min(choices.Count, (page + 1) * size); i++) {
            var donor = choices[i]; var slot = i - page * size;
            var button = Button(donor.name, () => { filter.Query = search.text.Trim(); if (field == "icon") { Owner.draftSkillData.iconName = donor.GetSkillIcon(); Owner.RefreshSkillIcon(); Owner.CloseChooseUI(); Clicks.Clear(); return; } if (field == "base") { NativeKungfuEditor.SelectBase(donor.skillID); return; } TrainerBehaviour.ApplyNativeVisualSource(Owner.draftSkillData, donor, field); Owner.RefreshSkillIcon(); VisualMenu(); });
            Place(button, (slot % 5) * 132, 96 + (slot / 5) * 44, 125);
        }
        if (choices.Count == 0) { var empty = Button("没有匹配项，请调整搜索或筛选", () => { }); Place(empty, 0, 110, 650); Clicks.RemoveAt(Clicks.Count - 1); }
        FooterButton("上一页", 0, () => { filter.Query = search.text.Trim(); VisualPicker(field, page - 1); }, page > 0);
        FooterButton("下一页", 1, () => { filter.Query = search.text.Trim(); VisualPicker(field, page + 1); }, (page + 1) * size < choices.Count);
        FooterButton(field == "icon" ? "导入图片…" : "清除此项", 2, () => { if (field == "icon") { ImportIcon(); return; } TrainerBehaviour.ApplyNativeVisualSource(Owner.draftSkillData, null, field); VisualMenu(); }, field != "all" && field != "base");
        FooterButton("返回", 3, () => { filter.Query = search.text.Trim(); if (field == "base") ChooseBaseCategory(); else if (field == "icon") { Owner.CloseChooseUI(); Clicks.Clear(); } else VisualMenu(); });
    }
    private static void VisualPreview(KungfuSkillData skill, string? field, Action back)
    {
        Begin("视觉预览 · " + skill.name);
        var detail = Button(TrainerBehaviour.NativeVisualDetails(skill), () => { });
        Clicks.RemoveAt(Clicks.Count - 1);
        Place(detail, 0, 0, 420);
        detail.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 265);
        detail.GetComponent<Button>().interactable = false;
        detail.GetComponentInChildren<Text>(true).alignment = TextAnchor.UpperLeft;
        var stage = new GameObject("NativeAnimationPreview");
        var rect = stage.AddComponent<RectTransform>(); rect.SetParent(Fields, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(455, -48); rect.sizeDelta = new Vector2(200, 265);
        stage.AddComponent<RectMask2D>();
        var status = Button("点击播放动作；弹道和场景特效此处显示参数", () => { });
        Clicks.RemoveAt(Clicks.Count - 1); Place(status, 0, 275, 650);
        status.GetComponent<Button>().interactable = false;
        FooterButton("播放动作", 0, () => status.GetComponentInChildren<Text>(true).text = TrainerBehaviour.PlayNativeVisualPreview(skill, stage.transform));
        FooterButton("停止", 1, TrainerBehaviour.StopNativeVisualPreview);
        FooterButton("采用此来源", 2, () => {
            TrainerBehaviour.ApplyNativeVisualSource(Owner.draftSkillData, skill, field!); Owner.RefreshSkillIcon(); VisualMenu();
        }, field != null);
        FooterButton("返回", 3, back);
        status.GetComponentInChildren<Text>(true).text = TrainerBehaviour.PlayNativeVisualPreview(skill, stage.transform);
    }
    private static void ImportIcon()
    {
        var key = TrainerBehaviour.ImportNativeKungfuIcon();
        _openedFrame = Time.frameCount; // Ignore the mouse release returning from the file dialog.
        if (key == null) return;
        Owner.draftSkillData.iconName = key; Owner.RefreshSkillIcon(); Owner.CloseChooseUI(); Clicks.Clear();
    }
    private static void EnsureMainButtons()
    {
        if (MainButtons.Count != 0) return;
        void Add(string label, float x, float y, float width, Action action, Transform? ownAnchor = null) {
            var anchor = ownAnchor ?? Owner.chooseNewSpeAddDataButton.transform;
            var go = Object.Instantiate(Owner.chooseButtonPrefab, Owner.customSkillCreateUI.transform);
            go.name = "NativeEditorToolbar";
            var b = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
            b.onClick = new Button.ButtonClickedEvent();
            go.GetComponentInChildren<Text>(true).text = label;
            go.SetActive(true); Place(go, 0, 0, width);
            var r = go.GetComponent<RectTransform>(); r.pivot = new Vector2(.5f, .5f);
            r.position = anchor.parent.TransformPoint(anchor.localPosition + new Vector3(x, y, 0));
            go.transform.SetAsLastSibling();
            MainButtons.Add(go); MainClicks.Add(new Click(b.GetComponent<RectTransform>(), action));
        }
        Add("更改基础", -115, -65, 105, () => ConfirmEditorAction(true), Owner.skillName.transform);
        Add("退出编辑", 115, -65, 105, () => ConfirmEditorAction(false), Owner.skillName.transform);
        Add("稀有度", 0, -65, 105, ChooseRarity, Owner.skillName.transform);
        Add("自定义图标…", 0, -80, 140, ImportIcon, Owner.skillIcon.transform);
        Add("上一页", -70, 42, 100, () => { _effectPage--; PageEffects(); }); EffectPager.Add(MainButtons[^1]);
        Add("下一页", 70, 42, 100, () => { _effectPage++; PageEffects(); }); EffectPager.Add(MainButtons[^1]);
        var rows = Owner.externalDamageUI.GetComponentsInChildren<CustomSkillDamageRatioController>(true);
        if (rows.Length > 0) {
            var start = MainButtons.Count;
            var anchor = rows[0].transform.parent;
            // Use the native ratio column's own coordinate system, not the adjacent effect column.
            Add("‹", -85, -150, 32, () => { _ratioPage--; PageRatios(); }, rows[0].transform); RatioPager.Add(MainButtons[^1]);
            Add("管理加成", 0, -150, 120, Ratios, rows[0].transform);
            Add("›", 85, -150, 32, () => { _ratioPage++; PageRatios(); }, rows[0].transform); RatioPager.Add(MainButtons[^1]);
            for (var i = 0; i < 3; i++) RatioToolbar.Add(MainButtons[start + i]);
        }
        Add("动作 / 战斗特效", 310, 42, 200, VisualMenu);
        Add("全部加成 / 批量编辑", 0, -250, 200, AllBonuses);
        PageRatios();
        Owner.chooseUI.transform.SetAsLastSibling();
    }
    private static void PositionRatioToolbar()
    {
        RectTransform? bottom = null;
        foreach (var row in Owner.externalDamageUI.GetComponentsInChildren<CustomSkillDamageRatioController>(true)) {
            if (!row.gameObject.activeInHierarchy) continue;
            var rect = row.transform.Find("Text")?.GetComponent<RectTransform>() ?? row.GetComponent<RectTransform>();
            if (bottom == null || rect.position.y < bottom.position.y) bottom = rect;
        }
        if (bottom == null) return;
        for (var i = 0; i < RatioToolbar.Count; i++)
            RatioToolbar[i].transform.position = bottom.TransformPoint(new Vector3(bottom.rect.center.x + (i - 1) * 85, bottom.rect.yMin - 28, 0));
    }
    private static void SyncToolbar()
    {
        PositionRatioToolbar();
        var visible = Active && !Owner.chooseUI.activeInHierarchy;
        foreach (var go in MainButtons) if (go != null) go.SetActive(visible);
        if (!visible) return;
        var effects = Owner.speAddDataGrid.GetComponentsInChildren<CustomSkillSpeAddDataController>(true).Length;
        foreach (var go in EffectPager) go.SetActive(effects > 4);
        var ratios = Owner.externalDamageUI.GetComponentsInChildren<CustomSkillDamageRatioController>(true).Length;
        foreach (var go in RatioPager) go.SetActive(ratios > 3);
    }
    private static void EffectList() => EffectListPage(0);
    private static void EffectListPage(int page)
    {
        Begin();
        var rows = Owner.speAddDataGrid.GetComponentsInChildren<CustomSkillSpeAddDataController>(true);
        for (var i = page * 24; i < Math.Min(rows.Length, (page + 1) * 24); i++) {
            var row = rows[i]; var item = row;
            var defs = GameDataController.Instance.speAddDataBase;
            var button = Button(defs[row.speAddType].name, () => Effect(item));
            Place(button, ((i - page * 24) % 4) * 165, ((i - page * 24) / 4) * 48, 155);
        }
        _slot = 18;
        if (page > 0) Button("上一页", () => EffectListPage(page - 1));
        if ((page + 1) * 24 < rows.Length) Button("下一页", () => EffectListPage(page + 1));
        Button("关闭", () => { Owner.CloseChooseUI(); Clicks.Clear(); });
    }
    private static void PageEffects()
    {
        if (!Active) return;
        var rows = Owner.speAddDataGrid.GetComponentsInChildren<CustomSkillSpeAddDataController>(true);
        _effectPage = Math.Clamp(_effectPage, 0, Math.Max(0, (rows.Length - 1) / 4));
        for (var i = 0; i < rows.Length; i++) rows[i].gameObject.SetActive(i / 4 == _effectPage);
        var grid = Owner.speAddDataGrid.GetComponent<UIGrid>();
        if (grid != null) { grid.hideInactive = true; grid.Reposition(); }
        var label = Owner.chooseNewSpeAddDataButton.GetComponentInChildren<Text>(true);
        if (label != null) { label.text = "新增特效 · " + (_effectPage + 1) + "/" + Math.Max(1, (rows.Length + 3) / 4); label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; }
    }
}

public static partial class TrainerBehaviour
{
    internal static void ApplyNativeVisualSource(KungfuSkillData target, KungfuSkillData? source, string field)
    {
        if (field is "animation" or "all") target.animationName = source?.animationName ?? "";
        if (field is "weapon" or "all") target.weaponName = source?.weaponName ?? "";
        if (field is "bullet" or "all") target.skillBullet = source?.skillBullet?.Clone()?.TryCast<SkillBulletData>();
        if (field is "visual" or "all") target.skillSpeEffects = CloneSkillVisualEffects(source?.skillSpeEffects);
        if (field is "trail" or "all") target.trailID = source?.trailID ?? 0;
        if (field == "all" && source != null) { target.iconName = source.GetSkillIcon(); target.autoHeroMove = source.autoHeroMove; }
    }
    internal static string NativeCategoryName(int id) => CustomKungfuTypeName(id);
    internal static string NativeRarityName(int id) => RarityName(id);
    internal static bool NativeEffectIsPercent(int id) => IsPercentageAffix(id);
}
