using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballGame;

/// <summary>
/// Janela de transferências da carreira. Mostra mercado de jogadores
/// disponíveis e permite comprar (se houver saldo) ou vender do elenco.
/// </summary>
public partial class TransferMarketUI : Control
{
    [Export] private Label         _lblBalance;
    [Export] private VBoxContainer _marketList;
    [Export] private VBoxContainer _squadList;
    [Export] private Button        _btnBack;
    [Export] private Label         _lblStatus;

    private CareerManager    _cm;
    private GameStateManager _gsm;
    private List<PlayerData> _available = new();
    private TeamData         _playerTeam;

    public override void _Ready()
    {
        _cm  = GetNodeOrNull<CareerManager>("/root/CareerManager");
        _gsm = GetNodeOrNull<GameStateManager>("/root/GameStateManager");

        if (_btnBack != null) _btnBack.Pressed += () => _gsm?.GoTo(GameStateManager.GameState.CareerHub);

        GenerateMarket();
        Refresh();
    }

    private void GenerateMarket()
    {
        _available.Clear();
        if (_cm?.Current == null) return;

        _playerTeam = _cm.GetTeamData(_cm.Current.PlayerTeamId);
        var rng     = new Random();
        int overall = _playerTeam?.OverallRating ?? 70;

        // 15 jogadores disponíveis no mercado com estrelas variadas
        for (int i = 0; i < 15; i++)
        {
            int ovr   = Mathf.Clamp(overall + rng.Next(-15, 20), 50, 95);
            var data  = PlayerGenerator.Create(null, ovr, "market", i);
            data.FullName  = $"Jogador {(char)('A' + i)} {rng.Next(10, 99)}";
            data.ShortName = data.FullName;
            data.Age       = rng.Next(18, 32);
            data.MarketValue = ovr * ovr * 200;
            _available.Add(data);
        }
    }

    private void Refresh()
    {
        if (_cm?.Current == null) return;

        if (_lblBalance != null)
            _lblBalance.Text = $"Saldo: R$ {_cm.Current.Balance:N0}";

        BuildMarketList();
        BuildSquadList();
    }

    private void BuildMarketList()
    {
        ClearContainer(_marketList);
        if (_marketList == null) return;

        foreach (var p in _available)
        {
            var row  = new HBoxContainer();
            var lbl  = new Label
            {
                Text              = $"{p.ShortName,-18}  OVR {p.OverallRating,2}  Idade {p.Age,2}  R$ {p.MarketValue:N0}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            var btn = new Button { Text = "Comprar" };

            var captured = p;
            bool canAfford = _cm.Current.Balance >= p.MarketValue;
            btn.Disabled = !canAfford;
            btn.Pressed += () => BuyPlayer(captured);

            row.AddChild(lbl);
            row.AddChild(btn);
            _marketList.AddChild(row);
        }
    }

    private void BuildSquadList()
    {
        ClearContainer(_squadList);
        if (_squadList == null || _playerTeam == null) return;

        var squad = _playerTeam.Squad;
        for (int i = 0; i < squad.Count; i++)
        {
            var p   = squad[i];
            var row = new HBoxContainer();
            var lbl = new Label
            {
                Text              = $"{p.ShortName,-18}  OVR {p.OverallRating,2}  R$ {p.MarketValue / 2:N0}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            var btn = new Button { Text = "Vender" };
            int idx = i;
            btn.Pressed += () => SellPlayer(idx);
            row.AddChild(lbl);
            row.AddChild(btn);
            _squadList.AddChild(row);
        }
    }

    private void BuyPlayer(PlayerData p)
    {
        if (_cm.Current.Balance < p.MarketValue)
        {
            ShowStatus("Saldo insuficiente!");
            return;
        }
        _cm.Current.Balance -= p.MarketValue;
        _available.Remove(p);

        if (_playerTeam != null)
            _playerTeam.Squad.Add(p);

        _cm.Save();
        ShowStatus($"{p.ShortName} contratado!");
        Refresh();
    }

    private void SellPlayer(int idx)
    {
        if (_playerTeam == null || idx >= _playerTeam.Squad.Count) return;
        var p = _playerTeam.Squad[idx];
        _cm.Current.Balance += p.MarketValue / 2;
        _playerTeam.Squad.RemoveAt(idx);
        _available.Add(p);
        _cm.Save();
        ShowStatus($"{p.ShortName} vendido por R$ {p.MarketValue / 2:N0}!");
        Refresh();
    }

    private void ShowStatus(string msg)
    {
        if (_lblStatus != null) _lblStatus.Text = msg;
    }

    private static void ClearContainer(Container c)
    {
        if (c == null) return;
        foreach (Node child in c.GetChildren()) child.QueueFree();
    }
}
