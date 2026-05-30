using Godot;

namespace FootballGame;

/// <summary>
/// Singleton global de estados do jogo. Gerencia transições entre telas
/// e mantém o resultado da última partida.
/// Registrado como Autoload: GameStateManager="*res://scripts/core/GameStateManager.cs"
/// </summary>
public partial class GameStateManager : Node
{
    public enum GameState
    {
        Boot,
        MainMenu,
        TeamSelect,
        Settings,
        Match,
        Paused,
        HalfTime,
        Result,
        CareerSetup,
        CareerHub,
        TransferMarket,
        EditorHub,
        PlayerEditor,
        ClubEditor
    }

    public struct MatchResult
    {
        public int        ScoreHome;
        public int        ScoreAway;
        public int        MinutesPlayed;
        public GameManager.MatchMode Mode;
        public MatchStats Stats; // referência às stats da partida (null se não disponível)
    }

    // ── Caminhos de cena ──────────────────────────────────────────
    private const string SceneMainMenu    = "res://scenes/ui/MainMenu.tscn";
    private const string SceneTeamSelect  = "res://scenes/ui/TeamSelect.tscn";
    private const string SceneSettings    = "res://scenes/ui/Settings.tscn";
    private const string SceneMatch       = "res://scenes/match/Match.tscn";
    private const string SceneResult      = "res://scenes/ui/Result.tscn";
    private const string SceneCareerSetup    = "res://scenes/ui/CareerSetup.tscn";
    private const string SceneCareerHub      = "res://scenes/ui/CareerHub.tscn";
    private const string SceneTransferMarket = "res://scenes/ui/TransferMarket.tscn";
    private const string SceneEditorHub      = "res://scenes/ui/EditorHub.tscn";
    private const string ScenePlayerEditor   = "res://scenes/ui/PlayerEditor.tscn";
    private const string SceneClubEditor     = "res://scenes/ui/ClubEditor.tscn";

    [Signal] public delegate void StateChangedEventHandler(int previous, int next);

    public GameState      CurrentState       { get; private set; } = GameState.Boot;
    public MatchResult    CurrentMatchResult { get; set; }

    // Cena de onde viemos antes de abrir Settings (para voltar corretamente)
    private GameState _stateBeforeSettings = GameState.MainMenu;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        // Estado inicial — a cena principal já está configurada como MainMenu.tscn
        CurrentState = GameState.MainMenu;
    }

    // ── API pública ───────────────────────────────────────────────

    /// <summary>Transita para o estado indicado e, se necessário, troca de cena.</summary>
    public void GoTo(GameState next)
    {
        var prev = CurrentState;
        CurrentState = next;
        EmitSignal(SignalName.StateChanged, (int)prev, (int)next);

        switch (next)
        {
            case GameState.MainMenu:
                GetTree().ChangeSceneToFile(SceneMainMenu);
                break;
            case GameState.TeamSelect:
                GetTree().ChangeSceneToFile(SceneTeamSelect);
                break;
            case GameState.Settings:
                _stateBeforeSettings = prev;
                GetTree().ChangeSceneToFile(SceneSettings);
                break;
            case GameState.Match:
                GetTree().ChangeSceneToFile(SceneMatch);
                break;
            // HalfTime é tratado como overlay sobre a partida ao vivo
            // (ver MatchBootstrap.OnHalfTime), não como troca de cena.
            case GameState.Result:
                GetTree().ChangeSceneToFile(SceneResult);
                break;
            case GameState.CareerSetup:
                GetTree().ChangeSceneToFile(SceneCareerSetup);
                break;
            case GameState.CareerHub:
                GetTree().ChangeSceneToFile(SceneCareerHub);
                break;
            case GameState.TransferMarket:
                GetTree().ChangeSceneToFile(SceneTransferMarket);
                break;
            case GameState.EditorHub:
                GetTree().ChangeSceneToFile(SceneEditorHub);
                break;
            case GameState.PlayerEditor:
                GetTree().ChangeSceneToFile(ScenePlayerEditor);
                break;
            case GameState.ClubEditor:
                GetTree().ChangeSceneToFile(SceneClubEditor);
                break;
        }
    }

    /// <summary>Volta ao estado anterior às configurações.</summary>
    public void ReturnFromSettings() => GoTo(_stateBeforeSettings);

    /// <summary>Pausa/despausa a árvore de cena.</summary>
    public void SetPaused(bool paused)
    {
        CurrentState = paused ? GameState.Paused : GameState.Match;
        GetTree().Paused = paused;
    }
}
