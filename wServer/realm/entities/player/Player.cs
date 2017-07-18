#region

using common;
using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using wServer.logic;
using wServer.networking;
using wServer.networking.cliPackets;
using wServer.networking.svrPackets;

#endregion

namespace wServer.realm.entities.player
{
    internal interface IPlayer
    {
        void Damage(int dmg, Entity chr);
        bool IsVisibleToEnemy();
    }

    public static class ComparableExtension
    {
        public static bool InRange<T>(this T value, T from, T to) where T : IComparable<T>
        {
            return value.CompareTo(from) >= 1 && value.CompareTo(to) <= -1;
        }
    }

    public partial class Player : Character, IContainer, IPlayer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Player));

        private bool dying;

        private Item[] inventory;

        private float hpRegenCounter;
        private float mpRegenCounter;
        private bool resurrecting;

        private byte[,] tiles;
        private int pingSerial;
        private SetTypeSkin setTypeSkin;

        public Player(RealmManager manager, Client psr)
            : base(manager, (ushort)psr.Character.ObjectType, psr.Random)
        {
            try
            {
                Client = psr;
                Manager = psr.Manager;
                StatsManager = new StatsManager(this, psr.Random.CurrentSeed);
                Name = psr.Account.Name;
                AccountId = psr.Account.AccountId;
                FameCounter = new FameCounter(this);
                Tokens = psr.Account.FortuneTokens;
                HpPotionPrice = 5;
                MpPotionPrice = 5;

                Level = psr.Character.Level == 0 ? 1 : psr.Character.Level;
                Experience = psr.Character.Experience;
                ExperienceGoal = GetExpGoal(Level);
                Stars = GetStars();
                Texture1 = psr.Character.Tex1;
                Texture2 = psr.Character.Tex2;
                Credits = psr.Account.Credits;
                NameChosen = psr.Account.NameChosen;
                CurrentFame = psr.Account.Fame;
                Fame = psr.Character.Fame;
                LootDropBoostTimeLeft = psr.Character.LootDropTimer;
                lootDropBoostFreeTimer = LootDropBoost;
                LootTierBoostTimeLeft = psr.Character.LootTierTimer;
                lootTierBoostFreeTimer = LootTierBoost;
                //var state =
                //    psr.Account.Stats.ClassStates.SingleOrDefault(_ => Utils.FromString(_.ObjectType) == ObjectType);
                FameGoal = GetFameGoal(FameCounter.ClassStats[ObjectType].BestFame);
                Glowing = false;
                Guild = "";
                GuildRank = -1;
                HP = psr.Character.HP <= 0 ? psr.Character.HP : psr.Character.HP;
                Mp = psr.Character.MP;
                ConditionEffects = 0;
                OxygenBar = 100;
                HasBackpack = psr.Character.HasBackpack;
                PlayerSkin = Client.Account.OwnedSkins.Contains(Client.Character.Skin) ? Client.Character.Skin : 0;
                HealthPotions = psr.Character.HealthPotions < 0 ? 0 : psr.Character.HealthPotions;
                MagicPotions = psr.Character.MagicPotions < 0 ? 0 : psr.Character.MagicPotions;

                Inventory =
                        psr.Character.Items.Select(
                            _ =>
                                _ == -1
                                    ? null
                                    : (Manager.GameData.Items.ContainsKey((ushort)_) ? Manager.GameData.Items[(ushort)_] : null))
                            .ToArray();
                var xElement = Manager.GameData.ObjectTypeToElement[ObjectType].Element("SlotTypes");
                if (xElement != null)
                    SlotTypes =
                        Utils.FromCommaSepString32(
                            xElement.Value);

                Stats = (int[])psr.Character.Stats.Clone();

                for (var i = 0; i < SlotTypes.Length; i++)
                    if (SlotTypes[i] == 0) SlotTypes[i] = 10;

                if (Client.Account.Rank >= 3) return;
                for (var i = 0; i < 4; i++)
                    if (Inventory[i]?.SlotType != SlotTypes[i])
                        Inventory[i] = null;
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

        ~Player()
        {
            WorldInstance = null;
            Quest = null;
        }

        //Stats
        public string AccountId { get; }

        public int[] Boost { get; private set; }

        public Client Client { get; }

        public int Credits { get; set; }
        public int Tokens { get; set; }
        public int CurrentFame { get; set; }

        public int Experience { get; set; }
        public int ExperienceGoal { get; set; }

        public int Fame { get; set; }

        public FameCounter FameCounter { get; }

        public int FameGoal { get; set; }

        public bool Glowing { get; set; }

        public bool HasBackpack { get; set; }

        public int HealthPotions { get; set; }

        public List<string> Ignored { get; set; }

        public bool Invited { get; set; }
        public bool Muted { get; set; }

        public int Level { get; set; }

        public List<string> Locked { get; set; }

        public bool LootDropBoost
        {
            get { return LootDropBoostTimeLeft > 0; }
            set { LootDropBoostTimeLeft = value ? LootDropBoostTimeLeft : 0.0f; }
        }
        public float LootDropBoostTimeLeft { get; set; }

        public bool LootTierBoost
        {
            get { return LootTierBoostTimeLeft > 0; }
            set { LootTierBoostTimeLeft = value ? LootTierBoostTimeLeft : 0.0f; }
        }
        public float LootTierBoostTimeLeft { get; set; }

        public bool XpBoosted { get; set; }
        public float XpBoostTimeLeft { get; set; }

        public int MagicPotions { get; set; }

        public ushort HpPotionPrice { get; set; }
        public ushort MpPotionPrice { get; set; }

        public bool HpFirstPurchaseTime { get; set; }
        public bool MpFirstPurchaseTime { get; set; }

        public new RealmManager Manager { get; }

        public int MaxHp { get; set; }

        public int MaxMp { get; set; }

        public int Mp { get; set; }

        public bool NameChosen { get; set; }

        public int OxygenBar { get; set; }

        public int Pet { get; set; }

        public int PlayerSkin { get; set; }

        public int Stars { get; set; }

        public int[] Stats { get; }

        public StatsManager StatsManager { get; }

        public int Texture1 { get; set; }

        public int Texture2 { get; set; }

        public Item[] Inventory
        {
            get { return inventory; }
            set { inventory = value; }
        }

        public string Guild { get; set; }

        public int GuildRank { get; set; }

        public int[] SlotTypes { get; set; }

        public void Damage(int dmg, Entity chr)
        {
            try
            {
                if (HasConditionEffect(ConditionEffectIndex.Paused) ||
                    HasConditionEffect(ConditionEffectIndex.Stasis) ||
                    HasConditionEffect(ConditionEffectIndex.Invincible))
                    return;

                dmg = (int)StatsManager.GetDefenseDamage(dmg, false);
                if (!HasConditionEffect(ConditionEffectIndex.Invulnerable))
                    HP -= dmg;
                UpdateCount++;
                Owner.BroadcastPacket(new DamagePacket
                {
                    TargetId = Id,
                    Effects = 0,
                    Damage = (ushort)dmg,
                    Killed = HP <= 0,
                    BulletId = 0,
                    ObjectId = chr.Id
                }, this);
                SaveToCharacter();

                if (HP <= 0)
                    Death(chr.ObjectDesc.DisplayId, chr.ObjectDesc);
            }
            catch (Exception e)
            {
                log.Error("Error while processing playerDamage: ", e);
            }
        }

        protected override void ExportStats(IDictionary<StatsType, object> stats)
        {
            base.ExportStats(stats);
            stats[StatsType.AccountId] = AccountId;
            stats[StatsType.Name] = Name;

            stats[StatsType.Experience] = Experience - GetLevelExp(Level);
            stats[StatsType.ExperienceGoal] = ExperienceGoal;
            stats[StatsType.Level] = Level;

            stats[StatsType.CurrentFame] = CurrentFame;
            stats[StatsType.Fame] = Fame;
            stats[StatsType.FameGoal] = FameGoal;
            stats[StatsType.Stars] = Stars;

            stats[StatsType.Guild] = Guild;
            stats[StatsType.GuildRank] = GuildRank;

            stats[StatsType.Credits] = Credits;
            stats[StatsType.Tokens] = Tokens;
            stats[StatsType.NameChosen] = NameChosen ? 1 : 0;
            stats[StatsType.Texture1] = Texture1;
            stats[StatsType.Texture2] = Texture2;

            if (Glowing)
                stats[StatsType.Glowing] = 1;

            stats[StatsType.HP] = HP;
            stats[StatsType.MP] = Mp;

            stats[StatsType.Inventory0] = (int)(Inventory[0]?.ObjectType ?? -1);
            stats[StatsType.Inventory1] = (int)(Inventory[1]?.ObjectType ?? -1);
            stats[StatsType.Inventory2] = (int)(Inventory[2]?.ObjectType ?? -1);
            stats[StatsType.Inventory3] = (int)(Inventory[3]?.ObjectType ?? -1);
            stats[StatsType.Inventory4] = (int)(Inventory[4]?.ObjectType ?? -1);
            stats[StatsType.Inventory5] = (int)(Inventory[5]?.ObjectType ?? -1);
            stats[StatsType.Inventory6] = (int)(Inventory[6]?.ObjectType ?? -1);
            stats[StatsType.Inventory7] = (int)(Inventory[7]?.ObjectType ?? -1);
            stats[StatsType.Inventory8] = (int)(Inventory[8]?.ObjectType ?? -1);
            stats[StatsType.Inventory9] = (int)(Inventory[9]?.ObjectType ?? -1);
            stats[StatsType.Inventory10] = (int)(Inventory[10]?.ObjectType ?? -1);
            stats[StatsType.Inventory11] = (int)(Inventory[11]?.ObjectType ?? -1);

            if (Boost == null) CalcBoost();

            if (Boost != null)
            {
                stats[StatsType.MaximumHP] = Stats[0] + Boost[0];
                stats[StatsType.MaximumMP] = Stats[1] + Boost[1];
                stats[StatsType.Attack] = Stats[2] + Boost[2];
                stats[StatsType.Defense] = Stats[3] + Boost[3];
                stats[StatsType.Speed] = Stats[4] + Boost[4];
                stats[StatsType.Vitality] = Stats[5] + Boost[5];
                stats[StatsType.Wisdom] = Stats[6] + Boost[6];
                stats[StatsType.Dexterity] = Stats[7] + Boost[7];

                stats[StatsType.HPBoost] = Boost[0];
                stats[StatsType.MPBoost] = Boost[1];
                stats[StatsType.AttackBonus] = Boost[2];
                stats[StatsType.DefenseBonus] = Boost[3];
                stats[StatsType.SpeedBonus] = Boost[4];
                stats[StatsType.VitalityBonus] = Boost[5];
                stats[StatsType.WisdomBonus] = Boost[6];
                stats[StatsType.DexterityBonus] = Boost[7];
            }

            stats[StatsType.Size] = setTypeSkin?.Size ?? Size;
            stats[StatsType.Has_Backpack] = HasBackpack.GetHashCode();

            stats[StatsType.Backpack0] = (int)(HasBackpack ? (Inventory[12]?.ObjectType ?? -1) : -1);
            stats[StatsType.Backpack1] = (int)(HasBackpack ? (Inventory[13]?.ObjectType ?? -1) : -1);
            stats[StatsType.Backpack2] = (int)(HasBackpack ? (Inventory[14]?.ObjectType ?? -1) : -1);
            stats[StatsType.Backpack3] = (int)(HasBackpack ? (Inventory[15]?.ObjectType ?? -1) : -1);
            stats[StatsType.Backpack4] = (int)(HasBackpack ? (Inventory[16]?.ObjectType ?? -1) : -1);
            stats[StatsType.Backpack5] = (int)(HasBackpack ? (Inventory[17]?.ObjectType ?? -1) : -1);
            stats[StatsType.Backpack6] = (int)(HasBackpack ? (Inventory[18]?.ObjectType ?? -1) : -1);
            stats[StatsType.Backpack7] = (int)(HasBackpack ? (Inventory[19]?.ObjectType ?? -1) : -1);

            stats[StatsType.Skin] = setTypeSkin?.SkinType ?? PlayerSkin;
            stats[StatsType.HealStackCount] = HealthPotions;
            stats[StatsType.MagicStackCount] = MagicPotions;

            if (Owner != null && Owner.Name == "Ocean Trench")
                stats[StatsType.OxygenBar] = OxygenBar;

            stats[StatsType.XpBoosterActive] = XpBoosted ? 1 : 0;
            stats[StatsType.XpBoosterTime] = (int)XpBoostTimeLeft;
            stats[StatsType.LootDropBoostTimer] = (int)LootDropBoostTimeLeft;
            stats[StatsType.LootTierBoostTimer] = (int)LootTierBoostTimeLeft;
        }

        public void CalcBoost()
        {
            CheckSetTypeSkin();
            if (Boost == null) Boost = new int[12];
            else
                for (var i = 0; i < Boost.Length; i++) Boost[i] = 0;
            for (var i = 0; i < 4; i++)
            {
                if (Inventory.Length < i || Inventory.Length == 0) return;
                if (Inventory[i] == null) continue;
                foreach (var pair in Inventory[i].StatsBoost)
                {
                    if (pair.Key == StatsType.MaximumHP) Boost[0] += pair.Value;
                    if (pair.Key == StatsType.MaximumMP) Boost[1] += pair.Value;
                    if (pair.Key == StatsType.Attack) Boost[2] += pair.Value;
                    if (pair.Key == StatsType.Defense) Boost[3] += pair.Value;
                    if (pair.Key == StatsType.Speed) Boost[4] += pair.Value;
                    if (pair.Key == StatsType.Vitality) Boost[5] += pair.Value;
                    if (pair.Key == StatsType.Wisdom) Boost[6] += pair.Value;
                    if (pair.Key == StatsType.Dexterity) Boost[7] += pair.Value;
                }
            }

            if (setTypeBoosts == null) return;
            for (var i = 0; i < 8; i++)
                Boost[i] += setTypeBoosts[i];
        }

        public bool CompareName(string name)
        {
            var rn = name.ToLower();
            return rn.Split(' ')[0].StartsWith("[") || Name.Split(' ').Length == 1
                ? Name.ToLower().StartsWith(rn)
                : Name.Split(' ')[1].ToLower().StartsWith(rn);
        }

        public void Death(string killer, ObjectDesc desc = null)
        {
            if (dying) return;
            dying = true;

            if (Client.Stage == ProtocalStage.Disconnected || resurrecting)
                return;
            if (CheckResurrection())
                return;

            if (Client.Character.Dead)
            {
                Client.Disconnect();
                return;
            }
            GenerateGravestone();
            if (desc != null)
                killer = desc.DisplayId;
            switch (killer)
            {
                case "":
                case "Unknown":
                    break;

                default:
                    Owner.BroadcastPacket(new TextPacket
                    {
                        BubbleTime = 0,
                        Stars = -1,
                        Name = "",
                        Text = "{\"key\":\"server.death\",\"tokens\":{\"player\":\"" + Name + "\",\"level\":\"" + Level + "\",\"enemy\":\"" + killer + "\"}}"
                    }, null);
                    break;
            }

            try
            {
                Client.Character.Dead = true;
                SaveToCharacter();
                Manager.Database.SaveCharacter(Client.Account, Client.Character, true);

                Manager.Database.Death(Manager.GameData, Client.Account, Client.Character, FameCounter.Stats, killer);
                if (Owner.Id != -6)
                {
                    Client.SendPacket(new DeathPacket
                    {
                        AccountId = AccountId,
                        CharId = Client.Character.CharId,
                        Killer = killer,
                        obf0 = -1,
                        obf1 = -1,
                    });
                    Owner.Timers.Add(new WorldTimer(1000, (w, t) => Client.Disconnect()));
                    Owner.LeaveWorld(this);
                }
                else
                    Client.Disconnect();
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

        public override bool HitByProjectile(Projectile projectile, RealmTime time)
        {
            if (projectile.ProjectileOwner is Player ||
                HasConditionEffect(ConditionEffectIndex.Paused) ||
                HasConditionEffect(ConditionEffectIndex.Stasis) ||
                HasConditionEffect(ConditionEffectIndex.Invincible))
                return false;

            return base.HitByProjectile(projectile, time);
        }

        public override void Init(World owner)
        {
            WorldInstance = owner;
            var rand = new Random();
            int x, y;
            do
            {
                x = rand.Next(0, owner.Map.Width);
                y = rand.Next(0, owner.Map.Height);
            } while (owner.Map[x, y].Region != TileRegion.Spawn);
            Move(x + 0.5f, y + 0.5f);
            tiles = new byte[owner.Map.Width, owner.Map.Height];
            SetNewbiePeriod();
            base.Init(owner);
            CheckSetTypeSkin();
        }

        public void SaveToCharacter()
        {
            var chr = Client.Character;
            chr.Experience = Experience;
            chr.Level = Level;
            chr.Tex1 = Texture1;
            chr.Tex2 = Texture2;

            chr.Fame = Fame;
            chr.HP = HP;
            chr.MP = Mp;
            //chr.Items = Inventory.Select(_ => _?.ObjectType ?? -1).ToArray();
            switch (Inventory.Length)
            {
                case 12:
                    chr.Items = Inventory.Select(_ => _?.ObjectType ?? -1).ToArray();
                    break;
            }
            chr.Stats = Stats;
            chr.HealthPotions = HealthPotions;
            chr.MagicPotions = MagicPotions;
            chr.HasBackpack = HasBackpack;
            chr.Skin = PlayerSkin;
            chr.LootDropTimer = (int)LootDropBoostTimeLeft;
            chr.LootTierTimer = (int)LootTierBoostTimeLeft;
            chr.FameStats = FameCounter.Stats.Write();
            chr.LastSeen = DateTime.Now;
        }

        public void Teleport(RealmTime time, TeleportPacket packet)
        {
            var obj = Client.Player.Owner.GetEntity(packet.ObjectId);
            try
            {
                if (obj == null) return;
                if (!TPCooledDown())
                {
                    SendError("Player.teleportCoolDown");
                    return;
                }
                if (obj.HasConditionEffect(ConditionEffectIndex.Invisible))
                {
                    SendError("server.no_teleport_to_invisible");
                    return;
                }
                if (obj.HasConditionEffect(ConditionEffectIndex.Paused))
                {
                    SendError("server.no_teleport_to_paused");
                    return;
                }
                var player = obj as Player;
                if (player != null && !player.NameChosen)
                {
                    SendError("server.teleport_needs_name");
                    return;
                }
                if (obj.Id == Id)
                {
                    SendError("server.teleport_to_self");
                    return;
                }
                if (!Owner.AllowTeleport)
                {
                    SendError(GetLanguageString("server.no_teleport_in_realm", new KeyValuePair<string, object>("realm", Owner.Name)));
                    return;
                }

                SetTPDisabledPeriod();
                Move(obj.X, obj.Y);
                FameCounter.Teleport();
                SetNewbiePeriod();
                UpdateCount++;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                SendError("player.cannotTeleportTo");
                return;
            }
            Owner.BroadcastPacket(new GotoPacket
            {
                ObjectId = Id,
                Position = new Position
                {
                    X = X,
                    Y = Y
                }
            }, null);
            Owner.BroadcastPacket(new ShowEffectPacket
            {
                EffectType = EffectType.Teleport,
                TargetId = Id,
                PosA = new Position
                {
                    X = X,
                    Y = Y
                },
                Color = new ARGB(0xFFFFFFFF)
            }, null);
        }

        public override void Tick(RealmTime time)
        {
            if (Client.Stage == ProtocalStage.Disconnected)
            {
                Owner.LeaveWorld(this);
                return;
            }
            if (Stats != null && Boost != null)
            {
                MaxHp = Stats[0] + Boost[0];
                MaxMp = Stats[1] + Boost[1];
            }

            if (!KeepAlive(time)) return;

            if (Boost == null) CalcBoost();

            TradeHandler?.Tick(time);
            HandleRegen(time);
            HandleQuest(time);
            HandleEffects(time);
            HandleGround(time);
            HandleBoosts();

            FameCounter.Tick(time);

            //if(pingSerial > 5)
            //    if (!Enumerable.Range(UpdatesSend, 5000).Contains(UpdatesReceived))
            //        Client.Disconnect();

            if (Mp < 0) Mp = 0;

            /* try
                * {
                *     psr.Database.SaveCharacter(psr.Account, psr.Character);
                *     UpdateCount++;
                * }
                * catch (ex)
                * {
                * }
            */

            try
            {
                if (Owner != null)
                {
                    SendUpdate(time);
                    if (!Owner.IsPassable((int)X, (int)Y) && Client.Account.Rank < 2)
                    {
                        log.Fatal($"Player {Name} No-Cliped at position: {X}, {Y}");
                        Client.Disconnect();
                    }
                }
            }
            catch (Exception e)
            {
                log.Error(e);
            }
            try
            {
                SendNewTick(time);
            }
            catch (Exception e)
            {
                log.Error(e);
            }

            if (HP < 0 && !dying)
            {
                Death("Unknown");
                return;
            }

            base.Tick(time);
        }

        private bool CheckResurrection()
        {
            for (var i = 0; i < 4; i++)
            {
                var item = Inventory[i];
                if (item == null || !item.Resurrects) continue;

                HP = Stats[0] + Stats[0];
                Mp = Stats[1] + Stats[1];
                Inventory[i] = null;
                Owner.BroadcastPacket(new TextPacket
                {
                    BubbleTime = 0,
                    Stars = -1,
                    Name = "",
                    Text = $"{Name}'s {item.ObjectId} breaks and he disappears"
                }, null);
                Client.Reconnect(new ReconnectPacket
                {
                    Host = "",
                    Port = Program.Settings.GetValue<int>("port"),
                    GameId = World.NEXUS_ID,
                    Name = "Nexus",
                    Key = Empty<byte>.Array,
                });

                resurrecting = true;
                return true;
            }
            return false;
        }

        private void GenerateGravestone()
        {
            var maxed = (from i in Manager.GameData.ObjectTypeToElement[ObjectType].Elements("LevelIncrease") let xElement = Manager.GameData.ObjectTypeToElement[ObjectType].Element(i.Value) where xElement != null let limit = int.Parse(xElement.Attribute("max").Value) let idx = StatsManager.StatsNameToIndex(i.Value) where Stats[idx] >= limit select limit).Count();

            ushort objType;
            int? time;
            switch (maxed)
            {
                case 8:
                    objType = 0x0735;
                    time = null;
                    break;

                case 7:
                    objType = 0x0734;
                    time = null;
                    break;

                case 6:
                    objType = 0x072b;
                    time = null;
                    break;

                case 5:
                    objType = 0x072a;
                    time = null;
                    break;

                case 4:
                    objType = 0x0729;
                    time = null;
                    break;

                case 3:
                    objType = 0x0728;
                    time = null;
                    break;

                case 2:
                    objType = 0x0727;
                    time = null;
                    break;

                case 1:
                    objType = 0x0726;
                    time = null;
                    break;

                default:
                    if (Level <= 1)
                    {
                        objType = 0x0723;
                        time = 30 * 1000;
                    }
                    else if (Level < 20)
                    {
                        objType = 0x0724;
                        time = 60 * 1000;
                    }
                    else
                    {
                        objType = 0x0725;
                        time = 5 * 60 * 1000;
                    }
                    break;
            }
            var obj = new StaticObject(Manager, objType, time, true, time != null, false);
            obj.Move(X, Y);
            obj.Name = Name;
            Owner.EnterWorld(obj);
        }

        private void HandleRegen(RealmTime time)
        {
            if (HP == Stats[0] + Boost[0] || !CanHpRegen())
                hpRegenCounter = 0;
            else
            {
                hpRegenCounter += StatsManager.GetHPRegen() * time.thisTickTimes / 1000f;
                var regen = (int)hpRegenCounter;
                if (regen > 0)
                {
                    HP = Math.Min(Stats[0] + Boost[0], HP + regen);
                    hpRegenCounter -= regen;
                    UpdateCount++;
                }
            }

            if (Mp == Stats[1] + Boost[1] || !CanMpRegen())
                mpRegenCounter = 0;
            else
            {
                mpRegenCounter += StatsManager.GetMPRegen() * time.thisTickTimes / 1000f;
                var regen = (int)mpRegenCounter;
                if (regen <= 0) return;
                Mp = Math.Min(Stats[1] + Boost[1], Mp + regen);
                mpRegenCounter -= regen;
                UpdateCount++;
            }
        }

        public new void Dispose()
        {
            tiles = null;
        }
    }
}