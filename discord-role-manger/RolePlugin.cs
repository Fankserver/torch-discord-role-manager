using DSharpPlus;
using DSharpPlus.Entities;
using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Commands;
using Torch.Event;
using Torch.Managers.ChatManager;
using Torch.Session;
using VRage.Game;

namespace DiscordRoleManager
{
    public class RolePlugin : TorchPluginBase, IWpfPlugin
    {
        public RoleConfig Config => _config?.Data;

        private DiscordClient _discord;
        private RoleControl _control;
        private TorchSessionManager _sessionManager;
        private ChatManagerServer _chatmanager;
        private IMultiplayerManagerBase _multibase;
        private Persistent<RoleConfig> _config;
        private HashSet<ulong> _conecting = new HashSet<ulong>();
        private Dictionary<ulong, string> _linkIds = new Dictionary<ulong, string>();
        private static readonly HttpClient client = new HttpClient();
        public static RolePlugin Instance { get; private set; }

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new RoleControl(this));

        public void Save() => _config.Save();

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            var configFile = Path.Combine(StoragePath, "DiscordRoleManager.cfg");

            try
            {
                _config = Persistent<RoleConfig>.Load(configFile);
            }
            catch (Exception e)
            {
                Log.Warn(e);
            }

            if (_config?.Data == null)
                _config = new Persistent<RoleConfig>(configFile, new RoleConfig());

            if (Config.BotToken.Length == 0)
                Log.Warn("No BOT token set, plugin will not work at all! Add your bot TOKEN, save and start torch.");

            _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager loaded!");

            var eventManager = Torch.Managers.GetManager<EventManager>();
            if (eventManager != null)
                eventManager.RegisterHandler(new RoleEventHandler());
            else
                Log.Warn("No event manager loaded!");

            ConnectDiscord();

            Instance = this;
        }

        private void ConnectDiscord()
        {
            if (_discord != null)
                return;

            if (Config.BotToken.Length == 0)
                return;

            _discord = new DiscordClient(new DiscordConfiguration
            {
                Token = Config.BotToken,
                TokenType = TokenType.Bot
            });
            _discord.ConnectAsync();
            _discord.MessageCreated += Discord_MessageCreated;
            _discord.Ready += async e =>
            {
                Log.Debug("Connected discord");
            };
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            if (Config.BotToken.Length == 0)
                return;

            switch (state)
            {
                case TorchSessionState.Loading:
                    ConnectDiscord();
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Config.APIPassword);
                    break;
                case TorchSessionState.Loaded:
                    _multibase = Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerBase>();
                    if (_multibase != null)
                    {
                        _multibase.PlayerJoined += _multibase_PlayerJoined;
                        MyEntities.OnEntityAdd += MyEntities_OnEntityAdd;
                        _multibase.PlayerLeft += _multibase_PlayerLeft;
                    }
                    else
                        Log.Warn("No join/leave manager loaded!");

                    _chatmanager = Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
                    if (_chatmanager == null)
                        Log.Warn("No chat manager loaded!");

                    Log.Warn("Starting Discord role manager!");

                    break;
                case TorchSessionState.Unloading:
                    if (_multibase != null)
                    {
                        _multibase.PlayerJoined -= _multibase_PlayerJoined;
                        MyEntities.OnEntityAdd -= MyEntities_OnEntityAdd;
                        _multibase.PlayerLeft -= _multibase_PlayerLeft;
                    }

                    if (_discord != null)
                        _discord.DisconnectAsync();

                    _conecting.Clear();
                    _linkIds.Clear();

                    break;
                case TorchSessionState.Unloaded:
                    Log.Info("Discord role manager unloaded!");

                    break;
                default:
                    // ignore
                    break;
            }
        }

        private void _multibase_PlayerLeft(IPlayer obj)
        {
            _conecting.Remove(obj.SteamId);
            _linkIds.Remove(obj.SteamId);
        }

        private void _multibase_PlayerJoined(IPlayer obj)
        {
            if (MySandboxGame.ConfigDedicated.Administrators.Contains(obj.SteamId.ToString()))
                return;

            string discordTag = GetDiscordTag(obj.SteamId).Result;

            if (discordTag != "")
                UpdatePlayerRank(obj.SteamId, discordTag);
            else
                _conecting.Add(obj.SteamId);
        }

        public void CommandLink(CommandContext context, ulong steamId)
        {
            if (Config.ChannelId == 0)
                return;

            var discordTag = GetDiscordTag(steamId).Result;
            if (discordTag != "")
            {
                context.Respond($"Your account is linked to {discordTag}", "DiscordRoleManager", "Green");
                UpdatePlayerRank(steamId, discordTag);
                return;
            }

            var randomString = "";
            if (_linkIds.ContainsKey(steamId))
            {
                randomString = _linkIds[steamId];
            }
            else
            {
                randomString = RandomString(4);
                _linkIds.Add(steamId, randomString);
            }
            var channel = _discord.Guilds.First().Value.GetChannel(Config.ChannelId);
            context.Respond($"Write '{randomString}' in the #{channel.Name} on our discord server", "DiscordRoleManager", "White");
        }

        private void MyEntities_OnEntityAdd(VRage.Game.Entity.MyEntity obj)
        {
            if (Config.NotifyLinkable && obj is MyCharacter character)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(Config.InfoDelay);
                    if (Config.NotifyLinkable && _conecting.Contains(character.ControlSteamId) && character.IsPlayer)
                    {
                        _chatmanager.SendMessageAsOther("DiscordRoleManager", "Write '!link' into the chat to link your steam account with discord", MyFontEnum.White, character.ControlSteamId);

                        //After spawn on world, remove from connecting list
                        _conecting.Remove(character.ControlSteamId);
                    }
                });
            }
        }

        private Task Discord_MessageCreated(DSharpPlus.EventArgs.MessageCreateEventArgs e)
        {
            if (e.Author.IsBot)
                return Task.CompletedTask;

            if (Config.ChannelId > 0 && e.Channel.Id.Equals(Config.ChannelId))
            {
                var message = e.Message.Content.ToUpper();
                foreach (var dict in _linkIds)
                {
                    if (dict.Value == message)
                    {
                        Log.Info($"Linked steamid:{dict.Key} with discord:{e.Author.Username}#{e.Author.Discriminator}");

                        if (AddDiscordRelation(dict.Key, $"{e.Author.Username}#{e.Author.Discriminator}").Result)
                        {

                            _linkIds.Remove(dict.Key);
                            _chatmanager.SendMessageAsOther("DiscordRoleManager", "Link successful", MyFontEnum.White, dict.Key);
                            e.Message.CreateReactionAsync(DiscordEmoji.FromName(_discord, ":heavy_check_mark:"));

                            return UpdatePlayerRank(dict.Key, $"{e.Author.Username}#{e.Author.Discriminator}");
                        }
                        else
                        {
                            _chatmanager.SendMessageAsOther("DiscordRoleManager", "Link unsuccessful", MyFontEnum.Red, dict.Key);
                            e.Message.CreateReactionAsync(DiscordEmoji.FromName(_discord, ":heavy_multiplication_x:"));
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        private Task<bool> AddDiscordRelation(ulong steamId, string discordTag)
        {
            var content = new StringContent(new JavaScriptSerializer().Serialize(new AddRelationEntry
            {
                steam_id = steamId,
                discord_tag = discordTag,
            }), Encoding.UTF8, "application/json");
            var response = client.PostAsync($"{Config.APIURL}/", content).Result;
            return Task.FromResult(response.IsSuccessStatusCode);
        }

        internal Task<string> GetDiscordTag(ulong steamId)
        {
            string discordTag = "";
            var response = client.GetAsync($"{Config.APIURL}/steamid/{steamId}").Result;
            if (response.IsSuccessStatusCode)
            {
                var obj = new JavaScriptSerializer().Deserialize<GetDiscordTag>(response.Content.ReadAsStringAsync().Result);
                discordTag = obj.discord_tag;
            }
            return Task.FromResult(discordTag);
        }

        internal Task<DiscordMember> GetDiscordMember(string discordTag)
        {
            string username = "";
            string discriminator = "";
            int idx = discordTag.LastIndexOf('#');
            if (idx != -1)
            {
                username = discordTag.Substring(0, idx);
                discriminator = discordTag.Substring(idx + 1);
            }

            return Task.FromResult(_discord?.Guilds.FirstOrDefault().Value.GetAllMembersAsync().Result?.FirstOrDefault((x) => x.Username == username && x.Discriminator == discriminator));
        }

        internal Task<VRage.Game.ModAPI.MyPromoteLevel> GetPromoteLevelByRoles(ulong steamId, string discordTag)
        {
            var member = GetDiscordMember(discordTag).Result;
            if (member == null)
                return Task.FromResult(VRage.Game.ModAPI.MyPromoteLevel.None);

            VRage.Game.ModAPI.MyPromoteLevel level = VRage.Game.ModAPI.MyPromoteLevel.None;
            foreach (var role in member.Roles)
            {
                if (Config.Rank4.Contains(role.Id.ToString()) && level < VRage.Game.ModAPI.MyPromoteLevel.Admin)
                    level = VRage.Game.ModAPI.MyPromoteLevel.Admin;
                else if (Config.Rank3.Contains(role.Id.ToString()) && level < VRage.Game.ModAPI.MyPromoteLevel.SpaceMaster)
                    level = VRage.Game.ModAPI.MyPromoteLevel.SpaceMaster;
                else if (Config.Rank2.Contains(role.Id.ToString()) && level < VRage.Game.ModAPI.MyPromoteLevel.Moderator)
                    level = VRage.Game.ModAPI.MyPromoteLevel.Moderator;
                else if (Config.Rank1.Contains(role.Id.ToString()) && level < VRage.Game.ModAPI.MyPromoteLevel.Scripter)
                    level = VRage.Game.ModAPI.MyPromoteLevel.Scripter;
                Log.Debug($"{steamId} check role {role.Name} -> {level.ToString()}");
            }
            return Task.FromResult(level);
        }

        private Task<bool> UpdatePlayerRank(ulong steamId, string discordTag)
        {
            var promotionLevel = MySession.Static.GetUserPromoteLevel(steamId);
            var newPromoteLevel = GetPromoteLevelByRoles(steamId, discordTag).Result;

            if (newPromoteLevel == VRage.Game.ModAPI.MyPromoteLevel.Admin && promotionLevel != newPromoteLevel)
            {
                Log.Info($"{steamId} set promotelevel to Admin");
                MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.Admin);
            }
            else if (newPromoteLevel == VRage.Game.ModAPI.MyPromoteLevel.SpaceMaster && promotionLevel != newPromoteLevel)
            {
                Log.Info($"{steamId} set promotelevel to SpaceMaster");
                MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.SpaceMaster);
            }
            else if (newPromoteLevel == VRage.Game.ModAPI.MyPromoteLevel.Moderator && promotionLevel != newPromoteLevel)
            {
                Log.Info($"{steamId} set promotelevel to Moderator");
                MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.Moderator);
            }
            else if (newPromoteLevel == VRage.Game.ModAPI.MyPromoteLevel.Scripter && promotionLevel != newPromoteLevel)
            {
                Log.Info($"{steamId} set promotelevel to Scripter");
                MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.Scripter);
            }
            else if (newPromoteLevel == VRage.Game.ModAPI.MyPromoteLevel.None && promotionLevel != newPromoteLevel)
            {
                Log.Info($"{steamId} set promotelevel to None");
                MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.None);
            }

            return Task.FromResult(true);
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ACDEFGHJKLMNOPQRSTUVWXYZ123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    public class GetDiscordTag
    {
        public string discord_tag { get; set; }
    }

    public class AddRelationEntry
    {
        public ulong steam_id { get; set; }
        public string discord_tag { get; set; }
    }
}
