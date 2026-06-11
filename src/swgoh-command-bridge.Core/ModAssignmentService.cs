using System.Collections.Generic;
using System.Linq;
using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.Core
{
    public class ModAssignmentService
    {
        public void AssignMods(List<Character> characters, List<GameMod> mods)
        {
            // Sort characters by priority in descending order
            var sortedCharacters = characters.OrderByDescending(c => c.Priority).ToList();

            foreach (var character in sortedCharacters)
            {
                // Placeholder for mod assignment logic.
                // For each character, find the best mods from the available mods list.
                // This logic can be complex and will depend on character stats, desired mod sets, etc.
                // For now, we'll just print a message.
                System.Console.WriteLine($"Assigning mods to {character.Name} with priority {character.Priority}");
            }
        }
    }
}
