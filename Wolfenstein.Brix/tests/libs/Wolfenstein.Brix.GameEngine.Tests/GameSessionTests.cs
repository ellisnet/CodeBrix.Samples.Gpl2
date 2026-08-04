using System.Linq;
using SilverAssertions;
using Wolfenstein.Brix.GameEngine.Assets;
using Wolfenstein.Brix.GameEngine.Logic;
using Xunit;

namespace Wolfenstein.Brix.GameEngine.Tests;

// Shell-flow checks: title -> menu -> difficulty -> get psyched ->
// playing, plus saves, intermission math and high scores, all headless.
public class GameSessionTests
{
    private static GameSession StartSession(IWolfStorage storage = null)
    {
        var assets = WolfAssets.Load(TestWl1.AssetsFolderPath);
        return new GameSession(assets, storage);
    }

    private static void Run(GameSession session, int tics, SessionInput input = default)
    {
        for (var i = 0; i < tics; i++)
        {
            session.Tic(input);
        }
    }

    private static readonly SessionInput Activate = new SessionInput { MenuActivate = true };
    private static readonly SessionInput Down = new SessionInput { MenuDown = true };
    private static readonly SessionInput Up = new SessionInput { MenuUp = true };
    private static readonly SessionInput Back = new SessionInput { MenuBack = true };

    private static GameSession StartPlaying(IWolfStorage storage = null)
    {
        var session = StartSession(storage);
        session.Tic(Activate);                       // Title -> menu
        session.Tic(Activate);                       // New Game -> difficulty
        session.Tic(Activate);                       // Baby -> get psyched
        Run(session, 75);                            // Get psyched -> playing
        return session;
    }

    [Fact]
    public void Boot_flow_reaches_gameplay()
    {
        var session = StartSession();
        session.Screen.Should().Be(SessionScreen.Title);

        session.Tic(Activate);
        session.Screen.Should().Be(SessionScreen.MainMenu);

        session.Tic(Activate); // New Game
        session.Screen.Should().Be(SessionScreen.DifficultySelect);

        session.Tic(Activate); // Can I play, Daddy?
        session.Screen.Should().Be(SessionScreen.GetPsyched);

        Run(session, 75);
        session.Screen.Should().Be(SessionScreen.Playing);
        session.Logic.Player.State.Should().Be(PlayState.Playing);
        session.Logic.Difficulty.Should().Be(DifficultyLevel.CanIPlayDaddy);
    }

    [Fact]
    public void Difficulty_menu_selection_picks_the_chosen_skill()
    {
        var session = StartSession();
        session.Tic(Activate);
        session.Tic(Activate);
        session.Tic(Down);
        session.Tic(Down);
        session.Tic(Activate); // Bring 'em on!
        Run(session, 75);
        session.Logic.Difficulty.Should().Be(DifficultyLevel.BringEmOn);
    }

    [Fact]
    public void Escape_during_play_opens_the_menu_and_back_to_game_resumes()
    {
        var session = StartPlaying();

        session.Tic(Back);
        session.Screen.Should().Be(SessionScreen.MainMenu);

        session.Tic(Back);
        session.Screen.Should().Be(SessionScreen.Playing);
    }

    [Fact]
    public void Save_and_load_round_trip_restores_the_game()
    {
        var storage = new MemoryWolfStorage();
        var session = StartPlaying(storage);

        // Walk a bit so there is real state to capture.
        Run(session, 30, new SessionInput { Game = new PlayerTicCommand { ForwardMove = 35 * 150 } });
        var savedX = session.Logic.Player.X;
        var savedY = session.Logic.Player.Y;

        // Save via the menu: Esc opens the menu with the cursor on
        // "Back To Game"; two ups reach "Save Game".
        session.Tic(Back);
        session.Tic(Up);
        session.Tic(Up);                              // Save Game
        session.Tic(Activate);                        // Slot list
        session.Screen.Should().Be(SessionScreen.SaveMenu);
        session.Tic(Activate);                        // Slot 0 -> name entry
        session.Tic(new SessionInput { TypedChar = 'A' });
        session.Tic(Activate);                        // Commit save
        session.Screen.Should().Be(SessionScreen.Playing);
        storage.GetSaveSlotDescriptions()[0].Should().Be("A");

        // Walk away from the saved spot, then load the slot back.
        Run(session, 40, new SessionInput { Game = new PlayerTicCommand { ForwardMove = -35 * 150 } });
        (session.Logic.Player.X != savedX || session.Logic.Player.Y != savedY).Should().BeTrue();

        session.Tic(Back);
        session.Tic(Up);
        session.Tic(Up);
        session.Tic(Up);                              // Load Game
        session.Tic(Activate);
        session.Screen.Should().Be(SessionScreen.LoadMenu);
        session.Tic(Activate);                        // Slot 0
        session.Screen.Should().Be(SessionScreen.Playing);
        session.Logic.Player.X.Should().Be(savedX);
        session.Logic.Player.Y.Should().Be(savedY);
    }

    [Fact]
    public void Level_exit_shows_intermission_and_advances()
    {
        var session = StartPlaying();
        var logic = session.Logic;

        // Trip the elevator by teleporting beside one (test shortcut).
        PlaceAtElevator(logic);
        Run(session, 3, new SessionInput { Game = new PlayerTicCommand { Use = true } });

        session.Screen.Should().Be(SessionScreen.Intermission);
        session.Intermission.Should().NotBeNull();
        session.Intermission.Floor.Should().Be(1);

        session.Tic(Activate);
        session.Screen.Should().Be(SessionScreen.GetPsyched);
        Run(session, 75);
        session.Screen.Should().Be(SessionScreen.Playing);
        logic.Level.LevelIndex.Should().Be(1);
    }

    [Fact]
    public void Intermission_grants_the_par_time_bonus()
    {
        var session = StartPlaying();
        var logic = session.Logic;
        var scoreBefore = logic.Player.Score;

        PlaceAtElevator(logic);
        Run(session, 3, new SessionInput { Game = new PlayerTicCommand { Use = true } });

        // Exited within seconds of starting: the full 90-second par of
        // E1L1 pays 500 per second under.
        (session.Intermission.Bonus >= 44000).Should().BeTrue();
        (logic.Player.Score > scoreBefore).Should().BeTrue();
    }

    [Fact]
    public void High_score_table_defaults_to_the_classic_names()
    {
        var session = StartSession();
        session.HighScores.Length.Should().Be(7);
        session.HighScores.All(h => h.Score == 10000).Should().BeTrue();
    }

    private static void PlaceAtElevator(WolfLogic logic)
    {
        var level = logic.Level;
        for (var x = 1; x < 63; x++)
        {
            for (var y = 1; y < 63; y++)
            {
                if ((level.TileMap[x, y] & TileFlag.Elevator) == 0)
                {
                    continue;
                }

                foreach (var dir in new[] { Dir4.East, Dir4.West })
                {
                    var px = x - WolfMath.Dx4Dir[(int)dir];
                    var py = y - WolfMath.Dy4Dir[(int)dir];
                    if ((level.TileMap[px, py] & (TileFlag.SolidTile | TileFlag.Door)) != 0 ||
                        (level.TileMap[px, py] & TileFlag.SecretLevel) != 0)
                    {
                        continue;
                    }

                    logic.Player.X = WolfMath.Tile2Pos(px);
                    logic.Player.Y = WolfMath.Tile2Pos(py);
                    logic.Player.TileX = px;
                    logic.Player.TileY = py;
                    logic.Player.Angle = WolfMath.Dir4Angle[(int)dir];
                    return;
                }
            }
        }
    }
}
