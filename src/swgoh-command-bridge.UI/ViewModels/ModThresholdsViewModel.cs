#nullable enable

using System.Collections.ObjectModel;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing the mod upgrade threshold management list.
    /// </summary>
    public class ModThresholdsViewModel : ViewModelBase
    {
        private string _headerText = "Manage Upgrade Rules & Thresholds";

        /// <summary>
        /// Gets dynamic collections of user-defined mod thresholds.
        /// </summary>
        public ObservableCollection<ModUpgradeThreshold> Thresholds { get; } = new();

        /// <summary>
        /// Gets or sets the view page header text.
        /// </summary>
        public string HeaderText
        {
            get => _headerText;
            set
            {
                if (_headerText != value)
                {
                    _headerText = value;
                    OnPropertyChanged(nameof(HeaderText));
                }
            }
        }
    }
}