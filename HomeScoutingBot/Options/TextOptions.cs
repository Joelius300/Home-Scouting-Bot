using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeScoutingBot.Options
{
    /// <summary>
    /// Contains format strings to build replies to the user.
    /// </summary>
    public class TextOptions
    {
        /// <summary>
        /// A format string which takes the username and the error message.
        /// </summary>
        public string CommandExecutionFailed { get; set; } = "Sorry {0}, something went wrong: {1}";

        /// <summary>
        /// A format string which takes the number of groups deleted.
        /// </summary>
        public string GroupsDeleted { get; set; } = "{0} groups deleted.";

        /// <summary>
        /// A format string which takes the number of groups created.
        /// </summary>
        public string GroupsCreated { get; set; } = "{0} groups added.";

        /// <summary>
        /// A format string which takes the number of distributed users and the number of groups the users were distributed to.
        /// </summary>
        public string UsersDistributed { get; set; } = "{0} users were split into {1} groups.";

        /// <summary>
        /// A string which says that all groups were broken up.
        /// </summary>
        public string GroupsBrokenUp { get; set; } = "All groups were broken up.";
    }
}
