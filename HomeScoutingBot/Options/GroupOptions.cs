using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeScoutingBot.Options
{
    public class GroupOptions
    {
        /// <summary>
        /// A format string which takes the number of the group.
        /// The resulting string has to be within Discords criteria.
        /// </summary>
        public string GroupChannelNameTemplate { get; set; } = "Group-{0}";
    }
}
