using Torch;

namespace DiscordRoleManager
{
    public class RoleConfig : ViewModel
    {
        private bool _notifyLinkable = true;
        public bool NotifyLinkable { get => _notifyLinkable; set => SetValue(ref _notifyLinkable, value); }

        private int _infoDelay = 5000;
        public int InfoDelay { get => _infoDelay; set => SetValue(ref _infoDelay, value); }

        private string _token = "";
        public string BotToken { get => _token; set => SetValue(ref _token, value); }

        private string _tokenVisibleState = "Visible";
        public string TokenVisibleState { get => _tokenVisibleState; set => SetValue(ref _tokenVisibleState, value); }

        private ulong _channelId = 0;
        public ulong ChannelId { get => _channelId; set => SetValue(ref _channelId, value); }

        private string _apiURL = "http://localhost:8080";
        public string APIURL { get => _apiURL; set => SetValue(ref _apiURL, value); }

        private string _apiPassword = "";
        public string APIPassword { get => _apiPassword; set => SetValue(ref _apiPassword, value); }

        private string _rank1 = "";
        public string Rank1 { get => _rank1; set => SetValue(ref _rank1, value); }

        private string _rank2 = "";
        public string Rank2 { get => _rank2; set => SetValue(ref _rank2, value); }

        private string _rank3 = "";
        public string Rank3 { get => _rank3; set => SetValue(ref _rank3, value); }

        private string _rank4 = "";
        public string Rank4 { get => _rank4; set => SetValue(ref _rank4, value); }

        private bool _enableReserved = false;
        public bool EnableReserved { get => _enableReserved; set => SetValue(ref _enableReserved, value); }

        private string _reservedRoleIds = "";
        public string ReservedRoleIds { get => _reservedRoleIds; set => SetValue(ref _reservedRoleIds, value); }
    }
}
