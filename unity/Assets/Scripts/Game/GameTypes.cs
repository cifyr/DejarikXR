using System.Collections.Generic;
using System.Linq;

namespace Dejarik
{
    public enum Player { P0 = 0, P1 = 1 }
    public enum Outcome { Kill, Push, CounterPush, CounterKill }
    public enum Phase { Play, AwaitPush, ToTheDeath, GameOver }
    public enum FxKind { Move, Attack, Hit, Death, Combat }

    public static class PlayerExt
    {
        public static Player Other(this Player p) => p == Player.P0 ? Player.P1 : Player.P0;
        public static int Num(this Player p) => (int)p + 1;   // 1-based for log lines
    }

    public sealed class GamePiece
    {
        public string Id;
        public PieceType Type;
        public Player Owner;
        public int Space;

        public GamePiece(string id, PieceType type, Player owner, int space)
        {
            Id = id; Type = type; Owner = owner; Space = space;
        }

        // Pieces are treated as immutable (a moved piece is replaced, not mutated), mirroring the web game.
        public GamePiece With(int space) => new GamePiece(Id, Type, Owner, space);
    }

    public sealed class PendingPush
    {
        public Outcome Outcome;       // push | counter-push
        public string MovedPieceId;   // piece being relocated
        public Player Chooser;        // who picks where it goes
        public List<int> Options;
    }

    public sealed class Duel
    {
        public string AId;
        public string BId;
    }

    public sealed class AttackResult
    {
        public int AttackRoll;
        public int DefenseRoll;
        public int[] AttackDice;
        public int[] DefenseDice;
        public Outcome Outcome;
    }

    // Transient per-action animation cue for the renderer (mirrors the web game's GameFx union). Each
    // reducer sets GameState.Fx to the events it produced; the renderer reads them once and plays the
    // matching creature clip. Unused fields stay at their default (null).
    public sealed class GameFx
    {
        public FxKind Kind;
        public string PieceId;
        public int[] Path;
        public int? Facing;          // board space a combatant turns toward
        public bool? Finisher;       // killing-blow (attacker plays finishing-move clip)
        public PieceType? PieceTypeVal;
        public Player? Owner;
        public int? Space;
        public PieceType? ByType;
        public bool? WasAttacker;
        public PieceType? AttackerType;
        public PieceType? DefenderType;
        public Player? AttackerOwner;
        public Player? DefenderOwner;
        public int[] AttackDice;
        public int[] DefenseDice;
        public Outcome? Outcome;

        public static GameFx Move(string pieceId, int[] path) =>
            new GameFx { Kind = FxKind.Move, PieceId = pieceId, Path = path };

        public static GameFx Attack(string pieceId, int facing, bool finisher) =>
            new GameFx { Kind = FxKind.Attack, PieceId = pieceId, Facing = facing, Finisher = finisher };

        public static GameFx Hit(string pieceId, int facing) =>
            new GameFx { Kind = FxKind.Hit, PieceId = pieceId, Facing = facing };

        public static GameFx Death(string pieceId, PieceType pieceType, Player owner, int space,
                                   PieceType? byType = null, int? facing = null, bool? wasAttacker = null) =>
            new GameFx
            {
                Kind = FxKind.Death, PieceId = pieceId, PieceTypeVal = pieceType, Owner = owner, Space = space,
                ByType = byType, Facing = facing, WasAttacker = wasAttacker,
            };

        public static GameFx Combat(PieceType attackerType, PieceType defenderType, Player attackerOwner,
                                    Player defenderOwner, int[] attackDice, int[] defenseDice, Outcome outcome) =>
            new GameFx
            {
                Kind = FxKind.Combat, AttackerType = attackerType, DefenderType = defenderType,
                AttackerOwner = attackerOwner, DefenderOwner = defenderOwner,
                AttackDice = attackDice, DefenseDice = defenseDice, Outcome = outcome,
            };
    }

    public sealed class GameState
    {
        public List<GamePiece> Pieces = new List<GamePiece>();
        public Player Turn;
        public int ActionsRemaining;
        public Phase Phase;
        public PendingPush Pending;
        public Player? Winner;             // null at GameOver means a draw
        public Duel Duel;
        public List<string> Log = new List<string>();
        public List<GameFx> Fx;
        // Count of each start-of-turn board position; a position recurring three times is a draw.
        public Dictionary<string, int> Repetition;

        // Shallow copy with fresh collections so reducers stay non-mutating (pieces/log are replaced,
        // never edited in place, matching the web game's immutable {...state} updates).
        public GameState Clone() => new GameState
        {
            Pieces = new List<GamePiece>(Pieces),
            Turn = Turn,
            ActionsRemaining = ActionsRemaining,
            Phase = Phase,
            Pending = Pending,
            Winner = Winner,
            Duel = Duel,
            Log = new List<string>(Log),
            Fx = Fx == null ? null : new List<GameFx>(Fx),
            Repetition = Repetition == null ? null : new Dictionary<string, int>(Repetition),
        };
    }
}
