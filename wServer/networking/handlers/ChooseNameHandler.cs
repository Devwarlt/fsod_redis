﻿using common;
using System.Linq;
using wServer.networking.cliPackets;
using wServer.networking.svrPackets;
using wServer.realm.entities.player;

namespace wServer.networking.handlers
{
    internal class ChooseNameHandler : PacketHandlerBase<ChooseNamePacket>
    {
        public override PacketID ID { get { return PacketID.CHOOSENAME; } }

        protected override void HandlePacket(Client client, ChooseNamePacket packet)
        {
            string name = packet.Name;
            if (name.Length < 3 || name.Length > 15 || !name.All(x => char.IsLetter(x) || char.IsNumber(x)))
                client.SendPacket(new NameResultPacket
                {
                    Success = false,
                    ErrorText = "Error.nameIsNotAlpha"
                });
            else
            {
                string key = Database.NAME_LOCK;
                string lockToken = null;
                try
                {
                    while ((lockToken = client.Manager.Database.AcquireLock(key)) == null) ;

                    if (client.Manager.Database.Hashes.Exists(0, "names", name.ToUpperInvariant()).Exec())
                    {
                        client.SendPacket(new NameResultPacket()
                        {
                            Success = false,
                            ErrorText = "Error.nameAlreadyInUse"
                        });
                        return;
                    }

                    if (client.Account.NameChosen && client.Account.Credits < 1000)
                        client.SendPacket(new NameResultPacket()
                        {
                            Success = false,
                            ErrorText = "server.not_enough_gold"
                        });
                    else
                    {
                        if (client.Account.NameChosen)
                            client.Manager.Database.UpdateCredit(client.Account, -1000);
                        while (!client.Manager.Database.RenameIGN(client.Account, name, lockToken)) ;
                        client.Player.Name = client.Account.Name;
                        client.Player.UpdateCount++;
                        client.SendPacket(new NameResultPacket()
                        {
                            Success = true,
                            ErrorText = "server.buy_success"
                        });
                    }
                }
                finally
                {
                    if (lockToken != null)
                        client.Manager.Database.ReleaseLock(key, lockToken);
                }
            }
        }

        private void Handle(Player player)
        {
            player.Credits = player.Client.Account.Credits;
            player.Name = player.Client.Account.Name;
            player.NameChosen = true;
            player.UpdateCount++;
        }
    }
}