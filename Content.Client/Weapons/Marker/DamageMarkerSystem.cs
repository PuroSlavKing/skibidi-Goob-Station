// SPDX-FileCopyrightText: 2023 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 TheBorzoiMustConsume <197824988+TheBorzoiMustConsume@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Weapons.Marker;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Weapons.Marker;

public sealed class DamageMarkerSystem : SharedDamageMarkerSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageMarkerComponent, AfterAutoHandleStateEvent>(OnMarkerStartup); //ShitChap - Un-hardcoded the sprites and sfx for the markers
        SubscribeLocalEvent<DamageMarkerComponent, ComponentShutdown>(OnMarkerShutdown);

    }

    private void OnMarkerStartup(EntityUid uid, DamageMarkerComponent component, AfterAutoHandleStateEvent args)
    {
        if (!_timing.ApplyingState || component.Effect == null || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var layer = _sprite.LayerMapReserve((uid, sprite), DamageMarkerKey.Key);
        _sprite.LayerSetRsi((uid, sprite), layer, component.Effect.RsiPath, component.Effect.RsiState);
    }

    private void OnMarkerShutdown(EntityUid uid, DamageMarkerComponent component, ComponentShutdown args)
    {
        if (!_timing.ApplyingState || !TryComp<SpriteComponent>(uid, out var sprite) || !_sprite.LayerMapTryGet((uid, sprite), DamageMarkerKey.Key, out var weh, false))
            return;

        _sprite.RemoveLayer((uid, sprite), weh);
    }

    private enum DamageMarkerKey : byte
    {
        Key
    }
}