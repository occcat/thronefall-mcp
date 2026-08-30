using System;
using ThronefallControl.Dto;
using NextWavePreview = ThronefallControl.Game.NextWave;

namespace ThronefallControl.Game;

public sealed class StateInclude
{
    public const string Slots = "slots";
    public const string Units = "units";
    public const string Training = "training";
    public const string Enemies = "enemies";
    public const string Spawns = "spawns";
    public const string NextWave = "nextWave";
    public const string Loadout = "loadout";
    public const string Cutters = "cutters";

    public bool All { get; private set; }
    public bool WantSlots { get; private set; }
    public bool WantUnits { get; private set; }
    public bool WantTraining { get; private set; }
    public bool WantEnemies { get; private set; }
    public bool WantSpawns { get; private set; }
    public bool WantNextWave { get; private set; }
    public bool WantLoadout { get; private set; }
    public bool WantCutters { get; private set; }

    public bool WantsSlots => All || WantSlots;
    public bool WantsUnits => All || WantUnits;
    public bool WantsTraining => All || WantTraining;
    public bool WantsEnemies => All || WantEnemies;
    public bool WantsSpawns => All || WantSpawns;
    public bool WantsNextWave => All || WantNextWave;
    public bool WantsLoadout => All || WantLoadout;
    public bool WantsCutters => All || WantCutters;

    public static StateInclude Parse(string? raw)
    {
        var include = new StateInclude();
        if (string.IsNullOrWhiteSpace(raw))
        {
            include.All = true;
            return include;
        }

        foreach (var part in raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.Trim().ToLowerInvariant())
            {
                case Slots:
                    include.WantSlots = true;
                    break;
                case Units:
                    include.WantUnits = true;
                    break;
                case Training:
                    include.WantTraining = true;
                    break;
                case Enemies:
                    include.WantEnemies = true;
                    break;
                case Spawns:
                    include.WantSpawns = true;
                    break;
                case "nextwave":
                    include.WantNextWave = true;
                    break;
                case Loadout:
                    include.WantLoadout = true;
                    break;
                case Cutters:
                    include.WantCutters = true;
                    break;
            }
        }

        if (!include.WantSlots && !include.WantUnits && !include.WantTraining &&
            !include.WantEnemies && !include.WantSpawns && !include.WantNextWave &&
            !include.WantLoadout &&
            !include.WantCutters)
        {
            // Unknown tokens only: still omit large arrays rather than dump everything.
            return include;
        }

        return include;
    }

    public void OmitUnrequested(StateDto dto)
    {
        if (!WantsSlots) dto.Slots = null;
        else dto.Slots ??= new();

        if (!WantsUnits) dto.Units = null;
        else dto.Units ??= new();

        if (!WantsTraining) dto.Training = null;
        else dto.Training ??= new();

        if (!WantsEnemies) dto.Enemies = null;
        else dto.Enemies ??= new EnemySummaryDto();

        if (!WantsSpawns) dto.Spawns = null;
        else dto.Spawns ??= new();

        if (!WantsNextWave) dto.NextWave = null;
        else
        {
            dto.NextWave ??= new NextWaveDto();
            dto.NextWave.Mouths = NextWavePreview.GroupByMouth(dto.NextWave.Groups);
        }

        if (!WantsLoadout) dto.Loadout = null;
        else dto.Loadout ??= new LoadoutDto();

        if (!WantsCutters) dto.Cutters = null;
        else dto.Cutters ??= new();
    }
}
