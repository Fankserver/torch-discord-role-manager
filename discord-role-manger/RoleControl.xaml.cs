using System.Windows;
using System.Windows.Controls;

namespace DiscordRoleManager
{
    /// <summary>
    /// Interaktionslogik für RoleControl.xaml
    /// </summary>
    public partial class RoleControl : UserControl
    {
        private RolePlugin Plugin { get; }

        public RoleControl()
        {
            InitializeComponent();
        }

        public RoleControl(RolePlugin plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
        }

        private void SaveConfig_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
        }
    }
}
