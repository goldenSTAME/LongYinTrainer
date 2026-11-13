using System.Text.Json;

TrainerBehaviour.Run();

public class Table
{
    public Dictionary<int, float> Values = new();
    public List<int> GetKeys() => Values.Keys.ToList();
    public float Get(int id) => Values.GetValueOrDefault(id);
    public List<float> attri = new(), fightSkill = new(), livingSkill = new();
    public float Hp, Mana, Power, Charm;
}
public class Skill
{
    public int type;
    public Table addDamageRatio = new(), skillNeeds = new(), upgradeAddData = new(), equipAddData = new(), useAddData = new();
}
public class GameDataController
{
    public static GameDataController Instance = new();
    public Dictionary<int, Skill> kungfuSkillDataBase = new();
}
public static partial class TrainerBehaviour
{
    private const int CustomKungfuIdMin = 900000;
    private class CustomKungfuRecord
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public bool EditOriginal { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<int> Effects { get; set; } = new();
    }
    private class CustomAttriNumSpec { public string Kind = ""; public int Index; }
    private static CustomKungfuRecord? _customDraft;
    private static string _customStatus = "", _customEffectSelectedKey = "", _customEffectEditorField = "";
    private static int _customPickerPreviewSkillId;
    private static bool FailTemplate;
    private static void SyncCustomKungfuDraftFromInputs() { }
    private static string CustomKungfuTypeName(int id) => id.ToString();
    private static CustomKungfuRecord NewCustomKungfuDraft(int type) => FailTemplate
        ? throw new Exception("missing template") : new() { Type = type, Effects = new() { type } };
    private static CustomKungfuRecord CloneCustomKungfuRecord(CustomKungfuRecord value)
        => JsonSerializer.Deserialize<CustomKungfuRecord>(JsonSerializer.Serialize(value))!;
    private static Skill? OriginalKungfu(GameDataController db, int id) => db.kungfuSkillDataBase.GetValueOrDefault(id);
    private static string CustomAttriSpecKey(string kind, int index) => kind + ":" + index;
    private static void AddCustomAttriList(List<CustomAttriNumSpec> entries, string kind, List<float> values, int count)
    { for (var i = 0; i < Math.Min(values.Count, count); i++) if (values[i] != 0) entries.Add(new() { Kind = kind, Index = i }); }
    private static int checks;
    private static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    public static void Run()
    {
        var db = GameDataController.Instance;
        for (var type = 0; type < 9; type++)
        {
            var s = new Skill { type = type };
            s.useAddData.Values[type] = 1;
            s.useAddData.Values[100] = 0;
            s.equipAddData.Values[20 + type] = 1;
            s.upgradeAddData.Values[40 + type] = 1;
            s.skillNeeds.Mana = 10;
            s.addDamageRatio.attri = Enumerable.Repeat(0f, 6).ToList();
            s.addDamageRatio.attri[type % 6] = 1;
            db.kungfuSkillDataBase[type] = s;
        }
        db.kungfuSkillDataBase[900000] = new Skill { type = 0, useAddData = new Table { Values = new() { [888] = 1 } } };
        for (var type = 0; type < 9; type++)
        {
            _customDraft = NewCustomKungfuDraft(type);
            Check(GetCategoryEffectKeys("use").SetEquals(new[] { "spe:" + type }), $"type {type}: excludes other categories, zero entries and custom skills");
            Check(GetCategoryEffectKeys("equip").SetEquals(new[] { "spe:" + (20 + type) }) &&
                  GetCategoryEffectKeys("upgrade").SetEquals(new[] { "spe:" + (40 + type) }), $"type {type}: effect tables separated");
            Check(GetCategoryEffectKeys("needs").SetEquals(new[] { "mana:0" }) &&
                  GetCategoryEffectKeys("ratio").SetEquals(new[] { "attri:" + type % 6 }), $"type {type}: requirement and ratio catalogs separated");
        }
        _customDraft = new() { Type = 0, Id = 900001, Name = "test", Description = "desc", Effects = new() { 77 } };
        SwitchCustomKungfuCategory(2);
        Check(_customDraft.Type == 2 && _customDraft.Effects.SequenceEqual(new[] { 2 }) && _customDraft.Id == 900001, "switch seeds destination category, preserves identity");
        _customDraft.Effects.Add(88);
        SwitchCustomKungfuCategory(0);
        Check(_customDraft.Effects.SequenceEqual(new[] { 77 }), "switch back restores original edits");
        SwitchCustomKungfuCategory(2);
        Check(_customDraft.Effects.SequenceEqual(new[] { 2, 88 }), "second category retains independent edits");
        var before = _customDraft;
        FailTemplate = true;
        SwitchCustomKungfuCategory(1);
        Check(ReferenceEquals(before, _customDraft) && _customStatus.Contains("失败"), "missing template leaves draft intact");
        FailTemplate = false;
        _customDraft = new() { Type = 0, Id = 900002 };
        SwitchCustomKungfuCategory(2);
        Check(_customDraft.Effects.SequenceEqual(new[] { 2 }) && _customDraft.Id == 900002, "different record cannot inherit previous category drafts");
        Console.WriteLine($"{checks} category checks passed (mock native data).");
    }
}
