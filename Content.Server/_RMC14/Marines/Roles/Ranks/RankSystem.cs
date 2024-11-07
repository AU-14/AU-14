﻿using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Marines.Roles.Ranks;

public sealed class RankSystem : SharedRankSystem
{
    [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
    [Dependency] private readonly SharedIdCardSystem _idCardSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RankComponent, TransformSpeakerNameEvent>(OnSpeakerNameTransform);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnSpeakerNameTransform(Entity<RankComponent> ent, ref TransformSpeakerNameEvent args)
    {
        var name = GetSpeakerRankName(ent);
        if (name == null)
            return;

        args.VoiceName = name;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var uid = ev.Mob;
        var jobId = ev.JobId;

        if (jobId == null)
            return;

        _prototypes.TryIndex<JobPrototype>(jobId, out var jobPrototype);

        if (jobPrototype == null)
            return;

        var ranks = jobPrototype.Ranks;

        if (ranks == null)
            return;

        if (!_tracking.TryGetTrackerTimes(ev.Player, out var playTimes))
        {
            // Playtimes haven't loaded.
            Log.Error($"Playtimes weren't ready yet for {ev.Player} on roundstart!");
            playTimes ??= new Dictionary<string, TimeSpan>();
        }

        foreach (var rank in ranks)
        {
            var failed = false;
            var jobRequirements = rank.Value;

            if (_prototypes.TryIndex<RankPrototype>(rank.Key, out var rankPrototype) && rankPrototype != null)
            {
                if (jobRequirements != null)
                {
                    foreach (var req in jobRequirements)
                    {
                        if (!req.Check(_entityManager, _prototypes, ev.Profile, playTimes, out _))
                            failed = true;
                    }
                }

                if (!failed)
                {
                    SetRank(uid, rankPrototype);
                    break;
                }
            }
        }
    }
}
