using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Dejarik;

// Ported from the web game's src/game/engine.test.ts. Validates the C# rules engine matches the
// original. Runs headless: Unity -runTests -testPlatform EditMode.
public class EngineTests
{
    // A die value v in [0,1) maps to face floor(v*6)+1. 0 -> 1, 0.99 -> 6.
    const double LOW = 0;
    const double HIGH = 0.99;

    static Rng Scripted(params double[] values)
    {
        int i = 0;
        return () => values[i++ % values.Length];
    }

    static GamePiece P(string id, PieceType t, Player o, int space) => new GamePiece(id, t, o, space);

    static GameState MakeState(GamePiece[] pieces, Player turn = Player.P0) => new GameState
    {
        Pieces = pieces.ToList(),
        Turn = turn,
        ActionsRemaining = 2,
        Phase = Phase.Play,
        Log = new List<string>(),
    };

    static int Inner(int ray) => Board.InnerId(ray);
    static int Outer(int ray) => Board.OuterId(ray);
    const int CENTER = Board.Center;

    // ---- board adjacency ----

    [Test]
    public void CenterConnectsToAll12InnerSpaces()
    {
        var got = Board.Neighbors(CENTER).OrderBy(x => x).ToArray();
        var want = Enumerable.Range(0, 12).Select(Inner).OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(want, got);
    }

    [Test]
    public void AdjacencyIsSymmetric()
    {
        for (int a = 0; a < Board.SpaceCount; a++)
            foreach (var b in Board.Adjacency[a])
                Assert.IsTrue(Board.AreAdjacent(b, a), $"{b} should be adjacent to {a}");
    }

    [Test]
    public void InnerSpaceConnectsToOrbitNeighboursCenterAndOuterRay()
    {
        var got = Board.Neighbors(Inner(3)).OrderBy(x => x).ToArray();
        var want = new[] { CENTER, Inner(2), Inner(4), Outer(3) }.OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(want, got);
    }

    [Test]
    public void OuterSpaceDoesNotConnectToCenter() =>
        CollectionAssert.DoesNotContain(Board.Neighbors(Outer(0)), CENTER);

    [Test]
    public void OrbitWrapsAround()
    {
        Assert.IsTrue(Board.AreAdjacent(Outer(0), Outer(11)));
        Assert.AreEqual(11, Board.RayOf(Outer(11)));
    }

    // ---- legal moves ----

    [Test]
    public void Movement1PieceReachesExactlyItsOpenNeighbours()
    {
        var s = MakeState(new[] { P("houjix", PieceType.Houjix, Player.P0, Outer(0)) }); // movement 1
        var got = Engine.LegalMoves(s, "houjix").OrderBy(x => x).ToArray();
        var want = new[] { Outer(11), Outer(1), Inner(0) }.OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(want, got);
    }

    [Test]
    public void MovingPieceNeverLandsBackOnStart()
    {
        var s = MakeState(new[] { P("strider", PieceType.Strider, Player.P0, Outer(0)) }); // movement 3
        var moves = Engine.LegalMoves(s, "strider");
        CollectionAssert.DoesNotContain(moves, Outer(0));
        Assert.Greater(moves.Count, 0);
    }

    [Test]
    public void CannotMoveThroughOrOntoOccupiedSpaces()
    {
        var s = MakeState(new[]
        {
            P("houjix", PieceType.Houjix, Player.P0, Outer(0)),
            P("ngok", PieceType.Ngok, Player.P1, Outer(1)),
        });
        var moves = Engine.LegalMoves(s, "houjix");
        CollectionAssert.DoesNotContain(moves, Outer(1));
        CollectionAssert.Contains(moves, Outer(11));
        CollectionAssert.Contains(moves, Inner(0));
    }

    // ---- combat resolution table ----

    [Test]
    public void AttackBeatsDefenseBy7OrMoreIsKill()
    {
        var r = Engine.ResolveCombat(6, 6, Scripted(HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, LOW, LOW, LOW, LOW, LOW, LOW));
        Assert.AreEqual(Outcome.Kill, r.Outcome);
    }

    [Test]
    public void DefenseBeatsAttackBy7OrMoreIsCounterKill()
    {
        var r = Engine.ResolveCombat(6, 6, Scripted(LOW, LOW, LOW, LOW, LOW, LOW, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH));
        Assert.AreEqual(Outcome.CounterKill, r.Outcome);
    }

    [Test]
    public void TieIsCounterPush()
    {
        var r = Engine.ResolveCombat(1, 1, Scripted(0.5, 0.5));
        Assert.AreEqual(r.AttackRoll, r.DefenseRoll);
        Assert.AreEqual(Outcome.CounterPush, r.Outcome);
    }

    [Test]
    public void SmallAttackMarginIsPush()
    {
        var r = Engine.ResolveCombat(2, 1, Scripted(2.0 / 6 + 0.01, 2.0 / 6 + 0.01, 4.0 / 6 + 0.01));
        Assert.AreEqual(Outcome.Push, r.Outcome);
    }

    // ---- attack application ----

    static GamePiece[] Four() => new[]
    {
        P("molator", PieceType.Molator, Player.P0, Outer(0)),  // attack 8
        P("ghhhk", PieceType.Ghhhk, Player.P1, Outer(1)),      // defense 3
        P("savrip", PieceType.Savrip, Player.P0, Inner(5)),
        P("monnok", PieceType.Monnok, Player.P1, Inner(8)),
    };

    [Test]
    public void KillRemovesDefenderAndConsumesAction()
    {
        var s = MakeState(Four());
        var outState = Engine.ApplyAttack(s, "molator", "ghhhk", Scripted(HIGH));
        Assert.IsNull(outState.Pieces.FirstOrDefault(p => p.Id == "ghhhk"));
        Assert.AreEqual(1, outState.ActionsRemaining);
    }

    [Test]
    public void PushEntersAwaitPushAndResolvePushRelocatesDefender()
    {
        var s = MakeState(Four());
        var pushState = s.Clone();
        pushState.Phase = Phase.AwaitPush;
        pushState.Pending = new PendingPush
        {
            Outcome = Outcome.Push, MovedPieceId = "ghhhk", Chooser = Player.P0,
            Options = new List<int> { Outer(2), Inner(1) },
        };
        var outState = Engine.ResolvePush(pushState, Outer(2));
        Assert.AreEqual(Outer(2), outState.Pieces.First(p => p.Id == "ghhhk").Space);
        Assert.AreEqual(Phase.Play, outState.Phase);
        Assert.AreEqual(1, outState.ActionsRemaining);
    }

    // ---- animation fx events ----

    [Test]
    public void KillEmitsFinisherAttackCombatAndDeathCues()
    {
        var outState = Engine.ApplyAttack(MakeState(Four()), "molator", "ghhhk", Scripted(HIGH));
        Assert.IsTrue(outState.Fx.Any(f => f.Kind == FxKind.Attack && f.PieceId == "molator"
            && f.Facing == Outer(1) && f.Finisher == true));
        Assert.IsTrue(outState.Fx.Any(f => f.Kind == FxKind.Death && f.PieceId == "ghhhk"
            && f.ByType == PieceType.Molator && f.Facing == Outer(0)));
        var combat = outState.Fx.First(f => f.Kind == FxKind.Combat);
        Assert.AreEqual(PieceType.Molator, combat.AttackerType);
        Assert.AreEqual(PieceType.Ghhhk, combat.DefenderType);
        Assert.AreEqual(Outcome.Kill, combat.Outcome);
        Assert.AreEqual(8, combat.AttackDice.Length);
        Assert.AreEqual(3, combat.DefenseDice.Length);
        Assert.IsTrue(combat.AttackDice.All(d => d >= 1 && d <= 6));
    }

    [Test]
    public void CounterKillDefenderFinishesAndAttackerFalls()
    {
        var values = Enumerable.Repeat(LOW, 8).Concat(Enumerable.Repeat(HIGH, 3)).ToArray();
        var outState = Engine.ApplyAttack(MakeState(Four()), "molator", "ghhhk", Scripted(values));
        Assert.IsTrue(outState.Fx.Any(f => f.Kind == FxKind.Death && f.PieceId == "molator"
            && f.ByType == PieceType.Ghhhk && f.WasAttacker == true));
        Assert.IsTrue(outState.Fx.Any(f => f.Kind == FxKind.Attack && f.PieceId == "ghhhk"
            && f.Facing == Outer(0) && f.Finisher == true));
        Assert.IsTrue(outState.Fx.Any(f => f.Kind == FxKind.Attack && f.PieceId == "molator" && f.Finisher == false));
    }

    [Test]
    public void MoveEmitsMoveCueWithBoardPathAndNoCombatCues()
    {
        var s = MakeState(new[] { P("houjix", PieceType.Houjix, Player.P0, Outer(0)) });
        int dest = Engine.LegalMoves(s, "houjix")[0];
        var outState = Engine.ApplyMove(s, "houjix", dest);
        Assert.AreEqual(1, outState.Fx.Count);
        Assert.AreEqual(FxKind.Move, outState.Fx[0].Kind);
        Assert.AreEqual("houjix", outState.Fx[0].PieceId);
        var path = outState.Fx[0].Path;
        Assert.AreEqual(Outer(0), path[0]);
        Assert.AreEqual(dest, path[path.Length - 1]);
    }

    // ---- findPath ----

    [Test]
    public void FindPathReturnsExactStepPathThroughEmptyCells()
    {
        var s = MakeState(new[] { P("strider", PieceType.Strider, Player.P0, Outer(0)) }); // movement 3
        int dest = Engine.LegalMoves(s, "strider").First(d => d != Outer(0));
        var path = Engine.FindPath(s, "strider", dest);
        Assert.IsNotNull(path);
        Assert.AreEqual(Outer(0), path[0]);
        Assert.AreEqual(dest, path[path.Length - 1]);
        Assert.AreEqual(4, path.Length); // start + 3 steps
        for (int i = 1; i < path.Length; i++) Assert.IsTrue(Board.AreAdjacent(path[i - 1], path[i]));
    }

    [Test]
    public void FindPathRoutesAroundOccupiedCell()
    {
        var s = MakeState(new[]
        {
            P("strider", PieceType.Strider, Player.P0, Outer(0)), // movement 3
            P("ngok", PieceType.Ngok, Player.P1, Inner(0)),       // blocks the direct spoke
        });
        var path = Engine.FindPath(s, "strider", CENTER);
        Assert.IsNotNull(path);
        CollectionAssert.DoesNotContain(path, Inner(0));
        Assert.AreEqual(CENTER, path[path.Length - 1]);
    }

    [Test]
    public void FindPathReturnsNullForUnreachableDestination()
    {
        var s = MakeState(new[] { P("houjix", PieceType.Houjix, Player.P0, Outer(0)) }); // movement 1
        Assert.IsNull(Engine.FindPath(s, "houjix", CENTER)); // 2 steps away
    }

    // ---- turn flow ----

    [Test]
    public void TwoActionsEndTurnAndPassToOtherPlayer()
    {
        var s = MakeState(new[]
        {
            P("strider", PieceType.Strider, Player.P0, Outer(0)),
            P("ngok", PieceType.Ngok, Player.P1, Outer(6)),
        });
        s = Engine.ApplyMove(s, "strider", Engine.LegalMoves(s, "strider")[0]);
        Assert.AreEqual(Player.P0, s.Turn);
        Assert.AreEqual(1, s.ActionsRemaining);
        s = Engine.ApplyMove(s, "strider", Engine.LegalMoves(s, "strider")[0]);
        Assert.AreEqual(Player.P1, s.Turn);
        Assert.AreEqual(2, s.ActionsRemaining);
    }

    // ---- end conditions ----

    [Test]
    public void ClearingOpponentWinsTheGame()
    {
        var s = MakeState(new[]
        {
            P("molator", PieceType.Molator, Player.P0, Outer(0)),
            P("savrip", PieceType.Savrip, Player.P0, Inner(0)),
            P("ghhhk", PieceType.Ghhhk, Player.P1, Outer(1)),
        });
        var outState = Engine.ApplyAttack(s, "molator", "ghhhk", Scripted(HIGH));
        Assert.AreEqual(Phase.GameOver, outState.Phase);
        Assert.AreEqual(Player.P0, outState.Winner);
    }

    [Test]
    public void DroppingTo1v1TriggersToTheDeath()
    {
        var s = MakeState(new[]
        {
            P("molator", PieceType.Molator, Player.P0, Outer(0)),
            P("ghhhk", PieceType.Ghhhk, Player.P1, Outer(1)),
            P("houjix", PieceType.Houjix, Player.P1, Inner(6)),
        });
        var outState = Engine.ApplyAttack(s, "molator", "ghhhk", Scripted(HIGH));
        Assert.AreEqual(Phase.ToTheDeath, outState.Phase);
        Assert.IsNotNull(outState.Duel);
    }

    [Test]
    public void ToTheDeathProducesAWinner()
    {
        var duelState = new GameState
        {
            Pieces = new List<GamePiece>
            {
                P("molator", PieceType.Molator, Player.P0, 0),
                P("houjix", PieceType.Houjix, Player.P1, 0),
            },
            Turn = Player.P0,
            ActionsRemaining = 0,
            Phase = Phase.ToTheDeath,
            Duel = new Duel { AId = "molator", BId = "houjix" },
            Log = new List<string>(),
        };
        var outState = Engine.ResolveToTheDeath(duelState, Scripted(HIGH, LOW));
        Assert.AreEqual(Phase.GameOver, outState.Phase);
        Assert.AreEqual(Player.P0, outState.Winner);
    }

    // ---- initial setup ----

    [Test]
    public void DealsFourPiecesToEachPlayerOnOuterRing()
    {
        var s = Engine.CreateInitialState(RngFactory.Make(42), Player.P0);
        Assert.AreEqual(4, Engine.PiecesOf(s, Player.P0).Count);
        Assert.AreEqual(4, Engine.PiecesOf(s, Player.P1).Count);
        Assert.AreEqual(8, s.Pieces.Select(p => p.Type).Distinct().Count());
        foreach (var p in s.Pieces) Assert.GreaterOrEqual(p.Space, 13);
    }

    [Test]
    public void IsReproducibleForAGivenSeed()
    {
        var a = Engine.CreateInitialState(RngFactory.Make(7), Player.P0);
        var b = Engine.CreateInitialState(RngFactory.Make(7), Player.P0);
        Assert.AreEqual(a.Pieces.Count, b.Pieces.Count);
        for (int i = 0; i < a.Pieces.Count; i++)
        {
            Assert.AreEqual(a.Pieces[i].Id, b.Pieces[i].Id);
            Assert.AreEqual(a.Pieces[i].Space, b.Pieces[i].Space);
            Assert.AreEqual(a.Pieces[i].Owner, b.Pieces[i].Owner);
        }
    }

    // ---- full-game simulation (exercises move/attack/push/duel/repetition end-to-end) ----

    [Test]
    public void SeededGamesPlayToCompletionWithoutErrors()
    {
        for (uint seed = 1; seed <= 25; seed++)
        {
            var rng = RngFactory.Make(seed);
            var state = Engine.CreateInitialState(rng, Player.P0);
            int steps = 0;
            while (state.Phase != Phase.GameOver && steps++ < 4000)
            {
                if (state.Phase == Phase.AwaitPush)
                {
                    state = Engine.ResolvePush(state, Bot.PushTarget(state));
                }
                else if (state.Phase == Phase.ToTheDeath)
                {
                    state = Engine.ResolveToTheDeath(state, rng);
                }
                else // Play
                {
                    var action = Bot.Action(state, rng);
                    if (action == null)
                        state = Engine.PassAction(state);
                    else if (action.Type == BotAction.Kind.Attack)
                        state = Engine.ApplyAttack(state, action.AttackerId, action.DefenderId, rng);
                    else
                        state = Engine.ApplyMove(state, action.PieceId, action.Dest);
                }
            }
            Assert.AreEqual(Phase.GameOver, state.Phase, $"seed {seed} did not terminate (steps={steps})");
        }
    }
}
