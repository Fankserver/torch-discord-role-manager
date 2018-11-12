using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace DiscordRoleManager
{
    public class RoleCommands : CommandModule
    {
        public RolePlugin Plugin => (RolePlugin)Context.Plugin;

        [Command("link")]
        [Permission(MyPromoteLevel.None)]
        public void Link(ulong steamId = 0)
        {
            if (steamId == 0 && Context?.Player?.SteamUserId > 0)
                steamId = Context.Player.SteamUserId;
            else
            {
                Context.Respond("Command can be used ingame only");
                return;
            }

            Plugin.CommandLink(Context, steamId);
        }
    }
}
