namespace FantasyBasketball.Domain.Leagues;

public enum RosterSlotKind
{
    PG,
    SG,
    SF,
    PF,
    C,
    G,
    F,
    UTIL,
    BENCH,
    IR,
}

public sealed record RosterSlot(RosterSlotKind Kind)
{
    public bool Accepts(string position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(position);

        return Kind switch
        {
            RosterSlotKind.PG => position is nameof(RosterSlotKind.PG),
            RosterSlotKind.SG => position is nameof(RosterSlotKind.SG),
            RosterSlotKind.SF => position is nameof(RosterSlotKind.SF),
            RosterSlotKind.PF => position is nameof(RosterSlotKind.PF),
            RosterSlotKind.C => position is nameof(RosterSlotKind.C),
            RosterSlotKind.G => position is nameof(RosterSlotKind.PG) or nameof(RosterSlotKind.SG),
            RosterSlotKind.F => position is nameof(RosterSlotKind.SF) or nameof(RosterSlotKind.PF),
            RosterSlotKind.UTIL or RosterSlotKind.BENCH or RosterSlotKind.IR =>
                position is nameof(RosterSlotKind.PG)
                    or nameof(RosterSlotKind.SG)
                    or nameof(RosterSlotKind.SF)
                    or nameof(RosterSlotKind.PF)
                    or nameof(RosterSlotKind.C),
            _ => false,
        };
    }
}
