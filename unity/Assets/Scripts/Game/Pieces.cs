using System;
using System.Collections.Generic;

namespace Dejarik
{
    public enum PieceType { Savrip, Monnok, Ghhhk, Houjix, Strider, Ngok, Klorslug, Molator }

    public readonly struct PieceStats
    {
        public readonly PieceType Type;
        public readonly string Name;
        public readonly int Attack;
        public readonly int Defense;
        public readonly int Movement;
        public readonly string Glyph;   // short fallback marker

        public PieceStats(PieceType type, string name, int attack, int defense, int movement, string glyph)
        {
            Type = type; Name = name; Attack = attack; Defense = defense; Movement = movement; Glyph = glyph;
        }
    }

    // Canonical Dejarik ratings from the Holochess ruleset (Mike Kelly, repost of the starwars-rpg.net
    // rules; mirrored at obj.vassalengine.org Dejarik_Rules.pdf). Verified against the web game's pieces.ts.
    public static class Pieces
    {
        public static readonly IReadOnlyDictionary<PieceType, PieceStats> Stats = new Dictionary<PieceType, PieceStats>
        {
            { PieceType.Savrip,   new PieceStats(PieceType.Savrip,   "Mantellian Savrip", 6, 6, 2, "SV") },
            { PieceType.Monnok,   new PieceStats(PieceType.Monnok,   "Monnok",            6, 5, 3, "MK") },
            { PieceType.Ghhhk,    new PieceStats(PieceType.Ghhhk,    "Ghhhk",             4, 3, 2, "GH") },
            { PieceType.Houjix,   new PieceStats(PieceType.Houjix,   "Houjix",            4, 4, 1, "HX") },
            { PieceType.Strider,  new PieceStats(PieceType.Strider,  "Kintan Strider",    2, 7, 3, "ST") },
            { PieceType.Ngok,     new PieceStats(PieceType.Ngok,     "Ng'ok",             3, 8, 1, "NG") },
            { PieceType.Klorslug, new PieceStats(PieceType.Klorslug, "K'lor'slug",        7, 3, 2, "KL") },
            { PieceType.Molator,  new PieceStats(PieceType.Molator,  "Molator",           8, 2, 2, "ML") },
        };

        // Stable order matching the web game's Object.keys(PIECE_STATS) (insertion order above).
        public static readonly PieceType[] All =
        {
            PieceType.Savrip, PieceType.Monnok, PieceType.Ghhhk, PieceType.Houjix,
            PieceType.Strider, PieceType.Ngok, PieceType.Klorslug, PieceType.Molator,
        };

        public static int HighestStat(PieceType type)
        {
            var s = Stats[type];
            return Math.Max(s.Attack, s.Defense);
        }

        // Lowercase id used as the piece id (matches the web game, e.g. "klorslug").
        public static string IdOf(PieceType type) => type.ToString().ToLowerInvariant();
    }
}
