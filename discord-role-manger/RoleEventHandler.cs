using NLog;
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

            string discordTag = RolePlugin.Instance.GetDiscordTag(ev.SteamID).Result;
            if (discordTag == "")
                return;

            var level = RolePlugin.Instance.GetPromoteLevelByRoles(ev.SteamID, discordTag).Result;
            if (level == VRage.Game.ModAPI.MyPromoteLevel.None)
                return;

            Log.Info($"Bypass {ev.SteamID} because of {level.ToString()}");

            ev.FutureVerdict = Task.FromResult(JoinResult.OK);
        }
    }
}
