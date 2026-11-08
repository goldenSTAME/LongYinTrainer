var passed = 0;
void Check(bool success, string name)
{
    if (!success) throw new Exception(name);
    passed++;
    Console.WriteLine("PASS " + name);
}
KungfuSkillLvData Skill(int pointer, int id) => new() { Pointer = (IntPtr)pointer, skillID = id };
var hero = new HeroData { Pointer = (IntPtr)4, internalSkill = Skill(10, 0), dodgeSkill = Skill(11, 1), uniqueSkill = Skill(12, 2), attackSkills = new() { Skill(13, 3), Skill(14, 4) } };
var c = new BattleController { Pointer = (IntPtr)1, nowActiveHeroUI = new() { Pointer = (IntPtr)2 }, nowActiveUnit = new() { Pointer = (IntPtr)3, heroData = hero } };
Check(!BattleSkillBarBinding.CanReuse(c), "first entry binds natively");
BattleSkillBarBinding.Remember(c);
for (var i = 0; i < 100; i++)
{
    hero.internalSkill!.cdTimeLeft = i;
    hero.internalSkill.activeTimeLeft = i;
    hero.internalSkill.power = i;
    hero.internalSkill.skillIconDirty = (i % 2) == 0;
    if (!BattleSkillBarBinding.CanReuse(c)) throw new Exception("round/state changes caused rebinding");
}
Check(true, "100 rounds and cooldown/power/dirty changes preserve bindings");
var unit = c.nowActiveUnit;
c.nowActiveUnit = null;
Check(!BattleSkillBarBinding.CanReuse(c), "no active unit falls back to native");
c.nowActiveUnit = unit;
Check(BattleSkillBarBinding.CanReuse(c), "temporary turn gap preserves binding cache");
hero.internalSkill = Skill(20, 0);
Check(!BattleSkillBarBinding.CanReuse(c), "replaced internal skill rebinds");
BattleSkillBarBinding.Remember(c);
hero.dodgeSkill!.skillID = 900005;
Check(!BattleSkillBarBinding.CanReuse(c), "in-place skill ID change rebinds");
BattleSkillBarBinding.Remember(c);
hero.uniqueSkill = null;
Check(!BattleSkillBarBinding.CanReuse(c), "unequipped utility skill rebinds");
BattleSkillBarBinding.Remember(c);
hero.attackSkills!.Reverse();
Check(!BattleSkillBarBinding.CanReuse(c), "attack slot order change rebinds");
BattleSkillBarBinding.Remember(c);
hero.attackSkills.Add(Skill(15, 5));
Check(!BattleSkillBarBinding.CanReuse(c), "added attack slot rebinds");
BattleSkillBarBinding.Remember(c);
c.nowActiveUnit = new() { Pointer = (IntPtr)30, heroData = hero };
Check(!BattleSkillBarBinding.CanReuse(c), "unit switch rebinds even with same hero");
BattleSkillBarBinding.Remember(c);
c.nowActiveHeroUI = new() { Pointer = (IntPtr)40 };
Check(!BattleSkillBarBinding.CanReuse(c), "recreated UI root rebinds");
BattleSkillBarBinding.Remember(c);
var other = new BattleController { Pointer = (IntPtr)90, nowActiveHeroUI = c.nowActiveHeroUI, nowActiveUnit = c.nowActiveUnit };
Check(!BattleSkillBarBinding.CanReuse(other), "different battle controller cannot reuse bindings");
BattleSkillBarBinding.Clear();
Check(!BattleSkillBarBinding.CanReuse(c), "battle end/start or edited definition invalidates cache");
c.nowActiveHeroUI = null;
BattleSkillBarBinding.Remember(c);
Check(!BattleSkillBarBinding.CanReuse(c), "incomplete native binding is never marked ready");
Console.WriteLine($"{passed} checks passed (mock native objects; battle trial still required).");

public class BattleController { public IntPtr Pointer; public UiRoot? nowActiveHeroUI; public BattleUnit? nowActiveUnit; }
public class UiRoot { public IntPtr Pointer; }
public class BattleUnit { public IntPtr Pointer; public HeroData? heroData; }
public class HeroData { public IntPtr Pointer; public KungfuSkillLvData? internalSkill, dodgeSkill, uniqueSkill; public List<KungfuSkillLvData>? attackSkills; }
public class KungfuSkillLvData { public IntPtr Pointer; public int skillID; public float cdTimeLeft, activeTimeLeft, power; public bool skillIconDirty; }
