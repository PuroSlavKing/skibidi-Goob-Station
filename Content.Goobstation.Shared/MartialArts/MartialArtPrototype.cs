// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <aviu00@protonmail.com>
// SPDX-FileCopyrightText: 2025 Baptr0b0t <152836416+baptr0b0t@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Baptr0b0t <bapt.buriot@gmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Lincoln McQueen <lincoln.mcqueen@gmail.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts;

[Prototype("martialArt")]
public sealed class MartialArtPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private init; } = default!;

    [DataField]
    public MartialArtsForms MartialArtsForm = MartialArtsForms.CloseQuartersCombat;

    [DataField]
    public int MinRandomDamageModifier;

    [DataField]
    public int MaxRandomDamageModifier = 5;

    [DataField]
    public FixedPoint2 BaseDamageModifier;

    [DataField]
    public bool RandomDamageModifier;

    [DataField]
    public ProtoId<ComboListPrototype> RoundstartCombos = "CQCMoves";

    [DataField]
    public List<LocId> RandomSayings = [];

    [DataField]
    public List<LocId> RandomSayingsDowned = [];

    [DataField]
    public GrabStage StartingStage = GrabStage.Soft;
}
