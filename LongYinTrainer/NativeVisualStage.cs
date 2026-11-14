using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// A visual-only stage, far outside the gameplay map. Never invokes skill or battle resolution.
internal static class NativeVisualStage
{
    private static GameObject? root;
    private static Camera? camera;
    private static RenderTexture? texture;
    private static RawImage? view;
    private static SkeletonAnimation? actor;
    private static readonly List<ParticleSystem> particles = new();
    private static GameObject? bullet;
    private static readonly List<(Transform Target, Spine.Bone Bone)> weaponEffects = new();
    private static float started;
    private static int diagnostics;
    internal static string PlaybackStatus = "";
    private static bool framed;
    private static bool moveActor;
    private static readonly List<Vector3> moveTargets = new();
    private static readonly List<GameObject> effectRoots = new();
    private static readonly HashSet<int> prepared = new();
    private static float cycleDuration = 3;
    private static Vector3 actorHome;
    private static float height = 2;
    private static KungfuSkillData? activeSkill;
    private static bool effectsPlaced;
    private static Spine.TrackEntry? track;
    private static readonly List<float> shootTimes = new();
    private static int nextShoot;
    private static bool fallbackTiming;
    private static float shootAt;
    private static float bulletStarted;
    private static List<Vector3> hitCells = new();
    private static Vector3 effectTarget;
    private static Bounds framingBounds;

    private static Material? rangeMaterial;
    internal static bool ShowRanges = true;
    internal static string? Outfit;
    internal static readonly List<string> Outfits = new();
    private static readonly Vector3 Origin = new(24000, -24000, 0);

    internal static void Close()
    {
        if (camera != null) camera.targetTexture = null;
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
        if (texture != null) { texture.Release(); Object.Destroy(texture); }
        if (view != null) Object.Destroy(view.gameObject);
        root = null; camera = null; texture = null; actor = null; bullet = null; view = null;
        if (rangeMaterial != null) Object.Destroy(rangeMaterial); rangeMaterial = null;
        track = null; shootTimes.Clear(); nextShoot = 0; hitCells.Clear();
        particles.Clear(); weaponEffects.Clear(); moveTargets.Clear(); effectRoots.Clear(); prepared.Clear(); diagnostics = 0; PlaybackStatus = ""; framed = false; effectsPlaced = false; activeSkill = null;
    }
    private static void Layer(Transform parent)
    {
        parent.gameObject.layer = 31;
        for (var i = 0; i < parent.childCount; i++) Layer(parent.GetChild(i));
    }
    internal static string Open(KungfuSkillData skill, Transform parent)
    {
        Close();
        try {
            var hero = GameController.Instance?.worldData?.Player() ?? throw new InvalidOperationException("请先载入存档");
            root = new GameObject("KungfuVisualOnlyStage"); root.transform.position = Origin;
            actor = hero.GenerateHeroSkeleton(root, Vector3.one);
            if (actor == null) throw new InvalidOperationException("原生人物生成失败");
            foreach (var component in actor.GetComponentsInChildren<MonoBehaviour>(true))
                if (component.TryCast<SkeletonAnimation>() == null) component.enabled = false;
            actor.Initialize(false); actor.UnscaledTime = false; actorHome = actor.transform.localPosition;
            Outfits.Clear();
            var data = actor.Skeleton.Data;
            for (var i = 0; i < data.Skins.Count; i++) if (data.Skins.Items[i].Name.StartsWith("skin", StringComparison.OrdinalIgnoreCase)) Outfits.Add(data.Skins.Items[i].Name);
            var skin = Outfit ?? hero.GetSkinName(true, -99, -1);
            if (data.FindSkin(skin) != null) { actor.Skeleton.SetSkin(skin); actor.Skeleton.SetSlotsToSetupPose(); }
            hero.SetSkillWeapon(actor, skill.weaponName ?? "");
            var animation = data.FindAnimation(skill.animationName);
            cycleDuration = Math.Max(3, (animation?.Duration ?? 1) + 1);
            fallbackTiming = true;
            if (animation != null) {
                for (var i = 0; i < animation.Timelines.Count; i++) {
                    var events = animation.Timelines.Items[i].TryCast<Spine.EventTimeline>();
                    if (events == null) continue;
                    foreach (var ev in events.Events)
                        if (ev != null && ev.Data.Name == "skillshoot") shootTimes.Add(ev.Time);
                }
                shootTimes.Sort(); fallbackTiming = shootTimes.Count == 0;
                if (fallbackTiming) shootTimes.Add(animation.Duration * .6f);
                track = actor.AnimationState.SetAnimation(0, skill.animationName, false);
            }
            shootAt = shootTimes.Count > 0 ? shootTimes[0] : float.PositiveInfinity;
            LongYinTrainerPlugin.Logger.LogInfo($"Preview timing: animation={skill.animationName}, skillshoot=[{string.Join(",", shootTimes)}], fallback={fallbackTiming}");
            var cameraObject = new GameObject("PreviewCamera"); cameraObject.transform.SetParent(root.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 2;
            camera.transform.position = Origin + new Vector3(0, 1, -200);
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.88f, .89f, .85f, 1); camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            texture = new RenderTexture(640, 520, 16); texture.Create(); camera.targetTexture = texture;
            var go = new GameObject("RenderedPreview"); var rect = go.AddComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            view = go.AddComponent<RawImage>(); view.texture = texture; view.raycastTarget = false;
            activeSkill = skill;
            Layer(root.transform); started = Time.realtimeSinceStartup; moveActor = skill.autoHeroMove;
            LongYinTrainerPlugin.Logger.LogInfo($"Visual stage: animation={skill.animationName}, skin={skin}, weapon={skill.weaponName}, outfits={Outfits.Count}");
            return (animation == null ? "当前人物不支持此动作；" : "动作播放；") + "蓝格=释放范围，红格=作用范围。效果按展示时序播放。";
        } catch (Exception ex) { Close(); LongYinTrainerPlugin.Logger.LogWarning("Visual stage: " + ex); return "预览失败：" + ex.Message; }
    }
    private static GameObject? Spawn(string name)
    {
        var prefab = Resources.Load("SpeEffect/" + name, Il2CppInterop.Runtime.Il2CppType.Of<GameObject>())?.TryCast<GameObject>();
        if (prefab == null || root == null) { LongYinTrainerPlugin.Logger.LogInfo("Preview resource unavailable: SpeEffect/" + name); return null; }
        var holder = new GameObject("VisualEffectOnly"); holder.SetActive(false); holder.transform.SetParent(root.transform, false);
        var effect = Object.Instantiate(prefab, holder.transform);
        effectRoots.Add(holder);
        PrepareVisual(effect);
        Layer(holder.transform); holder.SetActive(true);
        LongYinTrainerPlugin.Logger.LogInfo($"Preview instantiated: name={name}, particleSystems={effect.GetComponentsInChildren<ParticleSystem>(true).Length}, renderers={effect.GetComponentsInChildren<Renderer>(true).Length}");
        return holder;
    }
    // Keep the native prefab lifecycle, including generators, animation and cleanup.
    // These objects come only from the game's SpeEffect resources, never battle units.
    private static void PrepareVisual(GameObject effect)
    {
        foreach (var audio in effect.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
        foreach (var collider in effect.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true)) {
            if (!prepared.Add(ps.GetInstanceID())) continue;
            particles.Add(ps); // Do not restart bursts or override playOnAwake.
        }
        var actorRenderer = actor?.GetComponent<MeshRenderer>();
        foreach (var renderer in effect.GetComponentsInChildren<Renderer>(true)) {
            if (actorRenderer != null) { renderer.sortingLayerID = actorRenderer.sortingLayerID; renderer.sortingOrder = actorRenderer.sortingOrder + 10; }
        }
        Layer(effect.transform);
    }
    private static void MovePreview(float elapsed)
    {
        if (actor == null) return;
        actor.transform.localPosition = actorHome;
        if (!moveActor || moveTargets.Count == 0) return;
        // Native AttackHit moves to the hit grid; AttackFinish returns to the caster grid.
        // Sample targets represent occupied cells; no combat callbacks or damage are invoked.
        var phase = Mathf.Clamp01((elapsed - shootAt) / Math.Max(.5f, cycleDuration - shootAt - .5f));
        var segment = phase * (moveTargets.Count + 1);
        var index = Math.Min((int)segment, moveTargets.Count);
        var from = index == 0 ? actorHome : moveTargets[index - 1];
        var to = index == moveTargets.Count ? actorHome : moveTargets[index];
        actor.transform.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01((segment - index) * 3));
        if (phase >= 1) actor.transform.localPosition = actorHome;
    }
    private static List<Vector3> RangeCells(int type, int min, int max, float unit, Vector3 center, bool damageRange = false)
    {
        var result = new List<Vector3>();
        var controller = CustomSkillCreateController.Instance.externalDamageRangeUI.GetComponentInChildren<SkillRangeUIController>(true);
        controller ??= CustomSkillCreateController.Instance.GetComponentInChildren<SkillRangeUIController>(true);
        if (controller == null) return result;
        // Large ranges are sampled for preview only; the actual draft remains unchanged.
        var radius = Math.Clamp(max, 0, 8); var lines = radius * 2 + 1;
        // Native RefreshSkillRangeGrid uses direction=false for ordinary ranges.
        // Directional damage enums are mapped to the shared grid shape first.
        var directional = damageRange && type >= 4 && type != 7;
        if (directional) type = controller.GetDirectionDamageType((DamageRangeType)type);
        var grid = controller.FindRangeGrids(type, lines, Math.Min(min, radius), radius, directional);
        for (var x = 0; x < grid.Count; x++) for (var y = 0; y < grid[x].Count; y++) {
            if (grid[x][y] <= 0) continue;
            var dx = x - radius; var dy = y - radius;
            result.Add(center + new Vector3((dx - dy) * unit, (dx + dy) * unit * .5f, 0));
        }
        return result;
    }
    private static void Mark(Vector3 position, float unit, Color color)
    {
        if (root == null) return;
        rangeMaterial ??= new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject("PreviewRangeCell"); go.transform.SetParent(root.transform, false); go.transform.position = position; go.layer = 31;
        var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = rangeMaterial;
        line.useWorldSpace = false; line.positionCount = 5; line.startWidth = line.endWidth = unit * .025f;
        line.startColor = line.endColor = color; line.sortingOrder = -100;
        line.SetPosition(0, new Vector3(-unit, 0, 0)); line.SetPosition(1, new Vector3(0, unit * .5f, 0));
        line.SetPosition(2, new Vector3(unit, 0, 0)); line.SetPosition(3, new Vector3(0, -unit * .5f, 0)); line.SetPosition(4, new Vector3(-unit, 0, 0));
    }
    private static void PlaceEffects()
    {
        if (root == null || activeSkill == null) return;
        effectsPlaced = true; var unit = Math.Max(.1f, height * .3f);
        var attackCells = new List<Vector3>();
        if (activeSkill.attackRangeData != null) foreach (var range in activeSkill.attackRangeData)
            attackCells.AddRange(RangeCells((int)range.rangeType, range.minRange, range.maxRange, unit, Origin));
        var target = attackCells.Count > 0 ? attackCells[attackCells.Count / 2] : Origin;
        var damage = activeSkill.damageRangeData;
        var hit = damage == null ? new List<Vector3> { target } : RangeCells((int)damage.rangeType, damage.minRange, damage.maxRange, unit, target, true);
        if (hit.Count == 0) {
            hit.Add(target);
            LongYinTrainerPlugin.Logger.LogWarning("Preview hit-range sampling empty; using one sample target for visual playback only.");
        }
        LongYinTrainerPlugin.Logger.LogInfo($"Preview draft effects={activeSkill.skillSpeEffects?.Count ?? 0}, bullet={activeSkill.skillBullet?.bulletName}, damageRange={damage?.rangeType}/{damage?.minRange}/{damage?.maxRange}");
        moveTargets.Clear();
        for (var i = 0; i < Math.Min(3, hit.Count); i++) {
            var cell = hit[i * hit.Count / Math.Min(3, hit.Count)];
            moveTargets.Add(cell - Origin + actorHome);
            if (ShowRanges) Mark(cell, unit * .35f, new Color(1, .65f, .1f, 1));
        }
        if (ShowRanges) {
            if (activeSkill.attackRangeData != null) foreach (var range in activeSkill.attackRangeData)
                foreach (var cell in RangeCells((int)range.rangeType, range.minRange, range.maxRange, unit, Origin)) Mark(cell, unit * .85f, new Color(.2f,.45f,.9f,.6f));
            foreach (var cell in hit) Mark(cell, unit * .7f, new Color(.9f,.25f,.2f,.8f));
        }
        hitCells = hit; effectTarget = target;
        PlayEffects(SkillSpeEffectTriggerType.Start);
    }
    private static void PlayEffects(SkillSpeEffectTriggerType trigger)
    {
        if (activeSkill == null || root == null) return;
        var hit = hitCells; var target = effectTarget;
        var loaded = 0; var missing = 0;
        if (activeSkill.skillSpeEffects != null) foreach (var effect in activeSkill.skillSpeEffects) {
            if (effect == null || effect.triggerType != trigger) continue;
            if (effect.speEffectTargetType == SkillSpeEffectTargetType.Grid) {
                var n = 0;
                foreach (var cell in hit) { if (n++ >= 48) break; var go = Spawn(effect.speName); if (go != null) { go.transform.position = effect.selfSpe ? cell - target + Origin : cell; loaded++; } else { missing++; break; } }
            } else {
                var go = Spawn(effect.speName);
                if (go != null) {
                    go.transform.position = (effect.selfSpe ? Origin : target) + new Vector3(0, height * .45f, 0);
                    if (effect.speEffectTargetType == SkillSpeEffectTargetType.Weapon && actor != null) {
                        for (var i = 0; i < actor.Skeleton.Slots.Count; i++) {
                            var slot = actor.Skeleton.Slots.Items[i];
                            if (slot.Attachment != null && slot.Data.Name.Contains("weapon", StringComparison.OrdinalIgnoreCase)) { weaponEffects.Add((go.transform, slot.Bone)); break; }
                        }
                    }
                    loaded++;
                } else missing++;
            }
        }
        if (trigger == SkillSpeEffectTriggerType.Happen && activeSkill.skillBullet != null && !string.IsNullOrWhiteSpace(activeSkill.skillBullet.bulletName)) { if (bullet != null) Object.Destroy(bullet); bullet = Spawn(activeSkill.skillBullet.bulletName); bulletStarted = track?.AnimationTime ?? 0; }
        LongYinTrainerPlugin.Logger.LogInfo($"Preview effects: loaded={loaded}, missing={missing}, damageCells={hit.Count}; preview range limited to 8, grid effects to 48 per entry");
    }
    internal static void Tick()
    {
        if (root == null || actor == null || camera == null) return;
        if (view == null || !view.gameObject.activeInHierarchy) { Close(); return; }
        var renderer = actor.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.bounds.size.y > .01f) {
            if (!framed) { framingBounds = renderer.bounds; height = renderer.bounds.size.y; framed = true; }
            framingBounds.Encapsulate(renderer.bounds);
            if (!effectsPlaced) PlaceEffects();
            // Particle renderer bounds include conservative emission volumes, not just
            // visible pixels. Never use them to zoom the actor out of the preview.
            var aspect = 640f / 520f;
            camera.orthographicSize = Mathf.Clamp(Math.Max(framingBounds.extents.y, framingBounds.extents.x / aspect) * 1.18f + height * .08f, height * .8f, height * 2.5f);
            camera.transform.position = new Vector3(framingBounds.center.x, framingBounds.center.y, Origin.z - 200);
        }
        var animationTime = track?.AnimationTime ?? 0;
        MovePreview(animationTime);
        while (effectsPlaced && nextShoot < shootTimes.Count && animationTime >= shootTimes[nextShoot]) {
            PlayEffects(SkillSpeEffectTriggerType.Happen);
            LongYinTrainerPlugin.Logger.LogInfo($"Preview skillshoot: expected={shootTimes[nextShoot]}, actual={animationTime}");
            nextShoot++;
        }
        foreach (var binding in weaponEffects) if (binding.Target != null)
            binding.Target.position = actor.transform.TransformPoint(new Vector3(binding.Bone.WorldX, binding.Bone.WorldY, 0));
        if (bullet != null) bullet.transform.position = Origin + new Vector3(Mathf.Clamp(animationTime - bulletStarted, 0, 2) * height * .8f, height * .5f, 0);
        foreach (var go in effectRoots) if (go != null) PrepareVisual(go);
        // Observe only. Native components own emission, lifetime, delays and cleanup.
        var count = 0; var systems = 0;
        foreach (var ps in particles) if (ps != null && ps.gameObject.activeInHierarchy) {
            count += ps.particleCount; systems++;
        }
        PlaybackStatus = Time.timeScale == 0 ? "游戏时间暂停，原生特效也会暂停；可退出暂停后重播" : $"{(nextShoot == 0 ? "起手" : "出招/收招")} · {(fallbackTiming ? "无出招事件，估算时机" : "按原生出招事件同步")} · 粒子 {count}";
        if (diagnostics < 2 && Time.realtimeSinceStartup - started > .5f + diagnostics) {
            diagnostics++;
            LongYinTrainerPlugin.Logger.LogInfo($"Preview render check: timeScale={Time.timeScale}, systems={systems}, liveParticles={count}, heroHeight={height}, cameraSize={camera.orthographicSize}, camera={camera.transform.position}");
            foreach (var ps in particles) if (ps != null && ps.particleCount > 0) {
                var render = ps.GetComponent<ParticleSystemRenderer>();
                LongYinTrainerPlugin.Logger.LogInfo($"Preview particle bounds: name={ps.name}, position={ps.transform.position}, bounds={render?.bounds}, material={render?.sharedMaterial?.name}");
                break;
            }
        }

    }
}
