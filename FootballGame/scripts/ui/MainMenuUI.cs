using Godot;

namespace FootballGame;

/// <summary>
/// Controla o menu principal: navega para seleção de time, configurações ou encerra o jogo.
/// </summary>
public partial class MainMenuUI : Control
{
    private const string GameVersion = "0.1.0-dev";

    [Export] private Button _btnPlay;
    [Export] private Button _btnCareer;
    [Export] private Button _btnSettings;
    [Export] private Button _btnQuit;
    [Export] private Label  _lblVersion;

    public override void _Ready()
    {
        if (_lblVersion != null)
            _lblVersion.Text = $"v{GameVersion}";

        if (_btnPlay     != null) _btnPlay.Pressed     += OnPlayPressed;
        if (_btnCareer   != null) _btnCareer.Pressed   += OnCareerPressed;
        if (_btnSettings != null) _btnSettings.Pressed += OnSettingsPressed;
        if (_btnQuit     != null) _btnQuit.Pressed     += OnQuitPressed;
    }

    private void OnPlayPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.TeamSelect);
    }

    private void OnCareerPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.CareerSetup);
    }

    private void OnSettingsPressed()
    {
        var gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");
        gsm?.GoTo(GameStateManager.GameState.Settings);
    }

    private void OnQuitPressed() => GetTree().Quit();
}
