#nullable enable

using System.Collections.ObjectModel;

namespace swgoh_command_bridge.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing the workspace for defining individual priority tiers across unlocked roster units.
    /// </summary>
    public class CharacterPrioritiesViewModel : ViewModelBase
    {
        private string _headerText = "Configure Character Priorities";

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterPrioritiesViewModel"/> class.
        /// </summary>
        public CharacterPrioritiesViewModel()
        {
        }

        /// <summary>
        /// Gets or sets the header text.
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