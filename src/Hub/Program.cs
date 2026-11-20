using System;
using System.Net;
using System.Threading;
using System.Windows;

namespace Toolbox
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (args.Length > 0 && args[0] == "--finish-update")
            {
                try { ToolboxUpdater.Finish(args); }
                catch (Exception exception) { MessageBox.Show(exception.GetBaseException().Message, "Toolbox update", MessageBoxButton.OK, MessageBoxImage.Error); }
                return;
            }
            bool first;
            using (var instance = new Mutex(true, @"Local\ThongKhongToolbox.Sling", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Thong Khong Toolbox is already open.", "Thong Khong Toolbox");
                    return;
                }
                System.Threading.Tasks.Task.Run(() => ToolboxUpdater.RemoveOldDownloads(ToolboxUpdater.DownloadRoot, DateTime.UtcNow));
                try { new Application().Run(new ToolboxWindow()); }
                catch (Exception exception) { MessageBox.Show(exception.GetBaseException().Message, "Thong Khong Toolbox", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }
    }
}
