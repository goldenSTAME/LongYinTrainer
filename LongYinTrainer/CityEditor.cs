using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _cityEditorId = int.MinValue;
    private static IntPtr _cityEditorWorld;
    private static IntPtr _cityObservedWorld;

    private static void TickCityState()
    {
        var world = GameController.Instance?.worldData;
        var pointer = world?.Pointer ?? IntPtr.Zero;
        if (_cityObservedWorld != pointer)
        {
            _cityObservedWorld = pointer;
            _cityEditorWorld = IntPtr.Zero;
            _cityEditorId = int.MinValue;
            InvalidatePages(8);
        }
        CityStateLock.Poll(world, Time.realtimeSinceStartup);
    }

    private static void BuildNativeCity()
    {
        var p = NativePageParent;
        var world = GameController.Instance?.worldData;
        if (world?.Areas == null) { AddText(p, "请先读取存档。", Vector2.zero, new Vector2(900, 40)); return; }
        var cities = new List<AreaData>();
        for (var i = 0; i < world.Areas.Count; i++)
            if (world.Areas[i] != null) cities.Add(world.Areas[i]);
        cities.Sort((a, b) => a.areaID.CompareTo(b.areaID));
        if (cities.Count == 0) { AddText(p, "当前存档没有可编辑城市。", Vector2.zero, new Vector2(900, 40)); return; }
        if (_cityEditorWorld != world.Pointer)
        {
            _cityEditorWorld = world.Pointer;
            _cityEditorId = AreaController.Instance?.areaData?.areaID ?? cities[0].areaID;
        }
        var selected = Math.Max(0, cities.FindIndex(a => a.areaID == _cityEditorId));
        var matches = new List<AreaData>(cities);
        AddText(p, "城市／据点属性", Vector2.zero, new Vector2(680, 36), 20);
        var search = AddInput(p, "", new Vector2(0, 48), new Vector2(275, 36));
        var title = AddText(p, "", new Vector2(120, 102), new Vector2(670, 42), 20, TextAnchor.MiddleCenter);
        var fields = new InputField[5];
        var labels = new[] { "治安", "民心", "城防", "当前人口", "人口上限" };
        for (var i = 0; i < fields.Length; i++)
        {
            AddText(p, labels[i], new Vector2(25, 170 + i * 57), new Vector2(175, 38));
            fields[i] = AddInput(p, "", new Vector2(210, 170 + i * 57), new Vector2(220, 38));
            AddText(p, i < 3 ? "0–100" : i == 3 ? "不超过人口上限" : "1–10亿", new Vector2(450, 170 + i * 57), new Vector2(260, 38), 16);
        }
        var lockStatus = AddText(p, "", new Vector2(735, 165), new Vector2(315, 60), 19);
        void ReadSelection()
        {
            if (!ValidSelection()) return;
            var area = matches[selected];
            _cityEditorId = area.areaID;
            title.text = $"{area.GetAreaName()}（ID {area.areaID}）  {selected + 1}/{matches.Count}";
            var values = CityStateLock.Read(area);
            for (var i = 0; i < fields.Length; i++) fields[i].text = values[i].ToString("R", CultureInfo.InvariantCulture);
            lockStatus.text = CityStateLock.IsLocked(world, area) ? "本城：已锁定" : "本城：未锁定";
        }
        bool ValidSelection()
        {
            var current = GameController.Instance?.worldData;
            if (current?.Pointer == world.Pointer && current.GetArea(matches[selected].areaID)?.Pointer == matches[selected].Pointer) return true;
            _status = "存档或城市数据已变化，请重新打开城市页面。";
            RequestPageRebuild(); return false;
        }
        void Apply(bool shouldLock)
        {
            try
            {
                if (!ValidSelection()) return;
                var values = new float[5];
                for (var i = 0; i < fields.Length; i++)
                    if (!float.TryParse(fields[i].text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                        throw new ArgumentException($"{labels[i]}请输入有效数字。");
                var area = matches[selected];
                CityStateLock.Apply(world, area, values, shouldLock);
                ReadSelection();
                _status = $"{area.GetAreaName()}的属性已应用" + (CityStateLock.IsLocked(world, area) ? "并锁定。" : "。") + "请在游戏中保存数值。";
            }
            catch (Exception ex) { _status = "修改城市失败：" + ex.Message; }
        }
        AddButton(p, "查找名称／ID", new Vector2(285, 48), new Vector2(175, 36), () =>
        {
            var text = search.text.Trim();
            var found = cities.FindAll(a => a.GetAreaName().Contains(text, StringComparison.OrdinalIgnoreCase) || a.areaID.ToString() == text);
            if (found.Count == 0) { _status = "没有匹配的城市。"; return; }
            matches = found; selected = 0; ReadSelection();
        });
        AddButton(p, "读取当前场景", new Vector2(475, 48), new Vector2(225, 36), () =>
        {
            var area = AreaController.Instance?.areaData;
            var index = area == null ? -1 : cities.FindIndex(a => a.Pointer == area.Pointer);
            if (index < 0) { _status = "请先进入目标城市／据点。"; return; }
            matches = new List<AreaData>(cities); selected = index; ReadSelection();
        });
        AddButton(p, "全部城市", new Vector2(715, 48), new Vector2(175, 36), () => { matches = new List<AreaData>(cities); selected = 0; ReadSelection(); });
        AddButton(p, "上一个", new Vector2(0, 102), new Vector2(110, 42), () => { selected = (selected + matches.Count - 1) % matches.Count; ReadSelection(); });
        AddButton(p, "下一个", new Vector2(810, 102), new Vector2(110, 42), () => { selected = (selected + 1) % matches.Count; ReadSelection(); });
        AddButton(p, "应用数值", new Vector2(735, 240), new Vector2(300, 42), () => Apply(false));
        AddButton(p, "应用并锁定本城", new Vector2(735, 295), new Vector2(300, 42), () => Apply(true));
        AddButton(p, "解除本城锁定", new Vector2(735, 350), new Vector2(300, 42), () =>
        {
            if (!ValidSelection()) return;
            CityStateLock.Unlock(world, matches[selected]); ReadSelection(); _status = "已解除所选城市锁定。";
        });
        AddButton(p, "重读当前数值", new Vector2(735, 405), new Vector2(300, 42), () => { if (ValidSelection()) ReadSelection(); });
        AddText(p, "每座城市分别编辑，可同时锁定多城；切换城市前请先应用。\n关闭面板后锁定继续生效；读档、换存档或重启游戏后解除锁定。\n已锁定城市再次应用数值，会同时更新它的锁定值。", new Vector2(25, 490), new Vector2(1010, 115), 17);
        ReadSelection();
    }
}
