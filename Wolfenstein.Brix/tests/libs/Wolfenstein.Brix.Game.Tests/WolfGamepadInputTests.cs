using System;
using System.Collections.Generic;
using CodeBrix.Platform.GameEngine.Input.Gamepad;
using CodeBrix.Platform.GameEngine.Sdl2.Gamepad;
using SilverAssertions;
using Wolfenstein.Brix.GameEngine.Logic;
using Xunit;

namespace Wolfenstein.Brix.Game.Tests;

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

public class WolfGamepadInputTests
{
    private static (WolfGamepadInput Input, FakeGamepadAdapter Pad) NewInputWithPad()
    {
        //Arrange
        var pad = new FakeGamepadAdapter();
        var manager = new FakeGamepadManager();
        manager.Connect(pad);
        return (new WolfGamepadInput(manager), pad);
    }

    private static PlayerState NewPlayer(Weapon current, int ammo, params int[] ownedWeapons)
    {
        var player = new PlayerState
        {
            CurrentWeapon = current,
            Ammo = ammo,
        };

        foreach (var weapon in ownedWeapons)
        {
            player.Items |= (PlayerItems)((int)PlayerItems.Weapon1 << weapon);
        }

        return player;
    }

    [Fact]
    public void no_manager_yields_an_inert_sample()
    {
        //Arrange
        var input = new WolfGamepadInput(null);

        //Act
        var sample = input.Sample(null);

        //Assert -- every field has to be the "change nothing" value, because this is what
        // a keyboard-only game folds in on every one of its 70 tics a second.
        sample.Forward.Should().Be(0f);
        sample.Side.Should().Be(0f);
        sample.Turn.Should().Be(0f);
        sample.Fire.Should().BeFalse();
        sample.Use.Should().BeFalse();
        sample.Run.Should().BeFalse();
        sample.WeaponSlot.Should().Be(0);
        sample.MenuUp.Should().BeFalse();
        sample.MenuDown.Should().BeFalse();
        sample.MenuActivate.Should().BeFalse();
        sample.MenuBack.Should().BeFalse();
    }

    [Fact]
    public void resting_stick_drift_is_deadzoned_away()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.LeftStick = new GamepadStickState(0.08f, -0.06f);
        pad.RightStick = new GamepadStickState(0.05f, 0f);

        //Act
        var sample = input.Sample(null);

        //Assert
        sample.Forward.Should().Be(0f);
        sample.Side.Should().Be(0f);
        sample.Turn.Should().Be(0f);
    }

    [Fact]
    public void a_pushed_stick_moves_and_turns()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.LeftStick = new GamepadStickState(0.5f, 0.75f);   // X right, Y up.
        pad.RightStick = new GamepadStickState(-0.9f, 0f);

        //Act
        var sample = input.Sample(null);

        //Assert
        sample.Forward.Should().Be(0.75f);
        sample.Side.Should().Be(0.5f);
        sample.Turn.Should().Be(-0.9f);
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
        var sample = input.Sample(null);

        //Assert
        var magnitude = MathF.Sqrt((sample.Forward * sample.Forward) + (sample.Side * sample.Side));
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
        var light = input.Sample(null);
        pad.RightTrigger = 0.6f;
        pad.LeftTrigger = 0.6f;
        var firm = input.Sample(null);

        //Assert
        light.Fire.Should().BeFalse();
        light.Run.Should().BeFalse();
        firm.Fire.Should().BeTrue();
        firm.Run.Should().BeTrue();
    }

    [Fact]
    public void a_is_use_while_held_but_activates_only_on_the_press_edge()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.A);

        //Act
        var pressTic = input.Sample(null);
        var heldTic = input.Sample(null);

        //Assert
        pressTic.Use.Should().BeTrue();
        pressTic.MenuActivate.Should().BeTrue();
        heldTic.Use.Should().BeTrue();
        heldTic.MenuActivate.Should().BeFalse();
    }

    [Fact]
    public void both_b_and_start_mean_back()
    {
        //Arrange
        var (input, padB) = NewInputWithPad();
        padB.Press(SdlGamepadButtons.B);
        var (inputStart, padStart) = NewInputWithPad();
        padStart.Press(SdlGamepadButtons.Start);

        //Act
        var fromB = input.Sample(null);
        var fromStart = inputStart.Sample(null);

        //Assert
        fromB.MenuBack.Should().BeTrue();
        fromStart.MenuBack.Should().BeTrue();
    }

    [Fact]
    public void the_dpad_navigates_a_menu()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.DPadDown);

        //Act
        var sample = input.Sample(null);

        //Assert
        sample.MenuDown.Should().BeTrue();
        sample.MenuUp.Should().BeFalse();
    }

    [Fact]
    public void the_left_stick_navigates_only_when_pushed_well_past_the_move_deadzone()
    {
        //Arrange -- past the movement deadzone (0.20) but short of the menu one (0.60).
        var (input, pad) = NewInputWithPad();
        pad.LeftStick = new GamepadStickState(0f, 0.4f);

        //Act
        var nudged = input.Sample(null);
        pad.LeftStick = new GamepadStickState(0f, 0.8f);
        var pushed = input.Sample(null);

        //Assert
        nudged.MenuUp.Should().BeFalse();
        pushed.MenuUp.Should().BeTrue();
    }

    [Fact]
    public void a_held_direction_repeats_after_a_delay_and_then_at_a_steady_rate()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.DPadDown);

        //Act -- the press tic plus the next forty, at Wolfenstein's 70 Hz.
        var firedOn = new List<int>();
        for (var tic = 0; tic < 41; tic++)
        {
            if (input.Sample(null).MenuDown)
            {
                firedOn.Add(tic);
            }
        }

        //Assert -- immediately, then after a 24-tic delay, then every 8.
        firedOn.Should().BeEquivalentTo(new[] { 0, 24, 32, 40 });
    }

    [Fact]
    public void releasing_the_direction_restarts_the_repeat_delay()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.DPadDown);
        input.Sample(null);

        //Act
        pad.Release(SdlGamepadButtons.DPadDown);
        var releasedTic = input.Sample(null);
        pad.Press(SdlGamepadButtons.DPadDown);
        var repressedTic = input.Sample(null);

        //Assert
        releasedTic.MenuDown.Should().BeFalse();
        repressedTic.MenuDown.Should().BeTrue();
    }

    [Fact]
    public void the_shoulder_buttons_do_nothing_outside_a_game()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        pad.Press(SdlGamepadButtons.RightShoulder);

        //Act
        var sample = input.Sample(null);

        //Assert
        sample.WeaponSlot.Should().Be(0);
    }

    [Fact]
    public void the_right_shoulder_selects_the_next_owned_weapon()
    {
        //Arrange -- the starting kit plus a chain gun, no machine gun.
        var (input, pad) = NewInputWithPad();
        var player = NewPlayer(Weapon.Pistol, ammo: 8, (int)Weapon.Knife, (int)Weapon.Pistol, (int)Weapon.ChainGun);
        pad.Press(SdlGamepadButtons.RightShoulder);

        //Act
        var sample = input.Sample(player);

        //Assert -- one-based, so the chain gun is slot 4.
        sample.WeaponSlot.Should().Be((int)Weapon.ChainGun + 1);
    }

    [Fact]
    public void the_shoulder_buttons_fire_on_the_press_edge_only()
    {
        //Arrange
        var (input, pad) = NewInputWithPad();
        var player = NewPlayer(Weapon.Pistol, ammo: 8, (int)Weapon.Knife, (int)Weapon.Pistol);
        pad.Press(SdlGamepadButtons.RightShoulder);

        //Act
        var pressTic = input.Sample(player);
        var heldTic = input.Sample(player);

        //Assert
        pressTic.WeaponSlot.Should().Be((int)Weapon.Knife + 1);
        heldTic.WeaponSlot.Should().Be(0);
    }

    [Fact]
    public void cycling_wraps_around_in_both_directions()
    {
        //Arrange
        var player = NewPlayer(Weapon.Knife, ammo: 8, (int)Weapon.Knife, (int)Weapon.Pistol);

        //Act
        var backward = WolfGamepadInput.NextWeaponSlot(player, forward: false);
        player.CurrentWeapon = Weapon.Pistol;
        var forward = WolfGamepadInput.NextWeaponSlot(player, forward: true);

        //Assert
        backward.Should().Be((int)Weapon.Pistol + 1);
        forward.Should().Be((int)Weapon.Knife + 1);
    }

    [Fact]
    public void an_empty_pouch_leaves_only_the_knife_selectable()
    {
        //Arrange -- PlayerLogic.ChangeWeapon refuses every weapon but the knife at zero
        // ammo, so cycling has to skip them rather than pick one and be refused.
        var player = NewPlayer(Weapon.Pistol, ammo: 0,
            (int)Weapon.Knife, (int)Weapon.Pistol, (int)Weapon.MachineGun, (int)Weapon.ChainGun);

        //Act
        var next = WolfGamepadInput.NextWeaponSlot(player, forward: true);

        //Assert
        next.Should().Be((int)Weapon.Knife + 1);
    }

    [Fact]
    public void cycling_reports_nothing_when_only_the_current_weapon_is_selectable()
    {
        //Arrange
        var player = NewPlayer(Weapon.Knife, ammo: 0, (int)Weapon.Knife, (int)Weapon.Pistol);

        //Act
        var next = WolfGamepadInput.NextWeaponSlot(player, forward: true);

        //Assert
        next.Should().Be(0);
    }
}
