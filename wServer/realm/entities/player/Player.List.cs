#region

using common;
using System.Collections.Generic;
using wServer.networking.svrPackets;

#endregion

namespace wServer.realm.entities.player
{
    partial class Player
    {
        public void SendAccountList(List<string> list, int id)
        {
            for (var i = 0; i < list.Count; i++)
                list[i] = list[i].Trim();

            Client.SendPacket(new AccountListPacket
            {
                AccountListId = id,
                AccountIds = list.ToArray(),
                LockAction = -1
            });
        }
    }
}
