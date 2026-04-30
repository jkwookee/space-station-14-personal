using Content.Server._Impstation.StrangeMoods;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Examine;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Lightning;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Silicons.Laws;
using Content.Server.Singularity.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Server.Traits.Assorted;
using Content.Shared._EE.CCVar;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.DeviceLinking;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Chat.Systems;
using Content.Server.Singularity.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._EE.CCVar;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared._Impstation.StrangeMoods;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Chat;
using Content.Shared.DeviceLinking;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Speech;
using Content.Shared.Storage.Components;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._EE.Supermatter.Systems;

public sealed partial class SupermatterDelaminationSystem
{
    /// <summary>
    /// Handles core damage announcements
    /// </summary>
    private void AnnounceCoreDamage(EntityUid uid, SupermatterComponent sm)
    {
        // If undamaged, no need to announce anything
        if (sm.Damage == 0)
            return;

        string message;
        var global = false;

        var integrity = GetIntegrity(sm).ToString("0.00");

        // Instantly announce delamination
        if (sm.Delamming && !sm.DelamAnnounced)
        {
            var sb = new StringBuilder();
            var loc = sm.PreferredDelamType switch
            {
                DelamType.Cascade => "supermatter-delam-cascade",
                DelamType.Singulo => "supermatter-delam-overmass",
                DelamType.Tesla => "supermatter-delam-tesla",
                _ => "supermatter-delam-explosion"
            };

            sb.AppendLine(Loc.GetString(loc));
            sb.Append(Loc.GetString("supermatter-seconds-before-delam", ("seconds", sm.DelamTimer)));

            message = sb.ToString();
            global = true;
            sm.DelamAnnounced = true;
            sm.YellTimer = TimeSpan.FromSeconds(sm.DelamTimer / 2);

            SendSupermatterAnnouncement(uid, sm, message, global);
            return;
        }

        // Only announce every YellTimer seconds
        if (_timing.CurTime < sm.YellLast + sm.YellTimer)
            return;

        // Recovered after the delamination point
        if (sm.Damage < sm.DamageDelaminationPoint && sm.DelamAnnounced)
        {
            message = Loc.GetString("supermatter-delam-cancel", ("integrity", integrity));
            sm.DelamAnnounced = false;
            sm.YellTimer = TimeSpan.FromSeconds(_config.GetCVar(EECCVars.SupermatterYellTimer));
            global = true;

            SendSupermatterAnnouncement(uid, sm, message, global);
            return;
        }

        // Oh god oh fuck
        if (sm.Delamming && sm.DelamAnnounced)
        {
            var seconds = Math.Ceiling(sm.DelamEndTime.TotalSeconds - _timing.CurTime.TotalSeconds);

            if (seconds <= 0)
                return;

            var loc = seconds switch
            {
                > 5 => "supermatter-seconds-before-delam-countdown",
                <= 5 => "supermatter-seconds-before-delam-imminent",
                _ => string.Empty
            };

            sm.YellTimer = seconds switch
            {
                > 30 => TimeSpan.FromSeconds(10),
                > 5 => TimeSpan.FromSeconds(5),
                <= 5 => TimeSpan.FromSeconds(1),
                _ => TimeSpan.FromSeconds(_config.GetCVar(EECCVars.SupermatterYellTimer))
            };

            if (seconds <= 5 && TryComp<SpeechComponent>(uid, out var speech))
                // Prevent repeat sounds during the 5.. 4.. 3.. 2.. 1.. countdown
                speech.SoundCooldownTime = 4.5f;

            message = Loc.GetString(loc, ("seconds", seconds));
            global = true;

            SendSupermatterAnnouncement(uid, sm, message, global);
            return;
        }

        // We're safe
        if (sm.Damage < sm.DamageArchived && sm.Status >= SupermatterStatusType.Warning)
        {
            message = Loc.GetString("supermatter-healing", ("integrity", integrity));

            if (sm.Status >= SupermatterStatusType.Emergency)
                global = true;

            if (TryComp<SpeechComponent>(uid, out var speech))
                // Reset speech cooldown after healing is started
                speech.SoundCooldownTime = 0.0f;

            SendSupermatterAnnouncement(uid, sm, message, global);
            return;
        }

        // Ignore the 0% integrity alarm
        if (sm.Delamming)
            return;

        // We are not taking consistent damage, Engineers aren't needed
        if (sm.Damage <= sm.DamageArchived)
            return;

        // Announce damage and any dangerous thresholds
        if (sm.Damage >= sm.DamageWarningThreshold)
        {
            message = Loc.GetString("supermatter-warning", ("integrity", integrity));
            if (sm.Damage >= sm.DamageEmergencyThreshold)
            {
                message = Loc.GetString("supermatter-emergency", ("integrity", integrity));
                global = true;
            }

            SendSupermatterAnnouncement(uid, sm, message, global);

            global = false;

            if (sm.Power >= _config.GetCVar(EECCVars.SupermatterPowerPenaltyThreshold))
            {
                message = Loc.GetString("supermatter-threshold-power");
                SendSupermatterAnnouncement(uid, sm, message, global);

                if (sm.PowerlossInhibitor < 0.5)
                {
                    message = Loc.GetString("supermatter-threshold-powerloss");
                    SendSupermatterAnnouncement(uid, sm, message, global);
                }
            }

            if (sm.GasStorage != null && sm.GasStorage.TotalMoles >= _config.GetCVar(EECCVars.SupermatterMolePenaltyThreshold))
            {
                message = Loc.GetString("supermatter-threshold-mole");
                SendSupermatterAnnouncement(uid, sm, message, global);
            }
        }
    }

    /// <summary>
    /// Sends the given message to local chat and a radio channel
    /// </summary>
    /// <param name="global">If true, sends the message to the common radio</param>
    public void SendSupermatterAnnouncement(EntityUid uid, SupermatterComponent sm, string message, bool global = false)
    {
        if (sm.SuppressAnnouncements)
            return;

        if (message == String.Empty)
            return;

        var channel = sm.Channel;

        if (global)
            channel = sm.ChannelGlobal;

        // Ensure status, otherwise the wrong speech sound may be used
        HandleStatus(uid, sm);

        sm.YellLast = _timing.CurTime;
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, hideChat: false, checkRadioPrefix: true);
        _radio.SendRadioMessage(uid, message, channel, uid);
    }
}
