using System.Reflection;
using HList = Il2CppSystem.Collections.Generic.List<HeroData>;
using Teams = Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<HeroData>>;

static class Program
{
    static object? Call(string name, params object?[] args) => typeof(BuildingCombat).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
    static HeroData Hero(int id, int force = 2, int area = 10) => new() { heroID = id, belongForceID = force, area = area };
    static (HeroData player, HeroData enemy, HeroData friend, HeroData same, object?[] args) Setup(string callback = "DeathFightInteractHeroResult", bool reversed = false)
    {
        Call("Reset"); BuildingCombat.Enabled = new() { Value = true };
        GameController.Instance.worldData = new WorldData();
        var w = GameController.Instance.worldData;
        var p = Hero(0, 1); var e = Hero(1); var f = Hero(2, 3); var s = Hero(3);
        e.haters.Add(0); e.friends.Add(2);
        var ally = Hero(4); p.teamMates.Add(4);
        var dead = Hero(5); dead.dead = true;
        var prisoner = Hero(6); prisoner.inPrison = true;
        var far = Hero(7, 2, 11); var stranger = Hero(8, 4);
        foreach (var h in new[] {p,e,f,s,ally,dead,prisoner,far,stranger}) w.heroes.Add(h.heroID,h);
        AreaController.Instance.areaData = new() { areaID = 10, insideHeros = new() {1,2,3,4,5,6,7,8} };
        PlotController.Instance = new() { targetInteractHero = e };
        var members = reversed ? new Teams {new() {e},new() {p}} : new Teams {new() {p},new() {e}};
        var support = reversed ? new Teams {new() {f},new()} : new Teams {new(),new() {f}};
        BattleController.Instance = new() { winTeamID = reversed ? 1 : 0, playerTeam = new() { ID = reversed ? 1 : 0 } };
        AIController.Instance.departed.Clear();
        object?[] args = { BattleType.DeathFight, members, support, callback, 2 };
        return(p,e,f,s,args);
    }
    static void Enter(HeroData h, int team = 1, bool success = true) => Call("Joined", h, new BattleTeam {ID=team}, success ? new UnityEngine.GameObject() : null);
    static void Win(string callback="DeathFightInteractHeroResult")
    {
        Call("Ending", BattleController.Instance);
        Call("ResultBeginning", PlotController.Instance, typeof(PlotController).GetMethod(callback)!);
        // Simulate the original result showing its menu exactly once. The mod
        // must replace its data, not schedule another ChangePlot in a postfix.
        object?[] args = { PlotController.Instance, new SinglePlotData("native", new(), PlotTargetHeroType.HeroID, "1") };
        Call("ReplaceFirstMenu", args);
        var replaced = ((SinglePlotData)args[1]!).choices.Count > 0;
        if (replaced) PlotController.Instance.ChangePlot((SinglePlotData)args[1]!);
        Call("ResultFinished");
    }
    static bool Finish(string name="DeathFightHeroFinish") => (bool)Call("Finish",PlotController.Instance,typeof(PlotController).GetMethod(name)!)!;
    public static void Main()
    {
        var nullCase=Setup(); ((Teams)nullCase.args[2]!)[0]=null!;
        Call("Prepare",nullCase.args);
        Check(((Teams)nullCase.args[1]!)[1].Count==3,"real assault None~Hero: null player support still expands enemies");
        nullCase=Setup("NpcAttackPlayerResult"); nullCase.args[0]=BattleType.HardFight;
        ((Teams)nullCase.args[2]!)[1]=null!;
        Call("Prepare",nullCase.args);
        Check(((Teams)nullCase.args[1]!)[1].Count==3,"real provocation Hero~None: null enemy support still expands enemies");
        nullCase=Setup(); nullCase.args[2]=null; nullCase.args[4]=-1;
        Call("Prepare",nullCase.args);
        Check(((Teams)nullCase.args[1]!)[1].Count==3 && (int)nullCase.args[4]! == -1,"absent support container; preserve native unlimited cap");
        nullCase=Setup(); ((Teams)nullCase.args[2]!)[0]=null!; ((Teams)nullCase.args[2]!)[1]=null!;
        GameController.Instance.worldData.GetHero(8).haters.Add(0);
        GameController.Instance.worldData.GetHero(3).hide=true;
        Call("Prepare",nullCase.args);
        Check(((Teams)nullCase.args[1]!)[1].Select(h=>h.heroID).SequenceEqual(new[]{1,2,8}),"include independent local hater; exclude hidden hero; both support entries null");
        nullCase=Setup(); ((Teams)nullCase.args[2]!)[1].Add(null!);
        AreaController.Instance.areaData.insideHeros.Add(999);
        GameController.Instance.worldData.heroes.Add(999,null!);
        GameController.Instance.worldData.Player().teamMates=null!;
        Call("Prepare",nullCase.args);
        Check(((Teams)nullCase.args[1]!)[1].Any(h=>h.heroID==3),"null roster entries and null player teammate list are tolerated");
        nullCase=Setup(); nullCase.enemy.haters.Clear(); var unchanged=nullCase.args[1];
        Call("Prepare",nullCase.args);
        Check(ReferenceEquals(unchanged,nullCase.args[1]),"non-enemy interactions remain native");
        var x=Setup(); Call("Prepare",x.args);
        var enemies=((Teams)x.args[1]!)[1];
        Check(enemies.Select(h=>h.heroID).SequenceEqual(new[]{1,2,3}),"select local relations; exclude player team, dead, prisoner, remote, stranger");
        Check(((Teams)x.args[2]!)[1].Count==0 && (int)x.args[4]! ==3,"promote and deduplicate support; increase explicit cap");
        Enter(x.enemy); Enter(x.friend); Enter(x.friend); Enter(x.same,success:false); Enter(x.player,0); Win();
        Check(PlotController.Instance.menus.Count==1 && PlotController.Instance.targetInteractHero.heroID==1,"first menu belongs to main enemy");
        Check(!Finish() && PlotController.Instance.targetInteractHero.heroID==2,"finish advances to actual participant");
        Check(Finish() && AIController.Instance.departed.SequenceEqual(new[]{1}),"final native finish runs once; failed entry/duplicates excluded");
        Check(PlotController.Instance.menus.All(m=>m.choices.Count==4),"assault menu has exactly four mutually exclusive actions");
        var tips=PlotController.Instance.menus[0].choices;
        Check(tips[0]=="抢夺银两;DeathFightRobHeroMoney;;;♦抢夺对方约一半的银两\n♦根据抢夺数额增加恶名" &&
              tips[1]=="抢夺财物;DeathFightRobHero;;;♦抢夺对方一件物品\n♦根据物品价值增加恶名" &&
              tips[2]=="出手伤人;DeathFightHurtHero;;;♦重伤对方\n♦增加10恶名","tooltip text matches native literals including line breaks");
        Check(PlotController.Instance.menus.Count==2,"two participants schedule exactly two menus, no duplicate first render");
        x=Setup("NpcAttackPlayerResult",true); Call("Prepare",x.args); Enter(x.enemy,0);Enter(x.friend,0);Win("NpcAttackPlayerResult");
        Check(PlotController.Instance.menus[0].choices.Count==2 && !Finish("NpcAttackPlayerFinish"),"defensive victory and reversed team order");
        Call("Hidden"); Check(Finish("NpcAttackPlayerFinish"),"closed dialogue cannot retain settlement queue");
        x=Setup();Call("Prepare",x.args);Enter(x.enemy);BattleController.Instance.winTeamID=1;Win();
        Check(PlotController.Instance.menus.Count==0,"defeat produces no settlement");
        x=Setup();x.args[0]=BattleType.StudyFight;var old=x.args[1];Call("Prepare",x.args);
        Check(ReferenceEquals(old,x.args[1]),"sparring is untouched");
        x=Setup("StoryBattle");old=x.args[1];Call("Prepare",x.args);Check(ReferenceEquals(old,x.args[1]),"story callback is untouched");
        x=Setup();BuildingCombat.Enabled.Value=false;old=x.args[1];Call("Prepare",x.args);Check(ReferenceEquals(old,x.args[1]),"disabled option is untouched");
        x=Setup();Call("Prepare",x.args);Enter(x.enemy);GameController.Instance.worldData=new();Win();Check(PlotController.Instance.menus.Count==0,"save replacement clears captured participants");
        x=Setup();Call("Prepare",x.args);Enter(x.enemy);Enter(x.friend);Win();x.friend.dead=true;
        Check(!Finish() && PlotController.Instance.closed==1 && AIController.Instance.departed.SequenceEqual(new[]{1}),"invalid pending target closes once without repeated departure");
        x=Setup();Call("Prepare",x.args);Enter(x.enemy);Enter(x.friend);Win();PlotController.Instance.targetInteractHero=x.same;
        Check(Finish() && AIController.Instance.departed.Count==0,"unexpected interaction target cancels queue");
        Console.WriteLine($"{checks} checks passed (mock native objects; not an in-game test).");
    }
}

// Minimal engine doubles: the production implementation above is compiled
// unchanged. These tests exercise selection and queue lifecycle, not Harmony
// detours, native dialogue dispatch, placement, or IL2CPP object lifetime.
namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> { } }
namespace BepInEx.Configuration {
    public class ConfigEntry<T> { public T Value=default!; }
    public class ConfigFile { public ConfigEntry<T> Bind<T>(string s,string k,T v,string d)=>new(){Value=v}; }
}
namespace HarmonyLib {
    public class Harmony { public void Patch(MethodBase b,HarmonyMethod? p,HarmonyMethod? q){} }
    public class HarmonyMethod { public HarmonyMethod(Type t,string s){} }
    public static class AccessTools { public static MethodInfo? Method(Type t,string n,Type[] a)=>t.GetMethod(n,a); }
}
namespace UnityEngine { public class GameObject {} }
public enum BattleType { StudyFight, HardFight, DeathFight }
public enum PlotTargetHeroType { HeroID }
public class BattleMapTypeData {}
public class GridUnitData {}
public class HeroData {
    public int heroID,belongForceID,area; public bool hide,dead,inPrison,isTempHero; public string heroName="Test";
    public System.Collections.Generic.List<int> teamMates=new();
    public HashSet<int> haters=new(),friends=new();
    public int GetAreaID(bool b)=>area;
    public bool HaveHater(int id)=>haters.Contains(id);
    public bool SameForce(HeroData h)=>belongForceID==h.belongForceID;
    public bool HaveRelationBetterThanFriend(int id,bool a,bool b)=>friends.Contains(id);
}
public class WorldData {
    public object Pointer=new(); public Dictionary<int,HeroData> heroes=new();
    public HeroData Player()=>heroes[0]; public HeroData GetHero(int id)=>heroes[id];
}
public class GameController { public static GameController Instance=new(); public WorldData worldData=new(); }
public class AreaData {
    public int areaID; public System.Collections.Generic.List<int> insideHeros=new();
    public HeroData GetInsideHero(int i)=>GameController.Instance.worldData.GetHero(insideHeros[i]);
}
public class AreaController { public static AreaController Instance=new(); public AreaData areaData=new(); }
public class BattleTeam { public int ID; }
public class BattleUnit {}
public class BattleController {
    public static BattleController Instance=new(); public int winTeamID; public BattleTeam playerTeam=new();
    public Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<TeamMemPrepareData>> teamMemPrepareData=new();
    public BattleTeam GetPlayerTeam()=>playerTeam;
}
public class TeamMemPrepareData { public bool enterBattle; }
public class SinglePlotData {
    public Il2CppSystem.Collections.Generic.List<string> choices;
    public SinglePlotData(string t,Il2CppSystem.Collections.Generic.List<string> c,PlotTargetHeroType p,string id){choices=c;}
}
public class PlotController {
    public static PlotController Instance=new();public HeroData targetInteractHero=new();
    public System.Collections.Generic.List<SinglePlotData> menus=new(); public int closed;
    public void ChangePlot(SinglePlotData p)=>menus.Add(p);
    public void HideInteractUIOneDay()=>closed++;public void HideInteractUI()=>closed++;
    public void DeathFightInteractHeroResult(){} public void NpcAttackPlayerResult(){}
    public void DeathFightHeroFinish(){} public void NpcAttackPlayerFinish(){}
}
public class AIController {
    public static AIController Instance=new();public System.Collections.Generic.List<int> departed=new();
    public void HeroLoseFightOnBigMap(HeroData h)=>departed.Add(h.heroID);
}
public class GameDataController {}
public static class LongYinTrainerPlugin { public static TestLog Logger=new(); }
public class TestLog { public void LogInfo(string s){} public void LogWarning(string s)=>Console.WriteLine(s); public void LogError(string s)=>throw new Exception(s); }
