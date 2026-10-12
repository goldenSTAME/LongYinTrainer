using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _spawnEventRarity = 4;
    private static int? _spawnEventType;
    private static int _spawnEventPage;
    private const int SpawnEventPageSize = 8;
    private static List<int> _spawnEventIds = new();
    private static readonly Button?[] SpawnEventSlots = new Button?[SpawnEventPageSize];
    private static readonly Text?[] SpawnRarityLabels = new Text?[6];
    private static Text? _spawnTypeLabel, _spawnPageLabel;

    private static void RefreshSpawnEventPage(int delta = 0)
    {
        var pages = Math.Max(1, (_spawnEventIds.Count + SpawnEventPageSize - 1) / SpawnEventPageSize);
        _spawnEventPage = Math.Clamp(_spawnEventPage + delta, 0, pages - 1);
        if (_spawnEventType.HasValue && !_spawnEventIds.Contains(_spawnEventType.Value)) _spawnEventType = null;
        if (_spawnTypeLabel != null)
            _spawnTypeLabel.text = "当前类型：" + (_spawnEventType.HasValue
                ? RandomEventController.Instance.GetRandomEventDataBase(_spawnEventType.Value)?.Name() ?? "类型不可用" : "随机类型");
        if (_spawnPageLabel != null)
            _spawnPageLabel.text = $"{_spawnEventPage + 1} / {pages} 页 · {_spawnEventIds.Count} 种大地图奇遇";
        for (var row = 0; row < SpawnEventPageSize; row++)
        {
            var slot = SpawnEventSlots[row];
            if (slot == null) continue;
            var index = _spawnEventPage * SpawnEventPageSize + row;
            var template = index < _spawnEventIds.Count
                ? RandomEventController.Instance.GetRandomEventDataBase(_spawnEventIds[index]) : null;
            slot.gameObject.SetActive(template != null);
            if (template == null) continue;
            var id = _spawnEventIds[index];
            slot.GetComponentInChildren<Text>().text = (_spawnEventType == id ? "✓ " : "") + template.Name() + "  [" + id + "]";
        }
    }

    private static void SelectSpawnEventSlot(int row)
    {
        var index = _spawnEventPage * SpawnEventPageSize + row;
        if (index >= _spawnEventIds.Count) return;
        _spawnEventType = _spawnEventIds[index];
        RefreshSpawnEventPage();
    }

    private static void RefreshSpawnRarityLabels()
    {
        for (var i = 0; i < SpawnRarityLabels.Length; i++)
            if (SpawnRarityLabels[i] != null)
                SpawnRarityLabels[i]!.text = (_spawnEventRarity == i ? "✓ " : "") + RarityName(i);
    }

    // Read the native overworld pool afresh; do not retain IL2CPP event wrappers
    // across loading a different save. Never include mission or world-event pools.
    private static List<int> SpawnableMapEventIds()
    {
        var result = new List<int>();
        var database = RandomEventController.Instance;
        var ids = database?.bigMapRandomEventID;
        if (ids == null) return result;
        foreach (var id in ids)
        {
            var template = database!.GetRandomEventDataBase(id);
            if (template == null || template.isAreaEvent || template.plotData == null ||
                template.plotTargetEvent || template.missionTargetEvent || result.Contains(id)) continue;
            result.Add(id);
        }
        return result;
    }

    private static void BuildNativeEventSpawner()
    {
        var p = NativePageParent;
        AddText(p, "大地图 · 手动生成奇遇", Vector2.zero, new Vector2(720, 36), 22);
        AddText(p, "选择品质", new Vector2(0, 48), new Vector2(160, 30), 18);
        for (var i = 0; i <= 5; i++)
        {
            var rarity = i;
            var button = AddButton(p, "",
                new Vector2(i * 174, 86), new Vector2(165, 38), () =>
                { _spawnEventRarity = rarity; RefreshSpawnRarityLabels(); });
            SpawnRarityLabels[i] = button.GetComponentInChildren<Text>();
        }
        RefreshSpawnRarityLabels();
        _spawnEventIds = SpawnableMapEventIds();
        _spawnTypeLabel = AddText(p, "", new Vector2(0, 140), new Vector2(750, 34), 18);
        AddButton(p, "随机类型", new Vector2(820, 140), new Vector2(225, 36), () =>
        { _spawnEventType = null; RefreshSpawnEventPage(); });

        // Fixed slots retain their objects and click handlers across pagination.
        // Resolve the current row on click instead of capturing its original ID.
        for (var row = 0; row < SpawnEventPageSize; row++)
        {
            var slot = row;
            SpawnEventSlots[row] = AddButton(p, "",
                new Vector2((row % 2) * 540, 188 + (row / 2) * 48), new Vector2(505, 40), () =>
                SelectSpawnEventSlot(slot));
        }
        AddButton(p, "上一页", new Vector2(0, 394), new Vector2(160, 36), () =>
            RefreshSpawnEventPage(-1));
        _spawnPageLabel = AddText(p, "",
            new Vector2(180, 394), new Vector2(470, 36), 16);
        AddButton(p, "下一页", new Vector2(690, 394), new Vector2(160, 36), () =>
            RefreshSpawnEventPage(1));
        AddButton(p, "刷新类型", new Vector2(880, 394), new Vector2(165, 36), () =>
        { _spawnEventIds = SpawnableMapEventIds(); RefreshSpawnEventPage(); });
        AddButton(p, "在主角身边生成一个", new Vector2(0, 451), new Vector2(1045, 44), SpawnMapEventNearPlayer);
        AddText(p, "请在大地图行走状态使用。每次生成一个已发现的原版奇遇，收起面板后点击地图图标前往。\n品质同时决定事件难度；奖励按原版事件规则结算。新奇遇会随正常游戏存档保存，仍有原版有效期。",
            new Vector2(0, 514), new Vector2(1045, 100), 16);
        RefreshSpawnEventPage();
    }

    private static void SpawnMapEventNearPlayer()
    {
        EventData? created = null;
        BigMapController? map = null;
        Il2CppSystem.Collections.Generic.List<EventData>? events = null;
        var registered = false;
        try
        {
            var game = GameController.Instance;
            var world = game?.worldData;
            var player = world?.Player();
            map = BigMapController.Instance;
            if (world == null || player?.bigMapPos == null)
                throw new InvalidOperationException("请先载入存档。");
            if (map == null || !map.gameObject.activeInHierarchy || map.bigmapRoot == null ||
                !map.bigmapRoot.activeInHierarchy || map.playerArmy == null || !map.playerArmy.activeInHierarchy ||
                player.atAreaID >= 0 || BattleController.Instance?.BattleObj?.activeInHierarchy == true)
                throw new InvalidOperationException("请离开城市、结束战斗，回到大地图行走状态。");

            var ids = SpawnableMapEventIds();
            if (ids.Count == 0) throw new InvalidOperationException("大地图奇遇类型尚未载入，请刷新类型。");
            var id = _spawnEventType ?? ids[UnityEngine.Random.Range(0, ids.Count)];
            if (!ids.Contains(id)) throw new InvalidOperationException("所选类型已不可用，请刷新类型。");
            var template = RandomEventController.Instance.GetRandomEventDataBase(id)
                ?? throw new InvalidOperationException("所选奇遇模板已不可用，请刷新类型。");
            created = template.Clone().Cast<EventData>();
            created.difficulty = _spawnEventRarity * 2f;
            if (created.GetEventRareLv() != _spawnEventRarity)
                throw new InvalidOperationException("当前游戏的品质计算规则已变化，未生成奇遇。");

            // Clone preserves plot payload and native expiry. Only initialize the
            // occurrence state, difficulty and position for this new instance.
            created.seen = true;
            created.noticed = false;
            created.hovered = false;
            created.happened = false;
            created.plotTargetEvent = false;
            created.missionTargetEvent = false;
            created.randomSeed = UnityEngine.Random.Range(1, int.MaxValue);
            created.isAreaEvent = false;
            created.resourcePointID = -1;
            var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var position = player.bigMapPos.ToVector3() + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * .08f;
            position = map.LimitMapPos(position, 1f);
            created.bigMapPos = new BigMapPos(position);
            created.nearAreaID = map.GetNearAreaID(created.bigMapPos);
            // Native GetPosText indexes DirectionName even for a vague location.
            created.nearAreaDirection = 0;
            created.inaccuracyPosText = true;
            events = world.BigMapRandomEventDatas ?? throw new InvalidOperationException("存档奇遇列表不可用。");
            events.Add(created);
            registered = true;
            map.CreateBigMapRandomEventIcon(created);
            var found = false;
            foreach (var icon in map.bigmapRandomEventIcons)
            {
                if (icon == null) continue;
                var controller = icon.GetComponent<BigMapRandomEventController>();
                if (controller?.bigMapRandomEventData?.Pointer != created.Pointer) continue;
                controller.Init();
                found = true;
                break;
            }
            if (!found) throw new InvalidOperationException("原生奇遇图标未创建成功。");
            _status = $"已在主角附近生成：{created.Name()} · {RarityName(_spawnEventRarity)}。收起面板后点击奇遇图标前往。";
            LongYinTrainerPlugin.Logger.LogInfo($"Spawned map event id={id}, rarity={created.GetEventRareLv()}, difficulty={created.difficulty}, pos={created.bigMapPos.x},{created.bigMapPos.y}");
        }
        catch (Exception ex)
        {
            // Roll back just this occurrence and its icons, never other events.
            if (registered && created != null)
            {
                events?.Remove(created);
                var icons = map?.bigmapRandomEventIcons;
                if (icons != null)
                    for (var i = icons.Count - 1; i >= 0; i--)
                    {
                        var icon = icons[i];
                        if (icon?.GetComponent<BigMapRandomEventController>()?.bigMapRandomEventData?.Pointer != created.Pointer) continue;
                        icons.RemoveAt(i);
                        UnityEngine.Object.Destroy(icon);
                    }
            }
            _status = "生成奇遇失败：" + ex.Message;
            LongYinTrainerPlugin.Logger.LogWarning("Map event spawn: " + ex);
        }
    }
}
