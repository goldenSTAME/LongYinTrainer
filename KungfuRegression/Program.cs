using System.Reflection;

static class Program
{
    static int checks;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS " + label);
        checks++;
    }

    static KungfuSkillLvData Skill(int id, int type = 0) => new()
    {
        skillID = id, Type = type, lv = 7, extraAddData = new object(),
        equipUseSpeAddValue = 99, damageUseSpeAddValue = 99,
        selfUseSpeAddValue = 99, enemyUseSpeAddValue = 99
    };

    static object? Call(string name, params object?[] args) => typeof(TrainerBehaviour)
        .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);

    static void Main()
    {
        for (var type = 0; type < 9; type++)
        {
            var skill = Skill(900000 + type, type);
            var extra = skill.extraAddData;
            var hero = new HeroData { kungfuSkills = new() { skill } };
            if (type == 0) hero.internalSkill = skill;
            else if (type == 1) hero.dodgeSkill = skill;
            else if (type == 2) hero.uniqueSkill = skill;
            else hero.attackSkills.Add(skill);
            hero.Active = skill;
            var slots = hero.attackSkills.ToArray();
            var count = KungfuSkillRefresh.RefreshHero(hero, skill.skillID);
            Check(count == 1 && skill.EquipResets == 1 && skill.UseResets == 1 &&
                hero.Counts == 1 && hero.Total == 107 && skill.skillIconDirty &&
                skill.lv == 7 && ReferenceEquals(extra, skill.extraAddData) &&
                hero.attackSkills.SequenceEqual(slots), $"category {type}: native caches and totals refresh once; level, breakthrough and slots preserved");
        }
        var empty = Skill(1);
        empty.Empty = true;
        KungfuSkillRefresh.RefreshSkill(empty);
        Check(empty.equipUseSpeAddValue == 0 && empty.damageUseSpeAddValue == 0 &&
            empty.selfUseSpeAddValue == 0 && empty.enemyUseSpeAddValue == 0,
            "removing final effect clears all four costs despite native early returns");

        foreach (var slot in new[] { "internal", "dodge", "unique", "attack", "active" })
        {
            var skill = Skill(5);
            var hero = new HeroData { kungfuSkills = null };
            switch (slot)
            {
                case "internal": hero.internalSkill = skill; break;
                case "dodge": hero.dodgeSkill = skill; break;
                case "unique": hero.uniqueSkill = skill; break;
                case "attack": hero.attackSkills.Add(skill); break;
                case "active": hero.Active = skill; break;
            }
            Check(KungfuSkillRefresh.RefreshHero(hero, 5) == 1 && skill.EquipResets == 1 && hero.Counts == 1,
                $"{slot} slot refreshes even without a learned list");
        }
        var other = Skill(88);
        var unrelated = new HeroData { kungfuSkills = new() { other } };
        Check(KungfuSkillRefresh.RefreshHero(unrelated, 99) == 0 && other.EquipResets == 0 && unrelated.Counts == 0,
            "unrelated heroes are neither mutated nor recounted");
        var learned = Skill(7);
        var equippedClone = Skill(7);
        var cloneHero = new HeroData { kungfuSkills = new() { learned }, internalSkill = equippedClone };
        Check(KungfuSkillRefresh.RefreshHero(cloneHero, 7) == 2 && learned.EquipResets == 1 && equippedClone.EquipResets == 1,
            "distinct learned and equipped native instances both refresh");

        TrainerBehaviour.TestReset();
        TrainerBehaviour.AddRecord(0, true);
        var original = Skill(0);
        var hero0 = new HeroData { internalSkill = original };
        TrainerBehaviour.RefreshEditedKungfuHeroCaches(hero0);
        TrainerBehaviour.RefreshEditedKungfuHeroCaches(hero0);
        Check(original.EquipResets == 1 && hero0.Counts == 0, "load hook handles original ID zero without recursive recounts");
        TrainerBehaviour.PrepareEditedKungfuLoad();
        TrainerBehaviour.RefreshEditedKungfuHeroCaches(hero0);
        Check(original.EquipResets == 2, "loading another save invalidates refresh marks");
        var failing = Skill(0);
        failing.ThrowReset = true;
        var failingHero = new HeroData { internalSkill = failing };
        TrainerBehaviour.RefreshEditedKungfuHeroCaches(failingHero);
        failing.ThrowReset = false;
        TrainerBehaviour.RefreshEditedKungfuHeroCaches(failingHero);
        Check(failing.EquipResets == 1 && LongYinTrainerPlugin.Logger.Warnings == 1,
            "failed cache reset is reported and retried, not marked successful");

        var definitions = GameDataController.Instance!.kungfuSkillDataBase;
        definitions[0] = new KungfuSkillData { skillID = 0, type = 0, name = "Original" };
        Call("CaptureOriginalKungfuSources", GameDataController.Instance);
        definitions[0].name = "Edited";
        Call("CaptureOriginalKungfuSources", GameDataController.Instance);
        Check(TrainerBehaviour.Source(0).name == "Original", "repeated saves preserve the untouched original donor");
        GameDataController.Instance.kungfuSkillDataBase = new() { [0] = new() { name = "Reloaded" } };
        Call("CaptureOriginalKungfuSources", GameDataController.Instance);
        Check(TrainerBehaviour.Source(0).name == "Reloaded", "replacing native database invalidates donor snapshots");
        GameController.Instance!.worldData.Heros.Add(hero0);
        GameController.Instance.worldData.Heros.Add(hero0);
        BattleController.Instance!.activeUnitList.Add(new() { heroData = hero0 });
        Check(((IEnumerable<HeroData>)Call("KungfuHeroes")!).Count() == 1, "world/player/battle roster duplicates are deduplicated");
        Check((bool)Call("HasEquippedKungfu", hero0, 0)!, "utility slots qualify for native battle preview");
        var category = TrainerBehaviour.Record(0, true, 4);
        var blocked = false;
        try { Call("ValidateKungfuCategoryChange", GameDataController.Instance, category); }
        catch (TargetInvocationException e) { blocked = e.InnerException is InvalidOperationException; }
        Check(blocked, "changing equipped category cannot leave a skill in the wrong native slot");
        hero0.internalSkill = null;
        Call("ValidateKungfuCategoryChange", GameDataController.Instance, category);
        Check(true, "unequipped category can change");
        foreach (var (id, originalMode, expected) in new[] { (0,true,true), (899999,true,true), (900000,true,false),
            (900000,false,true), (999999,false,true), (-1,true,false), (0,false,false), (1000000,false,false) })
            Check((bool)Call("IsEditableKungfuRecord", TrainerBehaviour.Record(id, originalMode))! == expected,
                $"ID/mode validation: {id}/{originalMode}");
        TrainerBehaviour.TestReset();
        for (var type = 0; type < 9; type++)
        {
            var id = 100 + type;
            GameDataController.Instance!.kungfuSkillDataBase[id] = new KungfuSkillData { skillID = id, type = type, name = "native" };
            Call("LoadOriginalKungfuDraft", id);
            Check(TrainerBehaviour.DraftCategoryMatches(id, type), $"original editor type {type}: correct category before donor guard, original ID retained");
        }
        Console.WriteLine($"{checks} checks passed.");
    }
}

// Native calls are simulated here; the linked production code is exercised.
// These tests verify integration and invalidation, not the native game's math.
public class KungfuSkillLvData
{
    public IntPtr Pointer = Identity.Next();
    public int skillID, Type, lv, EquipResets, UseResets;
    public object? extraAddData;
    public float equipUseSpeAddValue, damageUseSpeAddValue, selfUseSpeAddValue, enemyUseSpeAddValue;
    public bool skillIconDirty, Empty, ThrowReset;
    public void ResetSpeEquipData()
    {
        if (ThrowReset) throw new InvalidOperationException("test reset failure");
        EquipResets++;
        if (!Empty) equipUseSpeAddValue = 100 + lv;
    }
    public void ResetSpeUseData()
    {
        UseResets++;
        if (!Empty) { damageUseSpeAddValue = 2; selfUseSpeAddValue = 3; enemyUseSpeAddValue = 4; }
    }
}
public class HeroData
{
    public IntPtr Pointer = Identity.Next();
    public int heroID, Counts;
    public float Total;
    public bool heroDetailDirty;
    public List<KungfuSkillLvData>? kungfuSkills = new();
    public List<KungfuSkillLvData> attackSkills = new();
    public KungfuSkillLvData? internalSkill, dodgeSkill, uniqueSkill, Active;
    public KungfuSkillLvData? GetNowActiveSkill() => Active;
    public void CheckHeroDetailDirty(bool force)
    {
        Counts++;
        Total = (internalSkill ?? dodgeSkill ?? uniqueSkill ?? attackSkills.FirstOrDefault() ?? Active)?.equipUseSpeAddValue ?? 0;
        heroDetailDirty = false;
    }
}
public class KungfuSkillData
{
    public int skillID, type;
    public string name = "", describe = "";
}
public class Definitions : Dictionary<int, KungfuSkillData> { public IntPtr Pointer = Identity.Next(); }
public class GameDataController
{
    public static GameDataController? Instance = new();
    public Definitions kungfuSkillDataBase = new();
}
public class WorldData { public List<HeroData> Heros = new(); }
public class GameController
{
    public static GameController? Instance = new();
    public WorldData worldData = new();
}
public class BattleUnit { public HeroData? heroData; }
public class BattleController
{
    public static BattleController? Instance = new();
    public List<BattleUnit> activeUnitList = new();
}
static class Identity { static int id; public static IntPtr Next() => new(++id); }
static class LongYinTrainerPlugin
{
    public static TestLogger Logger = new();
    public class TestLogger { public int Warnings; public void LogWarning(string value) => Warnings++; }
}
namespace Il2CppInterop.Runtime { }

public static partial class TrainerBehaviour
{
    private const int CustomKungfuIdMin = 900000, CustomKungfuIdMax = 999999;
    private static CustomKungfuStore _customKungfuStore = new();
    private static bool _customKungfuStoreLoadFailed;
    private static IntPtr _customKungfuRegisteredDatabasePointer;
    private static CustomKungfuRecord? _customDraft;
    private static int _customTab;
    private static string _customEffectEditorField = "", _customBasicChoiceField = "", _customStatus = "";
    private static HeroData? Player => GameController.Instance?.worldData.Heros.FirstOrDefault();
    private sealed class CustomKungfuStore { public List<CustomKungfuRecord> Skills = new(); }
    private sealed class CustomKungfuRecord
    {
        public int Id, Type, BaseDonorId;
        public bool EditOriginal;
        public string Name = "", Description = "";
    }
    public static bool DraftCategoryMatches(int id, int type) => _customDraft?.Type == type && _customDraft.Id == id && _customDraft.BaseDonorId == id && _customDraft.EditOriginal;
    public static void TestReset()
    {
        ResetOriginalKungfuSources();
        _customKungfuStore = new();
        _customKungfuStoreLoadFailed = false;
        GameController.Instance = new(); BattleController.Instance = new(); GameDataController.Instance = new();
    }
    public static object Record(int id, bool original, int type = 0) => new CustomKungfuRecord { Id = id, EditOriginal = original, Type = type };
    public static void AddRecord(int id, bool original) => _customKungfuStore.Skills.Add((CustomKungfuRecord)Record(id, original));
    public static KungfuSkillData Source(int id) => OriginalKungfuSources[id];
    private static void CopyKungfuDefinition(KungfuSkillData source, KungfuSkillData target)
    { target.skillID = source.skillID; target.name = source.name; target.type = source.type; }
    private static KungfuSkillData? OriginalKungfu(GameDataController database, int id)
        => OriginalKungfuSources.TryGetValue(id, out var value) ? value : database.kungfuSkillDataBase.GetValueOrDefault(id);
    private static CustomKungfuRecord? FindSavedCustomKungfu(int id) => _customKungfuStore.Skills.Find(record => record.Id == id);
    private static CustomKungfuRecord CloneCustomKungfuRecord(CustomKungfuRecord record) => new()
        { Id = record.Id, EditOriginal = record.EditOriginal, Type = record.Type, Name = record.Name, Description = record.Description };
    private static CustomKungfuRecord NewCustomKungfuDraft(int? category = null) => new() { Type = category ?? 0 };
    private static bool ApplyFullKungfuDonor(int id, bool scalars) => _customDraft?.Type == OriginalKungfu(GameDataController.Instance!, id)?.type;
}
