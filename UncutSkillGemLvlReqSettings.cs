using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace UncutSkillGemLvlReq;

public sealed class UncutSkillGemLvlReqSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);
    public RangeNode<int> TextSize { get; set; } = new(18, 12, 32);
    public RangeNode<int> OffsetX { get; set; } = new(0, -300, 300);
    public RangeNode<int> OffsetY { get; set; } = new(0, -100, 100);
}
