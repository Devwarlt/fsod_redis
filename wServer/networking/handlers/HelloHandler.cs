using common;
using System;
using System.Text;
using wServer.networking.cliPackets;
using wServer.networking.svrPackets;
using wServer.realm;
using wServer.realm.worlds;
using FailurePacket = wServer.networking.svrPackets.FailurePacket;

namespace wServer.networking.handlers
{
    internal class HelloHandler : PacketHandlerBase<HelloPacket>
    {
        public override PacketID ID { get { return PacketID.HELLO; } }

        protected override void HandlePacket(Client client, HelloPacket packet)
        {
            DbAccount acc;
            var s1 = client.Manager.Database.Verify(packet.GUID, packet.Password, out acc);
            if (s1 == LoginStatus.AccountNotExists)
            {
                var s2 = client.Manager.Database.Register(packet.GUID, packet.Password, true, out acc);
                if (s2 != RegisterStatus.OK)
                {
                    client.SendPacket(new FailurePacket
                    {
                        ErrorId = 0,
                        ErrorDescription = "bad login"
                    });
                    client.Disconnect();
                    return;
                }
            }
            else if (s1 == LoginStatus.InvalidCredentials)
                client.SendPacket(new FailurePacket
                {
                    ErrorId = 0,
                    ErrorDescription = "bad login"
                });

            if (!client.Manager.TryConnect(client))
            {
                client.Account = null;
                client.SendPacket(new FailurePacket
                {
                    ErrorId = 0,
                    ErrorDescription = "failed to conntect"
                });
                client.Disconnect();
                return;
            }

            if (!client.Manager.Database.AcquireLock(acc))
            {
                //SendFailure(client, "Account in Use (" +
                //    client.Manager.Database.GetLockTime(acc) + " seconds until timeout)");
                client.Disconnect();
                return;
            }

            World world = client.Manager.GetWorld(packet.GameId);
            if (world == null)
            {
                //SendFailure(client, "Invalid world");
                client.Disconnect();
                return;
            }

            if (world.Id == -6) //Test World
                (world as Test).LoadJson(Encoding.Default.GetString(packet.MapInfo));
            else if (world.IsLimbo)
                world = world.GetInstance(client);

            client.Account = acc;

            client.Random = new wRandom(world.Seed);
            client.TargetWorld = world.Id;
            client.SendPacket(new MapInfoPacket()
            {
                Width = world.Map.Width,
                Height = world.Map.Height,
                Name = world.Name,
                Seed = world.Seed,
                ClientWorldName = world.ClientWorldName, // change later
                Difficulty = world.Difficulty,
                Background = world.Background,
                AllowTeleport = world.AllowTeleport,
                ShowDisplays = world.ShowDisplays,
                ClientXML = world.ClientXml,
                ExtraXML = Manager.GameData.AdditionXml
            });
            client.Stage = ProtocalStage.Handshaked;
        }
    }
}