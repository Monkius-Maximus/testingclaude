using Godot;

namespace FootballGame;

/// <summary>
/// Hub do editor in-game: ponto de entrada para os editores de jogador e clube.
/// (Editor de estádio fica como trabalho futuro — ver docs/asset_placement_guide.md.)
/// </summary>
public partial class EditorHubUI : Control
{
    [Export] private Button _btnPlayerEditor;
    [Export] private Button _btnClubEditor;
    [Export] private Button _btnStadiumEditor;
    [Export] private Button _btnBack;
    [Export] private Label  _lblCounts;

    private GameStateManager _gsm;

    public override void _Ready()
    {
        _gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");

        if (_btnPlayerEditor  != null) _btnPlayerEditor.Pressed  += () => _gsm?.GoTo(GameStateManager.GameState.PlayerEditor);
        if (_btnClubEditor    != null) _btnClubEditor.Pressed    += () => _gsm?.GoTo(GameStateManager.GameState.ClubEditor);
        if (_btnStadiumEditor != null) _btnStadiumEditor.Pressed += () => _gsm?.GoTo(GameStateManager.GameState.StadiumEditor);
        if (_btnBack          != null) _btnBack.Pressed          += () => _gsm?.GoTo(GameStateManager.GameState.MainMenu);

        RefreshCounts();
    }

    private void RefreshCounts()
    {
        if (_lblCounts == null) return;
        int players  = CustomContent.LoadAllPlayers().Count;
        int teams    = CustomContent.LoadAllTeams().Count;
        int stadiums = CustomContent.LoadAllStadiums().Count;
        _lblCounts.Text = $"Conteúdo salvo:  {players} jogador(es)  ·  {teams} clube(s)  ·  {stadiums} estádio(s)";
    }
}
