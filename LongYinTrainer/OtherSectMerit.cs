using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static partial class TrainerBehaviour
{
    // The exchange currency belongs to each ForceData, not to the selected hero.
    private static void BuildOtherSectMerit(Transform p)
    {
        var world = GameController.Instance?.worldData;
        var choices = new List<ForceData>();
        if (world?.Forces != null)
            for (var i = 0; i < world.Forces.Count; i++)
            {
                var force = world.Forces[i];
                if (force != null && force.forceID > 0) choices.Add(force);
            }
        choices.Sort((a, b) => a.forceID.CompareTo(b.forceID));
        AddText(p, "其他门派 · 兑换功绩", new Vector2(540, 415), new Vector2(490, 32), 18);
        if (choices.Count == 0)
        {
            AddText(p, "请先读取存档。", new Vector2(540, 455), new Vector2(490, 34));
            return;
        }

        var selected = 0;
        var current = OtherForceContributionExchangeController.Instance;
        if (current?.exchangeUIPanel != null && current.exchangeUIPanel.activeInHierarchy && current.targetForceData != null)
        {
            var index = choices.FindIndex(f => f.forceID == current.targetForceData.forceID);
            if (index >= 0) selected = index;
        }
        var label = AddText(p, "", new Vector2(610, 454), new Vector2(345, 36), 17);
        var value = AddInput(p, "0", new Vector2(610, 499), new Vector2(195, 36));
        AddText(p, "功绩", new Vector2(540, 499), new Vector2(65, 36));
        void ReadSelection()
        {
            var target = choices[selected];
            label.text = $"{target.GetForceName(true)}（ID {target.forceID}）";
            value.text = target.playerOutForceContribution.ToString("R", CultureInfo.InvariantCulture);
        }
        AddButton(p, "上个", new Vector2(540, 454), new Vector2(65, 36), () =>
        {
            selected = (selected + choices.Count - 1) % choices.Count;
            ReadSelection();
        });
        AddButton(p, "下个", new Vector2(960, 454), new Vector2(65, 36), () =>
        {
            selected = (selected + 1) % choices.Count;
            ReadSelection();
        });
        AddButton(p, "应用功绩", new Vector2(815, 499), new Vector2(210, 36), () =>
        {
            try
            {
                var target = choices[selected];
                var liveWorld = GameController.Instance?.worldData;
                if (world == null || liveWorld == null || liveWorld.Pointer != world.Pointer ||
                    liveWorld.GetForce(target.forceID)?.Pointer != target.Pointer)
                {
                    _status = "存档或门派数据已变化，请重新打开势力页再修改。";
                    RequestPageRebuild();
                    return;
                }
                if (!float.TryParse(value.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) ||
                    !float.IsFinite(amount) || amount < 0 || amount > 100000000)
                {
                    _status = "功绩请输入 0–100000000 之间的有效数字。";
                    return;
                }
                target.playerOutForceContribution = amount;
                ReadSelection();
                _status = $"{target.GetForceName(true)}的兑换功绩已设为 {value.text}，其他门派不变。请在游戏中保存存档。";
                // Refresh only the open exchange panel for this exact faction.
                var exchange = OtherForceContributionExchangeController.Instance;
                if (exchange?.exchangeUIPanel != null && exchange.exchangeUIPanel.activeInHierarchy &&
                    exchange.targetForceData?.Pointer == target.Pointer)
                {
                    try { exchange.RefreshExchangeUI(); }
                    catch (Exception ex) { LongYinTrainerPlugin.Logger.LogWarning("功绩已写入，兑换界面刷新失败：" + ex.Message); }
                }
            }
            catch (Exception ex) { _status = "修改门派功绩失败：" + ex.Message; }
        });
        AddButton(p, "读取当前兑换门派", new Vector2(540, 544), new Vector2(290, 36), () =>
        {
            var exchange = OtherForceContributionExchangeController.Instance;
            var index = exchange?.exchangeUIPanel != null && exchange.exchangeUIPanel.activeInHierarchy && exchange.targetForceData != null
                ? choices.FindIndex(f => f.Pointer == exchange.targetForceData.Pointer) : -1;
            if (index < 0) { _status = "请先打开目标门派的功绩兑换界面。"; return; }
            selected = index;
            ReadSelection();
        });
        AddButton(p, "重读数值", new Vector2(840, 544), new Vector2(185, 36), ReadSelection);
        AddText(p, "逐门派独立修改；切换会重读数值，请先应用。\n此处对应兑换界面的“功绩”，修改后请保存游戏。", new Vector2(540, 589), new Vector2(490, 54), 15);
        ReadSelection();
    }
}
