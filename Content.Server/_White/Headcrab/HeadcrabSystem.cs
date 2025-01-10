using System.Linq;
using Content.Server.Actions;
using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.Popups;
using Content.Server.NPC.Systems;
using Content.Server.Nutrition.Components;
using Content.Shared.Zombies;
using Content.Shared.CombatMode;
using Content.Shared.Ghost;
using Content.Shared.Damage;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Nutrition.Components;
using Content.Shared._White.Headcrab;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Players;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._White.Headcrab;

public sealed partial class HeadcrabSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly GhostSystem _ghostSystem = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AutoEmoteSystem _autoEmote = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HeadcrabComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HeadcrabComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<HeadcrabComponent, ThrowDoHitEvent>(OnThrowDoHit);
        SubscribeLocalEvent<HeadcrabComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<HeadcrabComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<HeadcrabComponent, GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<HeadcrabComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<HeadcrabComponent, JumpActionEvent>(OnJump);
    }

    private void OnStartup(EntityUid uid, HeadcrabComponent component, ComponentStartup args)
    {
        _action.AddAction(uid, component.JumpAction);
    }

    private void OnThrowDoHit(EntityUid uid, HeadcrabComponent component, ThrowDoHitEvent args)
    {
        TryEquipHeadcrab(uid, args.Target, component);
    }

    private void OnGotEquipped(EntityUid uid, HeadcrabComponent component, GotEquippedEvent args)
    {
        if (args.Slot != "mask")
            return;

        EnsureComp<AutoEmoteComponent>(args.Equipee);
        _autoEmote.AddEmote(args.Equipee, "ZombieGroan");

        component.EquippedOn = args.Equipee;
        EnsureComp<PacifiedComponent>(uid);
        RemComp<NPCMeleeCombatComponent>(uid);
        var npcFaction = EnsureComp<NpcFactionMemberComponent>(args.Equipee);
        component.OldFactions.Clear();
        component.OldFactions.UnionWith(npcFaction.Factions);
        _npcFaction.ClearFactions((args.Equipee, npcFaction), false);
        _npcFaction.AddFaction((args.Equipee, npcFaction), component.HeadcrabFaction);

        component.HasNpc = !EnsureComp<HTNComponent>(args.Equipee, out var htn);
        htn.RootTask = new HTNCompoundTask { Task = component.TakeoverTask };
        htn.Blackboard.SetValue(NPCBlackboard.Owner, args.Equipee);
        _npc.WakeNPC(args.Equipee, htn);
        _htn.Replan(htn);

        if (TryComp<ActorComponent>(args.Equipee, out var actor) && actor.PlayerSession.GetMind() is { } mind)
        {
            var session = actor.PlayerSession;
            if (!_ghostSystem.OnGhostAttempt(mind, false))
                return;

            component.StolenMind = mind;
        }

        if (_mobState.IsDead(uid))
            return;

        _popup.PopupEntity(Loc.GetString("headcrab-hit-entity-head",
                ("entity", args.Equipee)),
            uid, uid, PopupType.LargeCaution);

        _popup.PopupEntity(Loc.GetString("headcrab-eat-other-entity-face",
            ("entity", args.Equipee)), args.Equipee, Filter.PvsExcept(uid), true, PopupType.Large);

        _stunSystem.TryParalyze(args.Equipee, component.ParalyzeTime, true);
        _damageableSystem.TryChangeDamage(args.Equipee, component.Damage, origin: uid);
    }

    private void OnUnequipAttempt(EntityUid uid, HeadcrabComponent component, BeingUnequippedAttemptEvent args)
    {
        if (args.Slot != "mask" ||
            component.EquippedOn != args.Unequipee ||
            HasComp<ZombieComponent>(args.Unequipee) ||
            _mobState.IsDead(uid))
            return;

        _popup.PopupEntity(Loc.GetString("headcrab-try-unequip"),
            args.Unequipee, args.Unequipee, PopupType.Large);
        args.Cancel();
    }

    private void OnGotEquippedHand(EntityUid uid, HeadcrabComponent component, GotEquippedHandEvent args)
    {
        if (_mobState.IsDead(uid) ||
            HasComp<ZombieComponent>(args.User) ||
            HasComp<GhostComponent>(args.User))
            return;

        _handsSystem.TryDrop(args.User, uid, checkActionBlocker: false);
        _damageableSystem.TryChangeDamage(args.User, component.Damage);
        _popup.PopupEntity(Loc.GetString("headcrab-entity-bite"),
            args.User, args.User);
    }

    private void OnGotUnequipped(EntityUid uid, HeadcrabComponent component, GotUnequippedEvent args)
    {
        if (args.Slot != "mask")
            return;

        _autoEmote.RemoveEmote(args.Equipee, "ZombieGroan");

        component.EquippedOn = EntityUid.Invalid;
        RemCompDeferred<PacifiedComponent>(uid);
        var combatMode = EnsureComp<CombatModeComponent>(uid);
        _combat.SetInCombatMode(uid, true, combatMode);
        EnsureComp<NPCMeleeCombatComponent>(uid);

        if (component.HasNpc)
            RemComp<HTNComponent>(args.Equipee);

        var npcFaction = EnsureComp<NpcFactionMemberComponent>(args.Equipee);
        _npcFaction.RemoveFaction((args.Equipee, npcFaction), component.HeadcrabFaction, false);
        _npcFaction.AddFactions((args.Equipee, npcFaction), component.OldFactions);

        component.OldFactions.Clear();

        if (Exists(component.StolenMind))
        {
            _mind.TransferTo(component.StolenMind.Value, args.Equipee);
        }
    }

    private void OnMeleeHit(EntityUid uid, HeadcrabComponent component, MeleeHitEvent args)
    {
        if (!args.HitEntities.Any() || !_random.Prob(component.ChancePounce / 100f))
            return;

        TryEquipHeadcrab(uid, args.HitEntities.First(), component);
    }

    private void OnJump(EntityUid uid, HeadcrabComponent component, JumpActionEvent args)
    {
        if (args.Handled || component.EquippedOn is { Valid: true })
            return;

        args.Handled = true;
        var xform = Transform(uid);
        var mapCoords = _transform.ToMapCoordinates(args.Target);
        var direction = mapCoords.Position - _transform.GetMapCoordinates(xform).Position;

        if (direction.LengthSquared() == 0)
        {
            return;
        }

        direction = direction.Normalized() * 5f;

        _throwing.TryThrow(uid, direction, 7F, uid, 10F);
        _audioSystem.PlayPvs(component.JumpSound, uid, component.JumpSound?.Params);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeadcrabComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Accumulator += frameTime;

            if (comp.Accumulator <= comp.DamageFrequency)
                continue;

            comp.Accumulator = 0;

            if (comp.EquippedOn is not { Valid: true } targetId ||
                HasComp<ZombieComponent>(comp.EquippedOn) ||
                _mobState.IsDead(uid))
                continue;

            if (!_mobState.IsAlive(targetId))
            {
                _inventory.TryUnequip(targetId, "mask", true, true);
                comp.EquippedOn = EntityUid.Invalid;
                continue;
            }

            _damageableSystem.TryChangeDamage(targetId, comp.Damage);
            _popup.PopupEntity(Loc.GetString("headcrab-eat-entity-face"),
                targetId, targetId, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("headcrab-eat-other-entity-face",
                ("entity", targetId)), targetId, Filter.PvsExcept(targetId), true);
        }
    }

    private bool TryEquipHeadcrab(EntityUid uid, EntityUid target, HeadcrabComponent component)
    {
        if (_mobState.IsDead(uid)
            || !_mobState.IsAlive(target)
            || !HasComp<HumanoidAppearanceComponent>(target)
            || HasComp<ZombieComponent>(target))
            return false;

        _inventory.TryGetSlotEntity(target, "head", out var headItem);
        return !HasComp<IngestionBlockerComponent>(headItem)
            && _inventory.TryEquip(target, uid, "mask", true);
    }
}
