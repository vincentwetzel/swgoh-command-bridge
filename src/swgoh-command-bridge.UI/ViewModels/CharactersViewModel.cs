#nullable enable

namespace swgoh_command_bridge.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing the character collection and priority scoring page.
    /// </summary>
    public class CharactersViewModel : ViewModelBase
    {
        private string _headerText = "Characters List";

        /// <summary>
        /// Gets or sets the header text for the characters panel.
        /// </summary>
        public string HeaderText
        {
            get => _headerText;
            set
            {
                if (_headerText != value)
                {
                    _headerText = value;
                    // Assumes ViewModelBase implements standard property change notifications
                    OnPropertyChanged(nameof(HeaderText));
                }
            }
        }
    }
}