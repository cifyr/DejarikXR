using System;
using System.Collections.Generic;
using System.Linq;

namespace Dejarik
{
    // Pure rules engine ported from the web game's src/game/engine.ts. All reducers are non-mutating:
    // they take a GameState (+ Rng) and return a new GameState. No Unity dependencies.
    public static class Engine
    {
        // Outer-ring ray indices each player starts on (symmetric under 180 deg rotation).
        static readonly Dictionary<Player, int[]> StartRays = new Dictionary<Player, int[]>
        {
            { Player.P0, new[] { 5, 6, 7, 8 } },
            { Player.P1, new[] { 11, 0, 1, 2 } },
        };

        static int OuterSpaceForRay(int ray) => 13 + ray;

        static T[] Shuffle<T>(T[] arr, Rng rng)
        {
            var a = (T[])arr.Clone();
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(rng() * (i + 1));
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }

        public static GameState CreateInitialState(Rng rng, Player firstPlayer = Player.P0)
        {
            var split = Shuffle(Pieces.All, rng);
            var p0Types = split.Take(4).ToArray();
            var p1Types = split.Skip(4).Take(4).ToArray();

            var pieces = new List<GamePiece>();
            void Place(PieceType[] types, Player owner)
            {
                for (int i = 0; i < types.Length; i++)
                    pieces.Add(new GamePiece(Pieces.IdOf(types[i]), types[i], owner,
                        OuterSpaceForRay(StartRays[owner][i])));
            }
            Place(p0Types, Player.P0);
            Place(p1Types, Player.P1);

            var state = new GameState
            {
                Pieces = pieces,
                Turn = firstPlayer,
                ActionsRemaining = 2,
                Phase = Phase.Play,
                Log = new List<string> { $"Pieces dealt. Player {firstPlayer.Num()} moves first." },
            };
            state.Repetition = new Dictionary<string, int> { { PositionKey(state), 1 } };
            return state;
        }

        public static GamePiece PieceAt(GameState state, int space) => state.Pieces.FirstOrDefault(p => p.Space == space);
        public static GamePiece GetPiece(GameState state, string id) => state.Pieces.FirstOrDefault(p => p.Id == id);
        public static List<GamePiece> PiecesOf(GameState state, Player owner) => state.Pieces.Where(p => p.Owner == owner).ToList();
        static bool Occupied(GameState state, int space) => state.Pieces.Any(p => p.Space == space);

        // Spaces reachable from the piece's space in exactly `movement` steps, stepping only through empty
        // spaces and never revisiting a space on the path.
        public static List<int> LegalMoves(GameState state, string pieceId)
        {
            var piece = GetPiece(state, pieceId);
            if (piece == null) return new List<int>();
            int steps = Pieces.Stats[piece.Type].Movement;
            var results = new HashSet<int>();

            void Walk(int current, int remaining, HashSet<int> visited)
            {
                if (remaining == 0)
                {
                    if (current != piece.Space) results.Add(current);
                    return;
                }
                foreach (var next in Board.Neighbors(current))
                {
                    if (visited.Contains(next)) continue;
                    if (next != piece.Space && Occupied(state, next)) continue; // can't pass through pieces
                    visited.Add(next);
                    Walk(next, remaining - 1, visited);
                    visited.Remove(next);
                }
            }

            Walk(piece.Space, steps, new HashSet<int> { piece.Space });
            return results.OrderBy(x => x).ToList();
        }

        static double SegLength(int a, int b)
        {
            var pa = Board.Point(a);
            var pb = Board.Point(b);
            double dx = pa.X - pb.X, dy = pa.Y - pb.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // Exact-step walk path (inclusive of start and dest) through empty spaces; of all legal paths the
        // straightest (shortest total board distance) is chosen. null if `dest` is not a legal destination.
        public static int[] FindPath(GameState state, string pieceId, int dest)
        {
            var piece = GetPiece(state, pieceId);
            if (piece == null) return null;
            int steps = Pieces.Stats[piece.Type].Movement;
            int[] best = null;
            double bestLen = double.PositiveInfinity;

            void Walk(int current, int remaining, HashSet<int> visited, List<int> path, double len)
            {
                if (remaining == 0)
                {
                    if (current == dest && len < bestLen)
                    {
                        bestLen = len;
                        best = path.ToArray();
                    }
                    return;
                }
                foreach (var next in Board.Neighbors(current))
                {
                    if (visited.Contains(next)) continue;
                    if (next != piece.Space && Occupied(state, next)) continue;
                    visited.Add(next);
                    path.Add(next);
                    Walk(next, remaining - 1, visited, path, len + SegLength(current, next));
                    path.RemoveAt(path.Count - 1);
                    visited.Remove(next);
                }
            }

            Walk(piece.Space, steps, new HashSet<int> { piece.Space }, new List<int> { piece.Space }, 0);
            return best;
        }

        // Enemy pieces adjacent to the given piece (valid attack targets).
        public static List<string> AttackTargets(GameState state, string pieceId)
        {
            var piece = GetPiece(state, pieceId);
            if (piece == null) return new List<string>();
            return Board.Adjacency[piece.Space]
                .Select(sp => PieceAt(state, sp))
                .Where(p => p != null && p.Owner != piece.Owner)
                .Select(p => p.Id)
                .ToList();
        }

        static int[] RollFaces(int count, Rng rng)
        {
            var r = new int[count];
            for (int i = 0; i < count; i++) r[i] = (int)Math.Floor(rng() * 6) + 1;
            return r;
        }

        static int Sum(int[] ns)
        {
            int s = 0;
            foreach (var n in ns) s += n;
            return s;
        }

        static int RollDice(int count, Rng rng) => Sum(RollFaces(count, rng));

        public static AttackResult ResolveCombat(int attack, int defense, Rng rng)
        {
            var attackDice = RollFaces(attack, rng);
            var defenseDice = RollFaces(defense, rng);
            int attackRoll = Sum(attackDice);
            int defenseRoll = Sum(defenseDice);
            int diff = attackRoll - defenseRoll;
            Outcome outcome;
            if (diff >= 7) outcome = Outcome.Kill;
            else if (diff >= 1) outcome = Outcome.Push;
            else if (diff >= -6) outcome = Outcome.CounterPush;  // includes ties
            else outcome = Outcome.CounterKill;
            return new AttackResult
            {
                AttackRoll = attackRoll, DefenseRoll = defenseRoll,
                AttackDice = attackDice, DefenseDice = defenseDice, Outcome = outcome,
            };
        }

        static List<int> OpenAdjacent(GameState state, int space) =>
            Board.Neighbors(space).Where(sp => !Occupied(state, sp)).ToList();

        static GameState RemovePiece(GameState state, string id)
        {
            var next = state.Clone();
            next.Pieces = next.Pieces.Where(p => p.Id != id).ToList();
            return next;
        }

        static GameState CheckEndConditions(GameState state)
        {
            int p0 = PiecesOf(state, Player.P0).Count;
            int p1 = PiecesOf(state, Player.P1).Count;
            if (p0 == 0 || p1 == 0)
            {
                Player winner = p1 == 0 ? Player.P0 : Player.P1;
                var s = state.Clone();
                s.Phase = Phase.GameOver;
                s.Winner = winner;
                s.Log.Add($"Player {winner.Num()} wins — all enemy pieces destroyed.");
                return s;
            }
            if (p0 == 1 && p1 == 1)
            {
                var a = PiecesOf(state, Player.P0)[0];
                var b = PiecesOf(state, Player.P1)[0];
                var s = state.Clone();
                s.Phase = Phase.ToTheDeath;
                s.Duel = new Duel { AId = a.Id, BId = b.Id };
                s.Log.Add("Only one piece each remains — To-The-Death!");
                return s;
            }
            return state;
        }

        const int DrawRepeats = 3;

        static string PositionKey(GameState state)
        {
            var parts = state.Pieces.Select(p => $"{p.Id}@{p.Space}").OrderBy(x => x, StringComparer.Ordinal);
            return string.Join(",", parts) + $"|{(int)state.Turn}";
        }

        static GameState RecordTurnStart(GameState state)
        {
            string key = PositionKey(state);
            int count = (state.Repetition != null && state.Repetition.TryGetValue(key, out var c) ? c : 0) + 1;
            var s = state.Clone();
            s.Repetition = state.Repetition == null ? new Dictionary<string, int>() : new Dictionary<string, int>(state.Repetition);
            s.Repetition[key] = count;
            if (count >= DrawRepeats)
            {
                s.Phase = Phase.GameOver;
                s.Winner = null;
                s.Log.Add("Draw — the same position recurred three times.");
            }
            return s;
        }

        static GameState ConsumeAction(GameState state)
        {
            int remaining = state.ActionsRemaining - 1;
            if (remaining <= 0)
            {
                var s = state.Clone();
                s.Turn = state.Turn.Other();
                s.ActionsRemaining = 2;
                return RecordTurnStart(s);
            }
            var t = state.Clone();
            t.ActionsRemaining = remaining;
            return t;
        }

        public static GameState ApplyMove(GameState state, string pieceId, int dest)
        {
            if (state.Phase != Phase.Play) return state;
            var piece = GetPiece(state, pieceId);
            if (piece == null || piece.Owner != state.Turn) return state;
            if (!LegalMoves(state, pieceId).Contains(dest)) return state;

            var path = FindPath(state, pieceId, dest);
            var moved = state.Clone();
            moved.Pieces = state.Pieces.Select(p => p.Id == pieceId ? p.With(dest) : p).ToList();
            moved.Fx = new List<GameFx> { GameFx.Move(pieceId, path) };
            moved.Log.Add($"{Pieces.Stats[piece.Type].Name} moves to space {dest}.");
            return ConsumeAction(moved);
        }

        public static GameState ApplyAttack(GameState state, string attackerId, string defenderId, Rng rng)
        {
            if (state.Phase != Phase.Play) return state;
            var attacker = GetPiece(state, attackerId);
            var defender = GetPiece(state, defenderId);
            if (attacker == null || defender == null) return state;
            if (attacker.Owner != state.Turn || defender.Owner == attacker.Owner) return state;
            if (!AttackTargets(state, attackerId).Contains(defenderId)) return state;

            var aStats = Pieces.Stats[attacker.Type];
            var dStats = Pieces.Stats[defender.Type];
            var result = ResolveCombat(aStats.Attack, dStats.Defense, rng);
            string header = $"{aStats.Name} attacks {dStats.Name} ({result.AttackRoll} vs {result.DefenseRoll}) — {OutcomeStr(result.Outcome)}.";

            // Combatants square off; a "kill" is a finishing blow (attacker plays its finishing-move clip).
            var fx = new List<GameFx>
            {
                GameFx.Attack(attackerId, defender.Space, result.Outcome == Outcome.Kill),
                GameFx.Combat(attacker.Type, defender.Type, attacker.Owner, defender.Owner,
                    result.AttackDice, result.DefenseDice, result.Outcome),
            };
            var next = state.Clone();
            next.Fx = fx;
            next.Log.Add(header);

            switch (result.Outcome)
            {
                case Outcome.Kill:
                    fx.Add(GameFx.Death(defenderId, defender.Type, defender.Owner, defender.Space,
                        byType: attacker.Type, facing: attacker.Space));
                    next = RemovePiece(next, defenderId);
                    next = CheckEndConditions(next);
                    return next.Phase == Phase.Play ? ConsumeAction(next) : next;

                case Outcome.CounterKill:
                    // Defender turns the attack into a finishing counter; the attacker lunges in, then falls.
                    fx.Add(GameFx.Attack(defenderId, attacker.Space, true));
                    fx.Add(GameFx.Death(attackerId, attacker.Type, attacker.Owner, attacker.Space,
                        byType: defender.Type, facing: defender.Space, wasAttacker: true));
                    next = RemovePiece(next, attackerId);
                    next = CheckEndConditions(next);
                    return next.Phase == Phase.Play ? ConsumeAction(next) : next;

                case Outcome.Push:
                case Outcome.CounterPush:
                {
                    if (result.Outcome == Outcome.Push)
                        fx.Add(GameFx.Hit(defenderId, attacker.Space));
                    // push: attacker relocates the defender. counter-push: defender's owner relocates the attacker.
                    string movedPieceId = result.Outcome == Outcome.Push ? defenderId : attackerId;
                    Player chooser = result.Outcome == Outcome.Push ? attacker.Owner : defender.Owner;
                    var moved = GetPiece(next, movedPieceId);
                    var options = OpenAdjacent(next, moved.Space);
                    if (options.Count == 0)
                    {
                        next.Log.Add("No open space to push into — piece holds position.");
                        return ConsumeAction(next);
                    }
                    next.Phase = Phase.AwaitPush;
                    next.Pending = new PendingPush
                    {
                        Outcome = result.Outcome, MovedPieceId = movedPieceId, Chooser = chooser, Options = options,
                    };
                    return next;
                }
            }
            return next; // unreachable
        }

        public static GameState ResolvePush(GameState state, int dest)
        {
            if (state.Phase != Phase.AwaitPush || state.Pending == null) return state;
            if (!state.Pending.Options.Contains(dest)) return state;
            string movedPieceId = state.Pending.MovedPieceId;
            var piece = GetPiece(state, movedPieceId);
            int from = piece.Space;
            var moved = state.Clone();
            moved.Pieces = state.Pieces.Select(p => p.Id == movedPieceId ? p.With(dest) : p).ToList();
            moved.Phase = Phase.Play;
            moved.Pending = null;
            moved.Fx = new List<GameFx> { GameFx.Move(movedPieceId, new[] { from, dest }) };
            moved.Log.Add($"{Pieces.Stats[piece.Type].Name} is pushed to space {dest}.");
            return ConsumeAction(moved);
        }

        // Resolve the final 1v1 duel: each piece rolls its highest stat; reroll until a margin of 7+ decides.
        public static GameState ResolveToTheDeath(GameState state, Rng rng)
        {
            if (state.Phase != Phase.ToTheDeath || state.Duel == null) return state;
            var a = GetPiece(state, state.Duel.AId);
            var b = GetPiece(state, state.Duel.BId);
            int aDice = Pieces.HighestStat(a.Type);
            int bDice = Pieces.HighestStat(b.Type);
            var log = new List<string>(state.Log);

            List<GameFx> DuelFx(int[] aFaces, int[] bFaces, GamePiece atk, GamePiece def) => new List<GameFx>
            {
                GameFx.Combat(atk.Type, def.Type, atk.Owner, def.Owner, aFaces, bFaces, Outcome.Kill),
                GameFx.Death(def.Id, def.Type, def.Owner, def.Space, byType: atk.Type, facing: atk.Space),
            };

            for (int round = 0; round < 1000; round++)
            {
                var aFaces = RollFaces(aDice, rng);
                var bFaces = RollFaces(bDice, rng);
                int aRoll = Sum(aFaces);
                int bRoll = Sum(bFaces);
                int diff = aRoll - bRoll;
                log.Add($"Duel round {round + 1}: {Pieces.Stats[a.Type].Name} {aRoll} vs {Pieces.Stats[b.Type].Name} {bRoll}.");
                if (diff >= 7)
                {
                    log.Add("Player 1 wins the duel!");
                    return new GameState { Phase = Phase.GameOver, Winner = Player.P0, Pieces = new List<GamePiece> { a }, Log = log, Fx = DuelFx(aFaces, bFaces, a, b), Turn = state.Turn, ActionsRemaining = state.ActionsRemaining, Repetition = state.Repetition };
                }
                if (diff <= -7)
                {
                    log.Add("Player 2 wins the duel!");
                    return new GameState { Phase = Phase.GameOver, Winner = Player.P1, Pieces = new List<GamePiece> { b }, Log = log, Fx = DuelFx(bFaces, aFaces, b, a), Turn = state.Turn, ActionsRemaining = state.ActionsRemaining, Repetition = state.Repetition };
                }
            }
            // Safety fallback (astronomically unlikely): decide by a final roll.
            Player fallbackWinner = RollDice(aDice, rng) >= RollDice(bDice, rng) ? Player.P0 : Player.P1;
            log.Add($"Duel decided by sudden death — Player {fallbackWinner.Num()} wins.");
            return new GameState { Phase = Phase.GameOver, Winner = fallbackWinner, Pieces = new List<GamePiece> { fallbackWinner == Player.P0 ? a : b }, Log = log, Turn = state.Turn, ActionsRemaining = state.ActionsRemaining, Repetition = state.Repetition };
        }

        // Used only when the active player has no legal move or attack available.
        public static GameState PassAction(GameState state)
        {
            if (state.Phase != Phase.Play) return state;
            var s = state.Clone();
            s.Fx = new List<GameFx>();
            s.Log.Add($"Player {state.Turn.Num()} has no legal action — forfeits.");
            return ConsumeAction(s);
        }

        public static bool HasAnyAction(GameState state) =>
            PiecesOf(state, state.Turn).Any(p => LegalMoves(state, p.Id).Count > 0 || AttackTargets(state, p.Id).Count > 0);

        static string OutcomeStr(Outcome o) => o switch
        {
            Outcome.Kill => "kill",
            Outcome.Push => "push",
            Outcome.CounterPush => "counter-push",
            Outcome.CounterKill => "counter-kill",
            _ => o.ToString(),
        };
    }
}
