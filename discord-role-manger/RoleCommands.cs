using Sandbox.Game.World;
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
        public void Link()
        {
            if (!(Context?.Player?.SteamUserId > 0))
            {
                Context.Respond("Command can be used ingame only");
                return;
            }

            Plugin.CommandLink(Context, Context.Player.SteamUserId);
        }

        [Command("hidelink")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void HideLink()
        {
            if (!(Context?.Player?.SteamUserId > 0))
            {
                Context.Respond("Command can be used ingame only");
                return;
            }

            MySession.Static.SetUserPromoteLevel(Context.Player.SteamUserId, VRage.Game.ModAPI.MyPromoteLevel.None);
        }
    }
}
