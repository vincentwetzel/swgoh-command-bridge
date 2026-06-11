#nullable enable

using System.Threading.Tasks;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core.Services
{
    /// <summary>
    /// Provides operations to load, update, and atomically persist application settings.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the current loaded settings.
        /// </summary>
        AppSettings CurrentSettings { get; }

        /// <summary>
        /// Loads the settings from the local application data directory.
        /// </summary>
        Task LoadSettingsAsync();

        /// <summary>
        /// Atomically saves the provided settings to the local application data directory.
        /// </summary>
        Task SaveSettingsAsync(AppSettings settings);
    }
}