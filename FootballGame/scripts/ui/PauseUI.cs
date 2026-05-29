using Godot;

namespace FootballGame;

/// <summary>
/// Menu de pausa. Aparece sobre a partida como CanvasLayer.
/// Ativa/desativa com a ação <c>p1_pause</c> (Escape).
/// </summary>
public partial class PauseUI : CanvasLayer
{
    [Export] private Button _btnContinue;
    [Export] private Button _btnSettings;
    [Export] private Button _btnAbandon;
    [Export] private Control _panel;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Hide();

        if (_btnContinue != null) _btnContinue.Pressed += OnContinuePressed;
        if (_btnSettings != null) _btnSettings.Pressed += OnSettingsPressed;
        if (_btnAbandon  != null) _btnAbandon.Pressed  += OnAbandonPressed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("p1_pause"))
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Alterna o estado de pausa.</summary>
    public void TogglePause()
    {
        bool nowPaused = !GetTree().Paused;
        GetTree().Paused = nowPaused;

        if (_panel != null) _panel.Visible = nowPaused;
        if (nowPaused) Show(); else Hide();
    }

    private void OnContinuePressed()
    {
        GetTree().Paused = false;
        if (_panel != null) _panel.Visible = false;
        Hide();
    }

    private void OnSettingsPressed()
    {
        GetTree().Paused = false;
        Hide();
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.Settings);
    }

    private void OnAbandonPressed()
    {
        GetTree().Paused = false;
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.MainMenu);
    }
}
