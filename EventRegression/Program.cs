using UnityEngine;
using UnityEngine.UI;

TrainerBehaviour.RunTests();

public static partial class TrainerBehaviour
{
    private static string _status = "";
    private static object NativePageParent = new();
    private static int uiObjects;
    private static readonly Dictionary<string,Action> uiActions = new();
    private static Text AddText(object p, string s, Vector2 pos, Vector2 size, int font = 0) { uiObjects++; return new() {text=s}; }
    private static Button AddButton(object p, string s, Vector2 pos, Vector2 size, Action action)
    { uiObjects++; if(s.Length>0) uiActions[s]=action; return new() {label=new() {text=s}, action=action}; }
    private static string RarityName(int i) => i.ToString();
    public static void RunTests()
    {
        void Check(bool value, string name)
        { if (!value) throw new Exception(name + " / " + _status); Console.WriteLine("PASS " + name); }
        var database = RandomEventController.Instance;
        database.templates[10] = new EventData { id = 10, eventName = "A", leftTime = 40, difficulty = 3 };
        database.templates[20] = new EventData { id = 20, eventName = "B", leftTime = 30 };
        database.templates[30] = new EventData { id = 30, isAreaEvent = true };
        database.templates[40] = new EventData { id = 40, missionTargetEvent = true };
        database.templates[50] = new EventData { id = 50, plotData = null };
        database.bigMapRandomEventID.AddRange(new[] {10, 20, 30, 40, 50, 10});
        Check(SpawnableMapEventIds().SequenceEqual(new[] {10, 20}), "native pool excludes area/mission/missing plot events and duplicates");
        var world = GameController.Instance.worldData!;
        var map = BigMapController.Instance;
        var existing = new EventData { id = 99 };
        world.BigMapRandomEventDatas.Add(existing);
        _spawnEventType = 20;
        for (var rarity = 0; rarity <= 5; rarity++)
        {
            _spawnEventRarity = rarity;
            var before = world.BigMapRandomEventDatas.Count;
            SpawnMapEventNearPlayer();
            var created = world.BigMapRandomEventDatas.Last();
            Check(world.BigMapRandomEventDatas.Count == before + 1 && created.id == 20 && created.GetEventRareLv() == rarity,
                "exactly one event of selected type and rarity " + rarity);
            Check(created.Pointer != database.templates[20].Pointer && created.seen && !created.happened && created.leftTime == 30 &&
                created.bigMapPos.Distance(world.player.bigMapPos) > 0 && created.bigMapPos.Distance(world.player.bigMapPos) <= 8.01f &&
                created.nearAreaDirection >= 0 && map.bigmapRandomEventIcons.Last().GetComponent<BigMapRandomEventController>()!.inited,
                "independent discovered event, nearby position, expiry and initialized icon " + rarity);
        }
        Check(world.BigMapRandomEventDatas[0] == existing && database.templates[20].difficulty == 0 && !database.templates[20].seen,
            "existing events and template unchanged");
        _spawnEventType = null;
        SpawnMapEventNearPlayer();
        Check(new[] {10,20}.Contains(world.BigMapRandomEventDatas.Last().id), "random mode draws eligible type");
        var eventCount = world.BigMapRandomEventDatas.Count;
        var iconCount = map.bigmapRandomEventIcons.Count;
        world.player.atAreaID = 1;
        SpawnMapEventNearPlayer();
        Check(world.BigMapRandomEventDatas.Count == eventCount, "city state rejected without mutation");
        world.player.atAreaID = -1;
        BattleController.Instance.BattleObj.activeInHierarchy = true;
        SpawnMapEventNearPlayer();
        Check(world.BigMapRandomEventDatas.Count == eventCount, "battle state rejected without mutation");
        BattleController.Instance.BattleObj.activeInHierarchy = false;
        GameController.Instance.worldData = null;
        SpawnMapEventNearPlayer();
        Check(world.BigMapRandomEventDatas.Count == eventCount, "unloaded save rejected");
        GameController.Instance.worldData = world;
        _spawnEventType = 123456;
        SpawnMapEventNearPlayer();
        Check(world.BigMapRandomEventDatas.Count == eventCount, "stale selected type rejected");
        _spawnEventType = 10;
        map.throwAfterIcon = true;
        SpawnMapEventNearPlayer();
        Check(world.BigMapRandomEventDatas.Count == eventCount && map.bigmapRandomEventIcons.Count == iconCount && world.BigMapRandomEventDatas[0] == existing,
            "partial icon failure rolls back only newly added event and icon");
        map.throwAfterIcon = false;
        EventData.wrongRarity = true;
        SpawnMapEventNearPlayer();
        Check(world.BigMapRandomEventDatas.Count == eventCount && map.bigmapRandomEventIcons.Count == iconCount,
            "changed native rarity rule rejected before mutation");
        EventData.wrongRarity = false;
        world.player.bigMapPos = new BigMapPos { x = 999, y = 999 };
        SpawnMapEventNearPlayer();
        var edgeEvent = world.BigMapRandomEventDatas.Last();
        Check(Math.Abs(edgeEvent.bigMapPos.x) <= 1000 && Math.Abs(edgeEvent.bigMapPos.y) <= 1000,
            "spawn coordinates remain within map boundary");
        BuildNativeEventSpawner();
        for(var id=60;id<69;id++) { database.templates[id]=new() {id=id,eventName="Type"+id}; database.bigMapRandomEventID.Add(id); }
        uiActions["刷新类型"]();
        var retainedSlot=SpawnEventSlots[0];
        var retainedRarity=SpawnRarityLabels[5];
        var retainedObjects=uiObjects;
        _spawnEventRarity=5;
        _spawnEventType=20;
        uiActions["下一页"]();
        Check(_spawnEventPage==1 && uiObjects==retainedObjects && ReferenceEquals(retainedSlot,SpawnEventSlots[0]) &&
            ReferenceEquals(retainedRarity,SpawnRarityLabels[5]) && _spawnEventRarity==5 && _spawnEventType==20,
            "next page reuses all UI objects and retains rarity/type selections");
        SpawnEventSlots[0]!.action();
        Check(_spawnEventType==66, "reused row click resolves current page ID rather than previous page ID");
        Check(!SpawnEventSlots[3]!.gameObject.activeInHierarchy, "last page hides unused rows");
        uiActions["下一页"]();
        Check(_spawnEventPage==1 && uiObjects==retainedObjects, "next at final page is stable");
        uiActions["上一页"]();
        Check(_spawnEventPage==0 && SpawnEventSlots[3]!.gameObject.activeInHierarchy && uiObjects==retainedObjects,
            "previous page restores rows without UI rebuild");
        uiActions["随机类型"]();
        Check(_spawnEventType==null && uiObjects==retainedObjects, "random selection updates in place");
        Console.WriteLine("All event spawning regression checks passed (mock game; native runtime requires in-game verification).");
    }
}

public class EventData
{
    private static int next;
    public IntPtr Pointer = (IntPtr)(++next);
    public int id, resourcePointID, nearAreaID, nearAreaDirection, randomSeed, leftTime;
    public string eventName = "";
    public bool isAreaEvent, plotTargetEvent, missionTargetEvent, seen, noticed, hovered, happened, inaccuracyPosText;
    public float difficulty;
    public object? plotData = new();
    public BigMapPos bigMapPos = new();
    public static bool wrongRarity;
    public int GetEventRareLv() => wrongRarity ? -1 : (int)(MathF.Round(difficulty) * .5f);
    public string Name() => eventName;
    public EventData Clone() => new() { id=id, eventName=eventName, difficulty=difficulty, leftTime=leftTime, plotData=new() };
    public T Cast<T>() => (T)(object)this;
}
public class BigMapPos
{
    public float x, y;
    public BigMapPos() { }
    public BigMapPos(Vector3 pos) { x = pos.x * 100; y = pos.y * 100; }
    public Vector3 ToVector3() => new(x * .01f, y * .01f, 0);
    public float Distance(BigMapPos p) => MathF.Sqrt((x-p.x)*(x-p.x)+(y-p.y)*(y-p.y));
}
public class HeroData { public BigMapPos bigMapPos = new(); public int atAreaID = -1; }
public class World
{
    public HeroData player = new();
    public Il2CppSystem.Collections.Generic.List<EventData> BigMapRandomEventDatas = new();
    public HeroData Player() => player;
}
public class GameController { public static GameController Instance = new(); public World? worldData = new(); }
public class BattleController { public static BattleController Instance = new(); public GameObject BattleObj = new() {activeInHierarchy=false}; }
public class RandomEventController
{
    public static RandomEventController Instance = new();
    public List<int> bigMapRandomEventID = new();
    public Dictionary<int,EventData> templates = new();
    public EventData? GetRandomEventDataBase(int id) => templates.GetValueOrDefault(id);
}
public class BigMapRandomEventController
{
    public EventData? bigMapRandomEventData;
    public bool inited;
    public void Init() => inited = true;
}
public class BigMapController
{
    public static BigMapController Instance = new();
    public GameObject gameObject = new(), bigmapRoot = new(), playerArmy = new();
    public List<GameObject> bigmapRandomEventIcons = new();
    public bool throwAfterIcon;
    public int GetNearAreaID(BigMapPos p) => 1;
    public Vector3 LimitMapPos(Vector3 p, float scale) => new(Math.Clamp(p.x,-10,10),Math.Clamp(p.y,-10,10),p.z);
    public void CreateBigMapRandomEventIcon(EventData data)
    {
        bigmapRandomEventIcons.Add(new() { controller = new BigMapRandomEventController { bigMapRandomEventData=data } });
        if (throwAfterIcon) throw new InvalidOperationException("injected icon failure");
    }
}
public static class LongYinTrainerPlugin { public static Log Logger = new(); }
public class Log { public void LogInfo(string s) { } public void LogWarning(string s) { } }
namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> { } }
namespace UnityEngine
{
    public class GameObject
    {
        public bool activeInHierarchy = true;
        public object? controller;
        public T? GetComponent<T>() where T:class => controller as T;
        public void SetActive(bool active) => activeInHierarchy=active;
    }
    public static class Object { public static void Destroy(GameObject o) { } }
    public struct Vector2 { public Vector2(float x, float y) { } public static Vector2 zero => new(); }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
        public static Vector3 operator +(Vector3 a,Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a,float v) => new(a.x*v,a.y*v,a.z*v);
    }
    public static class Mathf { public const float PI = MathF.PI; public static float Cos(float a)=>MathF.Cos(a); public static float Sin(float a)=>MathF.Sin(a); }
    public static class Random
    {
        private static System.Random rng = new(12);
        public static int Range(int a,int b) => rng.Next(a,b);
        public static float Range(float a,float b) => a+(b-a)*(float)rng.NextDouble();
    }
}
namespace UnityEngine.UI
{
    public class Text { public string text=""; }
    public class Button
    {
        public UnityEngine.GameObject gameObject=new();
        public Text label=new();
        public Action action=()=>{};
        public T GetComponentInChildren<T>() => (T)(object)label;
    }
}
