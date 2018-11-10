using Torch;

namespace DiscordRoleManager
{
    public class RoleConfig : ViewModel
    {
        private string _token = "";
        public string BotToken { get => _token; set => SetValue(ref _token, value); }

        private string _tokenVisibleState = "Visible";
        public string TokenVisibleState { get => _tokenVisibleState; set => SetValue(ref _tokenVisibleState, value); }

        private ulong _channelId = 0;
        public ulong ChannelId { get => _channelId; set => SetValue(ref _channelId, value); }

        private string _spreadsheetId = "";
        public string SpreadsheetId { get => _spreadsheetId; set => SetValue(ref _spreadsheetId, value); }

        private string _spreadsheetMappingTab = "Mapping";
        public string SpreadsheetMappingTab { get => _spreadsheetMappingTab; set => SetValue(ref _spreadsheetMappingTab, value); }

        private int _infoDelay = 5000;
        public int InfoDelay { get => _infoDelay; set => SetValue(ref _infoDelay, value); }

        private ulong _rank1 = 0;
        public ulong Rank1 { get => _rank1; set => SetValue(ref _rank1, value); }

        private ulong _rank2 = 0;
        public ulong Rank2 { get => _rank2; set => SetValue(ref _rank2, value); }

        private ulong _rank3 = 0;
        public ulong Rank3 { get => _rank3; set => SetValue(ref _rank3, value); }

        private ulong _rank4 = 0;
        public ulong Rank4 { get => _rank4; set => SetValue(ref _rank4, value); }
    }
}
