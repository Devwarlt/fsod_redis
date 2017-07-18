#region

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using common;
using log4net;
using log4net.Config;
using wServer.networking;
using wServer.realm;
using System.Net.Mail;
using System.Net;

#endregion

namespace wServer
{
    internal static class Program
    {
        public static bool WhiteList { get; private set; }
        public static bool Verify { get; private set; }
        internal static Settings Settings;

        private static readonly ILog log = LogManager.GetLogger("Server");
        private static RealmManager manager;

        public static DateTime WhiteListTurnOff { get; private set; }

        private static void Main(string[] args)
        {
            Console.Title = "Fabiano Swagger of Doom - World Server";
            try
            {
                XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net_wServer.config"));

                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.Name = "Entry";

                Settings = new Settings("wServer");
                using (var db = new Database(
                    Settings.GetValue<string>("db_host", "127.0.0.1"),
                    Settings.GetValue<int>("db_port", "6379"),
                    Settings.GetValue<string>("db_auth", "")))
                {

                    manager = new RealmManager(
                        Settings.GetValue<int>("maxClients", "100"),
                        Settings.GetValue<int>("tps", "20"),
                        db);

                    WhiteList = Settings.GetValue<bool>("whiteList", "false");
                    Verify = Settings.GetValue<bool>("verifyEmail", "false");
                    WhiteListTurnOff = Settings.GetValue<DateTime>("whitelistTurnOff");

                    manager.Initialize();
                    manager.Run();

                    Server server = new Server(manager);
                    PolicyServer policy = new PolicyServer();

                    Console.CancelKeyPress += (sender, e) => e.Cancel = true;

                    policy.Start();
                    server.Start();
                    log.Info("Server initialized.");

                    while (Console.ReadKey(true).Key != ConsoleKey.Escape) ;

                    log.Info("Terminating...");
                    server.Stop();
                policy.Stop();
                manager.Stop();
                log.Info("Server terminated.");
                }
            }
            catch (Exception e)
            {
                log.Fatal(e);

                foreach (var c in manager.Clients)
                {
                    c.Value.Disconnect();
                }
                Console.ReadLine();
            }
        }

        public static void SendEmail(MailMessage message, bool enableSsl = true)
        {
            SmtpClient client = new SmtpClient
            {
                Host = Settings.GetValue<string>("smtpHost", "smtp.gmail.com"),
                Port = Settings.GetValue<int>("smtpPort", "587"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                EnableSsl = true,
                Credentials =
                    new NetworkCredential(Settings.GetValue<string>("serverEmail"),
                        Settings.GetValue<string>("serverEmailPassword"))
            };

            client.Send(message);
        }
    }
}