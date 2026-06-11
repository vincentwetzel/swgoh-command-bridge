using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using swgoh_command_bridge.Core;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ModAssignmentService _modAssignmentService;

    [ObservableProperty]
    private ViewModelBase _currentView;

    public string Greeting => "Character Mod Priorities";

    public ObservableCollection<Character> Characters { get; }
    
    public ModThresholdsViewModel ModThresholdsViewModel { get; }

    public MainWindowViewModel()
    {
        _modAssignmentService = new ModAssignmentService();
        Characters = new ObservableCollection<Character>
        {
            new Character("JEDIKNIGHTREVAN", "Jedi Knight Revan", 85, 13, 7, 30000, 10, new Dictionary<ModSlot, GameMod>()),
            new Character("DARTHTRAYA", "Darth Traya", 85, 12, 5, 28000, 9, new Dictionary<ModSlot, GameMod>()),
            new Character("GENERALSKYWALKER", "General Skywalker", 85, 13, 8, 32000, 8, new Dictionary<ModSlot, GameMod>())
        };
        ModThresholdsViewModel = new ModThresholdsViewModel();
        _currentView = this;
    }

    [RelayCommand]
    private void AssignMods()
    {
        // For now, we'll use an empty list of mods.
        var mods = new List<GameMod>();
        _modAssignmentService.AssignMods(Characters.ToList(), mods);
    }
    
    [RelayCommand]
    private void GoToThresholds()
    {
        CurrentView = ModThresholdsViewModel;
    }
    
    [RelayCommand]
    private void GoToHome()
    {
        CurrentView = this;
    }
}
