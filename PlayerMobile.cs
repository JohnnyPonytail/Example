using System;
using System.Collections;
using System.Collections.Generic;
using Server.Misc;
using Server.Items;
using Server.Gumps;
using Server.Multis;
using Server.Engines.Help;
using Server.ContextMenus;
using Server.Network;
using Server.Spells;
using Server.Spells.Fifth;
using Server.Spells.Seventh;
using Server.Targeting;
using Server.Engines.Quests;
using Server.Factions;
using Server.Regions;
using Server.Accounting;
using Server.Engines.CannedEvil;
using Server.Engines.Craft;
using Server.Engines.PartySystem;
using Server.Scripts.Custom.Citizenship;
using Server.Scripts.Custom.Citizenship.CommonwealthBonuses;
using Server.Scripts.Engines.Factions.Instances.Factions;
using Server.Scripts;
using Server.Scripts.Custom;
using Server.Scripts.Custom.BountySystem;
using Server.Scripts.Custom.WebService;
using Server.Scripts.Custom.AntiRail;
using Server.SkillHandlers;
using Server.Games;
using Server.Poker;
using Server.Engines.XmlSpawner2;
using Server.Guilds;
using Server.Achievements;

namespace Server.Mobiles
{
    #region Enums
    [Flags]
    public enum PlayerFlag // First 16 bits are reserved for default-distro use, start custom flags at 0x00010000
    {
        None = 0x00000000,
        Glassblowing = 0x00000001,
        Masonry = 0x00000002,
        SandMining = 0x00000004,
        StoneMining = 0x00000008,
        ToggleMiningStone = 0x00000010,
        KarmaLocked = 0x00000020,
        AutoRenewInsurance = 0x00000040,
        UseOwnFilter = 0x00000080,
        PublicMyRunUO = 0x00000100,
        PagingSquelched = 0x00000200,
        Young = 0x00000400,
        AcceptGuildInvites = 0x00000800,
        DisplayChampionTitle = 0x00001000,
        HasStatReward = 0x00002000
    }

    public enum NpcGuild
    {
        None,
        MagesGuild,
        WarriorsGuild,
        ThievesGuild,
        RangersGuild,
        HealersGuild,
        MinersGuild,
        MerchantsGuild,
        TinkersGuild,
        TailorsGuild,
        FishermensGuild,
        BardsGuild,
        BlacksmithsGuild
    }

    public enum SolenFriendship
    {
        None,
        Red,
        Black
    }
    #endregion

    public class PlayerMobile : Mobile
    {
        public override bool OnDragLift(Item item)
        {
            if (StolenItem.IsStolen(item))
            {
                this.SendMessage("After stealing it, you still need a moment to get a handle on the item.");
                return false;
            }
            return true;
        }

        private class CountAndTimeStamp
        {
            private int m_Count;
            private DateTime m_Stamp;

            public CountAndTimeStamp()
            {
            }

            public DateTime TimeStamp { get { return m_Stamp; } }
            public int Count
            {
                get { return m_Count; }
                set { m_Count = value; m_Stamp = DateTime.Now; }
            }
        }

        private DesignContext m_DesignContext;


        private int m_AutomatedBounty = 0;
        private int m_PlayerBounty = 0;
        private bool m_BountyHunter;
        private DateTime m_QuittingBountyHuntingAt = DateTime.MaxValue;
        private NpcGuild m_NpcGuild;
        private DateTime m_NpcGuildJoinTime;
        private DateTime m_NextBODTurnInTime;
        private TimeSpan m_NpcGuildGameTime;
        private PlayerFlag m_Flags;
        private int m_StepsTaken;
        private int m_RunningStepsTaken = 0;
        private DateTime m_LastMovement = DateTime.Now;
        private int m_Profession;
        private int m_NonAutoreinsuredItems; // number of items that could not be automaitically reinsured because gold in bank was not enough
        private bool m_NinjaWepCooldown;
        private int m_MiniHealStrength;

        private int m_LastDamageAmount = 0;
        [CommandProperty(AccessLevel.GameMaster)]
        public int LastDamageAmount { get { return m_LastDamageAmount; } set { m_LastDamageAmount = value; } }

        private DateTime m_SpiritSpeakGhostSightExpiration = DateTime.Now;

        /*
         * a value of zero means, that the mobile is not executing the spell. Otherwise,
         * the value should match the BaseMana required
        */
        private int m_ExecutesLightningStrike; // move to Server.Mobiles??

        private DateTime m_LastOnline;
        private Server.Guilds.RankDefinition m_GuildRank;

        private int m_GuildMessageHue, m_AllianceMessageHue;

        private List<Mobile> m_AutoStabled;
        private List<Mobile> m_AllFollowers;
        private List<Mobile> m_RecentlyReported;

        private bool mHasBadName;

        private List<ExileTimer> mExileTimers;

        //Database variables
        private int mMurdsThisSession = 0;
        private int mMurdererKillsThisSession = 0;
        private int mMilitiaKillsThisSession = 0;
        private int mMilitiaDeathsThisSession = 0;
        private int mKillsThisSession = 0;
        private int mDeathsThisSession = 0;
        private int mOreMinedThisSession = 0;
        private int mFishCaughtThisSession = 0;
        private int mWoodHarvestedThisSession = 0;
        private int mItemsCraftedThisSession = 0;
        private int mChestsPickedThisSession = 0;
        private int mMapsDecodedThisSession = 0;
        private int mSheepShornThisSession = 0;
        private int mGuardWhacksThisSession = 0;
        private int mStealAttempsThisSession = 0;
        private int mRecallsThisSession = 0;
        private int mPlayerRessurrectsThisSession = 0;
        private int mHeroesCapturedThisSession = 0;
        private int mHeroesRescuedThisSession = 0;
        private int mWorldWarsFlagsCapturedThisSession = 0;
        private int mBossesKilledThisSession = 0;
        private int mPlayersKilledThisSession = 0;
        private int mCorpsesCarvedThisSession = 0;
        private int mHeadsTurnedInThisSession = 0;
        private int mSilverEarnedThisSession = 0;
        private int mItemsImbuedThisSession = 0;
        private int mStepsWalkedThisSession = 0;
        private int mStealthStepsWalkedThisSession = 0;
        private int mGuildKillsThisSession = 0;
        private int mGuildDeathsThisSession = 0;
        private int mPotionsConsumedThisSession = 0;
        private int mGoldSpentThisSession = 0;
        private int mCampfiresStartedThisSession = 0;
        private int mTimesCriminalThisSession = 0;
        private int mCorpsesInspectedThisSession = 0;
        private int mPlayersAttackedThisSession = 0;
        private int mEscortsTakenThisSession = 0;
        private int mAnimalsTamedThisSession = 0;
        private int mProjectilesFiredThisSession = 0;
        private int mBandagesUsedThisSession = 0;
        private int mPoisonsAppliedThisSession = 0;
        private int mPoisonsCastedThisSession = 0;
        private int mCottonPickedThisSession = 0;
        private int mHidesSkinnedThisSession = 0;
        private int mFeathersPluckedThisSession = 0;
        private int mSuccessfulStealsThisSession = 0;

        private DatabaseUpdateTimer mDBUpdateTimer;

        //Texas Holdem
        private PokerGame m_PokerGame;
        public PokerGame PokerGame
        {
            get { return m_PokerGame; }
            set { m_PokerGame = value; }
        }
        //End Texas Holdem


        #region Getters & Setters

        public List<Mobile> RecentlyReported
        {
            get
            {
                return m_RecentlyReported;
            }
            set
            {
                m_RecentlyReported = value;
            }
        }

        public List<Mobile> AutoStabled { get { return m_AutoStabled; } }

        public bool NinjaWepCooldown
        {
            get
            {
                return m_NinjaWepCooldown;
            }
            set
            {
                m_NinjaWepCooldown = value;
            }
        }

        public int MiniHealStrength
        {
            get
            {
                return m_MiniHealStrength;
            }
            set
            {
                m_MiniHealStrength = value;
            }
        }

        public List<Mobile> AllFollowers
        {
            get
            {
                if (m_AllFollowers == null)
                    m_AllFollowers = new List<Mobile>();
                return m_AllFollowers;
            }
        }

        public Server.Guilds.RankDefinition GuildRank
        {
            get
            {
                if (this.AccessLevel >= AccessLevel.GameMaster)
                    return Server.Guilds.RankDefinition.Leader;
                else
                    return m_GuildRank;
            }
            set { m_GuildRank = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int GuildMessageHue
        {
            get { return m_GuildMessageHue; }
            set { m_GuildMessageHue = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int AllianceMessageHue
        {
            get { return m_AllianceMessageHue; }
            set { m_AllianceMessageHue = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Profession
        {
            get { return m_Profession; }
            set { m_Profession = value; }
        }

        public int StepsTaken
        {
            get { return m_StepsTaken; }
            set { m_StepsTaken = value; }
        }

        public int RunningStepsTaken
        {
            get { return m_RunningStepsTaken; }
            set { m_RunningStepsTaken = value; }
        }

        public DateTime SpiritSpeakGhostSightExpiration
        {
            get { return m_SpiritSpeakGhostSightExpiration; }
            set { m_SpiritSpeakGhostSightExpiration = value; }
        }

        public DateTime LastMovement
        {
            get { return m_LastMovement; }
            set { m_LastMovement = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int AutomatedBounty
        {
            get { return m_AutomatedBounty; }
            set { m_AutomatedBounty = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int PlayerBounty
        {
            get { return m_PlayerBounty; }
            set { m_PlayerBounty = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Bounty
        {
            get { return m_AutomatedBounty + m_PlayerBounty; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool BountyHunter
        {
            get { return m_BountyHunter; }
            set
            {
                m_BountyHunter = value;
                //ApplyCorrectTitle();
            }
        }

        /* private void ApplyCorrectTitle()
        {
            
            if (this.PlayerMurdererStatus == MurdererStatus.Parole)
            {
                this.Prefix = "Dreadlord";
            }
            else if (this.BountyHunter)
            {
                this.Title = " "; // hack to get the name hue to change in Mobile.cs
            }
            else
            {
                this.Title = "";
            }
        } */

        private MurdererStatus m_PlayerMurdererStatus;
        [CommandProperty(AccessLevel.GameMaster)]
        public MurdererStatus PlayerMurdererStatus
        {
            get { return m_PlayerMurdererStatus; }
            set
            {
                m_PlayerMurdererStatus = value;
                //ApplyCorrectTitle();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Citizen_ForceRemove
        {
            get { return false; }
            set
            {
                if (value == true && this.mCitizenshipPlayerState != null && this.mCitizenshipPlayerState != null)
                {
                    mCitizenshipPlayerState.LeftTownTime = DateTime.MinValue;
                    mCitizenshipPlayerState.Commonwealth.RemoveCitizenFromLock(this, false);
                }
            }
        }

        public enum MurdererStatus
        {
            None,
            Parole//,
            //Outcast
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime QuittingBountyHuntingAt
        {
            get { return m_QuittingBountyHuntingAt; }
            set { m_QuittingBountyHuntingAt = value; }
        }

        public void QuitBountyHunter()
        {
            if (this.BountyHunter == false) return;

            this.BountyHunter = false;
            m_QuittingBountyHuntingAt = DateTime.MaxValue;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public NpcGuild NpcGuild
        {
            get { return m_NpcGuild; }
            set { m_NpcGuild = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NpcGuildJoinTime
        {
            get { return m_NpcGuildJoinTime; }
            set { m_NpcGuildJoinTime = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextBODTurnInTime
        {
            get { return m_NextBODTurnInTime; }
            set { m_NextBODTurnInTime = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastOnline
        {
            get { return m_LastOnline; }
            set { m_LastOnline = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NpcGuildGameTime
        {
            get { return m_NpcGuildGameTime; }
            set { m_NpcGuildGameTime = value; }
        }

        private int m_ToTItemsTurnedIn;

        [CommandProperty(AccessLevel.GameMaster)]
        public int ToTItemsTurnedIn
        {
            get { return m_ToTItemsTurnedIn; }
            set { m_ToTItemsTurnedIn = value; }
        }

        private int m_ToTTotalMonsterFame;

        [CommandProperty(AccessLevel.GameMaster)]
        public int ToTTotalMonsterFame
        {
            get { return m_ToTTotalMonsterFame; }
            set { m_ToTTotalMonsterFame = value; }
        }

        public int ExecutesLightningStrike
        {
            get { return m_ExecutesLightningStrike; }
            set { m_ExecutesLightningStrike = value; }
        }

        public bool HasBadName { get { return mHasBadName; } set { mHasBadName = value; } }
        //Database properties
        public int MurdersThisSession { get { return mMurdsThisSession; } set { mMurdsThisSession = value; } }
        public int MurdererKillsThisSession { get { return mMurdererKillsThisSession; } set { mMurdererKillsThisSession = value; } }
        public int MilitiaKillsThisSession { get { return mMilitiaKillsThisSession; } set { mMilitiaKillsThisSession = value; } }
        public int MilitiaDeathsThisSession { get { return mMilitiaDeathsThisSession; } set { mMilitiaDeathsThisSession = value; } }
        public int KillsThisSession { get { return mKillsThisSession; } set { mKillsThisSession = value; } }
        public int DeathsThisSession { get { return mDeathsThisSession; } set { mDeathsThisSession = value; } }
        public int OreMinedThisSession { get { return mOreMinedThisSession; } set { mOreMinedThisSession = value; } }
        public int FishCaughtThisSession { get { return mFishCaughtThisSession; } set { mFishCaughtThisSession = value; } }
        public int WoodHarvestedThisSession { get { return mWoodHarvestedThisSession; } set { mWoodHarvestedThisSession = value; } }
        public int ItemsCraftedThisSession { get { return mItemsCraftedThisSession; } set { mItemsCraftedThisSession = value; } }
        public int ChestsPickedThisSession { get { return mChestsPickedThisSession; } set { mChestsPickedThisSession = value; } }
        public int MapsDecodedThisSession { get { return mMapsDecodedThisSession; } set { mMapsDecodedThisSession = value; } }
        public int SheepShornThisSession { get { return mSheepShornThisSession; } set { mSheepShornThisSession = value; } }
        public int GuardWhacksThisSession { get { return mGuardWhacksThisSession; } set { mGuardWhacksThisSession = value; } }
        public int StealAttemptsThisSession { get { return mStealAttempsThisSession; } set { mStealAttempsThisSession = value; } }
        public int RecallsThisSession { get { return mRecallsThisSession; } set { mRecallsThisSession = value; } }
        public int ResurrectionsThisSession { get { return mPlayerRessurrectsThisSession; } set { mPlayerRessurrectsThisSession = value; } }
        public int HeroesCapturedThisSession { get { return mHeroesCapturedThisSession; } set { mHeroesCapturedThisSession = value; } }
        public int HeroesRescuedThisSession { get { return mHeroesRescuedThisSession; } set { mHeroesRescuedThisSession = value; } }
        public int WorldWarsFlagsCapturedThisSession { get { return mWorldWarsFlagsCapturedThisSession; } set { mWorldWarsFlagsCapturedThisSession = value; } }
        public int BossesKilledThisSession { get { return mBossesKilledThisSession; } set { mBossesKilledThisSession = value; } }
        public int CorpsesCarvedThisSession { get { return mCorpsesCarvedThisSession; } set { mCorpsesCarvedThisSession = value; } }
        public int PlayersKilledThisSession { get { return mPlayersKilledThisSession; } set { mPlayersKilledThisSession = value; } }
        public int HeadsTurnedInThisSession { get { return mHeadsTurnedInThisSession; } set { mHeadsTurnedInThisSession = value; } }
        public int SilverEarnedThisSession { get { return mSilverEarnedThisSession; } set { mSilverEarnedThisSession = value; } }
        public int ItemsImbuedThisSession { get { return mItemsImbuedThisSession; } set { mItemsImbuedThisSession = value; } }
        public int StepsWalkedThisSession { get { return mStepsWalkedThisSession; } set { mStepsWalkedThisSession = value; } }
        public int StealthStepsWalkedThisSession { get { return mStealthStepsWalkedThisSession; } set { mStealthStepsWalkedThisSession = value; } }
        public int GuildKillsThisSession { get { return mGuildKillsThisSession; } set { mGuildKillsThisSession = value; } }
        public int GuildDeathsThisSession { get { return mGuildDeathsThisSession; } set { mGuildDeathsThisSession = value; } }
        public int PotionsConsumedThisSession { get { return mPotionsConsumedThisSession; } set { mPotionsConsumedThisSession = value; } }
        public int GoldSpentThisSession { get { return mGoldSpentThisSession; } set { mGoldSpentThisSession = value; } }
        public int CampfiresStartedThisSession { get { return mCampfiresStartedThisSession; } set { mCampfiresStartedThisSession = value; } }
        public int TimesCriminalThisSession { get { return mTimesCriminalThisSession; } set { mTimesCriminalThisSession = value; } }
        public int CorpsesInspectedThisSession { get { return mCorpsesInspectedThisSession; } set { mCorpsesInspectedThisSession = value; } }
        public int PlayersAttackedThisSession { get { return mPlayersAttackedThisSession; } set { mPlayersAttackedThisSession = value; } }
        public int EscortsTakenThisSession { get { return mEscortsTakenThisSession; } set { mEscortsTakenThisSession = value; } }
        public int AnimalsTamedThisSession { get { return mAnimalsTamedThisSession; } set { mAnimalsTamedThisSession = value; } }
        public int ProjectilesFiredThisSession { get { return mProjectilesFiredThisSession; } set { mProjectilesFiredThisSession = value; } }
        public int BandagesUsedThisSession { get { return mBandagesUsedThisSession; } set { mBandagesUsedThisSession = value; } }
        public int PoisonsAppliedThisSession { get { return mPoisonsAppliedThisSession; } set { mPoisonsAppliedThisSession = value; } }
        public int PoisonsCastedThisSession { get { return mPoisonsCastedThisSession; } set { mPoisonsCastedThisSession = value; } }
        public int CottonPickedThisSession { get { return mCottonPickedThisSession; } set { mCottonPickedThisSession = value; } }
        public int HidesSkinnedThisSession { get { return mHidesSkinnedThisSession; } set { mHidesSkinnedThisSession = value; } }
        public int FeathersPluckedThisSession { get { return mFeathersPluckedThisSession; } set { mFeathersPluckedThisSession = value; } }
        public int SuccessfulStealsThisSession { get { return mSuccessfulStealsThisSession; } set { mSuccessfulStealsThisSession = value; } }

        public DatabaseUpdateTimer DBUpdateTimer { get { return mDBUpdateTimer; } set { mDBUpdateTimer = value; } }
        #endregion

        #region PlayerFlags
        public PlayerFlag Flags
        {
            get { return m_Flags; }
            set { m_Flags = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual string PseudoSeerPermissions
        {
            get
            {
                if (PseudoSeerStone.Instance == null)
                {
                    return null;
                }
                return PseudoSeerStone.Instance.GetPermissionsFor(this.Account);
            }
            set
            {
                if (PseudoSeerStone.Instance == null)
                {
                    return;
                }
                string oldPermissions = PseudoSeerStone.Instance.CurrentPermissionsClipboard;
                PseudoSeerStone.Instance.CurrentPermissionsClipboard = value;
                if (value == null)
                {
                    PseudoSeerStone.Instance.PseudoSeerRemove = this;
                }
                else
                {
                    PseudoSeerStone.Instance.PseudoSeerAdd = this;
                }
                PseudoSeerStone.Instance.CurrentPermissionsClipboard = oldPermissions;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual string TeamFlagsReadable
        {
            get
            {
                string output = "";
                ulong i = 1UL;
                for (int j = 0; j < 64; j++)
                {
                    if ((m_TeamFlags & i) > 0)
                    {
                        output += Enum.ToObject(typeof(AITeamList.TeamFlags), (m_TeamFlags & i)).ToString() + ", ";
                    }
                    i = i << 1;
                }
                return output == "" ? "None" : output;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual AITeamList.TeamFlags TeamFlagsAdd { get { return AITeamList.TeamFlags.None; } set { m_TeamFlags |= (ulong)value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual AITeamList.TeamFlags TeamFlagsDelete { get { return AITeamList.TeamFlags.None; } set { m_TeamFlags &= ~(ulong)value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual ulong TeamFlagsCitizenship
        {
            get
            {
                if (this.CitizenshipPlayerState != null && this.CitizenshipPlayerState.Commonwealth != null)
                {
                    ICommonwealth commonwealth = this.CitizenshipPlayerState.Commonwealth;
                    if (commonwealth.Definition != null)
                    {
                        switch (commonwealth.Definition.TownName.ToString())
                        {
                            case "Yew":
                                return (long)AITeamList.TeamFlags.Team1;
                            case "Trinsic":
                                return (long)AITeamList.TeamFlags.Team2;
                            case "Blackrock":
                                return (long)AITeamList.TeamFlags.Team3;
                            case "Calor":
                                return (long)AITeamList.TeamFlags.Team4;
                            case "Vermell":
                                return (long)AITeamList.TeamFlags.Team5;
                        }
                    }
                }
                return 1;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public virtual string Citizenship
        {
            get
            {
                if (this.CitizenshipPlayerState != null && this.CitizenshipPlayerState.Commonwealth != null)
                {
                    ICommonwealth commonwealth = this.CitizenshipPlayerState.Commonwealth;
                    if (commonwealth.Definition != null)
                    {
                        return commonwealth.Definition.TownName;
                    }
                }
                return null;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool PagingSquelched
        {
            get { return GetFlag(PlayerFlag.PagingSquelched); }
            set { SetFlag(PlayerFlag.PagingSquelched, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Glassblowing
        {
            get { return GetFlag(PlayerFlag.Glassblowing); }
            set { SetFlag(PlayerFlag.Glassblowing, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Masonry
        {
            get { return GetFlag(PlayerFlag.Masonry); }
            set { SetFlag(PlayerFlag.Masonry, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool SandMining
        {
            get { return GetFlag(PlayerFlag.SandMining); }
            set { SetFlag(PlayerFlag.SandMining, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool StoneMining
        {
            get { return GetFlag(PlayerFlag.StoneMining); }
            set { SetFlag(PlayerFlag.StoneMining, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool ToggleMiningStone
        {
            get { return GetFlag(PlayerFlag.ToggleMiningStone); }
            set { SetFlag(PlayerFlag.ToggleMiningStone, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool KarmaLocked
        {
            get { return GetFlag(PlayerFlag.KarmaLocked); }
            set { SetFlag(PlayerFlag.KarmaLocked, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool AutoRenewInsurance
        {
            get { return GetFlag(PlayerFlag.AutoRenewInsurance); }
            set { SetFlag(PlayerFlag.AutoRenewInsurance, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool UseOwnFilter
        {
            get { return GetFlag(PlayerFlag.UseOwnFilter); }
            set { SetFlag(PlayerFlag.UseOwnFilter, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool PublicMyRunUO
        {
            get { return GetFlag(PlayerFlag.PublicMyRunUO); }
            set { SetFlag(PlayerFlag.PublicMyRunUO, value); InvalidateMyRunUO(); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool AcceptGuildInvites
        {
            get { return GetFlag(PlayerFlag.AcceptGuildInvites); }
            set { SetFlag(PlayerFlag.AcceptGuildInvites, value); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool HasStatReward
        {
            get { return GetFlag(PlayerFlag.HasStatReward); }
            set { SetFlag(PlayerFlag.HasStatReward, value); }
        }
        #endregion

        #region Auto Arrow Recovery
        private Dictionary<Type, int> m_RecoverableAmmo = new Dictionary<Type, int>();

        public Dictionary<Type, int> RecoverableAmmo
        {
            get { return m_RecoverableAmmo; }
        }

        public void RecoverAmmo()
        {
            if (Core.SE && Alive)
            {
                foreach (KeyValuePair<Type, int> kvp in m_RecoverableAmmo)
                {
                    if (kvp.Value > 0)
                    {
                        Item ammo = null;

                        try
                        {
                            ammo = Activator.CreateInstance(kvp.Key) as Item;
                        }
                        catch
                        {
                        }

                        if (ammo != null)
                        {
                            string name = ammo.Name;
                            ammo.Amount = kvp.Value;

                            if (name == null)
                            {
                                if (ammo is Arrow)
                                    name = "arrow";
                                else if (ammo is Bolt)
                                    name = "bolt";
                            }

                            if (name != null && ammo.Amount > 1)
                                name = String.Format("{0}s", name);

                            if (name == null)
                                name = String.Format("#{0}", ammo.LabelNumber);

                            PlaceInBackpack(ammo);
                            SendLocalizedMessage(1073504, String.Format("{0}\t{1}", ammo.Amount, name)); // You recover ~1_NUM~ ~2_AMMO~.
                        }
                    }
                }

                m_RecoverableAmmo.Clear();
            }
        }

        #endregion

        private bool m_ViolentSwingReady = false;

        private PlayerMobile m_LastExplosionAttacker = null;
        [CommandProperty(AccessLevel.GameMaster)]
        public PlayerMobile LastExplosionAttacker
        {
            get
            {
                return m_LastExplosionAttacker;
            }
            set
            {
                m_LastExplosionAttacker = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool ViolentSwingReady
        {
            get
            {
                return m_ViolentSwingReady;
            }
            set
            {
                m_ViolentSwingReady = value;
            }
        }

        private DateTime m_NextToxicSwing = DateTime.MinValue;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextToxicSwing
        {
            get { return m_NextToxicSwing; }
            set { m_NextToxicSwing = value; }
        }

        private bool m_ToxicSwingReady = false;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool ToxicSwingReady
        {
            get
            {
                return m_ToxicSwingReady;
            }
            set
            {
                m_ToxicSwingReady = value;
            }
        }

        private DateTime m_NextViolentSwing = DateTime.MinValue;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextViolentSwing
        {
            get { return m_NextViolentSwing; }
            set { m_NextViolentSwing = value; }
        }

        private DateTime m_NextStunPunch = DateTime.MinValue;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextStunPunch
        {
            get { return m_NextStunPunch; }
            set { m_NextStunPunch = value; }
        }

        private DateTime m_AnkhNextUse;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime AnkhNextUse
        {
            get { return m_AnkhNextUse; }
            set { m_AnkhNextUse = value; }
        }

        private DateTime m_NextReflectAt = DateTime.MinValue;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextReflectAt
        {
            get { return m_NextReflectAt; }
            set { m_NextReflectAt = value; }
        }

        private DateTime m_NextReactiveArmorAt = DateTime.MinValue;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextReactiveArmorAt
        {
            get { return m_NextReactiveArmorAt; }
            set { m_NextReactiveArmorAt = value; }
        }

        private DateTime m_NextProtectionAt = DateTime.MinValue;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextProtectionAt
        {
            get { return m_NextProtectionAt; }
            set { m_NextProtectionAt = value; }
        }

        private DateTime m_NextWallOfStoneAt = DateTime.MinValue;
        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextWallOfStone
        {
            get { return m_NextWallOfStoneAt; }
            set { m_NextWallOfStoneAt = value; }
        }

        private DateTime m_NextTeleportAt = DateTime.MinValue;
        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextTeleportAt
        {
            get { return m_NextTeleportAt; }
            set { m_NextTeleportAt = value; }
        }

        private TimeSpan m_Pseu_NextPossessDelay = TimeSpan.FromMinutes(5.0);
        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan Pseu_NextPossessDelay
        {
            get { return m_Pseu_NextPossessDelay; }
            set
            {
                m_Pseu_NextPossessDelay = value;
                if (DateTime.Now + m_Pseu_NextPossessDelay < Pseu_NextPossessAllowed)
                {
                    Pseu_NextPossessAllowed = DateTime.Now + m_Pseu_NextPossessDelay;
                }
            }
        }

        private DateTime m_Pseu_NextPossessAllowed = DateTime.MinValue;
        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime Pseu_NextPossessAllowed
        {
            get { return m_Pseu_NextPossessAllowed; }
            set { m_Pseu_NextPossessAllowed = value; }
        }

        private int m_Pseu_SpawnsAllowed = 0;
        [CommandProperty(AccessLevel.GameMaster)]
        public int Pseu_SpawnsAllowed
        {
            get { return m_Pseu_SpawnsAllowed; }
            set { m_Pseu_SpawnsAllowed = value; }
        }

        private TimeSpan m_Pseu_NextBroadcastDelay = TimeSpan.FromMinutes(5.0);
        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan Pseu_NextBroadcastDelay
        {
            get { return m_Pseu_NextBroadcastDelay; }
            set
            {
                m_Pseu_NextBroadcastDelay = value;
                if (DateTime.Now + m_Pseu_NextBroadcastDelay < Pseu_NextBroadcastAllowed)
                {
                    Pseu_NextBroadcastAllowed = DateTime.Now + m_Pseu_NextBroadcastDelay;
                }
            }
        }

        private DateTime m_Pseu_NextBroadcastAllowed = DateTime.MaxValue;
        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime Pseu_NextBroadcastAllowed
        {
            get { return m_Pseu_NextBroadcastAllowed; }
            set { m_Pseu_NextBroadcastAllowed = value; }
        }

        private bool m_Pseu_DungeonWatchAllowed = false;
        [CommandProperty(AccessLevel.GameMaster)]
        public bool Pseu_DungeonWatchAllowed
        {
            get { return m_Pseu_DungeonWatchAllowed; }
            set { m_Pseu_DungeonWatchAllowed = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan DisguiseTimeLeft
        {
            get { return DisguiseTimers.TimeRemaining(this); }
        }

        private DateTime m_PeacedUntil;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime PeacedUntil
        {
            get { return m_PeacedUntil; }
            set { m_PeacedUntil = value; }
        }

        public override bool FollowersMaxChanged()
        {
            //Remove Excess Followers if Somehow Over the Limit
            return true;
        }

        public override int DetermineFollowersMax()
        {
            int maxFollowers = FeatureList.Followers.DefaultPlayerFollowers;

            int animalLoreBonusFollowers = (int)(Math.Floor(Skills[SkillName.AnimalLore].Value / FeatureList.Followers.AnimaLoreBonusControlSlotDivisor));
            int SpiritSpeakBonusFollowers = (int)(Math.Floor(Skills[SkillName.SpiritSpeak].Value / FeatureList.Followers.SpiritSpeakBonusControlSlotDivisor));

            maxFollowers += animalLoreBonusFollowers;
            maxFollowers += SpiritSpeakBonusFollowers;

            //Max 6 Followers
            if (maxFollowers > FeatureList.Followers.TotalMaxFollowers)
            {
                maxFollowers = FeatureList.Followers.TotalMaxFollowers;
            }

            return maxFollowers;
        }

        #region Scroll of Alacrity
        private DateTime m_AcceleratedStart;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime AcceleratedStart
        {
            get { return m_AcceleratedStart; }
            set { m_AcceleratedStart = value; }
        }

        private SkillName m_AcceleratedSkill;

        [CommandProperty(AccessLevel.GameMaster)]
        public SkillName AcceleratedSkill
        {
            get { return m_AcceleratedSkill; }
            set { m_AcceleratedSkill = value; }
        }
        #endregion

        [CommandProperty(AccessLevel.Counselor)]
        public TimeSpan BountyParoleExpiration
        {
            get { return m_BountyParoleExpiration; }
            set { m_BountyParoleExpiration = value; }
        }

        private TimeSpan m_BountyParoleExpiration = TimeSpan.MaxValue;

        public void SufferBountyParole(TimeSpan duration)
        {
            if (duration == TimeSpan.Zero)
                return;

            TimeSpan newBountyExpiration = GameTime + duration;

            if (PlayerMurdererStatus != PlayerMobile.MurdererStatus.Parole || newBountyExpiration > m_BountyParoleExpiration)
                m_BountyParoleExpiration = newBountyExpiration;

            this.PlayerMurdererStatus = MurdererStatus.Parole;
            SendMessage("Since your head has been turned into the bounty hunters, you are now making atonement for your sins.");
            //SendMessage("Since your head has been turned into the bounty hunters, you are now making atonement for your sins.  During this atonement period, you cannot attack innocents.  You may say \"i wish to continue killing\" to regain this ability.  BUT BE WARNED!  If you get bounty hunted again, you will suffer a PERMANENT 50% stat loss, and become a regular murderer again (with no bounty on your head).");
        }

        //public void ApplyOutcastStatloss()
        public void SufferBountyStatloss()
        {
            for (int i = 0; i < Skills.Length; i++)
            {
                Skill sk = Skills[i];

                // reduce all skill values by 20%
                sk.Base = sk.Base - (sk.Base * 0.2); // 100 - (100 * 0.20) = 80.0
            }

            this.PlayerMurdererStatus = MurdererStatus.None;
        }

        private bool m_AwaitingAntiRailResponse = false;
        private int m_ResourcesGatheredSinceLastChallenge = 0;
        private int m_IncorrectCaptchaResponses = 0;

        [CommandProperty(AccessLevel.Counselor)]
        public bool AntiRailResponseNeeded
        {
            get { return m_AwaitingAntiRailResponse; }
            set { m_AwaitingAntiRailResponse = value; }
        }

        [CommandProperty(AccessLevel.Counselor)]
        public int ResourcesSinceLastChallenge
        {
            get { return m_ResourcesGatheredSinceLastChallenge; }
            set { m_ResourcesGatheredSinceLastChallenge = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string IP
        {
            get
            {
                if (this.Deleted || this.NetState == null || this.NetState.Address == null) { return null; }
                return "" + this.NetState.Address.ToString().Replace(".", "-");
            }
        }

        [CommandProperty(AccessLevel.Counselor)]
        public int IncorrectCaptchaResponses
        {
            get { return m_IncorrectCaptchaResponses; }
            set { m_IncorrectCaptchaResponses = value; }
        }

        public override bool OnMoveOver(Mobile m)
        {
            if (m is BaseCreature && !((BaseCreature)m).Controlled)
                return (!Alive || !m.Alive || IsDeadBondedPet || m.IsDeadBondedPet) || (Hidden && m.AccessLevel > AccessLevel.Player);

            return base.OnMoveOver(m);
        }

        public override bool CheckShove(Mobile shoved)
        {
            if (m_IgnoreMobiles)
                return true;
            else
                return base.CheckShove(shoved);
        }

        public static Direction GetDirection4(Point3D from, Point3D to)
        {
            int dx = from.X - to.X;
            int dy = from.Y - to.Y;

            int rx = dx - dy;
            int ry = dx + dy;

            Direction ret;

            if (rx >= 0 && ry >= 0)
                ret = Direction.West;
            else if (rx >= 0 && ry < 0)
                ret = Direction.South;
            else if (rx < 0 && ry < 0)
                ret = Direction.East;
            else
                ret = Direction.North;

            return ret;
        }

        public override bool OnDroppedItemToWorld(Item item, Point3D location)
        {
            if (!base.OnDroppedItemToWorld(item, location))
                return false;

            IPooledEnumerable mobiles = Map.GetMobilesInRange(location, 0);

            foreach (Mobile m in mobiles)
            {
                if (m.Z >= location.Z && m.Z < location.Z + 16)
                {
                    mobiles.Free();
                    return false;
                }
            }

            mobiles.Free();

            BounceInfo bi = item.GetBounce();

            if (bi != null)
            {
                Type type = item.GetType();

                if (type.IsDefined(typeof(FurnitureAttribute), true) || type.IsDefined(typeof(DynamicFlipingAttribute), true))
                {
                    object[] objs = type.GetCustomAttributes(typeof(FlipableAttribute), true);

                    if (objs != null && objs.Length > 0)
                    {
                        FlipableAttribute fp = objs[0] as FlipableAttribute;

                        if (fp != null)
                        {
                            int[] itemIDs = fp.ItemIDs;

                            Point3D oldWorldLoc = bi.m_WorldLoc;
                            Point3D newWorldLoc = location;

                            if (oldWorldLoc.X != newWorldLoc.X || oldWorldLoc.Y != newWorldLoc.Y)
                            {
                                Direction dir = GetDirection4(oldWorldLoc, newWorldLoc);

                                if (itemIDs.Length == 2)
                                {
                                    switch (dir)
                                    {
                                        case Direction.North:
                                        case Direction.South: item.ItemID = itemIDs[0]; break;
                                        case Direction.East:
                                        case Direction.West: item.ItemID = itemIDs[1]; break;
                                    }
                                }
                                else if (itemIDs.Length == 4)
                                {
                                    switch (dir)
                                    {
                                        case Direction.South: item.ItemID = itemIDs[0]; break;
                                        case Direction.East: item.ItemID = itemIDs[1]; break;
                                        case Direction.North: item.ItemID = itemIDs[2]; break;
                                        case Direction.West: item.ItemID = itemIDs[3]; break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        public override int GetPacketFlags()
        {
            int flags = base.GetPacketFlags();

            if (m_IgnoreMobiles)
                flags |= 0x10;

            return flags;
        }

        public override int GetOldPacketFlags()
        {
            int flags = base.GetOldPacketFlags();

            if (m_IgnoreMobiles)
                flags |= 0x10;

            return flags;
        }

        public bool GetFlag(PlayerFlag flag)
        {
            return ((m_Flags & flag) != 0);
        }

        public void SetFlag(PlayerFlag flag, bool value)
        {
            if (value)
                m_Flags |= flag;
            else
                m_Flags &= ~flag;
        }

        public DesignContext DesignContext
        {
            get { return m_DesignContext; }
            set { m_DesignContext = value; }
        }

        public static void Initialize()
        {
            if (FastwalkPrevention)
                PacketHandlers.RegisterThrottler(0x02, new ThrottlePacketCallback(MovementThrottle_Callback));

            EventSink.Login += new LoginEventHandler(OnLogin);
            EventSink.Logout += new LogoutEventHandler(OnLogout);
            EventSink.Connected += new ConnectedEventHandler(EventSink_Connected);
            EventSink.Disconnected += new DisconnectedEventHandler(EventSink_Disconnected);
        }

        private static void CheckPets()
        {
            foreach (Mobile m in World.Mobiles.Values)
            {
                if (m is PlayerMobile)
                {
                    PlayerMobile pm = (PlayerMobile)m;

                    if (((!pm.Mounted || (pm.Mount != null && pm.Mount is EtherealMount)) && (pm.AllFollowers.Count > pm.AutoStabled.Count)) ||
                        (pm.Mounted && (pm.AllFollowers.Count > (pm.AutoStabled.Count + 1))))
                    {
                        pm.AutoStablePets(); /* autostable checks summons, et al: no need here */
                    }
                }
            }
        }

        public override void OnSkillInvalidated(Skill skill)
        {
            //Skill Amounts Changing: Re-evaluate Certain Skill-Based Properties

            //If AnimalLore / Spirit Speak Changed, FollowersMax May Change                  
            FollowersMax = DetermineFollowersMax();
        }

        public override int GetMaxResistance(ResistanceType type)
        {
            if (AccessLevel > AccessLevel.Player)
                return int.MaxValue;

            int max = base.GetMaxResistance(type);

            if (type != ResistanceType.Physical && 60 < max && Spells.Fourth.CurseSpell.UnderEffect(this))
                max = 60;

            if (Core.ML && this.Race == Race.Elf && type == ResistanceType.Energy)
                max += 5; //Intended to go after the 60 max from curse

            return max;
        }

        protected override void OnRaceChange(Race oldRace)
        {
            ValidateEquipment();
            UpdateResistances();
        }

        private int m_LastGlobalLight = -1, m_LastPersonalLight = -1;

        public override void OnNetStateChanged()
        {
            m_LastGlobalLight = -1;
            m_LastPersonalLight = -1;
        }

        public override void ComputeBaseLightLevels(out int global, out int personal)
        {
            global = LightCycle.ComputeLevelFor(this);

            bool racialNightSight = (Core.ML && this.Race == Race.Elf);

            if (this.LightLevel < 21 && (AosAttributes.GetValue(this, AosAttribute.NightSight) > 0 || racialNightSight))
                personal = 21;
            else
                personal = this.LightLevel;
        }

        public override void CheckLightLevels(bool forceResend)
        {
            NetState ns = this.NetState;

            if (ns == null)
                return;

            int global, personal;

            ComputeLightLevels(out global, out personal);

            if (!forceResend)
                forceResend = (global != m_LastGlobalLight || personal != m_LastPersonalLight);

            if (!forceResend)
                return;

            m_LastGlobalLight = global;
            m_LastPersonalLight = personal;

            ns.Send(GlobalLightLevel.Instantiate(global));
            ns.Send(new PersonalLightLevel(this, personal));
        }

        public override bool SendSpeedControl(SpeedControlType type)
        {
            return base.SendSpeedControl(type);
        }

        public override int GetMinResistance(ResistanceType type)
        {
            int magicResist = (int)(Skills[SkillName.MagicResist].Value * 10);
            int min = int.MinValue;

            if (magicResist >= 1000)
                min = 40 + ((magicResist - 1000) / 50);
            else if (magicResist >= 400)
                min = (magicResist - 400) / 15;

            if (min > MaxPlayerResistance)
                min = MaxPlayerResistance;

            int baseMin = base.GetMinResistance(type);

            if (min < baseMin)
                min = baseMin;

            return min;
        }

        public override void OnManaChange(int oldValue)
        {
            base.OnManaChange(oldValue);
        }

        private static void OnLogin(LoginEventArgs e)
        {
            Mobile from = e.Mobile;

            CheckAtrophies(from);

            if (AccountHandler.LockdownLevel > AccessLevel.Player)
            {
                string notice;

                Accounting.Account acct = from.Account as Accounting.Account;

                if (acct == null || !acct.HasAccess(from.NetState))
                {
                    if (from.AccessLevel == AccessLevel.Player)
                        notice = "The server is currently under lockdown. No players are allowed to log in at this time.";
                    else
                        notice = "The server is currently under lockdown. You do not have sufficient access level to connect.";

                    Timer.DelayCall(TimeSpan.FromSeconds(1.0), new TimerStateCallback(Disconnect), from);
                }
                else if (from.AccessLevel >= AccessLevel.Administrator)
                {
                    notice = "The server is currently under lockdown. As you are an administrator, you may change this from the [Admin gump.";
                }
                else
                {
                    notice = "The server is currently under lockdown. You have sufficient access level to connect.";
                }

                from.SendGump(new NoticeGump(1060637, 30720, notice, 0xFFC000, 300, 140, null, null));
                return;
            }

            if (from is PlayerMobile)
            {
                PlayerMobile pm = (PlayerMobile)from;

                pm.MiniHealStrength = 11;

                Timer mht = new MiniHealTimer(pm);
                mht.Start();

                pm.ClaimAutoStabledPets();

                pm.CheckIfNoLongerBountyHunter();

                WorldWarsRegion wwRegion = WorldWarsRegion.Find(pm.Location, Map.Felucca) as WorldWarsRegion;

                if (wwRegion != null)
                {
                    //players dont belong on vorshun on login!
                    if (WorldWarsController.Instance != null && !WorldWarsController.Instance.Active)
                    {
                        pm.MoveToWorld(WorldWarsController.Instance.GetRemoveToLocation(from), Map.Felucca);
                    }
                    else if (WorldWarsController.Instance == null)
                    {
                        pm.MoveToWorld(new Point3D(1354, 1382, 0), Map.Felucca); //back to galven
                    }
                }

                if (pm.HasBadName)
                    UniqueNameChecker.CheckName(pm, pm.Name);

                ResponseTimerRegistry.RemoveTimer(e.Mobile.Serial);

                //Database variables
                pm.MurdersThisSession = 0;
                pm.MurdererKillsThisSession = 0;
                pm.MilitiaKillsThisSession = 0;
                pm.MilitiaDeathsThisSession = 0;
                pm.KillsThisSession = 0;
                pm.DeathsThisSession = 0;
                pm.OreMinedThisSession = 0;
                pm.FishCaughtThisSession = 0;
                pm.WoodHarvestedThisSession = 0;
                pm.ItemsCraftedThisSession = 0;
                pm.ChestsPickedThisSession = 0;
                pm.mMapsDecodedThisSession = 0;
                pm.SheepShornThisSession = 0;
                pm.GuardWhacksThisSession = 0;
                pm.StealAttemptsThisSession = 0;
                pm.RecallsThisSession = 0;
                pm.ResurrectionsThisSession = 0;
                pm.HeroesCapturedThisSession = 0;
                pm.HeroesRescuedThisSession = 0;
                pm.WorldWarsFlagsCapturedThisSession = 0;
                pm.BossesKilledThisSession = 0;
                pm.PlayersKilledThisSession = 0;
                pm.CorpsesCarvedThisSession = 0;
                pm.HeadsTurnedInThisSession = 0;
                pm.SilverEarnedThisSession = 0;
                pm.ItemsImbuedThisSession = 0;
                pm.StepsWalkedThisSession = 0;
                pm.StealthStepsWalkedThisSession = 0;
                pm.GuildKillsThisSession = 0;
                pm.GuildDeathsThisSession = 0;
                pm.PotionsConsumedThisSession = 0;
                pm.GoldSpentThisSession = 0;
                pm.CampfiresStartedThisSession = 0;
                pm.TimesCriminalThisSession = 0;
                pm.CorpsesInspectedThisSession = 0;
                pm.PlayersAttackedThisSession = 0;
                pm.EscortsTakenThisSession = 0;
                pm.AnimalsTamedThisSession = 0;
                pm.ProjectilesFiredThisSession = 0;
                pm.BandagesUsedThisSession = 0;
                pm.PoisonsAppliedThisSession = 0;
                pm.PoisonsCastedThisSession = 0;
                pm.CottonPickedThisSession = 0;
                pm.HidesSkinnedThisSession = 0;
                pm.FeathersPluckedThisSession = 0;
                pm.SuccessfulStealsThisSession = 0;
                //DatabaseController.UpdateCharacterOnLogin(pm);
                //pm.DBUpdateTimer = new DatabaseUpdateTimer(pm);
                //pm.DBUpdateTimer.Start();
                if (pm.OnWaterTile())
                    pm.ReturnToGalven();
            }
        }
        public bool OnWaterTile()
        {
            LandTile landTile = Map.Tiles.GetLandTile(Location.X, Location.Y);
            if ((landTile.ID >= 0xA8 && landTile.ID <= 0xAB) || (landTile.ID >= 0x136 && landTile.ID <= 0x137))
                return true;
            else
                return false;
        }
        public override void Attack(Mobile m)
        {
            PlayerMobile playerTarget = m as PlayerMobile;
            if (playerTarget != null)
                mPlayersAttackedThisSession++;

            base.Attack(m);
        }

        private class MiniHealTimer : Timer
        {
            private static TimeSpan delay = TimeSpan.FromSeconds(7.0);
            private PlayerMobile player;

            public MiniHealTimer(PlayerMobile pm)
                : base(TimeSpan.Zero, delay)
            {
                player = pm;
            }

            protected override void OnTick()
            {
                if (player.MiniHealStrength < 11)
                    player.MiniHealStrength += 2;
            }
        }

        private void CheckIfNoLongerBountyHunter()
        {
            if (m_BountyHunter && (m_QuittingBountyHuntingAt < DateTime.Now))
            {
                this.SendMessage(FeatureList.BountyHunters.InformResignationText);

                this.BountyHunter = false;
                m_QuittingBountyHuntingAt = DateTime.MaxValue;
            }
        }

        private bool m_NoDeltaRecursion;

        public void ValidateEquipment()
        {
            if (m_NoDeltaRecursion || Map == null || Map == Map.Internal)
                return;

            if (this.Items == null)
                return;

            m_NoDeltaRecursion = true;
            Timer.DelayCall(TimeSpan.Zero, new TimerCallback(ValidateEquipment_Sandbox));
        }

        private void ValidateEquipment_Sandbox()
        {
            try
            {
                if (Map == null || Map == Map.Internal)
                    return;

                List<Item> items = this.Items;

                if (items == null)
                    return;

                bool moved = false;

                int str = this.Str;
                int dex = this.Dex;
                int intel = this.Int;

                #region Factions
                int factionItemCount = 0;
                #endregion

                Mobile from = this;

                #region Ethics
                Ethics.Ethic ethic = Ethics.Ethic.Find(from);
                #endregion

                for (int i = items.Count - 1; i >= 0; --i)
                {
                    if (i >= items.Count)
                        continue;

                    Item item = items[i];

                    #region Ethics
                    if ((item.SavedFlags & 0x100) != 0)
                    {
                        if (item.Hue != Ethics.Ethic.Hero.Definition.PrimaryHue)
                        {
                            item.SavedFlags &= ~0x100;
                        }
                        else if (ethic != Ethics.Ethic.Hero)
                        {
                            from.AddToBackpack(item);
                            moved = true;
                            continue;
                        }
                    }
                    else if ((item.SavedFlags & 0x200) != 0)
                    {
                        if (item.Hue != Ethics.Ethic.Evil.Definition.PrimaryHue)
                        {
                            item.SavedFlags &= ~0x200;
                        }
                        else if (ethic != Ethics.Ethic.Evil)
                        {
                            from.AddToBackpack(item);
                            moved = true;
                            continue;
                        }
                    }
                    #endregion

                    if (item is BaseWeapon)
                    {
                        BaseWeapon weapon = (BaseWeapon)item;

                        bool drop = false;

                        if (dex < weapon.DexRequirement)
                            drop = true;
                        else if (str < AOS.Scale(weapon.StrRequirement, 100 - weapon.GetLowerStatReq()))
                            drop = true;
                        else if (intel < weapon.IntRequirement)
                            drop = true;
                        else if (weapon.RequiredRace != null && weapon.RequiredRace != this.Race)
                            drop = true;

                        if (drop)
                        {
                            string name = weapon.Name;

                            if (name == null)
                                name = String.Format("#{0}", weapon.LabelNumber);

                            from.SendLocalizedMessage(1062001, name); // You can no longer wield your ~1_WEAPON~
                            from.AddToBackpack(weapon);
                            moved = true;
                        }
                    }

                    else if (item is BaseArmor)
                    {
                        BaseArmor armor = (BaseArmor)item;

                        bool drop = false;

                        if (!armor.AllowMaleWearer && !from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (!armor.AllowFemaleWearer && from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (armor.RequiredRace != null && armor.RequiredRace != this.Race)
                        {
                            drop = true;
                        }
                        else
                        {
                            int strBonus = armor.ComputeStatBonus(StatType.Str), strReq = armor.ComputeStatReq(StatType.Str);
                            int dexBonus = armor.ComputeStatBonus(StatType.Dex), dexReq = armor.ComputeStatReq(StatType.Dex);
                            int intBonus = armor.ComputeStatBonus(StatType.Int), intReq = armor.ComputeStatReq(StatType.Int);

                            if (dex < dexReq || (dex + dexBonus) < 1)
                                drop = true;
                            else if (str < strReq || (str + strBonus) < 1)
                                drop = true;
                            else if (intel < intReq || (intel + intBonus) < 1)
                                drop = true;
                        }

                        if (drop)
                        {
                            string name = armor.Name;

                            if (name == null)
                                name = String.Format("#{0}", armor.LabelNumber);

                            if (armor is BaseShield)
                                from.SendLocalizedMessage(1062003, name); // You can no longer equip your ~1_SHIELD~
                            else
                                from.SendLocalizedMessage(1062002, name); // You can no longer wear your ~1_ARMOR~

                            from.AddToBackpack(armor);
                            moved = true;
                        }
                    }
                    else if (item is BaseClothing)
                    {
                        BaseClothing clothing = (BaseClothing)item;

                        bool drop = false;

                        if (!clothing.AllowMaleWearer && !from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (!clothing.AllowFemaleWearer && from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (clothing.RequiredRace != null && clothing.RequiredRace != this.Race)
                        {
                            drop = true;
                        }
                        else
                        {
                            int strBonus = clothing.ComputeStatBonus(StatType.Str);
                            int strReq = clothing.ComputeStatReq(StatType.Str);

                            if (str < strReq || (str + strBonus) < 1)
                                drop = true;
                        }

                        if (drop)
                        {
                            string name = clothing.Name;

                            if (name == null)
                                name = String.Format("#{0}", clothing.LabelNumber);

                            from.SendLocalizedMessage(1062002, name); // You can no longer wear your ~1_ARMOR~

                            from.AddToBackpack(clothing);
                            moved = true;
                        }
                    }

                    FactionItem factionItem = FactionItem.Find(item);

                    if (factionItem != null)
                    {
                        bool drop = false;

                        Faction ourFaction = Faction.Find(this);

                        if (ourFaction == null || ourFaction != factionItem.Faction)
                            drop = true;
                        else if (++factionItemCount > FactionItem.GetMaxWearables(this))
                            drop = true;

                        if (drop)
                        {
                            from.AddToBackpack(item);
                            moved = true;
                        }
                    }
                }

                if (moved)
                    from.SendLocalizedMessage(500647); // Some equipment has been moved to your backpack.
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                m_NoDeltaRecursion = false;
            }
        }

        public override void Delta(MobileDelta flag)
        {
            base.Delta(flag);

            if ((flag & MobileDelta.Stat) != 0)
                ValidateEquipment();

            if ((flag & (MobileDelta.Name | MobileDelta.Hue)) != 0)
                InvalidateMyRunUO();

            if ((flag & MobileDelta.Noto) != 0)
                if (Criminal)
                    mTimesCriminalThisSession++;
        }

        private static void Disconnect(object state)
        {
            NetState ns = ((Mobile)state).NetState;

            if (ns != null)
                ns.Dispose();
        }

        private static void OnLogout(LogoutEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;
            if (pm != null)
            {
                pm.AutoStablePets();
                //DatabaseController.UpdateCharacterLogout(e.Mobile);
                if (pm.DBUpdateTimer != null)
                {
                    pm.DBUpdateTimer.Stop();
                    pm.DBUpdateTimer = null;
                }

            }
        }

        private static void EventSink_Connected(ConnectedEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;

            if (pm != null)
            {
                pm.m_SessionStart = DateTime.Now;

                if (pm.m_Quest != null)
                    pm.m_Quest.StartTimer();

                pm.BedrollLogout = false;
                pm.LastOnline = DateTime.Now;
            }

            DisguiseTimers.StartTimer(e.Mobile);

            Timer.DelayCall(TimeSpan.Zero, new TimerStateCallback(ClearSpecialMovesCallback), e.Mobile);
        }

        private static void ClearSpecialMovesCallback(object state)
        {
            Mobile from = (Mobile)state;

            SpecialMove.ClearAllMoves(from);
        }

        private static void EventSink_Disconnected(DisconnectedEventArgs e)
        {
            Mobile from = e.Mobile;
            DesignContext context = DesignContext.Find(from);

            if (context != null)
            {
                /* Client disconnected
                 *  - Remove design context
                 *  - Eject all from house
                 *  - Restore relocated entities
                 */

                // Remove design context
                DesignContext.Remove(from);

                // Eject all from house
                from.RevealingAction();

                foreach (Item item in context.Foundation.GetItems())
                    item.Location = context.Foundation.BanLocation;

                foreach (Mobile mobile in context.Foundation.GetMobiles())
                    mobile.Location = context.Foundation.BanLocation;

                // Restore relocated entities
                context.Foundation.RestoreRelocatedEntities();
            }

            PlayerMobile pm = e.Mobile as PlayerMobile;

            if (pm != null)
            {
                pm.m_GameTime += (DateTime.Now - pm.m_SessionStart);

                if (pm.m_Quest != null)
                    pm.m_Quest.StopTimer();

                pm.m_SpeechLog = null;
                pm.LastOnline = DateTime.Now;
            }

            DisguiseTimers.StopTimer(from);
        }

        public override void RevealingAction()
        {
            if (m_DesignContext != null)
                return;

            Spells.Sixth.InvisibilitySpell.RemoveTimer(this);

            base.RevealingAction();
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override bool Hidden
        {
            get
            {
                return base.Hidden;
            }
            set
            {
                base.Hidden = value;

                RemoveBuff(BuffIcon.Invisibility);	//Always remove, default to the hiding icon EXCEPT in the invis spell where it's explicitly set

                if (!Hidden)
                {
                    RemoveBuff(BuffIcon.HidingAndOrStealth);
                }

                else// if( !InvisibilitySpell.HasTimer( this ) )
                {
                    BuffInfo.AddBuff(this, new BuffInfo(BuffIcon.HidingAndOrStealth, 1075655));	//Hidden/Stealthing & You Are Hidden
                }
            }
        }

        public override void OnSubItemAdded(Item item)
        {
            if (AccessLevel < AccessLevel.GameMaster && item.IsChildOf(this.Backpack))
            {
                int maxWeight = WeightOverloading.GetMaxWeight(this);
                int curWeight = Mobile.BodyWeight + this.TotalWeight;

                if (curWeight > maxWeight)
                    this.SendLocalizedMessage(1019035, true, String.Format(" : {0} / {1}", curWeight, maxWeight));
            }
        }

        public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness)
        {
            if (m_DesignContext != null || (target is PlayerMobile && ((PlayerMobile)target).m_DesignContext != null))
                return false;

            // Prevent players from farming their own Champions or Champion spawns
            Faction playerFaction = Faction.Find(this, true);
            string cannotAttackMsg = "You cannot attack a member of your own champion spawn!";

            Type[] forsakenTypes = new Type[] { typeof(ForsakenMinion), typeof(ForsakenDK), typeof(ForsakenNecromancer), typeof(Chandrian), typeof(Haliax) };
            Type[] urukhaiTypes = new Type[] { typeof(UrukGrunt), typeof(UrukRaider), typeof(UrukShaman), typeof(UrukScout), typeof(Gojira) };
            Type[] paladinTypes = new Type[] { typeof(POSquire), typeof(POKnight), typeof(POPaladin), typeof(Amyr), typeof(DupreThePaladin) };
            
            if ((playerFaction == Forsaken.Instance || this.Citizenship == "Yew") && Array.Exists(forsakenTypes, element => element == target.GetType())) // forsaken , yew
            {
                SendMessage(cannotAttackMsg);
                return false;
            }
            else if ((playerFaction == Uruk.Instance || this.Citizenship == "Blackrock") && Array.Exists(urukhaiTypes, element => element == target.GetType())) // urukhai , blackrock
            {
                SendMessage(cannotAttackMsg);
                return false;
            }
            else if ((playerFaction == Paladins.Instance || this.Citizenship == "Trinsic") && Array.Exists(paladinTypes, element => element == target.GetType())) // palading , trinsic
            {
                SendMessage(cannotAttackMsg);
                return false;
            }


            if ((target is BaseVendor && ((BaseVendor)target).IsInvulnerable) || target is PlayerVendor || target is TownCrier || target is TownHero)
            {
                if (message)
                {
                    if (target.Title == null)
                        SendMessage("{0} the vendor cannot be harmed.", target.Name);
                    else
                        SendMessage("{0} {1} cannot be harmed.", target.Name, target.Title);
                }

                return false;
            }

            return base.CanBeHarmful(target, message, ignoreOurBlessedness);
        }

        public override bool CanBeBeneficial(Mobile target, bool message, bool allowDead)
        {
            if (m_DesignContext != null || (target is PlayerMobile && ((PlayerMobile)target).m_DesignContext != null))
                return false;

            return base.CanBeBeneficial(target, message, allowDead);
        }

        public override bool CheckContextMenuDisplay(IEntity target)
        {
            return (m_DesignContext == null);
        }

        public override void OnItemAdded(Item item)
        {
            base.OnItemAdded(item);

            if (item is BaseArmor || item is BaseWeapon)
            {
                Hits = Hits; Stam = Stam; Mana = Mana;
            }

            if (this.NetState != null)
                CheckLightLevels(false);

            InvalidateMyRunUO();
        }

        public override void OnItemRemoved(Item item)
        {
            base.OnItemRemoved(item);

            if (item is BaseArmor || item is BaseWeapon)
            {
                Hits = Hits; Stam = Stam; Mana = Mana;
            }

            if (this.NetState != null)
                CheckLightLevels(false);

            InvalidateMyRunUO();
        }

        public override void CalculateEquippedArmor()
        {
            double rating = 0.0;
            // This, I believe, is the source of the polymorph => 0 AR bug
            //if (this.Body.IsHuman)
            //{
            AddArmorRating(ref rating, NeckArmor);
            AddArmorRating(ref rating, HandArmor);
            AddArmorRating(ref rating, HeadArmor);
            AddArmorRating(ref rating, ArmsArmor);
            AddArmorRating(ref rating, LegsArmor);
            AddArmorRating(ref rating, ChestArmor);

            BaseArmor shield = ShieldArmor as BaseArmor;

            if (shield != null)
            {
                double arShield = FeatureList.ArmorChanges.ShieldARWithoutParry * shield.ArmorRating;
                double arShieldSkill = FeatureList.ArmorChanges.ShieldARParryBonus * (Skills.Parry.Value / 100) * shield.ArmorRating;

                rating += arShield;
                rating += arShieldSkill;
            }
            //}

            EquipmentArmor = rating;
            Delta(MobileDelta.Armor);
        }

        private void AddArmorRating(ref double rating, Item armor)
        {
            BaseArmor ar = armor as BaseArmor;

            if (ar != null && (ar.ArmorAttributes.MageArmor == 0))
            {
                rating += ar.ArmorRating;
            }
        }

        #region [Stats]Max
        [CommandProperty(AccessLevel.GameMaster)]
        public override int HitsMax
        {
            get
            {
                int strOffs = GetStatOffset(StatType.Str);
                return this.RawStr + strOffs;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int StamMax
        {
            get { return base.StamMax + AosAttributes.GetValue(this, AosAttribute.BonusStam); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int ManaMax
        {
            get { return base.ManaMax + AosAttributes.GetValue(this, AosAttribute.BonusMana) + ((Core.ML && Race == Race.Elf) ? 20 : 0); }
        }
        #endregion

        #region Stat Getters/Setters

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Str
        {
            get
            {
                if (Core.ML && this.AccessLevel == AccessLevel.Player)
                    return Math.Min(base.Str, 150);

                return base.Str;
            }
            set
            {
                base.Str = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Int
        {
            get
            {
                if (Core.ML && this.AccessLevel == AccessLevel.Player)
                    return Math.Min(base.Int, 150);

                return base.Int;
            }
            set
            {
                base.Int = value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Dex
        {
            get
            {
                if (Core.ML && this.AccessLevel == AccessLevel.Player)
                    return Math.Min(base.Dex, 150);

                return base.Dex;
            }
            set
            {
                base.Dex = value;
            }
        }

        #endregion

        public override bool Move(Direction d)
        {
            NetState ns = this.NetState;

            if (ns != null)
            {
                if (HasGump(typeof(ResurrectGump)))
                {
                    if (Alive)
                    {
                        CloseGump(typeof(ResurrectGump));
                    }
                    else
                    {
                        SendLocalizedMessage(500111); // You are frozen and cannot move.
                        return false;
                    }
                }
            }

            TimeSpan speed = ComputeMovementSpeed(d);

            bool res;

            if (!Alive)
                Server.Movement.MovementImpl.IgnoreMovableImpassables = true;

            res = base.Move(d);

            Server.Movement.MovementImpl.IgnoreMovableImpassables = false;

            if (!res)
                return false;

            m_NextMovementTime += speed;

            return true;
        }

        public override bool CheckMovement(Direction d, out int newZ)
        {
            DesignContext context = m_DesignContext;

            if (context == null)
                return base.CheckMovement(d, out newZ);

            HouseFoundation foundation = context.Foundation;

            newZ = foundation.Z + HouseFoundation.GetLevelZ(context.Level, context.Foundation);

            int newX = this.X, newY = this.Y;
            Movement.Movement.Offset(d, ref newX, ref newY);

            int startX = foundation.X + foundation.Components.Min.X + 1;
            int startY = foundation.Y + foundation.Components.Min.Y + 1;
            int endX = startX + foundation.Components.Width - 1;
            int endY = startY + foundation.Components.Height - 2;

            return (newX >= startX && newY >= startY && newX < endX && newY < endY && Map == foundation.Map);
        }

        public override bool AllowItemUse(Item item)
        {
            return DesignContext.Check(this);
        }

        public SkillName[] AnimalFormRestrictedSkills { get { return m_AnimalFormRestrictedSkills; } }

        private SkillName[] m_AnimalFormRestrictedSkills = new SkillName[]
        {
            SkillName.ArmsLore, SkillName.Begging, SkillName.Discordance, SkillName.Forensics,
            SkillName.Inscribe, SkillName.ItemID, SkillName.Meditation, SkillName.Peacemaking,
            SkillName.Provocation, SkillName.RemoveTrap, SkillName.SpiritSpeak, SkillName.Stealing,
            SkillName.TasteID
        };

        public override bool AllowSkillUse(SkillName skill)
        {
            return DesignContext.Check(this);
        }

        private bool m_LastProtectedMessage;
        private int m_NextProtectionCheck = 10;

        public virtual void RecheckTownProtection()
        {
            m_NextProtectionCheck = 10;

            Regions.GuardedRegion reg = (Regions.GuardedRegion)this.Region.GetRegion(typeof(Regions.GuardedRegion));
            bool isProtected = (reg != null && !reg.IsDisabled());

            if (isProtected != m_LastProtectedMessage)
            {
                if (isProtected)
                    SendLocalizedMessage(500112); // You are now under the protection of the town guards.
                else
                    SendLocalizedMessage(500113); // You have left the protection of the town guards.

                m_LastProtectedMessage = isProtected;
            }
        }

        public override void MoveToWorld(Point3D loc, Map map)
        {
            base.MoveToWorld(loc, map);

            RecheckTownProtection();
        }

        public override void SetLocation(Point3D loc, bool isTeleport)
        {
            if (!isTeleport && AccessLevel == AccessLevel.Player)
            {
                // moving, not teleporting
                int zDrop = (this.Location.Z - loc.Z);

                if (zDrop > 20) // we fell more than one story
                    Hits -= ((zDrop / 20) * 10) - 5; // deal some damage; does not kill, disrupt, etc
            }

            base.SetLocation(loc, isTeleport);

            if (isTeleport || --m_NextProtectionCheck == 0)
                RecheckTownProtection();
        }

        public override void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, list);

            if (from == this)
            {
                if (m_Quest != null)
                    m_Quest.GetContextMenuEntries(list);

                if (Alive && InsuranceEnabled)
                {
                    list.Add(new CallbackEntry(6201, new ContextCallback(ToggleItemInsurance)));

                    if (AutoRenewInsurance)
                        list.Add(new CallbackEntry(6202, new ContextCallback(CancelRenewInventoryInsurance)));
                    else
                        list.Add(new CallbackEntry(6200, new ContextCallback(AutoRenewInventoryInsurance)));
                }

                BaseHouse house = BaseHouse.FindHouseAt(this);

                if (house != null)
                {
                    if (Alive && house.InternalizedVendors.Count > 0 && house.IsOwner(this))
                        list.Add(new CallbackEntry(6204, new ContextCallback(GetVendor)));

                    if (house.IsAosRules)
                        list.Add(new CallbackEntry(6207, new ContextCallback(LeaveHouse)));
                }

                //if( Alive )
                //list.Add( new CallbackEntry( 6210, new ContextCallback( ToggleChampionTitleDisplay ) ) );
            }
            if (from != this)
            {
                if (Alive && Core.Expansion >= Expansion.AOS)
                {
                    Party theirParty = from.Party as Party;
                    Party ourParty = this.Party as Party;

                    if (theirParty == null && ourParty == null)
                    {
                        list.Add(new AddToPartyEntry(from, this));
                    }
                    else if (theirParty != null && theirParty.Leader == from)
                    {
                        if (ourParty == null)
                        {
                            list.Add(new AddToPartyEntry(from, this));
                        }
                        else if (ourParty == theirParty)
                        {
                            list.Add(new RemoveFromPartyEntry(from, this));
                        }
                    }
                }

                BaseHouse curhouse = BaseHouse.FindHouseAt(this);

                if (curhouse != null)
                {
                    if (Alive && Core.Expansion >= Expansion.AOS && curhouse.IsAosRules && curhouse.IsFriend(from))
                        list.Add(new EjectPlayerEntry(from, this));
                }
            }
        }

        #region Insurance

        private void ToggleItemInsurance()
        {
            if (!CheckAlive())
                return;

            BeginTarget(-1, false, TargetFlags.None, new TargetCallback(ToggleItemInsurance_Callback));
            SendLocalizedMessage(1060868); // Target the item you wish to toggle insurance status on <ESC> to cancel
        }

        private bool CanInsure(Item item)
        {
            if (((item is Container) && !(item is BaseQuiver)) || item is BagOfSending || item is KeyRing)
                return false;

            if ((item is Spellbook && item.LootType == LootType.Blessed) || item is Runebook || item is PotionKeg)
                return false;

            if (item.Stackable)
                return false;

            if (item.LootType == LootType.Cursed)
                return false;

            if (item.ItemID == 0x204E) // death shroud
                return false;

            return true;
        }

        private void ToggleItemInsurance_Callback(Mobile from, object obj)
        {
            if (!CheckAlive())
                return;

            Item item = obj as Item;

            if (item == null || !item.IsChildOf(this))
            {
                BeginTarget(-1, false, TargetFlags.None, new TargetCallback(ToggleItemInsurance_Callback));
                SendLocalizedMessage(1060871, "", 0x23); // You can only insure items that you have equipped or that are in your backpack
            }
            else if (item.Insured)
            {
                item.Insured = false;

                SendLocalizedMessage(1060874, "", 0x35); // You cancel the insurance on the item

                BeginTarget(-1, false, TargetFlags.None, new TargetCallback(ToggleItemInsurance_Callback));
                SendLocalizedMessage(1060868, "", 0x23); // Target the item you wish to toggle insurance status on <ESC> to cancel
            }
            else if (!CanInsure(item))
            {
                BeginTarget(-1, false, TargetFlags.None, new TargetCallback(ToggleItemInsurance_Callback));
                SendLocalizedMessage(1060869, "", 0x23); // You cannot insure that
            }
            else if (item.LootType == LootType.Blessed || item.LootType == LootType.Newbied || item.BlessedFor == from)
            {
                BeginTarget(-1, false, TargetFlags.None, new TargetCallback(ToggleItemInsurance_Callback));
                SendLocalizedMessage(1060870, "", 0x23); // That item is blessed and does not need to be insured
                SendLocalizedMessage(1060869, "", 0x23); // You cannot insure that
            }
            else
            {
                if (!item.PayedInsurance)
                {
                    if (Banker.Withdraw(from, 600))
                    {
                        SendLocalizedMessage(1060398, "600"); // ~1_AMOUNT~ gold has been withdrawn from your bank box.
                        item.PayedInsurance = true;
                    }
                    else
                    {
                        SendLocalizedMessage(1061079, "", 0x23); // You lack the funds to purchase the insurance
                        return;
                    }
                }

                item.Insured = true;

                SendLocalizedMessage(1060873, "", 0x23); // You have insured the item

                BeginTarget(-1, false, TargetFlags.None, new TargetCallback(ToggleItemInsurance_Callback));
                SendLocalizedMessage(1060868, "", 0x23); // Target the item you wish to toggle insurance status on <ESC> to cancel
            }
        }

        private void AutoRenewInventoryInsurance()
        {
            if (!CheckAlive())
                return;

            SendLocalizedMessage(1060881, "", 0x23); // You have selected to automatically reinsure all insured items upon death
            AutoRenewInsurance = true;
        }

        private void CancelRenewInventoryInsurance()
        {
            if (!CheckAlive())
                return;

            if (Core.SE)
            {
                if (!HasGump(typeof(CancelRenewInventoryInsuranceGump)))
                    SendGump(new CancelRenewInventoryInsuranceGump(this));
            }
            else
            {
                SendLocalizedMessage(1061075, "", 0x23); // You have cancelled automatically reinsuring all insured items upon death
                AutoRenewInsurance = false;
            }
        }

        private class CancelRenewInventoryInsuranceGump : Gump
        {
            private PlayerMobile m_Player;

            public CancelRenewInventoryInsuranceGump(PlayerMobile player)
                : base(250, 200)
            {
                m_Player = player;

                AddBackground(0, 0, 240, 142, 0x13BE);
                AddImageTiled(6, 6, 228, 100, 0xA40);
                AddImageTiled(6, 116, 228, 20, 0xA40);
                AddAlphaRegion(6, 6, 228, 142);

                AddHtmlLocalized(8, 8, 228, 100, 1071021, 0x7FFF, false, false); // You are about to disable inventory insurance auto-renewal.

                AddButton(6, 116, 0xFB1, 0xFB2, 0, GumpButtonType.Reply, 0);
                AddHtmlLocalized(40, 118, 450, 20, 1060051, 0x7FFF, false, false); // CANCEL

                AddButton(114, 116, 0xFA5, 0xFA7, 1, GumpButtonType.Reply, 0);
                AddHtmlLocalized(148, 118, 450, 20, 1071022, 0x7FFF, false, false); // DISABLE IT!
            }

            public override void OnResponse(NetState sender, RelayInfo info)
            {
                if (!m_Player.CheckAlive())
                    return;

                if (info.ButtonID == 1)
                {
                    m_Player.SendLocalizedMessage(1061075, "", 0x23); // You have cancelled automatically reinsuring all insured items upon death
                    m_Player.AutoRenewInsurance = false;
                }
                else
                {
                    m_Player.SendLocalizedMessage(1042021); // Cancelled.
                }
            }
        }

        #endregion

        private void GetVendor()
        {
            BaseHouse house = BaseHouse.FindHouseAt(this);

            if (CheckAlive() && house != null && house.IsOwner(this) && house.InternalizedVendors.Count > 0)
            {
                CloseGump(typeof(ReclaimVendorGump));
                SendGump(new ReclaimVendorGump(house));
            }
        }

        private void LeaveHouse()
        {
            BaseHouse house = BaseHouse.FindHouseAt(this);

            if (house != null)
                this.Location = house.BanLocation;
        }

        private delegate void ContextCallback();

        private class CallbackEntry : ContextMenuEntry
        {
            private ContextCallback m_Callback;

            public CallbackEntry(int number, ContextCallback callback)
                : this(number, -1, callback)
            {
            }

            public CallbackEntry(int number, int range, ContextCallback callback)
                : base(number, range)
            {
                m_Callback = callback;
            }

            public override void OnClick()
            {
                if (m_Callback != null)
                    m_Callback();
            }
        }

        public override void DisruptiveAction()
        {
            if (Meditating)
            {
                RemoveBuff(BuffIcon.ActiveMeditation);
            }

            base.DisruptiveAction();
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (this == from && !Warmode)
            {
                IMount mount = Mount;

                if (mount != null && !DesignContext.Check(this))
                    return;
            }

            base.OnDoubleClick(from);
        }

        public override void DisplayPaperdollTo(Mobile to)
        {
            if (DesignContext.Check(this))
                base.DisplayPaperdollTo(to);
        }

        private static bool m_NoRecursion;

        public override bool CheckEquip(Item item)
        {
            if (!base.CheckEquip(item))
                return false;

            #region Factions
            FactionItem factionItem = FactionItem.Find(item);

            if (factionItem != null)
            {
                Faction faction = Faction.Find(this);

                if (faction == null)
                {
                    SendLocalizedMessage(1010371); // You cannot equip a faction item!
                    return false;
                }
                else if (faction != factionItem.Faction)
                {
                    SendLocalizedMessage(1010372); // You cannot equip an opposing faction's item!
                    return false;
                }
                else
                {
                    int maxWearables = FactionItem.GetMaxWearables(this);

                    for (int i = 0; i < Items.Count; ++i)
                    {
                        Item equiped = Items[i];

                        if (item != equiped && FactionItem.Find(equiped) != null)
                        {
                            if (--maxWearables == 0)
                            {
                                SendLocalizedMessage(1010373); // You do not have enough rank to equip more faction items!
                                return false;
                            }
                        }
                    }
                }
            }
            #endregion

            if (this.AccessLevel < AccessLevel.GameMaster && item.Layer != Layer.Mount && this.HasTrade)
            {
                BounceInfo bounce = item.GetBounce();

                if (bounce != null)
                {
                    if (bounce.m_Parent is Item)
                    {
                        Item parent = (Item)bounce.m_Parent;

                        if (parent == this.Backpack || parent.IsChildOf(this.Backpack))
                            return true;
                    }
                    else if (bounce.m_Parent == this)
                    {
                        return true;
                    }
                }

                SendLocalizedMessage(1004042); // You can only equip what you are already carrying while you have a trade pending.
                return false;
            }

            return true;
        }

        public override bool CheckTrade(Mobile to, Item item, SecureTradeContainer cont, bool message, bool checkItems, int plusItems, int plusWeight)
        {
            int msgNum = 0;

            if (cont == null)
            {
                //Cant Start Trade If You Are Hidden
                if (this.Hidden)
                {
                    this.SendMessage("You cannot initiate a trade with someone while hidden.");

                    return false;
                }

                //Shouldnt Be Possible, But Can't Start Trade With a Hidden Target
                if (to.Hidden)
                {
                    this.SendMessage("You cannot initiate a trade with someone that is hidden.");

                    return false;
                }

                if (to.Holding != null)
                    msgNum = 1062727; // You cannot trade with someone who is dragging something.
                else if (this.HasTrade)
                    msgNum = 1062781; // You are already trading with someone else!
                else if (to.HasTrade)
                    msgNum = 1062779; // That person is already involved in a trade
            }

            if (msgNum == 0)
            {
                if (cont != null)
                {
                    plusItems += cont.TotalItems;
                    plusWeight += cont.TotalWeight;
                }

                if (this.Backpack == null || !this.Backpack.CheckHold(this, item, false, checkItems, plusItems, plusWeight))
                    msgNum = 1004040; // You would not be able to hold this if the trade failed.
                else if (to.Backpack == null || !to.Backpack.CheckHold(to, item, false, checkItems, plusItems, plusWeight))
                    msgNum = 1004039; // The recipient of this trade would not be able to carry this.
                else
                    msgNum = CheckContentForTrade(item);
            }

            if (msgNum != 0)
            {
                if (message)
                    this.SendLocalizedMessage(msgNum);

                return false;
            }

            return true;
        }

        private static int CheckContentForTrade(Item item)
        {
            if (item is TrapableContainer && ((TrapableContainer)item).TrapType != TrapType.None)
                return 1004044; // You may not trade trapped items.

            if (SkillHandlers.StolenItem.IsStolen(item))
                return 1004043; // You may not trade recently stolen items.

            if (item is Container)
            {
                foreach (Item subItem in item.Items)
                {
                    int msg = CheckContentForTrade(subItem);

                    if (msg != 0)
                        return msg;
                }
            }

            return 0;
        }

        public override bool CheckNonlocalDrop(Mobile from, Item item, Item target)
        {
            if (!base.CheckNonlocalDrop(from, item, target))
                return false;

            if (from.AccessLevel >= AccessLevel.GameMaster)
                return true;

            Container pack = this.Backpack;
            if (from == this && this.HasTrade && (target == pack || target.IsChildOf(pack)))
            {
                BounceInfo bounce = item.GetBounce();

                if (bounce != null && bounce.m_Parent is Item)
                {
                    Item parent = (Item)bounce.m_Parent;

                    if (parent == pack || parent.IsChildOf(pack))
                        return true;
                }

                SendLocalizedMessage(1004041); // You can't do that while you have a trade pending.
                return false;
            }

            return true;
        }

        protected override void OnLocationChange(Point3D oldLocation)
        {
            CheckLightLevels(false);

            DesignContext context = m_DesignContext;

            if (context == null || m_NoRecursion)
                return;

            m_NoRecursion = true;

            HouseFoundation foundation = context.Foundation;

            int newX = this.X, newY = this.Y;
            int newZ = foundation.Z + HouseFoundation.GetLevelZ(context.Level, context.Foundation);

            int startX = foundation.X + foundation.Components.Min.X + 1;
            int startY = foundation.Y + foundation.Components.Min.Y + 1;
            int endX = startX + foundation.Components.Width - 1;
            int endY = startY + foundation.Components.Height - 2;

            if (newX >= startX && newY >= startY && newX < endX && newY < endY && Map == foundation.Map)
            {
                if (Z != newZ)
                    Location = new Point3D(X, Y, newZ);

                m_NoRecursion = false;
                return;
            }

            Location = new Point3D(foundation.X, foundation.Y, newZ);
            Map = foundation.Map;

            m_NoRecursion = false;
        }

        protected override void OnMapChange(Map oldMap)
        {
            if ((Map != Faction.Facet && oldMap == Faction.Facet) || (Map == Faction.Facet && oldMap != Faction.Facet))
                InvalidateProperties();

            DesignContext context = m_DesignContext;

            if (context == null || m_NoRecursion)
                return;

            m_NoRecursion = true;

            HouseFoundation foundation = context.Foundation;

            if (Map != foundation.Map)
                Map = foundation.Map;

            m_NoRecursion = false;
        }

        public override void OnBeneficialAction(Mobile target, bool isCriminal)
        {
            Faction targetFaction = Faction.Find(target, true);

            if (this.CitizenshipPlayerState != null && targetFaction != null)
            {
                if (CitizenshipPlayerState.Commonwealth == targetFaction.OwningCommonwealth)
                {
                    this.AssistedOwnMilitia = true;
                    Delta(MobileDelta.Noto);
                    InvalidateProperties();
                }
            }

            base.OnBeneficialAction(target, isCriminal);
        }

        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            int disruptThreshold;
            LastDamageAmount = amount;
            if (!Core.AOS)
                disruptThreshold = 0;
            else if (from != null && from.Player)
                disruptThreshold = 18;
            else
                disruptThreshold = 25;

            if (amount > disruptThreshold)
            {
                BandageContext c = BandageContext.GetContext(this);

                if (c != null)
                    c.Slip();
            }

            if (willKill && from is PlayerMobile)
                Timer.DelayCall(TimeSpan.FromSeconds(10), new TimerCallback(((PlayerMobile)from).RecoverAmmo));
            XmlAttach.CheckOnHit(this, from);
            base.OnDamage(amount, from, willKill);
        }

        public override void Resurrect()
        {
            bool wasAlive = this.Alive;

            base.Resurrect();

            //Clears Visibility of Mobiles/Items/Etc
            ClearScreen();

            //Reloads Visbility of Mobiles/Items/Etc
            SendEverything();

            if (this.Alive && !wasAlive)
            {


                //Wearing Paints
                if (HueMod == 1451 || HueMod == 1108 || HueMod == 1882 || HueMod == 0)
                {
                    // this.HueMod = -1; // this will reset hue on death. 
                }

                //Otherwise Remove HueMods on Ress (From Polymorph or Effect)
                else
                {
                    this.HueMod = -1;
                }
                if (this.Region.IsPartOf(typeof(WorldWarsRegion)) || this.Region.IsPartOf(typeof(StagingAreaRegion)))
                    return;

                if (Server.Scripts.Custom.VorshunWarsEquipment.VorshunStorage.ContainsStuffBelongingTo(this))
                    return;

                bool alreadyHasDeathRobe = false;
                foreach (Item item in this.Backpack.Items)
                {
                    if (item is DeathRobe && item.Hue == 2301)
                    {
                        EquipItem(item);
                        alreadyHasDeathRobe = true;
                        break;
                    }
                }

                if (!alreadyHasDeathRobe)
                {
                    Item deathRobe = new DeathRobe();
                    if (!EquipItem(deathRobe))
                        deathRobe.Delete();
                }
            }
        }

        public override double RacialSkillBonus
        {
            get
            {
                if (Core.ML && this.Race == Race.Human)
                    return 20.0;

                return 0;
            }
        }

        public override void OnWarmodeChanged()
        {
            if (!Warmode)
                Timer.DelayCall(TimeSpan.FromSeconds(10), new TimerCallback(RecoverAmmo));
        }

        private Mobile m_InsuranceAward;
        private int m_InsuranceCost;
        private int m_InsuranceBonus;

        private bool FindItems_Callback(Item item)
        {
            if (!item.Deleted && (item.LootType == LootType.Blessed || item.Insured == true))
            {
                if (this.Backpack != item.ParentEntity)
                {
                    return true;
                }
            }
            return false;
        }

        public override bool OnBeforeDeath()
        {
            NetState state = NetState;

            if (state != null)
                state.CancelAllTrades();

            DropHolding();

            //Zombiex
            if (FindMostRecentDamager(true) is Zombiex)
            {
                Zombiex zomb = new Zombiex();
                zomb.NewZombie(this);
            }
            //Zombiex end

            if (Backpack != null && !Backpack.Deleted)
            {
                List<Item> ilist = Backpack.FindItemsByType<Item>(FindItems_Callback);

                for (int i = 0; i < ilist.Count; i++)
                {
                    Backpack.AddItem(ilist[i]);
                }
            }

            m_NonAutoreinsuredItems = 0;
            m_InsuranceCost = 0;
            m_InsuranceAward = base.FindMostRecentDamager(false);

            if (m_InsuranceAward is BaseCreature)
            {
                Mobile master = ((BaseCreature)m_InsuranceAward).GetMaster();

                if (master != null)
                    m_InsuranceAward = master;
            }

            if (m_InsuranceAward != null && (!m_InsuranceAward.Player || m_InsuranceAward == this))
                m_InsuranceAward = null;

            if (m_InsuranceAward is PlayerMobile)
                ((PlayerMobile)m_InsuranceAward).m_InsuranceBonus = 0;

            RecoverAmmo();
            XmlQuest.RegisterKill(this, this.LastKiller);
            return base.OnBeforeDeath();
        }

        private bool CheckInsuranceOnDeath(Item item)
        {
            if (InsuranceEnabled && item.Insured)
            {
                if (AutoRenewInsurance)
                {
                    int cost = (m_InsuranceAward == null ? 600 : 300);

                    if (Banker.Withdraw(this, cost))
                    {
                        m_InsuranceCost += cost;
                        item.PayedInsurance = true;
                        SendLocalizedMessage(1060398, cost.ToString()); // ~1_AMOUNT~ gold has been withdrawn from your bank box.
                    }
                    else
                    {
                        SendLocalizedMessage(1061079, "", 0x23); // You lack the funds to purchase the insurance
                        item.PayedInsurance = false;
                        item.Insured = false;
                        m_NonAutoreinsuredItems++;
                    }
                }
                else
                {
                    item.PayedInsurance = false;
                    item.Insured = false;
                }

                if (m_InsuranceAward != null)
                {
                    if (Banker.Deposit(m_InsuranceAward, 300))
                    {
                        if (m_InsuranceAward is PlayerMobile)
                            ((PlayerMobile)m_InsuranceAward).m_InsuranceBonus += 300;
                    }
                }

                return true;
            }

            return false;
        }

        public override DeathMoveResult GetParentMoveResultFor(Item item)
        {
            if (CheckInsuranceOnDeath(item))
                return DeathMoveResult.MoveToBackpack;

            DeathMoveResult res = base.GetParentMoveResultFor(item);

            return res;
        }

        public override DeathMoveResult GetInventoryMoveResultFor(Item item)
        {
            if (CheckInsuranceOnDeath(item))
                return DeathMoveResult.MoveToBackpack;

            DeathMoveResult res = base.GetInventoryMoveResultFor(item);

            return res;
        }

        private double FindTwentyPercent(double amount)
        {
            double newAmount = (amount / 100.0) * 20.0;

            return newAmount;
        }

        public override void OnDeath(Container c)
        {
            if (m_NonAutoreinsuredItems > 0)
            {
                SendLocalizedMessage(1061115);
            }

            base.OnDeath(c);

            //Clears Visibility of Mobiles/Items/Etc
            ClearScreen();

            //Reloads Visbility of Mobiles/Items/Etc
            SendEverything();

            NameMod = null;
            SavagePaintExpiration = TimeSpan.Zero;

            SetHairMods(-1, -1);

            PolymorphSpell.StopTimer(this);
            IncognitoSpell.StopTimer(this);
            DisguiseTimers.RemoveTimer(this);


            EndAction(typeof(PolymorphSpell));
            EndAction(typeof(IncognitoSpell));

            MeerMage.StopEffect(this, false);

            //SkillHandlers.StolenItem.ReturnOnDeath(this, c);

            if (m_PermaFlags.Count > 0)
            {
                m_PermaFlags.Clear();

                if (c is Corpse)
                    ((Corpse)c).Criminal = true;

                if (SkillHandlers.Stealing.ClassicMode)
                    Criminal = true;
            }

            if (m_InsuranceAward is PlayerMobile)
            {
                PlayerMobile pm = (PlayerMobile)m_InsuranceAward;

                if (pm.m_InsuranceBonus > 0)
                    pm.SendLocalizedMessage(1060397, pm.m_InsuranceBonus.ToString()); // ~1_AMOUNT~ gold has been deposited into your bank box.
            }

            Mobile killer = this.FindMostRecentDamager(true);

            if (killer is BaseCreature)
            {
                BaseCreature bc = (BaseCreature)killer;

                Mobile master = bc.GetMaster();
                if (master != null)
                    killer = master;
            }

            // This will also count if the player's pet does the kill
            if (BountyHunter && killer != null && killer is PlayerMobile && killer.Kills >= 5)
            {
                Mobile killed = (Mobile)this;

                //Faction.ApplyBountyHunterSkillLoss(killed);

                this.SendMessage("You have suffered stat loss for 15 minutes due to being killed by {0}", killer.Name);
            }

            Faction.HandleDeath(this, killer);

            Server.Guilds.Guild.HandleDeath(this, killer);

            DeathsThisSession++;
            PlayerMobile killerPM = killer as PlayerMobile;

            if (killerPM != null)
            {
                if (killerPM != this)
                {

                    killerPM.KillsThisSession++;
                    killerPM.PlayersKilledThisSession++;
                    if (Kills >= 5)
                        killerPM.MurdererKillsThisSession++;
                    else if (Notoriety.Compute(killerPM, this) != Notoriety.Innocent) // I believe this is correct (mob)
                        killerPM.MurdersThisSession++;
                }
            }

            if (this.PlayerMurdererStatus == MurdererStatus.Parole)
            {
                this.SufferBountyStatloss();
            }

            if (killer is BaseGuard)
            {
                GuardWhacksThisSession++;
            }
            if (m_BuffTable != null)
            {
                List<BuffInfo> list = new List<BuffInfo>();

                foreach (BuffInfo buff in m_BuffTable.Values)
                {
                    if (!buff.RetainThroughDeath)
                    {
                        list.Add(buff);
                    }
                }

                for (int i = 0; i < list.Count; i++)
                {
                    RemoveBuff(list[i]);
                }
            }
            if (c is Corpse)
            {
                Corpse corpse = c as Corpse;
                foreach (Item item in corpse.Items)
                {
                    if (StolenItem.IsStolen(item))
                    {
                        StolenItem.Remove(item);
                    }
                }
            }
        }

        private List<Mobile> m_PermaFlags;
        private List<Mobile> m_VisList;
        private Hashtable m_AntiMacroTable;
        private TimeSpan m_GameTime;
        private TimeSpan m_ShortTermElapse;
        private TimeSpan m_LongTermElapse;
        private DateTime m_SessionStart;
        private DateTime m_LastEscortTime;
        private DateTime m_LastPetBallTime;
        private DateTime m_NextSmithBulkOrder;
        private DateTime m_NextTailorBulkOrder;
        private DateTime m_NextCarpentryBulkOrder;
        private DateTime m_NextAlchemyBulkOrder;
        private DateTime m_NextInscriptionBulkOrder;
        private DateTime m_NextTinkeringBulkOrder;
        private DateTime m_SavagePaintExpiration;
        private SkillName m_Learning = (SkillName)(-1);

        public SkillName Learning
        {
            get { return m_Learning; }
            set { m_Learning = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan SavagePaintExpiration
        {
            get
            {
                TimeSpan ts = m_SavagePaintExpiration - DateTime.Now;

                if (ts < TimeSpan.Zero)
                    ts = TimeSpan.Zero;

                return ts;
            }
            set
            {
                m_SavagePaintExpiration = DateTime.Now + value;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NextSmithBulkOrder
        {
            get
            {
                TimeSpan ts = m_NextSmithBulkOrder - DateTime.Now;

                if (ts < TimeSpan.Zero)
                    ts = TimeSpan.Zero;

                return ts;
            }
            set
            {
                try { m_NextSmithBulkOrder = DateTime.Now + value; }
                catch { }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NextTailorBulkOrder
        {
            get
            {
                TimeSpan ts = m_NextTailorBulkOrder - DateTime.Now;

                if (ts < TimeSpan.Zero)
                    ts = TimeSpan.Zero;

                return ts;
            }
            set
            {
                try { m_NextTailorBulkOrder = DateTime.Now + value; }
                catch { }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NextCarpentryBulkOrder
        {
            get
            {
                TimeSpan ts = m_NextCarpentryBulkOrder - DateTime.Now;

                if (ts < TimeSpan.Zero)
                    ts = TimeSpan.Zero;

                return ts;
            }
            set
            {
                try { m_NextCarpentryBulkOrder = DateTime.Now + value; }
                catch { }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NextAlchemyBulkOrder
        {
            get
            {
                TimeSpan ts = m_NextAlchemyBulkOrder - DateTime.Now;

                if (ts < TimeSpan.Zero)
                    ts = TimeSpan.Zero;

                return ts;
            }
            set
            {
                try { m_NextAlchemyBulkOrder = DateTime.Now + value; }
                catch { }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NextInscriptionBulkOrder
        {
            get
            {
                TimeSpan ts = m_NextInscriptionBulkOrder - DateTime.Now;

                if (ts < TimeSpan.Zero)
                    ts = TimeSpan.Zero;

                return ts;
            }
            set
            {
                try { m_NextInscriptionBulkOrder = DateTime.Now + value; }
                catch { }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NextTinkeringBulkOrder
        {
            get
            {
                TimeSpan ts = m_NextTinkeringBulkOrder - DateTime.Now;

                if (ts < TimeSpan.Zero)
                    ts = TimeSpan.Zero;

                return ts;
            }
            set
            {
                try { m_NextTinkeringBulkOrder = DateTime.Now + value; }
                catch { }
            }
        }


        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastEscortTime
        {
            get { return m_LastEscortTime; }
            set { m_LastEscortTime = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastPetBallTime
        {
            get { return m_LastPetBallTime; }
            set { m_LastPetBallTime = value; }
        }


        public const int OneBillion = 1000000000; // 1,000,000,000

        private int _BloodCoins;
        private int _PlatinumCoins;

        [CommandProperty(AccessLevel.GameMaster)]
        public int BloodCoins
        {
            get { return _BloodCoins; }
            set { _BloodCoins = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int PlatinumCoins
        {
            get { return _PlatinumCoins; }
            set { _PlatinumCoins = value; }
        }

        public bool AddBloodCoins(int amount)
        {
            if (_BloodCoins + amount <= OneBillion)
                return true;

            return false;
        }

        public bool AddPlatinumCoins(int amount)
        {
            if (_PlatinumCoins + amount <= OneBillion)
                return true;

            return false;
        }

        public PlayerMobile()
        {
            m_AutoStabled = new List<Mobile>();

            m_VisList = new List<Mobile>();
            m_PermaFlags = new List<Mobile>();
            m_AntiMacroTable = new Hashtable();
            m_RecentlyReported = new List<Mobile>();

            m_BOBFilter = new Engines.BulkOrders.BOBFilter();

            m_GameTime = TimeSpan.Zero;
            m_ShortTermElapse = TimeSpan.FromHours(8.0);
            m_LongTermElapse = TimeSpan.FromHours(40.0);

            m_GuildRank = Guilds.RankDefinition.Lowest;

            m_ChampionTitles = new ChampionTitleInfo();

            mExileTimers = new List<ExileTimer>();
            InvalidateMyRunUO();

            _BloodCoins = 0;
            _PlatinumCoins = 0;
        }

        public override bool MutateSpeech(List<Mobile> hears, ref string text, ref object context)
        {
            if (Alive)
                return false;

            if (Core.ML && Skills[SkillName.SpiritSpeak].Value >= 100.0)
                return false;

            if (Core.AOS)
            {
                for (int i = 0; i < hears.Count; ++i)
                {
                    Mobile m = hears[i];

                    if (m != this && m.Skills[SkillName.SpiritSpeak].Value >= 100.0)
                        return false;
                }
            }

            return base.MutateSpeech(hears, ref text, ref context);
        }

        public override void DoSpeech(string text, int[] keywords, MessageType type, int hue)
        {
            if (Guilds.Guild.NewGuildSystem && (type == MessageType.Guild || type == MessageType.Alliance))
            {
                Guilds.Guild g = this.Guild as Guilds.Guild;
                if (g == null)
                {
                    SendLocalizedMessage(1063142); // You are not in a guild!
                }
                else if (type == MessageType.Alliance)
                {
                    if (g.Alliance != null && g.Alliance.IsMember(g))
                    {
                        //g.Alliance.AllianceTextMessage( hue, "[Alliance][{0}]: {1}", this.Name, text );
                        g.Alliance.AllianceChat(this, text);
                        SendToStaffMessage(this, "[Alliance]: {0}", text);

                        m_AllianceMessageHue = hue;
                    }
                    else
                    {
                        SendLocalizedMessage(1071020); // You are not in an alliance!
                    }
                }
                else	//Type == MessageType.Guild
                {
                    m_GuildMessageHue = hue;

                    g.GuildChat(this, text);
                    SendToStaffMessage(this, "[Guild]: {0}", text);
                }
            }
            else
            {
                base.DoSpeech(text, keywords, type, hue);
            }
        }

        private static void SendToStaffMessage(Mobile from, string text)
        {
            Packet p = null;

            foreach (NetState ns in from.GetClientsInRange(8))
            {
                Mobile mob = ns.Mobile;

                if (mob != null && mob.AccessLevel >= AccessLevel.GameMaster && mob.AccessLevel > from.AccessLevel)
                {
                    if (p == null)
                        p = Packet.Acquire(new UnicodeMessage(from.Serial, from.Body, MessageType.Regular, from.SpeechHue, 3, from.Language, from.Name, text));

                    ns.Send(p);
                }
            }

            Packet.Release(p);
        }

        private static void SendToStaffMessage(Mobile from, string format, params object[] args)
        {
            SendToStaffMessage(from, String.Format(format, args));
        }

        public override void Damage(int amount, Mobile from)
        {
            base.Damage(amount, from);
        }

        #region Poison

        public override ApplyPoisonResult ApplyPoison(Mobile from, Poison poison)
        {
            if (!Alive)
                return ApplyPoisonResult.Immune;

            ApplyPoisonResult result = base.ApplyPoison(from, poison);

            if (from != null && result == ApplyPoisonResult.Poisoned && PoisonTimer is PoisonImpl.PoisonTimer)
                (PoisonTimer as PoisonImpl.PoisonTimer).From = from;

            mPoisonsAppliedThisSession++;

            return result;
        }

        public override bool CheckPoisonImmunity(Mobile from, Poison poison)
        {
            return base.CheckPoisonImmunity(from, poison);
        }

        public override void OnPoisonImmunity(Mobile from, Poison poison)
        {
            base.OnPoisonImmunity(from, poison);
        }

        #endregion

        public PlayerMobile(Serial s)
            : base(s)
        {
            m_VisList = new List<Mobile>();
            m_AntiMacroTable = new Hashtable();
            InvalidateMyRunUO();
        }

        public List<Mobile> VisibilityList
        {
            get { return m_VisList; }
        }

        public List<Mobile> PermaFlags
        {
            get { return m_PermaFlags; }
        }

        public override int Luck { get { return AosAttributes.GetValue(this, AosAttribute.Luck); } }

        public override bool IsHarmfulCriminal(Mobile target)
        {
            if (SkillHandlers.Stealing.ClassicMode && target is PlayerMobile && ((PlayerMobile)target).m_PermaFlags.Count > 0)
            {
                int noto = Notoriety.Compute(this, target);

                if (noto == Notoriety.Innocent)
                    target.Delta(MobileDelta.Noto);

                return false;
            }

            if (target is BaseCreature && ((BaseCreature)target).InitialInnocent && !((BaseCreature)target).Controlled)
                return false;

            if (Core.ML && target is BaseCreature && ((BaseCreature)target).Controlled && this == ((BaseCreature)target).ControlMaster)
                return false;

            return base.IsHarmfulCriminal(target);
        }

        public bool AntiMacroCheck(Skill skill, object obj)
        {
            if (obj == null || m_AntiMacroTable == null || this.AccessLevel != AccessLevel.Player)
                return true;

            Hashtable tbl = (Hashtable)m_AntiMacroTable[skill];
            if (tbl == null)
                m_AntiMacroTable[skill] = tbl = new Hashtable();

            CountAndTimeStamp count = (CountAndTimeStamp)tbl[obj];
            if (count != null)
            {
                if (count.TimeStamp + SkillCheck.AntiMacroExpire <= DateTime.Now)
                {
                    count.Count = 1;
                    return true;
                }
                else
                {
                    ++count.Count;
                    if (count.Count <= SkillCheck.Allowance)
                        return true;
                    else
                        return false;
                }
            }
            else
            {
                tbl[obj] = count = new CountAndTimeStamp();
                count.Count = 1;

                return true;
            }
        }

        private void RevertHair()
        {
            SetHairMods(-1, -1);
        }

        private Engines.BulkOrders.BOBFilter m_BOBFilter;

        public Engines.BulkOrders.BOBFilter BOBFilter
        {
            get { return m_BOBFilter; }
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            switch (version)
            {
                case 40:
                    {
                        NextCarpentryBulkOrder = reader.ReadTimeSpan();
                        NextAlchemyBulkOrder = reader.ReadTimeSpan();
                        NextInscriptionBulkOrder = reader.ReadTimeSpan();
                        NextTinkeringBulkOrder = reader.ReadTimeSpan();

                        goto case 39;
                    }
                case 39:
                    {
                        _BloodCoins = reader.ReadInt();
                        _PlatinumCoins = reader.ReadInt();

                        goto case 38;
                    }
                case 38:
                    {
                        m_Pseu_NextPossessDelay = reader.ReadTimeSpan();
                        m_Pseu_NextPossessAllowed = reader.ReadDateTime();
                        m_Pseu_SpawnsAllowed = reader.ReadInt();
                        m_Pseu_NextBroadcastDelay = reader.ReadTimeSpan();
                        m_Pseu_NextBroadcastAllowed = reader.ReadDateTime();
                        m_Pseu_DungeonWatchAllowed = reader.ReadBool();
                        goto case 37;
                    }
                case 37:
                    {
                        m_TeamFlags = reader.ReadULong();
                        goto case 36;
                    }
                case 36:
                    {
                        PlayerMurdererStatus = (MurdererStatus)reader.ReadInt();
                        goto case 35;
                    }
                case 35:
                    {
                        HueMod = reader.ReadInt();
                        goto case 34;
                    }
                case 34:
                    {
                        mExileTimers = new List<ExileTimer>();

                        int exileTimerCount = reader.ReadInt();
                        for (int i = 0; i < exileTimerCount; i++)
                        {
                            ICommonwealth exilingTownship = Commonwealth.ReadReference(reader);
                            DateTime exileTime = reader.ReadDateTime();
                            ExileTimer exileTimer = new ExileTimer(exilingTownship, this, exileTime);

                            exileTimer.Start();

                            mExileTimers.Add(exileTimer);

                        }
                        goto case 33;
                    }
                case 33:
                    {
                        mHasBadName = reader.ReadBool();
                        goto case 32;
                    }
                case 32:
                    {
                        m_AwaitingAntiRailResponse = reader.ReadBool();
                        m_IncorrectCaptchaResponses = reader.ReadInt();
                        goto case 31;
                    }
                case 31:                    // added for story 58: bounties and statloss
                    {
                        m_BountyParoleExpiration = reader.ReadTimeSpan();

                        // we leave this in to prevent forced deletion of existing playermobiles
                        // but we have commented out the if statement below that sets the new parole system
                        bool applyStatloss = reader.ReadBool();
                        // applyStatloss will be false now with new parole/outcast murder system, unless this is
                        // the first deserialize after the system is put in.  In that case, if it is true
                        // we simple do not apply the statloss as before, but rather put them straight into parole
                        /* if (applyStatloss == true)
                        {
                            this.PlayerMurdererStatus = MurdererStatus.Parole;
                        } */
                        goto case 30;
                    }
                case 30:                    // added for story 56: bounties and reporting
                    {
                        m_AutomatedBounty = reader.ReadInt();
                        m_PlayerBounty = reader.ReadInt();

                        goto case 29;
                    }
                case 29:                    // added for story 60: player bounty hunters
                    {
                        m_QuittingBountyHuntingAt = reader.ReadDateTime();
                        BountyHunter = reader.ReadBool();

                        goto case 28;
                    }
                case 28:
                    {
                        m_PeacedUntil = reader.ReadDateTime();

                        goto case 27;
                    }
                case 27:
                    {
                        m_AnkhNextUse = reader.ReadDateTime();

                        goto case 26;
                    }
                case 26:
                    {
                        m_AutoStabled = reader.ReadStrongMobileList();

                        //Followers Recalculation
                        FollowersMax = DetermineFollowersMax();

                        goto case 25;
                    }
                case 25:
                    {
                        int recipeCount = reader.ReadInt();

                        if (recipeCount > 0)
                        {
                            m_AcquiredRecipes = new Dictionary<int, bool>();

                            for (int i = 0; i < recipeCount; i++)
                            {
                                int r = reader.ReadInt();
                                if (reader.ReadBool())	//Don't add in recipies which we haven't gotten or have been removed
                                    m_AcquiredRecipes.Add(r, true);
                            }
                        }
                        goto case 24;
                    }
                case 24:
                    {
                        reader.ReadDeltaTime();
                        goto case 23;
                    }
                case 23:
                    {
                        m_ChampionTitles = new ChampionTitleInfo(reader);
                        goto case 22;
                    }
                case 22:
                    {
                        reader.ReadDateTime();
                        goto case 21;
                    }
                case 21:
                    {
                        m_ToTItemsTurnedIn = reader.ReadEncodedInt();
                        m_ToTTotalMonsterFame = reader.ReadInt();
                        goto case 20;
                    }
                case 20:
                    {
                        m_AllianceMessageHue = reader.ReadEncodedInt();
                        m_GuildMessageHue = reader.ReadEncodedInt();

                        goto case 19;
                    }
                case 19:
                    {
                        int rank = reader.ReadEncodedInt();
                        int maxRank = Guilds.RankDefinition.Ranks.Length - 1;
                        if (rank > maxRank)
                            rank = maxRank;

                        m_GuildRank = Guilds.RankDefinition.Ranks[rank];
                        m_LastOnline = reader.ReadDateTime();
                        goto case 18;
                    }
                case 18:
                    {
                        m_SolenFriendship = (SolenFriendship)reader.ReadEncodedInt();

                        goto case 17;
                    }
                case 17: // changed how DoneQuests is serialized
                case 16:
                    {
                        m_Quest = QuestSerializer.DeserializeQuest(reader);

                        if (m_Quest != null)
                            m_Quest.From = this;

                        int count = reader.ReadEncodedInt();

                        if (count > 0)
                        {
                            m_DoneQuests = new List<QuestRestartInfo>();

                            for (int i = 0; i < count; ++i)
                            {
                                Type questType = QuestSerializer.ReadType(QuestSystem.QuestTypes, reader);
                                DateTime restartTime;

                                if (version < 17)
                                    restartTime = DateTime.MaxValue;
                                else
                                    restartTime = reader.ReadDateTime();

                                m_DoneQuests.Add(new QuestRestartInfo(questType, restartTime));
                            }
                        }

                        m_Profession = reader.ReadEncodedInt();
                        goto case 15;
                    }
                case 15:
                    {
                        reader.ReadDeltaTime();
                        goto case 14;
                    }
                case 14:
                    {
                        int dummy = reader.ReadEncodedInt();

                        if (dummy > 0)
                            reader.ReadDeltaTime();

                        goto case 13;
                    }
                case 13: // just removed m_PayedInsurance list
                case 12:
                    {
                        m_BOBFilter = new Engines.BulkOrders.BOBFilter(reader);
                        goto case 11;
                    }
                case 11:
                    {
                        if (version < 13)
                        {
                            List<Item> payed = reader.ReadStrongItemList();

                            for (int i = 0; i < payed.Count; ++i)
                                payed[i].PayedInsurance = true;
                        }

                        goto case 10;
                    }
                case 10:
                    {
                        if (reader.ReadBool())
                        {
                            m_HairModID = reader.ReadInt();
                            m_HairModHue = reader.ReadInt();
                            m_BeardModID = reader.ReadInt();
                            m_BeardModHue = reader.ReadInt();
                        }

                        goto case 9;
                    }
                case 9:
                    {
                        SavagePaintExpiration = reader.ReadTimeSpan();
                        /*
                        if (SavagePaintExpiration > TimeSpan.Zero)
                        {
                            BodyMod = (Female ? 184 : 183);
                            HueMod = 0;
                        }
                        */
                        goto case 8;
                    }
                case 8:
                    {
                        m_NpcGuild = (NpcGuild)reader.ReadInt();
                        m_NpcGuildJoinTime = reader.ReadDateTime();
                        m_NpcGuildGameTime = reader.ReadTimeSpan();
                        goto case 7;
                    }
                case 7:
                    {
                        m_PermaFlags = reader.ReadStrongMobileList();
                        goto case 6;
                    }
                case 6:
                    {
                        NextTailorBulkOrder = reader.ReadTimeSpan();
                        goto case 5;
                    }
                case 5:
                    {
                        NextSmithBulkOrder = reader.ReadTimeSpan();
                        goto case 4;
                    }
                case 4:
                    {
                        reader.ReadDeltaTime();
                        reader.ReadStrongMobileList();
                        goto case 3;
                    }
                case 3:
                    {
                        reader.ReadDeltaTime();
                        reader.ReadDeltaTime();
                        reader.ReadInt();
                        goto case 2;
                    }
                case 2:
                    {
                        m_Flags = (PlayerFlag)reader.ReadInt();
                        goto case 1;
                    }
                case 1:
                    {
                        m_LongTermElapse = reader.ReadTimeSpan();
                        m_ShortTermElapse = reader.ReadTimeSpan();
                        m_GameTime = reader.ReadTimeSpan();
                        goto case 0;
                    }
                case 0:
                    {
                        if (version < 26)
                            m_AutoStabled = new List<Mobile>();
                        break;
                    }
            }

            if (m_RecentlyReported == null)
                m_RecentlyReported = new List<Mobile>();

            // Professions weren't verified on 1.0 RC0
            if (!CharacterCreation.VerifyProfession(m_Profession))
                m_Profession = 0;

            if (m_PermaFlags == null)
                m_PermaFlags = new List<Mobile>();

            if (m_BOBFilter == null)
                m_BOBFilter = new Engines.BulkOrders.BOBFilter();

            if (m_GuildRank == null)
                m_GuildRank = Guilds.RankDefinition.Member;	//Default to member if going from older verstion to new version (only time it should be null)

            if (m_LastOnline == DateTime.MinValue && Account != null)
                m_LastOnline = ((Account)Account).LastLogin;

            if (m_ChampionTitles == null)
                m_ChampionTitles = new ChampionTitleInfo();

            if (AccessLevel > AccessLevel.Player)
                m_IgnoreMobiles = true;

            List<Mobile> list = this.Stabled;

            for (int i = 0; i < list.Count; ++i)
            {
                BaseCreature bc = list[i] as BaseCreature;

                if (bc != null)
                    bc.IsStabled = true;
            }

            CheckAtrophies(this);

            if (Hidden)	//Hiding is the only buff where it has an effect that's serialized.
                AddBuff(new BuffInfo(BuffIcon.HidingAndOrStealth, 1075655));
        }

        public override void Serialize(GenericWriter writer)
        {
            //cleanup our anti-macro table
            foreach (Hashtable t in m_AntiMacroTable.Values)
            {
                ArrayList remove = new ArrayList();
                foreach (CountAndTimeStamp time in t.Values)
                {
                    if (time.TimeStamp + SkillCheck.AntiMacroExpire <= DateTime.Now)
                        remove.Add(time);
                }

                for (int i = 0; i < remove.Count; ++i)
                    t.Remove(remove[i]);
            }

            CheckKillDecay();

            CheckBountyParoleDecay();

            CheckAtrophies(this);

            base.Serialize(writer);

            writer.Write((int)40); // version

            // 40
            writer.Write(NextCarpentryBulkOrder);
            writer.Write(NextAlchemyBulkOrder);
            writer.Write(NextInscriptionBulkOrder);
            writer.Write(NextTinkeringBulkOrder);


            // 39
            writer.Write((int)_BloodCoins);
            writer.Write((int)_PlatinumCoins);

            // verion 38
            writer.Write((TimeSpan)m_Pseu_NextPossessDelay);
            writer.Write((DateTime)m_Pseu_NextPossessAllowed);
            writer.Write((int)m_Pseu_SpawnsAllowed);
            writer.Write((TimeSpan)m_Pseu_NextBroadcastDelay);
            writer.Write((DateTime)m_Pseu_NextBroadcastAllowed);
            writer.Write((bool)m_Pseu_DungeonWatchAllowed);

            // version 37
            writer.Write((ulong)m_TeamFlags);

            // version...
            writer.Write((int)this.PlayerMurdererStatus);

            writer.Write((int)HueMod);

            if (mExileTimers == null)
                mExileTimers = new List<ExileTimer>(); //shitty but this keeps from save errors

            writer.Write((int)mExileTimers.Count);

            for (int i = 0; i < mExileTimers.Count; i++)
            {
                Commonwealth.WriteReference(writer, mExileTimers[i].Township);
                writer.Write((DateTime)mExileTimers[i].ExileTime);
            }

            writer.Write((bool)mHasBadName);

            writer.Write(m_AwaitingAntiRailResponse);
            writer.Write(m_IncorrectCaptchaResponses);

            writer.Write(BountyParoleExpiration);
            writer.Write(false); // used to be InBountyStatloss, which will henceforth be false

            writer.Write(m_AutomatedBounty);
            writer.Write(m_PlayerBounty);
            writer.Write(m_QuittingBountyHuntingAt);
            writer.Write(m_BountyHunter);
            writer.Write((DateTime)m_PeacedUntil);
            writer.Write((DateTime)m_AnkhNextUse);
            writer.Write(m_AutoStabled, true);

            if (m_AcquiredRecipes == null)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(m_AcquiredRecipes.Count);

                foreach (KeyValuePair<int, bool> kvp in m_AcquiredRecipes)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            writer.WriteDeltaTime(DateTime.MinValue);

            ChampionTitleInfo.Serialize(writer, m_ChampionTitles);

            writer.Write(DateTime.MinValue);
            writer.WriteEncodedInt(m_ToTItemsTurnedIn);
            writer.Write(m_ToTTotalMonsterFame);	//This ain't going to be a small #.

            writer.WriteEncodedInt(m_AllianceMessageHue);
            writer.WriteEncodedInt(m_GuildMessageHue);

            writer.WriteEncodedInt(m_GuildRank.Rank);
            writer.Write(m_LastOnline);

            writer.WriteEncodedInt((int)m_SolenFriendship);

            QuestSerializer.Serialize(m_Quest, writer);

            if (m_DoneQuests == null)
            {
                writer.WriteEncodedInt((int)0);
            }
            else
            {
                writer.WriteEncodedInt((int)m_DoneQuests.Count);

                for (int i = 0; i < m_DoneQuests.Count; ++i)
                {
                    QuestRestartInfo restartInfo = m_DoneQuests[i];

                    QuestSerializer.Write((Type)restartInfo.QuestType, QuestSystem.QuestTypes, writer);
                    writer.Write((DateTime)restartInfo.RestartTime);
                }
            }

            writer.WriteEncodedInt((int)m_Profession);

            writer.WriteDeltaTime(DateTime.MinValue);

            writer.WriteEncodedInt(0);

            m_BOBFilter.Serialize(writer);

            bool useMods = (m_HairModID != -1 || m_BeardModID != -1);

            writer.Write(useMods);

            if (useMods)
            {
                writer.Write((int)m_HairModID);
                writer.Write((int)m_HairModHue);
                writer.Write((int)m_BeardModID);
                writer.Write((int)m_BeardModHue);
            }

            writer.Write(SavagePaintExpiration);

            writer.Write((int)m_NpcGuild);
            writer.Write((DateTime)m_NpcGuildJoinTime);
            writer.Write((TimeSpan)m_NpcGuildGameTime);

            writer.Write(m_PermaFlags, true);

            writer.Write(NextTailorBulkOrder);
            writer.Write(NextSmithBulkOrder);


            writer.WriteDeltaTime(DateTime.MinValue);     // dummy value so we don't break serialization
            writer.Write(new List<Mobile>(), true);         // dummy value so we don't break serialization

            writer.WriteDeltaTime(DateTime.MinValue);       // dummy value so we don't break serialization
            writer.WriteDeltaTime(DateTime.MinValue);       // dummy value so we don't break serialization
            writer.Write(0);

            writer.Write((int)m_Flags);

            writer.Write(m_LongTermElapse);
            writer.Write(m_ShortTermElapse);
            writer.Write(this.GameTime);
        }


        private void CheckBountyParoleDecay()
        {
            if (m_BountyParoleExpiration < this.GameTime && this.PlayerMurdererStatus != MurdererStatus.Parole)
            {
                this.PlayerMurdererStatus = MurdererStatus.None;
            }
        }

        public static void CheckAtrophies(Mobile m)
        {
            if (m is PlayerMobile)
                ChampionTitleInfo.CheckAtrophy((PlayerMobile)m);
        }

        public bool CheckKillDecay()
        {
            if (ShortTermMurders > 0)
                ShortTermMurders--;
            return ShortTermMurders < 5;
        }

        public void ResetKillTime()
        {
            m_ShortTermElapse = this.GameTime + TimeSpan.FromHours(8);
            m_LongTermElapse = this.GameTime + TimeSpan.FromHours(40);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime SessionStart
        {
            get { return m_SessionStart; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan GameTime
        {
            get
            {
                if (NetState != null)
                    return m_GameTime + (DateTime.Now - m_SessionStart);
                else
                    return m_GameTime;
            }
        }

        public override bool CanSee(Mobile m)
        {
            return CanSee(m, this.GetDistanceToSqrt(m));
        }

        public override bool CanSee(Mobile m, double distance)
        {
            //Beholder or Behold is Delete or Not In Correct Map
            if (this.Deleted || m.Deleted || this.Map == Map.Internal || m.Map == Map.Internal)
                return false;

            //Display Character Statue Stuff
            if (m is CharacterStatue)
                ((CharacterStatue)m).OnRequestedAnimation(this);

            //Staff Can Always See Things
            if (this.AccessLevel > AccessLevel.Player)
                return true;

            //Can Always See Self
            if (this == m)
                return true;

            PlayerMobile pm = m as PlayerMobile;

            //Visibility List For Player Watching
            if (pm != null && pm.m_VisList.Contains(this))
                return true;

            //Player is Alive OR they are a pseudoseer or dead staffmember
            if (this.Alive || (PseudoSeerStone.Instance != null && CreaturePossession.HasAnyPossessPermissions(this)))
            {
                //Target is Alive
                if (m.Alive)
                {
                    //Target is Hidden
                    if (m.Hidden)
                    {
                        //Cannot Passive Detect/Track Staff
                        if (m.AccessLevel > AccessLevel.Player)
                            return false;

                        //Detect Hidden + Tracking Override                        
                        double trackingSkill = Math.Floor(this.Skills[SkillName.Tracking].Value / 5) - 4;

                        if (trackingSkill < 0)
                            trackingSkill = 0;

                        double detectHiddenSkill = Math.Floor(this.Skills[SkillName.DetectHidden].Value / 5) - 4;

                        if (detectHiddenSkill < 0)
                            detectHiddenSkill = 0;

                        double effectiveSkill = 0;

                        //Get Lowest of The Skills
                        if (trackingSkill >= detectHiddenSkill)
                        {
                            effectiveSkill = detectHiddenSkill;
                        }

                        else
                        {
                            effectiveSkill = trackingSkill;
                        }

                        double distanceToTarget = distance;
                        double effectiveRange = effectiveSkill;

                        //Base Range Multiplier
                        effectiveRange *= 1;

                        //In Same Guild or Party
                        if ((this.Guild != null && this.Guild == m.Guild) || (this.Party != null && this.Party == m.Party))
                        {
                            //Fix This Eventually
                            //DONT TOUCH UNTIL PACKET ADDING/REMOVAL FOR ADDING / REMOVING PARTY RESOLVED
                            effectiveRange *= 1;
                        }

                        //IF They Can Detect At Least 1 Space and Can Detect Them at Their Range
                        if (effectiveRange >= 1 && (effectiveRange >= distanceToTarget))
                        {
                            return true;
                        }

                        //Cant' Detect Them at Their Range
                        else
                        {
                            return false;
                        }
                    }

                    //Target is Revealed
                    else
                    {
                        return true;
                    }
                }

                //Target is Dead
                else
                {
                    //Target is in Warmode
                    if (m.Warmode)
                    {
                        return true;
                    }

                    //Target is not in Warmode
                    else
                    {
                        return false;
                    }
                }
            }

            //Player is Dead
            else
            {
                //Target is BaseHealer
                if (m is BaseHealer)
                {
                    return true;
                }

                //Player is in Same Guild or Party as Target
                else if ((this.Guild != null && this.Guild == m.Guild) || (this.Party != null && this.Party == m.Party))
                {
                    return true;
                }

                //Target Has Recently Used Spirit Speaking
                else if (pm != null && pm.SpiritSpeakGhostSightExpiration > DateTime.Now)
                {
                    return true;
                }

                //Target is Normal
                else
                {
                    return false;
                }
            }

            /*
            if (m_Deleted || m.m_Deleted || m_Map == Map.Internal || m.m_Map == Map.Internal)
                return false;

            return this == m || (
                m.m_Map == m_Map &&
                (!m.Hidden || (m_AccessLevel != AccessLevel.Player && (m_AccessLevel >= m.AccessLevel || m_AccessLevel >= AccessLevel.Developer))) &&
                ((m.Alive || (Core.SE && Skills.SpiritSpeak.Value >= 100.0)) || !Alive || m_AccessLevel > AccessLevel.Player || m.Warmode));
            */

            /*
            return base.CanSee(m);
            */
        }

        public override bool CanSee(Item item)
        {
            if (m_DesignContext != null && m_DesignContext.Foundation.IsHiddenToCustomizer(item))
                return false;

            return base.CanSee(item);
        }

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();

            Faction faction = Faction.Find(this);

            if (faction != null)
                faction.RemoveMember(this);

            BaseHouse.HandleDeletion(this);

            DisguiseTimers.RemoveTimer(this);
        }

        public override bool NewGuildDisplay { get { return Server.Guilds.Guild.NewGuildSystem; } }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);

            if (Map == Faction.Facet)
            {
                PlayerState pl = PlayerState.Find(this);

                if (pl != null)
                {
                    Faction faction = pl.Faction;

                    if (faction.Commander == this)
                        list.Add(1042733, faction.Definition.PropName); // Commanding Lord of the ~1_FACTION_NAME~
                    else if (pl.MerchantTitle != MerchantTitle.None)
                        list.Add(1060776, "{0}\t{1}", MerchantTitles.GetInfo(pl.MerchantTitle).Title, faction.Definition.PropName); // ~1_val~, ~2_val~
                    else
                        list.Add(pl.Rank.Title + " of " + faction.Definition.TownshipName);
                }

                if (CitizenshipPlayerState != null && pl == null) //always display the faction text over the citizen text
                {
                    if (CitizenshipPlayerState.Commonwealth.Minister == this)
                        list.Add("Minister of " + CitizenshipPlayerState.Commonwealth.Definition.TownName);
                    else
                        list.Add("Citizen of " + CitizenshipPlayerState.Commonwealth.Definition.TownName);

                }
            }

        }

        public override void OnSingleClick(Mobile from)
        {
            PlayerState pl = PlayerState.Find(this);

            string text = string.Empty;
            bool ascii = false;
            int hue = 0;
            string prefix = string.Empty;

            if (pl != null)
            {
                if (CitizenshipPlayerState != null && CitizenshipPlayerState.Commonwealth != null) //always display the faction text over the citizen text
                {
                    if (CitizenshipPlayerState.Commonwealth.Minister == this)
                        text = "[Minister of " + CitizenshipPlayerState.Commonwealth.Definition.TownName + "]";
                    else
                        text = "[Citizen of " + CitizenshipPlayerState.Commonwealth.Definition.TownName + "]";

                    if (CitizenshipPlayerState.Commonwealth.Militia != null)
                        hue = CitizenshipPlayerState.Commonwealth.Militia.Definition.HueSecondary + FeatureList.Citizenship.TagTextHueModifier;
                }

                Faction faction = pl.Faction;

                if (faction != null)
                {
                    if (faction.Commander == this)
                        text = String.Concat(this.Female ? "[Commanding Lady of the " : "[Commanding Lord of the ", faction.Definition.FriendlyName, "]");
                    else
                    {
                        ascii = true;

                        if (pl.MerchantTitle != MerchantTitle.None)
                            text = String.Concat("[", MerchantTitles.GetInfo(pl.MerchantTitle).Title.String, ", ", faction.Definition.FriendlyName, "]");
                        else
                            text = String.Concat("[", pl.Rank.Title.String, " | ", faction.Definition.FriendlyName, "]");
                    }

                    hue = faction.Definition.HueSecondary;
                }
            }



            if (prefix != string.Empty)
                text = String.Concat(prefix, " ", text);

            PrivateOverheadMessage(MessageType.Label, hue, ascii, text, from.NetState);

            /*
                REMOVED CALL OF BASE (server/Mobile.cs) AND INSTEAD MOVED THAT CODE TO THE PLAYERMOBILE LEVEL
                THIS ALLOWS PROPER CHECKS FOR IF THE PLAYER IS A BOUNTY HUNTER (and they would have a kill count)
                AND THEN ADD THE DREAD LORD/LADY PREFIX TO THEIR NAME AND SET THE HUE TO DARKER RED IF THEY ARE
            */
            //base.OnSingleClick(from);
            ShowName(from);
        }

        private static string[] m_GuildTypes = new string[]
            {
                "",
                " (Chaos)",
                " (Order)"
            };

        public void ShowName(Mobile from)
        {
            if (this.Deleted)
                return;
            else if (this.AccessLevel == AccessLevel.Player && DisableHiddenSelfClick && this.Hidden && from == this)
                return;

            if (GuildClickMessage)
            {
                BaseGuild guild = Guild;

                if (guild != null && (DisplayGuildTitle || (Player && guild.Type != GuildType.Regular)))
                {
                    string title = GuildTitle;
                    string type;

                    if (title == null)
                        title = "";
                    else
                        title = title.Trim();

                    if (guild.Type >= 0 && (int)guild.Type < m_GuildTypes.Length)
                        type = m_GuildTypes[(int)guild.Type];
                    else
                        type = "";

                    string text = String.Format(title.Length <= 0 ? "[{1}]{2}" : "[{0}, {1}]{2}", title, guild.Abbreviation, type);

                    PrivateOverheadMessage(MessageType.Regular, SpeechHue, true, text, from.NetState);
                }
            }

            int hue;

            if (NameHue != -1)
                hue = NameHue;
            else if (AccessLevel > AccessLevel.Player)
                hue = 11;
            else
                hue = Notoriety.GetHue(Notoriety.Compute(from, this));

            string name = Name;

            if (name == null)
                name = String.Empty;

            string prefix = "";

            if (this.PlayerMurdererStatus == MurdererStatus.Parole)
            {
                prefix = "Dreadlord";
                hue = 0x020; // dark red
            }
            else if (Title == " " && Notoriety.GetHue(Notoriety.Compute(this, from)) == 0x022)
            {
                // this is a murderer, seeing a bounty hunter 
                //hue = 0x47E; // bright white
                hue = 0x7FD; // bright white
            }

            string suffix = "";

            if (ClickTitle && Title != null && Title.Length > 0)
                suffix = Title;

            suffix = ApplyNameSuffix(suffix);

            string val;

            if (prefix.Length > 0 && suffix.Length > 0)
                val = String.Concat(prefix, " ", name, " ", suffix);
            else if (prefix.Length > 0)
                val = String.Concat(prefix, " ", name);
            else if (suffix.Length > 0)
                val = String.Concat(name, " ", suffix);
            else
                val = name;

            PrivateOverheadMessage(MessageType.Label, hue, true, val, from.NetState);
        }

        protected override bool OnMove(Direction d)
        {
            //Texas Holdem
            if (m_PokerGame != null)
            {
                if (!HasGump(typeof(PokerLeaveGump)))
                {
                    SendGump(new PokerLeaveGump(this, m_PokerGame));

                    return false;
                }
            }
            //End Texas Holdem

            bool running = (d & Direction.Running) != 0;

            if (AccessLevel != AccessLevel.Player)
                return true;

            //If Player hasn't moved for StationaryRefreshTimer length, Running Steps tracker refreshes
            if (m_LastMovement.AddSeconds(FeatureList.PlayerMovement.StationaryRefreshTimer) < DateTime.Now)
            {
                m_RunningStepsTaken = 0;
            }

            m_LastMovement = DateTime.Now;

            if (running)
            {
                m_RunningStepsTaken++;

                //If Player has accumulated enough Running Steps, will lose 1 stamina
                if (m_RunningStepsTaken >= FeatureList.PlayerMovement.RunningStepsForStaminaLoss)
                {
                    if (this.Stam > 0)
                    {
                        this.Stam--;
                    }

                    m_RunningStepsTaken = 0;
                }
            }

            if (Hidden && DesignContext.Find(this) == null)	//Hidden & NOT customizing a house
            {
                if (!Mounted)
                {
                    if (running || CheckStealthSkillForRevealingAction())
                    {
                        RevealingAction();
                        mStealthStepsWalkedThisSession++;
                    }
                }

                else
                {
                    RevealingAction();
                }
            }

            mStepsWalkedThisSession++;

            return true;
        }
        #region RPSkillChanges        

        //returns true if there is a revealing action
        private bool CheckStealthSkillForRevealingAction()
        {
            if (!IsStealthing)
                return true;

            //Player Has Stealth Steps Available (No Check)           
            if (AllowedStealthSteps > 0)
            {
                AllowedStealthSteps--;
                return false;
            }

            //No Free Steps Remaining
            else
            {
                double BaseSuccessPercent = FeatureList.SkillChanges.StealthStepSuccessBasePercent;
                double BonusSuccessPerecent = FeatureList.SkillChanges.StealthStepSkillBonusDivider;

                double chance = (BaseSuccessPercent + (this.Skills[SkillName.Stealth].Value / BonusSuccessPerecent)) / 100;

                if (chance >= Utility.RandomDouble())
                {
                    return false;
                }
            }

            return true;
        }

        public override void OnSaid(SpeechEventArgs e)
        {
            if (Squelched)
            {
                SendMessage("You can not say anything, you have been squelched.");
            }

            if (Hidden && AccessLevel == AccessLevel.Player)
            {
                PublicHiddenOverheadMessage(FeatureList.SkillChanges.HiddenTextHue, e.Speech);
            }
        }

        public void PublicHiddenOverheadMessage(int hue, string message)
        {
            IPooledEnumerable playersInRange = Map.GetClientsInRange(Location);
            Packet p = null;
            foreach (NetState state in playersInRange)
            {
                if (state.Mobile.InLOS(this) && state.Mobile.InRange(this.Location, 15) && state.Mobile != this)
                {
                    if (p == null)
                    {
                        p = new UnicodeMessage(Serial, Body, MessageType.Regular, hue, 3, Language, string.Empty, "Someone whispers: " + message);
                        p.Acquire();
                    }
                    state.Send(p);
                }
            }
            Packet.Release(p);
            playersInRange.Free();
        }
        private DateTime mNextDisarmingMove;
        public DateTime NextDisarmingMove { get { return mNextDisarmingMove; } set { mNextDisarmingMove = value; } }


        public override void OnAfterAttack()
        {

            if (Weapon is BaseRanged && NextCombatTime < DateTime.Now)
            {
                NextCombatTime = DateTime.Now + TimeSpan.FromSeconds(-FeatureList.SkillChanges.ArcheryShotTimeDexReduction * Dex + FeatureList.SkillChanges.ArcheryInitialBaseShotTime);
            }
        }
        #endregion
        private bool m_BedrollLogout;

        public bool BedrollLogout
        {
            get { return m_BedrollLogout; }
            set { m_BedrollLogout = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override bool Paralyzed
        {
            get
            {
                return base.Paralyzed;
            }
            set
            {
                base.Paralyzed = value;

                if (value)
                    AddBuff(new BuffInfo(BuffIcon.Paralyze, 1075827));	//Paralyze/You are frozen and can not move
                else
                    RemoveBuff(BuffIcon.Paralyze);
            }
        }

        #region Ethics
        private Ethics.Player m_EthicPlayer;

        [CommandProperty(AccessLevel.GameMaster)]
        public Ethics.Player EthicPlayer
        {
            get { return m_EthicPlayer; }
            set { m_EthicPlayer = value; }
        }
        #endregion

        #region Citizenship
        private PlayerCitizenshipState mCitizenshipPlayerState;
        [CommandProperty(AccessLevel.GameMaster)]
        public PlayerCitizenshipState CitizenshipPlayerState { get { return mCitizenshipPlayerState; } set { mCitizenshipPlayerState = value; } }

        public float GetSkillGainChanceBonus(Skill skill)
        {
            if (mCitizenshipPlayerState == null)
                return 0f;

            CommonwealthSkillBonus csb = mCitizenshipPlayerState.SkillBonuses.Find(
                delegate (CommonwealthSkillBonus x)
                {
                    if (x.SkillNames.Contains(skill.SkillName))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });

            if (csb != null)
            {
                return csb.ChanceBonus;
            }
            else
                return 0f;
        }
        #endregion
        #region Factions
        private PlayerState m_FactionPlayerState;

        public PlayerState FactionPlayerState
        {
            get { return m_FactionPlayerState; }
            set { m_FactionPlayerState = value; }
        }
        #endregion

        #region Quests
        private QuestSystem m_Quest;
        private List<QuestRestartInfo> m_DoneQuests;
        private SolenFriendship m_SolenFriendship;

        public QuestSystem Quest
        {
            get { return m_Quest; }
            set { m_Quest = value; }
        }

        public List<QuestRestartInfo> DoneQuests
        {
            get { return m_DoneQuests; }
            set { m_DoneQuests = value; }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public SolenFriendship SolenFriendship
        {
            get { return m_SolenFriendship; }
            set { m_SolenFriendship = value; }
        }
        #endregion

        #region MyRunUO Invalidation
        private bool m_ChangedMyRunUO;

        public bool ChangedMyRunUO
        {
            get { return m_ChangedMyRunUO; }
            set { m_ChangedMyRunUO = value; }
        }

        public void InvalidateMyRunUO()
        {
            if (!Deleted && !m_ChangedMyRunUO)
            {
                m_ChangedMyRunUO = true;
                //Engines.MyRunUO.MyRunUO.QueueMobileUpdate(this);
            }
        }

        public override void OnKillsChange(int oldValue)
        {
            if (this.Kills < 5 && oldValue >= 5)
            {
                this.BountyParoleExpiration = TimeSpan.MinValue;
                this.PlayerMurdererStatus = MurdererStatus.None;
                SeveredHead.DecayAllHeads(this.Serial);
                Delta(MobileDelta.Noto);
                InvalidateProperties();
            }
            else if (this.Kills >= 5 && this.BountyHunter)
            {
                this.BountyHunter = false;
                this.SendMessage("You are no longer considered a bounty hunter as a result of your actions!");
                this.Delta(MobileDelta.Noto);
                this.InvalidateProperties();

                //DatabaseController.UpdateCharacterBountyHunter(this, false);
            }
            InvalidateMyRunUO();
        }

        public override void OnGenderChanged(bool oldFemale)
        {
            InvalidateMyRunUO();
        }

        public override void OnGuildChange(Server.Guilds.BaseGuild oldGuild)
        {
            InvalidateMyRunUO();
        }

        public override void OnGuildTitleChange(string oldTitle)
        {
            InvalidateMyRunUO();
        }

        public override void OnKarmaChange(int oldValue)
        {
            InvalidateMyRunUO();
        }

        public override void OnFameChange(int oldValue)
        {
            InvalidateMyRunUO();
        }

        public override void OnSkillChange(SkillName skill, double oldBase)
        {
            // Preserve any existing invalidation logic
            base.OnSkillChange(skill, oldBase);

            // Trigger any character‐type achievements that care about skills
            Skill sk = this.Skills[skill];
            AchievementSystem.CheckAchievement(this, AchievementType.Character, sk);
        }
        public override void OnAccessLevelChanged(AccessLevel oldLevel)
        {
            if (AccessLevel == AccessLevel.Player)
                IgnoreMobiles = false;
            else
                IgnoreMobiles = true;

            InvalidateMyRunUO();
        }

        public override void OnRawStatChange(StatType stat, int oldValue)
        {
            InvalidateMyRunUO();
        }

        public override void OnDelete()
        {
            if (CitizenshipPlayerState != null)
                CitizenshipPlayerState.Commonwealth.RemoveDeletedCitizen(this);

            if (BountyRegistry.Contains(this))
                BountyRegistry.ClearBounty(this);

            InvalidateMyRunUO();
            //DatabaseController.RemoveCharacter(this);
        }

        #endregion

        #region Fastwalk Prevention
        private static bool FastwalkPrevention = true; // Is fastwalk prevention enabled?
        private static TimeSpan FastwalkThreshold = TimeSpan.FromSeconds(0.4); // Fastwalk prevention will become active after 0.4 seconds

        private DateTime m_NextMovementTime;

        public virtual bool UsesFastwalkPrevention { get { return (AccessLevel < AccessLevel.Counselor); } }

        public override TimeSpan ComputeMovementSpeed(Direction dir, bool checkTurning)
        {
            if (checkTurning && (dir & Direction.Mask) != (this.Direction & Direction.Mask))
                return Mobile.RunMount;	// We are NOT actually moving (just a direction change)

            TransformContext context = TransformationSpellHelper.GetContext(this);

            bool running = ((dir & Direction.Running) != 0);

            bool onHorse = (this.Mount != null);

            if (onHorse)
                return (running ? Mobile.RunMount : Mobile.WalkMount);

            return (running ? Mobile.RunFoot : Mobile.WalkFoot);
        }

        public static bool MovementThrottle_Callback(NetState ns)
        {
            PlayerMobile pm = ns.Mobile as PlayerMobile;
            BaseCreature bc = ns.Mobile as BaseCreature;
            TimeSpan ts = TimeSpan.Zero;
            if (pm != null)
            {
                if (pm == null || !pm.UsesFastwalkPrevention)
                    return true;

                if (pm.m_NextMovementTime == DateTime.MinValue)
                {
                    // has not yet moved
                    pm.m_NextMovementTime = DateTime.Now;
                    return true;
                }

                ts = pm.m_NextMovementTime - DateTime.Now;

                if (ts < TimeSpan.Zero)
                {
                    // been a while since we've last moved
                    pm.m_NextMovementTime = DateTime.Now;
                    return true;
                }
            } // Check base creatures too so you can't speedhack / poison bug run fast
            else if (bc != null)
            {
                if (bc.m_NextMovementTime == DateTime.MinValue)
                {
                    // has not yet moved
                    bc.m_NextMovementTime = DateTime.Now;
                    return true;
                }

                ts = bc.m_NextMovementTime - DateTime.Now;

                if (ts < TimeSpan.Zero)
                {
                    // been a while since we've last moved
                    bc.m_NextMovementTime = DateTime.Now;
                    return true;
                }
            }

            return (ts < FastwalkThreshold);
        }

        #endregion

        #region Enemy of One
        private Type m_EnemyOfOneType;
        private bool m_WaitingForEnemy;

        public Type EnemyOfOneType
        {
            get { return m_EnemyOfOneType; }
            set
            {
                Type oldType = m_EnemyOfOneType;
                Type newType = value;

                if (oldType == newType)
                    return;

                m_EnemyOfOneType = value;

                DeltaEnemies(oldType, newType);
            }
        }

        public bool WaitingForEnemy
        {
            get { return m_WaitingForEnemy; }
            set { m_WaitingForEnemy = value; }
        }

        private void DeltaEnemies(Type oldType, Type newType)
        {
            foreach (Mobile m in this.GetMobilesInRange(18))
            {
                Type t = m.GetType();

                if (t == oldType || t == newType)
                {
                    NetState ns = this.NetState;

                    if (ns != null)
                    {
                        if (ns.StygianAbyss)
                        {
                            ns.Send(new MobileMoving(m, Notoriety.Compute(this, m)));
                        }
                        else
                        {
                            ns.Send(new MobileMovingOld(m, Notoriety.Compute(this, m)));
                        }
                    }
                }
            }
        }

        #endregion

        #region Hair and beard mods
        private int m_HairModID = -1, m_HairModHue;
        private int m_BeardModID = -1, m_BeardModHue;

        public void SetHairMods(int hairID, int beardID)
        {
            if (hairID == -1)
                InternalRestoreHair(true, ref m_HairModID, ref m_HairModHue);
            else if (hairID != -2)
                InternalChangeHair(true, hairID, ref m_HairModID, ref m_HairModHue);

            if (beardID == -1)
                InternalRestoreHair(false, ref m_BeardModID, ref m_BeardModHue);
            else if (beardID != -2)
                InternalChangeHair(false, beardID, ref m_BeardModID, ref m_BeardModHue);
        }

        private void CreateHair(bool hair, int id, int hue)
        {
            if (hair)
            {
                //TODO Verification?
                HairItemID = id;
                HairHue = hue;
            }
            else
            {
                FacialHairItemID = id;
                FacialHairHue = hue;
            }
        }

        private void InternalRestoreHair(bool hair, ref int id, ref int hue)
        {
            if (id == -1)
                return;

            if (hair)
                HairItemID = 0;
            else
                FacialHairItemID = 0;

            //if( id != 0 )
            CreateHair(hair, id, hue);

            id = -1;
            hue = 0;
        }

        private void InternalChangeHair(bool hair, int id, ref int storeID, ref int storeHue)
        {
            if (storeID == -1)
            {
                storeID = hair ? HairItemID : FacialHairItemID;
                storeHue = hair ? HairHue : FacialHairHue;
            }
            CreateHair(hair, id, 0);
        }

        #endregion

        public override string ApplyNameSuffix(string suffix)
        {
            #region Ethics
            if (m_EthicPlayer != null)
            {
                if (suffix.Length == 0)
                    suffix = m_EthicPlayer.Ethic.Definition.Adjunct.String;
                else
                    suffix = String.Concat(suffix, " ", m_EthicPlayer.Ethic.Definition.Adjunct.String);
            }
            #endregion

            return base.ApplyNameSuffix(suffix);
        }

        public override TimeSpan GetLogoutDelay()
        {
            if (BedrollLogout || TestCenter.Enabled)
                return TimeSpan.Zero;

            return base.GetLogoutDelay();
        }


        #region Speech log
        private SpeechLog m_SpeechLog;

        public SpeechLog SpeechLog { get { return m_SpeechLog; } }

        public override void OnSpeech(SpeechEventArgs e)
        {
            if (SpeechLog.Enabled && this.NetState != null)
            {
                if (m_SpeechLog == null)
                    m_SpeechLog = new SpeechLog();

                m_SpeechLog.Add(e.Mobile, e.Speech);
            }
        }

        #endregion

        #region Champion Titles
        [CommandProperty(AccessLevel.GameMaster)]
        public bool DisplayChampionTitle
        {
            get { return GetFlag(PlayerFlag.DisplayChampionTitle); }
            set { SetFlag(PlayerFlag.DisplayChampionTitle, value); }
        }

        private ChampionTitleInfo m_ChampionTitles;

        [CommandProperty(AccessLevel.GameMaster)]
        public ChampionTitleInfo ChampionTitles { get { return m_ChampionTitles; } set { } }

        private void ToggleChampionTitleDisplay()
        {
            if (!CheckAlive())
                return;

            if (DisplayChampionTitle)
                SendLocalizedMessage(1062419, "", 0x23); // You have chosen to hide your monster kill title.
            else
                SendLocalizedMessage(1062418, "", 0x23); // You have chosen to display your monster kill title.

            DisplayChampionTitle = !DisplayChampionTitle;
        }

        [PropertyObject]
        public class ChampionTitleInfo
        {
            public static TimeSpan LossDelay = TimeSpan.FromDays(1.0);
            public const int LossAmount = 90;

            private class TitleInfo
            {
                private int m_Value;
                private DateTime m_LastDecay;

                public int Value { get { return m_Value; } set { m_Value = value; } }
                public DateTime LastDecay { get { return m_LastDecay; } set { m_LastDecay = value; } }

                public TitleInfo()
                {
                }

                public TitleInfo(GenericReader reader)
                {
                    int version = reader.ReadEncodedInt();

                    switch (version)
                    {
                        case 0:
                            {
                                m_Value = reader.ReadEncodedInt();
                                m_LastDecay = reader.ReadDateTime();
                                break;
                            }
                    }
                }

                public static void Serialize(GenericWriter writer, TitleInfo info)
                {
                    writer.WriteEncodedInt((int)0); // version

                    writer.WriteEncodedInt(info.m_Value);
                    writer.Write(info.m_LastDecay);
                }
            }

            private TitleInfo[] m_Values;

            private int m_Harrower;	//Harrower titles do NOT decay

            public int GetValue(ChampionSpawnType type)
            {
                return GetValue((int)type);
            }

            public void SetValue(ChampionSpawnType type, int value)
            {
                SetValue((int)type, value);
            }

            public void Award(ChampionSpawnType type, int value)
            {
                Award((int)type, value);
            }

            public int GetValue(int index)
            {
                if (m_Values == null || index < 0 || index >= m_Values.Length)
                    return 0;

                if (m_Values[index] == null)
                    m_Values[index] = new TitleInfo();

                return m_Values[index].Value;
            }

            public DateTime GetLastDecay(int index)
            {
                if (m_Values == null || index < 0 || index >= m_Values.Length)
                    return DateTime.MinValue;

                if (m_Values[index] == null)
                    m_Values[index] = new TitleInfo();

                return m_Values[index].LastDecay;
            }

            public void SetValue(int index, int value)
            {
                if (m_Values == null)
                    m_Values = new TitleInfo[ChampionSpawnInfo.Table.Length];

                if (value < 0)
                    value = 0;

                if (index < 0 || index >= m_Values.Length)
                    return;

                if (m_Values[index] == null)
                    m_Values[index] = new TitleInfo();

                m_Values[index].Value = value;
            }

            public void Award(int index, int value)
            {
                if (m_Values == null)
                    m_Values = new TitleInfo[ChampionSpawnInfo.Table.Length];

                if (index < 0 || index >= m_Values.Length || value <= 0)
                    return;

                if (m_Values[index] == null)
                    m_Values[index] = new TitleInfo();

                m_Values[index].Value += value;
            }

            public void Atrophy(int index, int value)
            {
                if (m_Values == null)
                    m_Values = new TitleInfo[ChampionSpawnInfo.Table.Length];

                if (index < 0 || index >= m_Values.Length || value <= 0)
                    return;

                if (m_Values[index] == null)
                    m_Values[index] = new TitleInfo();

                int before = m_Values[index].Value;

                if ((m_Values[index].Value - value) < 0)
                    m_Values[index].Value = 0;
                else
                    m_Values[index].Value -= value;

                if (before != m_Values[index].Value)
                    m_Values[index].LastDecay = DateTime.Now;
            }

            public override string ToString()
            {
                return "...";
            }

            [CommandProperty(AccessLevel.GameMaster)]
            public int Abyss { get { return GetValue(ChampionSpawnType.Abyss); } set { SetValue(ChampionSpawnType.Abyss, value); } }

            [CommandProperty(AccessLevel.GameMaster)]
            public int Arachnid { get { return GetValue(ChampionSpawnType.Arachnid); } set { SetValue(ChampionSpawnType.Arachnid, value); } }

            [CommandProperty(AccessLevel.GameMaster)]
            public int ColdBlood { get { return GetValue(ChampionSpawnType.ColdBlood); } set { SetValue(ChampionSpawnType.ColdBlood, value); } }

            [CommandProperty(AccessLevel.GameMaster)]
            public int ForestLord { get { return GetValue(ChampionSpawnType.ForestLord); } set { SetValue(ChampionSpawnType.ForestLord, value); } }

            [CommandProperty(AccessLevel.GameMaster)]
            public int UnholyTerror { get { return GetValue(ChampionSpawnType.UnholyTerror); } set { SetValue(ChampionSpawnType.UnholyTerror, value); } }

            [CommandProperty(AccessLevel.GameMaster)]
            public int VerminHorde { get { return GetValue(ChampionSpawnType.VerminHorde); } set { SetValue(ChampionSpawnType.VerminHorde, value); } }

            [CommandProperty(AccessLevel.GameMaster)]
            public int Harrower { get { return m_Harrower; } set { m_Harrower = value; } }

            public ChampionTitleInfo()
            {
            }

            public ChampionTitleInfo(GenericReader reader)
            {
                int version = reader.ReadEncodedInt();

                switch (version)
                {
                    case 0:
                        {
                            m_Harrower = reader.ReadEncodedInt();

                            int length = reader.ReadEncodedInt();
                            m_Values = new TitleInfo[length];

                            for (int i = 0; i < length; i++)
                            {
                                m_Values[i] = new TitleInfo(reader);
                            }

                            if (m_Values.Length != ChampionSpawnInfo.Table.Length)
                            {
                                TitleInfo[] oldValues = m_Values;
                                m_Values = new TitleInfo[ChampionSpawnInfo.Table.Length];

                                for (int i = 0; i < m_Values.Length && i < oldValues.Length; i++)
                                {
                                    m_Values[i] = oldValues[i];
                                }
                            }
                            break;
                        }
                }
            }

            public static void Serialize(GenericWriter writer, ChampionTitleInfo titles)
            {
                writer.WriteEncodedInt((int)0); // version

                writer.WriteEncodedInt(titles.m_Harrower);

                int length = titles.m_Values.Length;
                writer.WriteEncodedInt(length);

                for (int i = 0; i < length; i++)
                {
                    if (titles.m_Values[i] == null)
                        titles.m_Values[i] = new TitleInfo();

                    TitleInfo.Serialize(writer, titles.m_Values[i]);
                }
            }

            public static void CheckAtrophy(PlayerMobile pm)
            {
                ChampionTitleInfo t = pm.m_ChampionTitles;
                if (t == null)
                    return;

                if (t.m_Values == null)
                    t.m_Values = new TitleInfo[ChampionSpawnInfo.Table.Length];

                for (int i = 0; i < t.m_Values.Length; i++)
                {
                    if ((t.GetLastDecay(i) + LossDelay) < DateTime.Now)
                    {
                        t.Atrophy(i, LossAmount);
                    }
                }
            }

            public static void AwardHarrowerTitle(PlayerMobile pm)	//Called when killing a harrower.  Will give a minimum of 1 point.
            {
                ChampionTitleInfo t = pm.m_ChampionTitles;
                if (t == null)
                    return;

                if (t.m_Values == null)
                    t.m_Values = new TitleInfo[ChampionSpawnInfo.Table.Length];

                int count = 1;

                for (int i = 0; i < t.m_Values.Length; i++)
                {
                    if (t.m_Values[i].Value > 900)
                        count++;
                }

                t.m_Harrower = Math.Max(count, t.m_Harrower);	//Harrower titles never decay.
            }
        }

        #endregion

        #region Recipes

        private Dictionary<int, bool> m_AcquiredRecipes;

        public virtual bool HasRecipe(Recipe r)
        {
            if (r == null)
                return false;

            return HasRecipe(r.ID);
        }

        public virtual bool HasRecipe(int recipeID)
        {
            if (m_AcquiredRecipes != null && m_AcquiredRecipes.ContainsKey(recipeID))
                return m_AcquiredRecipes[recipeID];

            return false;
        }

        public virtual void AcquireRecipe(Recipe r)
        {
            if (r != null)
                AcquireRecipe(r.ID);
        }

        public virtual void AcquireRecipe(int recipeID)
        {
            if (m_AcquiredRecipes == null)
                m_AcquiredRecipes = new Dictionary<int, bool>();

            m_AcquiredRecipes[recipeID] = true;
        }

        public virtual void ResetRecipes()
        {
            m_AcquiredRecipes = null;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int KnownRecipes
        {
            get
            {
                if (m_AcquiredRecipes == null)
                    return 0;

                return m_AcquiredRecipes.Count;
            }
        }

        #endregion

        #region Buff Icons

        public void ResendBuffs()
        {
            if (!BuffInfo.Enabled || m_BuffTable == null)
                return;

            NetState state = this.NetState;

            if (state != null && state.BuffIcon)
            {
                foreach (BuffInfo info in m_BuffTable.Values)
                {
                    state.Send(new AddBuffPacket(this, info));
                }
            }
        }

        private Dictionary<BuffIcon, BuffInfo> m_BuffTable;

        public void AddBuff(BuffInfo b)
        {
            if (!BuffInfo.Enabled || b == null)
                return;

            RemoveBuff(b);	//Check & subsequently remove the old one.

            if (m_BuffTable == null)
                m_BuffTable = new Dictionary<BuffIcon, BuffInfo>();

            m_BuffTable.Add(b.ID, b);

            NetState state = this.NetState;

            if (state != null && state.BuffIcon)
            {
                state.Send(new AddBuffPacket(this, b));
            }
        }

        public void RemoveBuff(BuffInfo b)
        {
            if (b == null)
                return;

            RemoveBuff(b.ID);
        }

        public void RemoveBuff(BuffIcon b)
        {
            if (m_BuffTable == null || !m_BuffTable.ContainsKey(b))
                return;

            BuffInfo info = m_BuffTable[b];

            if (info.Timer != null && info.Timer.Running)
                info.Timer.Stop();

            m_BuffTable.Remove(b);

            NetState state = this.NetState;

            if (state != null && state.BuffIcon)
            {
                state.Send(new RemoveBuffPacket(this, b));
            }

            if (m_BuffTable.Count <= 0)
                m_BuffTable = null;
        }

        #endregion

        public void AutoStablePets()
        {
            if (AllFollowers.Count > 0)
            {
                for (int i = m_AllFollowers.Count - 1; i >= 0; --i)
                {
                    BaseCreature pet = AllFollowers[i] as BaseCreature;

                    if (pet == null || pet.ControlMaster == null)
                        continue;

                    if (pet.Summoned)
                    {
                        if (pet.Map != Map)
                        {
                            pet.PlaySound(pet.GetAngerSound());
                            Timer.DelayCall(TimeSpan.Zero, new TimerCallback(pet.Delete));
                        }
                        continue;
                    }

                    if (pet is IMount && ((IMount)pet).Rider != null)
                        continue;

                    if ((pet is PackLlama || pet is PackHorse || pet is Beetle || pet is HordeMinionFamiliar) && (pet.Backpack != null && pet.Backpack.Items.Count > 0))
                        continue;

                    if (pet is BaseEscortable)
                        continue;

                    pet.ControlTarget = null;
                    pet.ControlOrder = OrderType.Stay;
                    pet.Internalize();

                    pet.SetControlMaster(null);
                    pet.SummonMaster = null;

                    pet.IsStabled = true;

                    pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully happy

                    Stabled.Add(pet);
                    m_AutoStabled.Add(pet);
                }
            }
        }

        public void ClaimAutoStabledPets()
        {
            if (!Core.SE || m_AutoStabled.Count <= 0)
                return;

            if (!Alive)
            {
                SendLocalizedMessage(1076251); // Your pet was unable to join you while you are a ghost.  Please re-login once you have ressurected to claim your pets.
                return;
            }

            for (int i = m_AutoStabled.Count - 1; i >= 0; --i)
            {
                BaseCreature pet = m_AutoStabled[i] as BaseCreature;

                if (pet == null || pet.Deleted)
                {
                    pet.IsStabled = false;

                    if (Stabled.Contains(pet))
                        Stabled.Remove(pet);

                    continue;
                }

                if ((Followers + pet.ControlSlots) <= FollowersMax)
                {
                    pet.SetControlMaster(this);

                    if (pet.Summoned)
                        pet.SummonMaster = this;

                    pet.ControlTarget = this;
                    pet.ControlOrder = OrderType.Follow;

                    pet.MoveToWorld(Location, Map);

                    pet.IsStabled = false;

                    pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully Happy

                    if (Stabled.Contains(pet))
                        Stabled.Remove(pet);
                }
                else
                {
                    SendLocalizedMessage(1049612, pet.Name); // ~1_NAME~ remained in the stables because you have too many followers.
                }
            }

            m_AutoStabled.Clear();
        }


        public void ReturnToGalven()
        {
            this.MoveToWorld(new Point3D(1356, 1384, 0), Map.Felucca);
        }
        public void AddExileTimer(Commonwealth exilingCommonwealth)
        {
            ExileTimer toAdd = new ExileTimer(exilingCommonwealth, this, DateTime.Now);
            foreach (ExileTimer timer in mExileTimers)
            {
                if (timer.Township == exilingCommonwealth)
                    return;
            }
            toAdd.Start();
            mExileTimers.Add(toAdd);
        }
        public void RemoveExileTimer(ICommonwealth exilingTownship)
        {
            ExileTimer toRemove = null;
            foreach (ExileTimer timer in mExileTimers)
            {
                if (timer.Township == exilingTownship)
                {
                    toRemove = timer;
                }
            }

            if (toRemove != null)
            {
                toRemove.Stop();
                mExileTimers.Remove(toRemove);
            }
        }
        public override bool CheckAttack(Mobile m)
        {
            return (Utility.InUpdateRange(this, m) && CanSee(m) && InLOS(m) && !m.Hidden);
        }


        public class ExileTimer : Timer
        {
            private ICommonwealth mCommonwealth;
            private PlayerMobile mPlayer;
            private DateTime mExileTime;

            public ICommonwealth Township { get { return mCommonwealth; } set { mCommonwealth = value; } }
            public DateTime ExileTime { get { return mExileTime; } set { mExileTime = value; } }

            public ExileTimer(ICommonwealth commonwealth, PlayerMobile from, DateTime exileTime)
                : base(TimeSpan.FromMinutes(1.0), TimeSpan.FromHours(8))
            {
                mCommonwealth = commonwealth;
                mPlayer = from;
                mExileTime = exileTime;
            }

            protected override void OnTick()
            {
                base.OnTick();

                if (mExileTime.Add(TimeSpan.FromDays(FeatureList.Citizenship.ExileTimeInDays)) < DateTime.Now)
                {
                    if (mCommonwealth == null || mPlayer == null)
                    {
                        this.Stop();
                        return;
                    }
                    else
                    {
                        mCommonwealth.RemoveExile(mPlayer);
                        mPlayer.RemoveExileTimer(mCommonwealth);
                    }
                }
            }
        }

        public class DatabaseUpdateTimer : Timer
        {
            private PlayerMobile mPlayer;

            public DatabaseUpdateTimer(PlayerMobile player) :
                base(TimeSpan.FromSeconds(10.0),
                TimeSpan.FromSeconds(FeatureList.Database.CharacterUpdateTimeInSeconds))
            {
                mPlayer = player;
            }

            protected override void OnTick()
            {
                base.OnTick();

                if (mPlayer == null || mPlayer.Deleted)
                    return;

                //DatabaseController.UpdateCharacter(mPlayer);

                mPlayer.MurdersThisSession = 0;
                mPlayer.MurdererKillsThisSession = 0;
                mPlayer.MilitiaKillsThisSession = 0;
                mPlayer.MilitiaDeathsThisSession = 0;
                mPlayer.KillsThisSession = 0;
                mPlayer.DeathsThisSession = 0;
                mPlayer.OreMinedThisSession = 0;
                mPlayer.FishCaughtThisSession = 0;
                mPlayer.WoodHarvestedThisSession = 0;
                mPlayer.ItemsCraftedThisSession = 0;
                mPlayer.ChestsPickedThisSession = 0;
                mPlayer.mMapsDecodedThisSession = 0;
                mPlayer.SheepShornThisSession = 0;
                mPlayer.GuardWhacksThisSession = 0;
                mPlayer.StealAttemptsThisSession = 0;
                mPlayer.RecallsThisSession = 0;
                mPlayer.ResurrectionsThisSession = 0;
                mPlayer.HeroesCapturedThisSession = 0;
                mPlayer.HeroesRescuedThisSession = 0;
                mPlayer.WorldWarsFlagsCapturedThisSession = 0;
                mPlayer.BossesKilledThisSession = 0;
                mPlayer.PlayersKilledThisSession = 0;
                mPlayer.CorpsesCarvedThisSession = 0;
                mPlayer.HeadsTurnedInThisSession = 0;
                mPlayer.SilverEarnedThisSession = 0;
                mPlayer.ItemsImbuedThisSession = 0;
                mPlayer.StepsWalkedThisSession = 0;
                mPlayer.StealthStepsWalkedThisSession = 0;
                mPlayer.GuildKillsThisSession = 0;
                mPlayer.GuildDeathsThisSession = 0;
                mPlayer.PotionsConsumedThisSession = 0;
                mPlayer.GoldSpentThisSession = 0;
                mPlayer.CampfiresStartedThisSession = 0;
                mPlayer.TimesCriminalThisSession = 0;
                mPlayer.CorpsesInspectedThisSession = 0;
                mPlayer.PlayersAttackedThisSession = 0;
                mPlayer.EscortsTakenThisSession = 0;
                mPlayer.AnimalsTamedThisSession = 0;
                mPlayer.ProjectilesFiredThisSession = 0;
                mPlayer.BandagesUsedThisSession = 0;
                mPlayer.PoisonsAppliedThisSession = 0;
                mPlayer.PoisonsCastedThisSession = 0;
                mPlayer.CottonPickedThisSession = 0;
                mPlayer.HidesSkinnedThisSession = 0;
                mPlayer.FeathersPluckedThisSession = 0;
                mPlayer.SuccessfulStealsThisSession = 0;
            }
        }
    }
}