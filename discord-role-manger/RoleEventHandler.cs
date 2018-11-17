using NLog;
using Sandbox.Engine.Multiplayer;
using System;
using System.Threading.Tasks;
using Torch.API.Event;
using Torch.Server.Managers;
using VRage.Network;

namespace DiscordRoleManager
{
    public class RoleEventHandler : IEventHandler
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        [EventHandler]
        public void ValidateAuthTicketEvent(ref ValidateAuthTicketEvent ev)
        {
            if (!RolePlugin.Instance.Config.EnableReserved)
                return;

            // ignore until server full
            if (!(MyMultiplayer.Static.MemberLimit > 0 && MyMultiplayer.Static.MemberCount - 1 >= MyMultiplayer.Static.MemberLimit))
                return;

            string discordTag = RolePlugin.Instance.GetDiscordTag(ev.SteamID).Result;
            if (discordTag == "")
                return;

            var member = RolePlugin.Instance.GetDiscordMember(discordTag).Result;
            if (member == null)
                return;

            foreach (var role in member.Roles)
            {
                if (RolePlugin.Instance.Config.ReservedRoleIds.Contains(role.Id.ToString()))
                {
                    Log.Info($"Bypass {ev.SteamID} because of role {role.Name} ({role.Id})");
                    ev.FutureVerdict = Task.FromResult(JoinResult.OK);
                    break;
                }
            }
        }
    }
}
