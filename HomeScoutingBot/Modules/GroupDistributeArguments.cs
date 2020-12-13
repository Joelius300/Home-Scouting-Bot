using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace HomeScoutingBot.Modules
{
    [NamedArgumentType]
    public class GroupDistributeArguments
    {
        public IEnumerable<string> Exclude { get; set; } = Enumerable.Empty<string>();
        public GroupOverflowHandling OverflowHandling { get; set; } = default;
        public bool CreateMissingGroups { get; set; } = true;
    }

    public enum GroupOverflowHandling
    {
        /// <summary>
        /// Add the leftover users to their own, new group.
        /// The actual group-size will be the specified group-size or lower.
        /// Maximum one differently-sized group with a potentially big gap.
        /// </summary>
        NewGroup,
        /// <summary>
        /// Spread the leftover users and put them in the existing groups.
        /// The actual group-size will be the specified group-size or higher.
        /// Multiple groups could have a higher size but the gap is minimized.
        /// </summary>
        Spread,
        /// <summary>
        /// An error is returned and no groups are created if it can't be split up evenly.
        /// </summary>
        Error
    }
}
