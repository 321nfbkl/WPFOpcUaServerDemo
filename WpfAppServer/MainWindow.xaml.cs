using System.Windows;

namespace WpfAppServer
{
    public partial class MainWindow : Window
    {
        public OpcUaServerHelper OpcUaServer = new OpcUaServerHelper();

        public MainWindow()
        {
            InitializeComponent();

            // 订阅 Helper 抛出的事件，在 UI 层统一负责展示
            OpcUaServer.OnLog += (msg) => MessageBox.Show(msg, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            OpcUaServer.OnError += (err) => MessageBox.Show(err, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            await OpcUaServer.StartServerAsync();
        }

        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            OpcUaServer.StopServer();
        }
    }
}