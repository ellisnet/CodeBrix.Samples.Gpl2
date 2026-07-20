using System;
using System.Collections.Generic;
using CodeBrix.Platform.GameEngine.Input.Gamepad;
using CodeBrix.Platform.GameEngine.Sdl2.Gamepad;
using ManagedDoom;
using SilverAssertions;
using Xunit;

namespace Doom.Brix.Game.Tests;

/// <summary>
/// A stand-in for a connected controller. The engine's adapter is sealed around a live
/// SDL2 handle, so these drive the interface it implements instead.
/// </summary>
internal sealed class FakeGamepadAdapter : IGamepadAdapter
{
    private readonly HashSet<string> pressed = new HashSet<string>();

    public string GamepadId { get; set; } = "fake-0";

    public IReadOnlyCollection<string> PressedButtons => pressed;

    public GamepadStickState? LeftStick { get; set; }

    public GamepadStickState? RightStick { get; set; }

    public float LeftTrigger { get; set; }

    public float RightTrigger { get; set; }

    public void Press(string button) => pressed.Add(button);

    public void Release(string button) => pressed.Remove(button);
}

internal sealed class FakeGamepadManager : IGamepadManager<IGamepadAdapter>
{
    private readonly List<IGamepadAdapter> adapters = new List<IGamepadAdapter>();

    public IReadOnlyCollection<IGamepadAdapter> ConnectedAdapters => adapters;

    public void Update()
    {
        // The engine drives the real manager; nothing to refresh here.
    }

    public void Connect(IGamepadAdapter adapter) => adapters.Add(adapter);

    public void DisconnectAll() => adapters.Clear();
}

public class DoomGamepadInputTests
{
    private const bool MenuLive = true;
    private const bool InPlay = false;

    private static (DoomGamepadInput Input, FakeGamepadAdapter Pad) NewInputWithPad()
    {
        //Arrange
        var pad = new FakeGamepadAdapter();
        var manager = new FakeGamepadManager();
        manager.Connect(pad);
        return (new DoomGamepadInput(manager), pad);
    }

    private static List<DoomKey> DrainKeys(DoomGamepadInput input)
    {
        var keys = new List<DoomKey>();
        while (input.TryDequeueMenuKey(out var key))
        {
            keys.Add(key);
        }

        return keys;
    }

    [Fact]
    public void no_manager_yields_an_empty_sample()
    {
        //Arrange
        var input = new DoomGamepadInput(null);

        //Act
        input.BeginTic(MenuLive, null);

        //Assert
        input.Sample.Forward.Should().Be(0f);
        input.Sample.Side.Should().Be(0f);
        input.Sample.Turn.Should().Be(0f);
        input.Sample.Fire.Should().BeFalse();
        input.Sample.Use.Should().BeFalse();
        input.Sample.Run.Should().BeFalse();
        input.Sample.WeaponSlot.Should().Be(0);
        DrainKeys(input).Should().BeEmpty();
    }

    [Fact]
    public void resting_stick_drift_is_deadzoned_away()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.LeftStick = new GamepadStickState(0.08f, -0.06f);
        pad.RightStick = new GamepadStickState(0.05f, 0f);

        //Act
        input.BeginTic(InPlay, null);

        //Assert
        input.Sample.Forward.Should().Be(0f);
        input.Sample.Side.Should().Be(0f);
        input.Sample.Turn.Should().Be(0f);
    }

    [Fact]
    public void a_pushed_stick_moves_and_turns()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.LeftStick = new GamepadStickState(0.5f, 0.75f);   // X right, Y up.
        pad.RightStick = new GamepadStickState(-0.9f, 0f);

        //Act
        input.BeginTic(InPlay, null);

        //Assert
        input.Sample.Forward.Should().Be(0.75f);
        input.Sample.Side.Should().Be(0.5f);
        input.Sample.Turn.Should().Be(-0.9f);
    }

    [Fact]
    public void a_corner_held_stick_does_not_exceed_full_speed()
    {
        //Arrange
        // X and Y are clamped independently, so a hard diagonal legitimately reports a
        // magnitude above 1; using it unshrunk is the diagonal-speed-boost bug.
        var (input, pad) = NewInputWithPad();
        pad.LeftStick = new GamepadStickState(1f, 1f);

        //Act
        input.BeginTic(InPlay, null);

        //Assert
        var magnitude = MathF.Sqrt(
            (input.Sample.Forward * input.Sample.Forward) +
            (input.Sample.Side * input.Sample.Side));
        magnitude.Should().BeApproximately(1f, 0.0001f);
    }

    [Fact]
    public void triggers_are_half_pull_buttons()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.RightTrigger = 0.4f;
        pad.LeftTrigger = 0.4f;

        //Act
        input.BeginTic(InPlay, null);

        //Assert
        input.Sample.Fire.Should().BeFalse();
        input.Sample.Run.Should().BeFalse();

        //Act
        pad.RightTrigger = 0.6f;
        pad.LeftTrigger = 0.6f;
        input.BeginTic(InPlay, null);

        //Assert
        input.Sample.Fire.Should().BeTrue();
        input.Sample.Run.Should().BeTrue();
    }

    [Fact]
    public void a_is_use_while_held()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.A);

        //Act
        input.BeginTic(InPlay, null);
        var firstTic = input.Sample.Use;
        input.BeginTic(InPlay, null);
        var secondTic = input.Sample.Use;

        //Assert -- held state, not an edge: it stays true across tics.
        firstTic.Should().BeTrue();
        secondTic.Should().BeTrue();
    }

    [Fact]
    public void start_and_back_raise_their_keys_on_the_press_edge_only()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.Start);
        pad.Press(SdlGamepadButtons.Back);

        //Act
        input.BeginTic(InPlay, null);
        var pressTic = DrainKeys(input);
        input.BeginTic(InPlay, null);
        var heldTic = DrainKeys(input);

        //Assert
        pressTic.Should().BeEquivalentTo(new[] { DoomKey.Escape, DoomKey.Tab });
        heldTic.Should().BeEmpty();
    }

    [Fact]
    public void start_and_back_are_not_gated_by_the_menu_state()
    {
        //Arrange -- in play, where the nav keys are suppressed.
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.Start);

        //Act
        input.BeginTic(InPlay, null);

        //Assert
        DrainKeys(input).Should().BeEquivalentTo(new[] { DoomKey.Escape });
    }

    [Fact]
    public void the_face_buttons_are_enter_and_escape_in_a_menu()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.A);
        pad.Press(SdlGamepadButtons.B);

        //Act
        input.BeginTic(MenuLive, null);

        //Assert
        DrainKeys(input).Should().BeEquivalentTo(new[] { DoomKey.Enter, DoomKey.Escape });
    }

    [Fact]
    public void the_dpad_navigates_a_menu()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.DPadDown);

        //Act
        input.BeginTic(MenuLive, null);

        //Assert
        DrainKeys(input).Should().BeEquivalentTo(new[] { DoomKey.Down });
    }

    [Fact]
    public void the_left_stick_navigates_a_menu_only_when_pushed_well_past_the_move_deadzone()
    {
        //Arrange -- past the movement deadzone (0.20) but short of the menu one (0.60).
        var (input, pad) = NewInputWithPad();
        pad.LeftStick = new GamepadStickState(0f, 0.4f);

        //Act
        input.BeginTic(MenuLive, null);

        //Assert
        DrainKeys(input).Should().BeEmpty();

        //Act
        pad.LeftStick = new GamepadStickState(0f, 0.8f);
        input.BeginTic(MenuLive, null);

        //Assert
        DrainKeys(input).Should().BeEquivalentTo(new[] { DoomKey.Up });
    }

    [Fact]
    public void navigation_keys_are_suppressed_in_play()
    {
        //Arrange -- ungated, a repeating Up would reach AutoMap.DoEvent, which reads the
        // arrow keys as press-and-hold pan and would never see a matching release.
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.DPadUp);

        //Act
        input.BeginTic(InPlay, null);

        //Assert
        DrainKeys(input).Should().BeEmpty();
    }

    [Fact]
    public void a_held_direction_repeats_after_a_delay_and_then_at_a_steady_rate()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.DPadDown);

        //Act -- the press tic plus the next twenty, at Doom's 35 Hz.
        var firedOn = new List<int>();
        for (var tic = 0; tic < 21; tic++)
        {
            input.BeginTic(MenuLive, null);
            if (DrainKeys(input).Count > 0)
            {
                firedOn.Add(tic);
            }
        }

        //Assert -- immediately, then after a 12-tic delay, then every 4.
        firedOn.Should().BeEquivalentTo(new[] { 0, 12, 16, 20 });
    }

    [Fact]
    public void releasing_the_direction_restarts_the_repeat_delay()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.DPadDown);
        input.BeginTic(MenuLive, null);
        DrainKeys(input);

        //Act
        pad.Release(SdlGamepadButtons.DPadDown);
        input.BeginTic(MenuLive, null);
        var releasedTic = DrainKeys(input);
        pad.Press(SdlGamepadButtons.DPadDown);
        input.BeginTic(MenuLive, null);
        var repressedTic = DrainKeys(input);

        //Assert
        releasedTic.Should().BeEmpty();
        repressedTic.Should().BeEquivalentTo(new[] { DoomKey.Down });
    }

    [Fact]
    public void the_vertical_axis_wins_a_diagonal()
    {
        //Arrange -- every Doom menu is a vertical list.
        var (input, pad) = NewInputWithPad();
        pad.LeftStick = new GamepadStickState(0.8f, 0.8f);

        //Act
        input.BeginTic(MenuLive, null);

        //Assert
        DrainKeys(input).Should().BeEquivalentTo(new[] { DoomKey.Up });
    }

    [Fact]
    public void reset_drops_queued_keys_and_forgets_held_buttons()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.Start);
        input.BeginTic(MenuLive, null);

        //Act
        input.Reset();
        var afterReset = DrainKeys(input);
        input.BeginTic(MenuLive, null);
        var nextTic = DrainKeys(input);

        //Assert -- Start is still held, but the press edge is seen afresh.
        afterReset.Should().BeEmpty();
        nextTic.Should().BeEquivalentTo(new[] { DoomKey.Escape });
    }

    [Fact]
    public void the_shoulder_buttons_do_nothing_outside_a_game()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.RightShoulder);

        //Act
        input.BeginTic(MenuLive, null);

        //Assert -- zero is the sample's "change nothing", not slot zero.
        input.Sample.WeaponSlot.Should().Be(0);
    }

    [Theory]
    [InlineData(WeaponType.Fist, 0)]
    [InlineData(WeaponType.Chainsaw, 0)]
    [InlineData(WeaponType.Pistol, 1)]
    [InlineData(WeaponType.Shotgun, 2)]
    [InlineData(WeaponType.SuperShotgun, 2)]
    [InlineData(WeaponType.Chaingun, 3)]
    [InlineData(WeaponType.Bfg, 6)]
    public void each_weapon_maps_to_the_slot_that_selects_it(WeaponType weapon, int expectedSlot)
    {
        //Act
        var slot = DoomGamepadInput.SlotOf(weapon);

        //Assert
        slot.Should().Be(expectedSlot);
    }

    [Fact]
    public void the_two_shared_slots_count_either_weapon_as_owning_them()
    {
        //Arrange -- the chainsaw with no fist, and the super shotgun with no shotgun.
        var owned = Owning(WeaponType.Chainsaw, WeaponType.SuperShotgun);

        //Assert
        DoomGamepadInput.OwnsSlot(owned, (int)WeaponType.Fist).Should().BeTrue();
        DoomGamepadInput.OwnsSlot(owned, (int)WeaponType.Shotgun).Should().BeTrue();
        DoomGamepadInput.OwnsSlot(owned, (int)WeaponType.Chaingun).Should().BeFalse();
    }

    [Fact]
    public void cycling_forward_skips_the_slots_that_are_empty()
    {
        //Arrange -- the shareware starting kit plus a chaingun.
        var owned = Owning(WeaponType.Fist, WeaponType.Pistol, WeaponType.Chaingun);

        //Act
        var fromPistol = DoomGamepadInput.NextWeaponSlot(
            DoomGamepadInput.SlotOf(WeaponType.Pistol), owned, forward: true);

        //Assert -- the shotgun slot is empty, so the chaingun is next.
        fromPistol.Should().Be((int)WeaponType.Chaingun);
    }

    [Fact]
    public void cycling_wraps_around_in_both_directions()
    {
        //Arrange
        var owned = Owning(WeaponType.Fist, WeaponType.Pistol, WeaponType.Chaingun);

        //Act
        var forwardFromChaingun = DoomGamepadInput.NextWeaponSlot(
            DoomGamepadInput.SlotOf(WeaponType.Chaingun), owned, forward: true);
        var backwardFromFist = DoomGamepadInput.NextWeaponSlot(
            DoomGamepadInput.SlotOf(WeaponType.Fist), owned, forward: false);

        //Assert
        forwardFromChaingun.Should().Be((int)WeaponType.Fist);
        backwardFromFist.Should().Be((int)WeaponType.Chaingun);
    }

    [Fact]
    public void cycling_reports_nothing_when_only_one_slot_is_occupied()
    {
        //Arrange -- the fist alone; there is nothing to cycle to.
        var owned = Owning(WeaponType.Fist);

        //Act
        var next = DoomGamepadInput.NextWeaponSlot(
            DoomGamepadInput.SlotOf(WeaponType.Fist), owned, forward: true);

        //Assert
        next.Should().Be(-1);
    }

    private static bool[] Owning(params WeaponType[] weapons)
    {
        var owned = new bool[(int)WeaponType.Count];
        foreach (var weapon in weapons)
        {
            owned[(int)weapon] = true;
        }

        return owned;
    }
}
