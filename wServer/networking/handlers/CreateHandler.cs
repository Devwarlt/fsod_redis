using common;
using wServer.networking.cliPackets;
using wServer.networking.svrPackets;
using wServer.realm;
using wServer.realm.entities.player;
using FailurePacket = wServer.networking.svrPackets.FailurePacket;

namespace wServer.networking.handlers
{
    internal class CreateHandler : PacketHandlerBase<CreatePacket>
    {
        public override PacketID ID { get { return PacketID.CREATE; } }

        protected override void HandlePacket(Client client, CreatePacket packet)
        {
            var db = client.Manager.Database;

            DbChar character;
            var status = client.Manager.Database.CreateCharacter(
                client.Manager.GameData, client.Account, (ushort)packet.ClassType, packet.SkinType, out character);

            if (status == CreateStatus.ReachCharLimit)
            {
                client.SendPacket(new FailurePacket
                {
                    ErrorDescription = "Failed to Load character."
                });
                client.Disconnect();
                return;
            }

            client.Character = character;

            var target = client.Manager.Worlds[Client.TargetWorld];
            //Delay to let client load remote texture
            target.Timers.Add(new WorldTimer(500, (w, t) =>
            {
                client.SendPacket(new Create_SuccessPacket()
                {
                    CharacterID = client.Character.CharId,
                    ObjectID =
                            client.Manager.Worlds[client.TargetWorld].EnterWorld(
                                client.Player = new Player(client.Manager, client))
                });
            }));
            client.Stage = ProtocalStage.Ready;
        }
    }
}