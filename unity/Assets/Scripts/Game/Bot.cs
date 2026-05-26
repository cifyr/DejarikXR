using System;
using System.Collections.Generic;
using System.Linq;

namespace Dejarik
{
    public sealed class BotAction
    {
        public enum Kind { Move, Attack }
        public Kind Type;
        public string PieceId;       // move
        public int Dest;             // move
        public string AttackerId;    // attack
        public string DefenderId;    // attack

        public static BotAction MoveTo(string pieceId, int dest) =>
            new BotAction { Type = Kind.Move, PieceId = pieceId, Dest = dest };
        public static BotAction AttackOf(string attackerId, string defenderId) =>
            new BotAction { Type = Kind.Attack, AttackerId = attackerId, DefenderId = defenderId };
    }

    // Greedy single-action bot ported from src/game/bot.ts.
    public static class Bot
    {
        const double DieMean = 3.5;
        const double DieVar = 35.0 / 12.0;     // variance of a fair d6
        const double EngageBonus = 0.5;        // tip marginal cases toward resolving adjacent combats
        const double EngageReward = 1.5;       // reward closing to adjacency, to avoid standoffs

        // Normal CDF via an erf approximation (Abramowitz & Stegun 7.1.26).
        static double Phi(double x)
        {
            double t = 1 / (1 + 0.2316419 * Math.Abs(x));
            double d = 0.3989423 * Math.Exp(-x * x / 2);
            double p = d * t * (0.3193815 + t * (-0.3565638 + t * (1.781478 + t * (-1.821256 + t * 1.330274))));
            return x >= 0 ? 1 - p : p;
        }

        public static (double kill, double counterKill) CombatOdds(int attack, int defense)
        {
            double mean = DieMean * (attack - defense);
            double sd = Math.Sqrt(DieVar * (attack + defense));
            double kill = 1 - Phi((6.5 - mean) / sd);          // P(diff >= 7)
            double counterKill = Phi((-6.5 - mean) / sd);      // P(diff <= -7)
            return (kill, counterKill);
        }

        static double PieceValue(PieceType type)
        {
            var s = Pieces.Stats[type];
            return s.Attack + s.Defense + s.Movement * 0.5;
        }

        static List<GamePiece> EnemyNeighbors(GameState state, int space, Player owner) =>
            Board.Neighbors(space)
                .Select(sp => state.Pieces.FirstOrDefault(p => p.Space == sp))
                .Where(p => p != null && p.Owner != owner)
                .ToList();

        static double AttackScore(GamePiece attacker, GamePiece defender)
        {
            var a = Pieces.Stats[attacker.Type];
            var d = Pieces.Stats[defender.Type];
            var odds = CombatOdds(a.Attack, d.Defense);
            return odds.kill * PieceValue(defender.Type) - odds.counterKill * PieceValue(attacker.Type);
        }

        static double PositionScore(GameState state, GamePiece piece, int space)
        {
            double score = 0;
            var me = Pieces.Stats[piece.Type];
            foreach (var enemy in EnemyNeighbors(state, space, piece.Owner))
            {
                var e = Pieces.Stats[enemy.Type];
                var mine = CombatOdds(me.Attack, e.Defense);
                var theirs = CombatOdds(e.Attack, me.Defense);
                score += mine.kill * PieceValue(enemy.Type);
                score -= theirs.kill * PieceValue(piece.Type) * 0.8;
                score += EngageReward; // closing to adjacency guarantees combats happen and progress is made
            }
            var enemies = Engine.PiecesOf(state, piece.Owner == Player.P0 ? Player.P1 : Player.P0);
            if (enemies.Count > 0)
            {
                int minDist = enemies.Min(en => SpaceDistance(space, en.Space));
                score -= minDist * 0.4;
            }
            return score;
        }

        // BFS shortest-path distance between two spaces.
        static int SpaceDistance(int a, int b)
        {
            if (a == b) return 0;
            var seen = new HashSet<int> { a };
            var frontier = new List<int> { a };
            int dist = 0;
            while (frontier.Count > 0)
            {
                dist++;
                var next = new List<int>();
                foreach (var sp in frontier)
                    foreach (var nb in Board.Neighbors(sp))
                    {
                        if (nb == b) return dist;
                        if (seen.Add(nb)) next.Add(nb);
                    }
                frontier = next;
            }
            return int.MaxValue;
        }

        // Greedy single-action choice. Optional rng adds small score jitter to break positional cycles.
        public static BotAction Action(GameState state, Rng rng = null)
        {
            if (state.Phase != Phase.Play) return null;
            var myPieces = Engine.PiecesOf(state, state.Turn);
            double Jitter() => rng != null ? (rng() - 0.5) * 0.4 : 0;

            double bestScore = double.NegativeInfinity;
            BotAction bestAction = null;

            foreach (var piece in myPieces)
            {
                foreach (var defId in Engine.AttackTargets(state, piece.Id))
                {
                    var def = Engine.GetPiece(state, defId);
                    double score = AttackScore(piece, def) + EngageBonus + Jitter();
                    if (score > bestScore) { bestScore = score; bestAction = BotAction.AttackOf(piece.Id, defId); }
                }

                double here = PositionScore(state, piece, piece.Space);
                foreach (var dest in Engine.LegalMoves(state, piece.Id))
                {
                    double score = PositionScore(state, piece, dest) - here * 0.5 + Jitter();
                    if (score > bestScore) { bestScore = score; bestAction = BotAction.MoveTo(piece.Id, dest); }
                }
            }

            return bestAction;
        }

        // Choose where a pushed piece lands. Called only when the bot is the chooser.
        public static int PushTarget(GameState state)
        {
            var pending = state.Pending;
            var moved = Engine.GetPiece(state, pending.MovedPieceId);
            bool botIsOwner = moved.Owner == pending.Chooser;

            int best = pending.Options[0];
            double bestScore = double.NegativeInfinity;
            foreach (var dest in pending.Options)
            {
                // Own piece (counter-push): move to safety. Enemy (push): make its new spot as bad as possible.
                double score = botIsOwner ? PositionScore(state, moved, dest) : -PositionScore(state, moved, dest);
                if (score > bestScore) { bestScore = score; best = dest; }
            }
            return best;
        }
    }
}
