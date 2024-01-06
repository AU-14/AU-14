using Content.Shared.Damage;
﻿using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared._CM14.Comtech.Barbed.Components
{
    [RegisterComponent]
    public sealed partial class BarbedComponent : Component
    {
        [DataField(required: true)]
        public DamageSpecifier ThornsDamage = default!;

        [DataField("isBarbed")]
        public bool IsBarbed = false;

        [DataField("wireTime")]
        public float WireTime = 3.0f;
    }

    [NetSerializable, Serializable]
    public enum BarbedWireVisuals : byte
    {
        Wired,
    }
}

[Serializable, NetSerializable]
public sealed partial class BarbedDoAfterEvent : SimpleDoAfterEvent
{
}
