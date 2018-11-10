using DSharpPlus;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
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
        private HashSet<ulong> _knownPlayer = new HashSet<ulong>();
        private Dictionary<ulong, string> _linkIds = new Dictionary<ulong, string>();
        private SheetsService _sheetService;

        public readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new RoleControl(this));

        public void Save() => _config.Save();

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            var configPath = Path.Combine(StoragePath, "DiscordRoleManager");
            var configFile = Path.Combine(configPath, "config.cfg");

            Directory.CreateDirectory(configPath);

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

            UserCredential credential;

            using (var stream = new FileStream(Path.Combine(configPath, "credentials.json"), FileMode.Open, FileAccess.Read))
            {
                string[] scopes = { SheetsService.Scope.Spreadsheets };
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = Path.Combine(configPath, "token.json");
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Log.Info("Credential file saved to: " + credPath);
            }

            _sheetService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "DiscordRoleManager",
            });
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            if (Config.BotToken.Length == 0)
                return;

            switch (state)
            {
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
                    if (_chatmanager != null)
                        _chatmanager.MessageRecieved += MessageRecieved;

                    else
                        Log.Warn("No chat manager loaded!");

                    Log.Warn("Starting Discord role manager!");

                    _discord = new DiscordClient(new DiscordConfiguration
                    {
                        Token = Config.BotToken,
                        TokenType = TokenType.Bot
                    });
                    _discord.ConnectAsync();
                    _discord.MessageCreated += Discord_MessageCreated;

                    break;
                case TorchSessionState.Unloading:
                    if (_multibase != null)
                    {
                        _multibase.PlayerJoined -= _multibase_PlayerJoined;
                        MyEntities.OnEntityAdd -= MyEntities_OnEntityAdd;
                        _multibase.PlayerLeft -= _multibase_PlayerLeft;
                    }

                    if (_chatmanager != null)
                        _chatmanager.MessageRecieved -= MessageRecieved;

                    if (_discord != null)
                        _discord.DisconnectAsync();

                    _conecting.Clear();
                    _linkIds.Clear();

                    break;
                case TorchSessionState.Unloaded:
                    Log.Warn("Discord role manager unloaded!");

                    break;
                default:
                    // ignore
                    break;
            }
        }

        private void _multibase_PlayerLeft(IPlayer obj)
        {
            _conecting.Remove(obj.SteamId);
            _knownPlayer.Remove(obj.SteamId);
            _linkIds.Remove(obj.SteamId);
        }

        private void _multibase_PlayerJoined(IPlayer obj)
        {
            string discordTag = GetDiscordTag(obj.SteamId).Result;

            if (discordTag != null)
                UpdatePlayerRank(obj.SteamId, discordTag);
            else 
                _conecting.Add(obj.SteamId);
        }

        private void MessageRecieved(TorchChatMessage msg, ref bool consumed)
        {
            if (Config.ChannelId == 0)
                return;

            if (msg.AuthorSteamId.HasValue && !_knownPlayer.Contains(msg.AuthorSteamId.Value) && (msg.Message == "/link" || msg.Message == "/verify"))
            {
                var randomString = RandomString(4);
                _linkIds.Add(msg.AuthorSteamId.Value, randomString);
                var channel = _discord.Guilds.First().Value.GetChannel(Config.ChannelId);
                _chatmanager.SendMessageAsOther("DiscordRoleManager", $"Write '{randomString}' in the #{channel.Name} on our discord server", MyFontEnum.White, msg.AuthorSteamId.Value);
            }
        }

        private void MyEntities_OnEntityAdd(VRage.Game.Entity.MyEntity obj)
        {
            if (obj is MyCharacter character)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(Config.InfoDelay);
                    if (_conecting.Contains(character.ControlSteamId) && character.IsPlayer)
                    {
                        _chatmanager.SendMessageAsOther("DiscordRoleManager", "Write '/link' into the chat to link your steam account with discord", MyFontEnum.White, character.ControlSteamId);

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
                foreach (var dict in _linkIds)
                {
                    if (dict.Value == e.Message.Content)
                    {
                        Log.Info($"Linked steamid:{dict.Key} with discord:{e.Author.Username}#{e.Author.Discriminator}");

                        IList<Object> obj = new List<Object>
                        {
                            dict.Key.ToString(),
                            $"{e.Author.Username}#{e.Author.Discriminator}"
                        };
                        IList<IList<Object>> values = new List<IList<Object>>
                        {
                            obj
                        };
                        SpreadsheetsResource.ValuesResource.AppendRequest request = _sheetService.Spreadsheets.Values.Append(new ValueRange() { Values = values }, Config.SpreadsheetId, $"{Config.SpreadsheetMappingTab}!A2:B");
                        request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
                        request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
                        request.Execute();

                        _linkIds.Remove(dict.Key);
                        _chatmanager.SendMessageAsOther("DiscordRoleManager", "Link successful", MyFontEnum.White, dict.Key);

                        return UpdatePlayerRank(dict.Key, $"{e.Author.Username}#{e.Author.Discriminator}");
                    }
                }
            }

            return Task.CompletedTask;
        }

        private Task<string> GetDiscordTag(ulong steamId)
        {
            SpreadsheetsResource.ValuesResource.GetRequest request = _sheetService.Spreadsheets.Values.Get(Config.SpreadsheetId, $"{Config.SpreadsheetMappingTab}!A2:B");
            ValueRange response = request.Execute();
            IList<IList<object>> values = response.Values;
            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    ulong parsedSteamId;
                    if (ulong.TryParse(row[0].ToString(), out parsedSteamId) && parsedSteamId == steamId)
                    {
                        string discordTag = row[1].ToString();
                        return Task.FromResult<string>(discordTag);
                    }
                }
            }

            return Task.FromResult<string>(null);
        }

        private Task<bool> UpdatePlayerRank(ulong steamId, string discordTag)
        {
            string username = "";
            string discriminator = "";
            int idx = discordTag.LastIndexOf('#');
            if (idx != -1)
            {
                username = discordTag.Substring(0, idx);
                discriminator = discordTag.Substring(idx + 1);
            }

            var member = _discord.Guilds.FirstOrDefault().Value.GetAllMembersAsync().Result.FirstOrDefault((x) => x.Username == username && x.Discriminator == discriminator);
            if (member == null)
            {
                MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.None);
                return Task.FromResult(false);
            }

            var promotionLevel = MySession.Static.GetUserPromoteLevel(steamId);

            foreach (var role in member.Roles)
            {
                if (Config.Rank4 > 0 && role.Id == Config.Rank4 && promotionLevel != VRage.Game.ModAPI.MyPromoteLevel.Admin)
                {
                    MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.Admin);
                }
                else if (Config.Rank3 > 0 && role.Id == Config.Rank3 && promotionLevel != VRage.Game.ModAPI.MyPromoteLevel.SpaceMaster)
                {
                    MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.SpaceMaster);
                }
                else if (Config.Rank2 > 0 && role.Id == Config.Rank2 && promotionLevel != VRage.Game.ModAPI.MyPromoteLevel.Moderator)
                {
                    MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.Moderator);
                }
                else if (Config.Rank1 > 0 && role.Id == Config.Rank1 && promotionLevel != VRage.Game.ModAPI.MyPromoteLevel.Scripter)
                {
                    MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.Scripter);
                }
                else if (promotionLevel != VRage.Game.ModAPI.MyPromoteLevel.None)
                {
                    MySession.Static.SetUserPromoteLevel(steamId, VRage.Game.ModAPI.MyPromoteLevel.None);
                }
            }
            _knownPlayer.Add(steamId);

            return Task.FromResult(true);
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
