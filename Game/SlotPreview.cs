using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public static class SlotPreview
{
    public static void Apply(
        SlotDto dto,
        string? tooltip,
        string? nextUpgradeLabel,
        IEnumerable<string>? buildingNames,
        IEnumerable<int>? slotIds)
    {
        if (dto == null)
            return;
        dto.Tooltip = tooltip ?? "";
        dto.NextUpgradeLabel = nextUpgradeLabel ?? "";
        dto.UnlockPreview = MapUnlock(buildingNames, slotIds);
    }

    public static SlotUnlockPreviewDto MapUnlock(
        IEnumerable<string>? buildingNames,
        IEnumerable<int>? slotIds)
    {
        var preview = new SlotUnlockPreviewDto();
        if (buildingNames != null)
        {
            foreach (var name in buildingNames)
            {
                if (!string.IsNullOrEmpty(name))
                    preview.BuildingNames.Add(name);
            }
        }

        if (slotIds != null)
        {
            foreach (var id in slotIds)
                preview.SlotIds.Add(id);
        }

        return preview;
    }
}
