﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LeagueSharp;
using LeagueSharp.Common;
using DevCommom;
using SharpDX;

/*
 * ##### DevCassio Mods #####
 * 
 * + AntiGapCloser with R when LowHealth
 * + Interrupt Danger Spell with R when LowHealth
 * + LastHit E On Posioned Minions
 * + Ignite KS
 * + Menu No-Face Exploit (PacketCast)
 * + Skin Hack
 * + Show E Damage on Enemy HPBar
 * + Assisted Ult
 * + Block Ult if will not hit
 * + Auto Ult Enemy Under Tower
 * + Auto Ult if will hit X
 * + Jungle Clear
*/

namespace DevCassio
{
    class Program
    {
        public const string ChampionName = "cassiopeia";

        public static Menu Config;
        public static Orbwalking.Orbwalker Orbwalker;
        public static List<Spell> SpellList = new List<Spell>();
        public static Obj_AI_Hero Player;
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        public static List<Obj_AI_Base> MinionList;
        public static SkinManager SkinManager;
        public static IgniteManager IgniteManager;

        public static bool mustDebug = false;


        static void Main(string[] args)
        {
            LeagueSharp.Common.CustomEvents.Game.OnGameLoad += onGameLoad;
        }

        private static void OnTick(EventArgs args)
        {
            try
            {
                switch (Orbwalker.ActiveMode)
                {
                    case Orbwalking.OrbwalkingMode.Combo:
                        Combo();
                        BurstCombo();
                        break;
                    case Orbwalking.OrbwalkingMode.Mixed:
                        Harass();
                        break;
                    case Orbwalking.OrbwalkingMode.LaneClear:
                        JungleClear();
                        WaveClear();
                        break;
                    case Orbwalking.OrbwalkingMode.LastHit:
                        Freeze();
                        break;
                    default:
                        break;
                }

                if (Config.Item("UseUltUnderTower").GetValue<bool>())
                    UseUltUnderTower();

                SkinManager.Update();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (mustDebug)
                    Game.PrintChat(ex.Message);
            }
        }


        public static void BurstCombo()
        {
            if (mustDebug)
                Game.PrintChat("BurstCombo Start");

            var eTarget = SimpleTs.GetTarget(R.Range, SimpleTs.DamageType.Magical);

            if (eTarget == null)
                return;

            var useQ = Config.Item("UseQCombo").GetValue<bool>();
            var useW = Config.Item("UseWCombo").GetValue<bool>();
            var useE = Config.Item("UseECombo").GetValue<bool>();
            var useR = Config.Item("UseRCombo").GetValue<bool>();
            var useIgnite = Config.Item("UseIgnite").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            double totalComboDamage = 0;
            totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.R);
            totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.Q);
            totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.E) * 3;
            totalComboDamage += IgniteManager.IsReady() ? Player.GetSummonerSpellDamage(eTarget, Damage.SummonerSpell.Ignite) : 0;

            double totalManaCost = 0;
            totalManaCost += Player.Spellbook.GetSpell(SpellSlot.R).ManaCost;
            totalManaCost += Player.Spellbook.GetSpell(SpellSlot.Q).ManaCost;

            if (mustDebug)
            {
                Game.PrintChat("BurstCombo Damage {0}/{1} {2}", Convert.ToInt32(totalComboDamage), Convert.ToInt32(eTarget.Health), eTarget.Health < totalComboDamage ? "BustKill" : "Harras");
                Game.PrintChat("BurstCombo Mana {0}/{1} {2}", Convert.ToInt32(totalManaCost), Convert.ToInt32(eTarget.Mana), Player.Mana >= totalManaCost ? "Mana OK" : "No Mana");
            }

            if (Q.IsReady(2000) && R.IsReady() && useR && eTarget.IsValidTarget(R.Range))
            {
                if (eTarget.Health < totalComboDamage && Player.Mana >= totalManaCost)
                {
                    R.Cast(eTarget.ServerPosition, packetCast);
                    IgniteManager.Cast(eTarget);
                }
            }
        }

        public static void Combo()
        {
            if (mustDebug)
                Game.PrintChat("Combo Start");

            var eTarget = SimpleTs.GetTarget(W.Range, SimpleTs.DamageType.Magical);

            if (eTarget == null)
                return;

            var useQ = Config.Item("UseQCombo").GetValue<bool>();
            var useW = Config.Item("UseWCombo").GetValue<bool>();
            var useE = Config.Item("UseECombo").GetValue<bool>();
            var useR = Config.Item("UseRCombo").GetValue<bool>();
            var useIgnite = Config.Item("UseIgnite").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            var RMinHit = Config.Item("RMinHit").GetValue<Slider>().Value;
            var RMinHitFacing = Config.Item("RMinHitFacing").GetValue<Slider>().Value;

            if (eTarget.IsValidTarget(R.Range) && R.IsReady() && useR)
            {
                var castPred = R.GetPrediction(eTarget, true, R.Range);
                var enemiesHit = DevHelper.GetEnemyList().Where(x => R.WillHit(eTarget, castPred.CastPosition));
                var enemiesFacing = enemiesHit.Where(x => x.IsFacing());

                if (enemiesHit.Count() >= RMinHit && enemiesFacing.Count() >= RMinHitFacing)
                    R.Cast(castPred.CastPosition, packetCast);
            }

            if (eTarget.IsValidTarget(E.Range) && E.IsReady() && useE)
            {
                if (eTarget.HasBuffOfType(BuffType.Poison) || Player.GetSpellDamage(eTarget, SpellSlot.E) > eTarget.Health)
                {
                    E.CastOnUnit(eTarget, packetCast);
                }
            }

            if (eTarget.IsValidTarget(Q.Range) && Q.IsReady() && useQ)
            {
                Q.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium, packetCast);
            }

            if (Config.Item("UseWCombo").GetValue<bool>())
                useW = (!eTarget.HasBuffOfType(BuffType.Poison) || (!eTarget.IsValidTarget(Q.Range) && eTarget.IsValidTarget(W.Range)));

            if (eTarget.IsValidTarget(W.Range) && W.IsReady() && useW)
            {
                W.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium, packetCast);
            }

            if (IgniteManager.HasIgnite && IgniteManager.IsReady() && IgniteManager.CanKill(eTarget))
            {
                IgniteManager.Cast(eTarget);
            }

        }

        public static void Harass()
        {
            if (mustDebug)
                Game.PrintChat("Harass Start");

            var eTarget = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Magical);

            if (eTarget == null)
                return;

            var useQ = Config.Item("UseQHarass").GetValue<bool>();
            var useW = Config.Item("UseWHarass").GetValue<bool>();
            var useE = Config.Item("UseEHarass").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var HarassMinMana = Config.Item("HarassMinMana").GetValue<Slider>().Value;

            if (mustDebug)
                Game.PrintChat("Harass Target -> " + eTarget.SkinName);

            if (eTarget.IsValidTarget(E.Range) && E.IsReady() && useE)
            {
                if (eTarget.HasBuffOfType(BuffType.Poison) || Damage.GetSpellDamage(Player, eTarget, SpellSlot.E) > eTarget.Health)
                {
                    E.CastOnUnit(eTarget, packetCast);
                }
            }

            if (eTarget.IsValidTarget(Q.Range) && Q.IsReady() && useQ && Player.GetManaPerc() >= HarassMinMana)
            {
                Q.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium, packetCast);
            }

            if (Config.Item("UseWHarass").GetValue<bool>())
                useW = (!eTarget.HasBuffOfType(BuffType.Poison) || (!eTarget.IsValidTarget(Q.Range) && eTarget.IsValidTarget(W.Range)));

            if (eTarget.IsValidTarget(W.Range) && W.IsReady() && useW && Player.GetManaPerc() >= HarassMinMana)
            {
                W.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium, packetCast);
            }

            if (mustDebug)
                Game.PrintChat("Harass Finish");
        }

        public static void WaveClear()
        {
            if (mustDebug)
                Game.PrintChat("WaveClear Start");

            var useQ = Config.Item("UseQLaneClear").GetValue<bool>();
            var useW = Config.Item("UseWLaneClear").GetValue<bool>();
            var useE = Config.Item("UseELaneClear").GetValue<bool>();
            var UseELastHitLaneClear = Config.Item("UseELastHitLaneClear").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            if (Q.IsReady() && useQ)
            {
                var allMinionsQ = MinionManager.GetMinions(Player.ServerPosition, Q.Range + Q.Width, MinionTypes.All);
                var allMinionsQNonPoisoned = allMinionsQ.Where(x => !x.HasBuffOfType(BuffType.Poison)).ToList();

                var farmNonPoisoned = Q.GetCircularFarmLocation(allMinionsQNonPoisoned, Q.Width * 0.8f);
                var farmAll = Q.GetCircularFarmLocation(allMinionsQ, Q.Width * 0.8f);

                if (farmNonPoisoned.MinionsHit >= 3)
                    Q.Cast(farmNonPoisoned.Position, packetCast);
                else if (farmAll.MinionsHit >= 2 || allMinionsQ.Count == 1)
                    Q.Cast(farmAll.Position, packetCast);
            }

            if (W.IsReady() && useW)
            {
                var allMinionsW = MinionManager.GetMinions(Player.ServerPosition, W.Range + W.Width, MinionTypes.All);
                var allMinionsWNonPoisoned = allMinionsW.Where(x => !x.HasBuffOfType(BuffType.Poison)).ToList();

                var farmNonPoisoned = W.GetCircularFarmLocation(allMinionsWNonPoisoned, W.Width);
                var farmAll = W.GetCircularFarmLocation(allMinionsW, W.Width);

                if (farmNonPoisoned.MinionsHit >= 3)
                    W.Cast(farmNonPoisoned.Position, packetCast);
                else if (farmAll.MinionsHit >= 2 || allMinionsW.Count == 1)
                    W.Cast(farmAll.Position, packetCast);
            }

            if (E.IsReady() && useE)
            {
                MinionList = MinionManager.GetMinions(Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health);

                foreach (var minion in MinionList)
                {
                    if (minion.IsValidTarget(E.Range) && minion.HasBuffOfType(BuffType.Poison))
                    {
                        if (UseELastHitLaneClear)
                        {
                            if (Player.GetSpellDamage(minion, SpellSlot.E) * 0.9d > minion.Health)
                                E.CastOnUnit(minion, packetCast);
                        }
                        else    
                        {
                            E.CastOnUnit(minion, packetCast);
                        }
                    }
                }
            }
        }

        public static void Freeze()
        {
            if (mustDebug)
                Game.PrintChat("Freeze Start");

            MinionList = MinionManager.GetMinions(Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health);

            if (MinionList.Count == 0)
                return;

            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var useE = Config.Item("UseEFreeze").GetValue<bool>();

            if (useE)
            {
                foreach (var minion in MinionList)
                {
                    if (E.IsReady() && Player.GetSpellDamage(minion, SpellSlot.E) * 0.9d > minion.Health && minion.IsValidTarget(E.Range))
                    {
                        if (minion.HasBuffOfType(BuffType.Poison))
                        {
                            E.CastOnUnit(minion, packetCast);
                        }
                    }
                }
            }
        }

        private static void JungleClear()
        {
            var mobs = MinionManager.GetMinions(Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

            if (mobs.Count == 0)
                return;

            var UseQJungleClear = Config.Item("UseQJungleClear").GetValue<bool>();
            var UseEJungleClear = Config.Item("UseEJungleClear").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            var mob = mobs.First();

            if (UseQJungleClear && Q.IsReady() && mob.IsValidTarget(Q.Range))
            {
                Q.Cast(mob.ServerPosition, packetCast);
            }

            if (UseEJungleClear && E.IsReady() && mob.HasBuffOfType(BuffType.Poison) && mob.IsValidTarget(E.Range))
            {
                E.CastOnUnit(mob, packetCast);
            }
        }

        private static void UseUltUnderTower()
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            foreach (var eTarget in DevHelper.GetEnemyList())
            {
                if (eTarget.IsValidTarget(R.Range) && eTarget.IsUnderEnemyTurret() && R.IsReady())
                {
                    //if (R.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium, packetCast))
                    //    Game.PrintChat("Ult Under Tower!");
                    R.Cast(eTarget.ServerPosition, packetCast);
                }
            }
        }

        public static void CastAssistedUlt()
        {
            if (mustDebug)
                Game.PrintChat("CastAssistedUlt Start");

            var eTarget = Player.GetNearestEnemy();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            if (eTarget.IsValidTarget(R.Range) && R.IsReady())
            {
                //if (R.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium, packetCast))
                //    Game.PrintChat(string.Format("AssistedUlt fired"));
                R.Cast(eTarget.ServerPosition, packetCast);
            }

            if (mustDebug)
                Game.PrintChat("CastAssistedUlt Finish");
        }

        private static void onGameLoad(EventArgs args)
        {
            try
            {
                Player = ObjectManager.Player;

                if (!Player.ChampionName.ToLower().Contains(ChampionName))
                    return;

                InitializeSpells();

                InitializeSkinManager();

                InitializeMainMenu();

                InitializeAttachEvents();

                Game.PrintChat(string.Format("<font color='#F7A100'>DevCassio Loaded v{0}</font>", Assembly.GetExecutingAssembly().GetName().Version));
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (mustDebug)
                    Game.PrintChat(ex.ToString());
            }
        }

        private static void InitializeAttachEvents()
        {
            if (mustDebug)
                Game.PrintChat("InitializeAttachEvents Start");

            Game.OnGameUpdate += OnTick;
            Game.OnGameSendPacket += Game_OnGameSendPacket;
            Game.OnWndProc += Game_OnWndProc;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += Interrupter_OnPossibleToInterrupt;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;

            Config.Item("EDamage").ValueChanged += (object sender, OnValueChangeEventArgs e) => { Utility.HpBarDamageIndicator.Enabled = e.GetNewValue<bool>(); };
            if (Config.Item("EDamage").GetValue<bool>())
            {
                Utility.HpBarDamageIndicator.DamageToUnit += GetEDamage;
                Utility.HpBarDamageIndicator.Enabled = true;
            }

            Config.Item("UltRange").ValueChanged += (object sender, OnValueChangeEventArgs e) => { R.Range = e.GetNewValue<Slider>().Value; };
            R.Range = Config.Item("UltRange").GetValue<Slider>().Value;

            if (mustDebug)
                Game.PrintChat("InitializeAttachEvents Finish");
        }

        static void Orbwalking_BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            var useAA = Config.Item("UseAACombo").GetValue<bool>();

            if (!useAA)
                args.Process = false;

            //if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            //{
            //    var useQ = Config.Item("UseQCombo").GetValue<bool>();
            //    var useW = Config.Item("UseWCombo").GetValue<bool>();
            //    var useAA = Config.Item("UseAACombo").GetValue<bool>();

            //    if (!useAA)
            //        args.Process = false;
            //    else if (Player.GetNearestEnemy().IsValidTarget(W.Range) && ((useQ && Q.IsReady()) || (useW && W.IsReady())))
            //            args.Process = false;
            //}
            //else
            //    if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed)
            //    {
            //        var useQ = Config.Item("UseQHarass").GetValue<bool>();
            //        var useW = Config.Item("UseWHarass").GetValue<bool>();

            //        if (Player.GetNearestEnemy().IsValidTarget(W.Range) && ((useQ && Q.IsReady()) || (useW && W.IsReady())))
            //            args.Process = false;
            //    }
        }

        private static void InitializeSpells()
        {
            if (mustDebug)
                Game.PrintChat("InitializeSpells Start");

            Q = new Spell(SpellSlot.Q, 850);
            Q.SetSkillshot(0.6f, 110, float.MaxValue, false, SkillshotType.SkillshotCircle);

            W = new Spell(SpellSlot.W, 850);
            W.SetSkillshot(0.5f, 150, 2500, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 700);
            E.SetTargetted(0.1f, float.MaxValue);

            R = new Spell(SpellSlot.R, 850);
            R.SetSkillshot(0.6f, (float)(80 * Math.PI / 180), float.MaxValue, false, SkillshotType.SkillshotCone);

            IgniteManager = new IgniteManager();

            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            if (mustDebug)
                Game.PrintChat("InitializeSpells Finish");
        }

        private static void InitializeSkinManager()
        {
            if (mustDebug)
                Game.PrintChat("InitializeSkinManager Start");

            SkinManager = new SkinManager();
            SkinManager.Add("Cassio");
            SkinManager.Add("Desperada Cassio");
            SkinManager.Add("Siren Cassio");
            SkinManager.Add("Mythic Cassio");
            SkinManager.Add("Jade Fang Cassio");

            if (mustDebug)
                Game.PrintChat("InitializeSkinManager Finish");
        }

        static void Game_OnGameSendPacket(GamePacketEventArgs args)
        {
            var BlockUlt = Config.Item("BlockUlt").GetValue<bool>();

            if (BlockUlt && args.PacketData[0] == Packet.C2S.Cast.Header)
            {
                var decodedPacket = Packet.C2S.Cast.Decoded(args.PacketData);
                if (decodedPacket.SourceNetworkId == Player.NetworkId && decodedPacket.Slot == SpellSlot.R)
                {
                    Vector3 vecCast = new Vector3(decodedPacket.ToX, decodedPacket.ToY, 0);
                    var query = DevHelper.GetEnemyList().Where(x => R.WillHit(x, vecCast));

                    if (query.Count() == 0)
                    {
                        args.Process = false;
                        Game.PrintChat(string.Format("Ult Blocked"));
                    }
                }
            }
        }

        static void Game_OnWndProc(WndEventArgs args)
        {
            if (MenuGUI.IsChatOpen)
                return;

            var UseAssistedUlt = Config.Item("UseAssistedUlt").GetValue<bool>();
            var AssistedUltKey = Config.Item("AssistedUltKey").GetValue<KeyBind>().Key;

            if (UseAssistedUlt && args.WParam == AssistedUltKey)
            {
                if (mustDebug)
                    Game.PrintChat("CastAssistedUlt");

                args.Process = false;
                CastAssistedUlt();
            }
        }

        static void Interrupter_OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var RInterrupetSpell = Config.Item("RInterrupetSpell").GetValue<bool>();
            var RAntiGapcloserMinHealth = Config.Item("RAntiGapcloserMinHealth").GetValue<Slider>().Value;

            if (RInterrupetSpell && Player.GetHealthPerc() < RAntiGapcloserMinHealth && unit.IsValidTarget(R.Range) && spell.DangerLevel == InterruptableDangerLevel.High)
            {
                if (R.CastIfHitchanceEquals(unit, unit.IsMoving ? HitChance.High : HitChance.Medium, packetCast))
                    Game.PrintChat(string.Format("OnPosibleToInterrupt -> RInterrupetSpell on {0} !", unit.SkinName));
            }
        }

        static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var RAntiGapcloser = Config.Item("RAntiGapcloser").GetValue<bool>();
            var RAntiGapcloserMinHealth = Config.Item("RAntiGapcloserMinHealth").GetValue<Slider>().Value;

            if (RAntiGapcloser && Player.GetHealthPerc() <= RAntiGapcloserMinHealth && gapcloser.Sender.IsValidTarget(R.Range) && R.IsReady())
            {
                R.Cast(gapcloser.Sender.ServerPosition, packetCast);
                //if (R.CastIfHitchanceEquals(gapcloser.Sender, gapcloser.Sender.IsMoving ? HitChance.High : HitChance.Medium, packetCast))
                //    Game.PrintChat(string.Format("OnEnemyGapcloser -> RAntiGapcloser on {0} !", gapcloser.Sender.SkinName));
            }
        }

        private static float GetEDamage(Obj_AI_Hero hero)
        {
            return (float)Damage.GetSpellDamage(Player, hero, SpellSlot.E);
        }

        private static void OnDraw(EventArgs args)
        {
            foreach (var spell in SpellList)
            {
                var menuItem = Config.Item(spell.Slot + "Range").GetValue<Circle>();
                if (menuItem.Active && spell.IsReady())
                {
                    Utility.DrawCircle(ObjectManager.Player.Position, spell.Range, menuItem.Color);
                }
            }
        }


        private static void InitializeMainMenu()
        {
            if (mustDebug)
                Game.PrintChat("InitializeMainMenu Start");

            Config = new Menu("DevCassio", "DevCassio", true);

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));

            Config.AddSubMenu(new Menu("Combo", "Combo"));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseQCombo", "Use Q").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseWCombo", "Use W").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseECombo", "Use E").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseRCombo", "Use R").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseIgnite", "Use Ignite").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseAACombo", "Use AA").SetValue(true));

            Config.AddSubMenu(new Menu("Harass", "Harass"));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseQHarass", "Use Q").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseWHarass", "Use W").SetValue(false));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseEHarass", "Use E").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("HarassMinMana", "Harras Min Mana").SetValue(new Slider(10, 0, 100)));

            Config.AddSubMenu(new Menu("Freeze", "Freeze"));
            Config.SubMenu("Freeze").AddItem(new MenuItem("UseEFreeze", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("LaneClear", "LaneClear"));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseQLaneClear", "Use Q").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseWLaneClear", "Use W").SetValue(false));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseELaneClear", "Use E").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseELastHitLaneClear", "Use E Only LastHit").SetValue(true));

            Config.AddSubMenu(new Menu("JungleClear", "JungleClear"));
            Config.SubMenu("JungleClear").AddItem(new MenuItem("UseQJungleClear", "Use Q").SetValue(true));
            Config.SubMenu("JungleClear").AddItem(new MenuItem("UseEJungleClear", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Gapcloser", "Gapcloser"));
            Config.SubMenu("Gapcloser").AddItem(new MenuItem("RAntiGapcloser", "R AntiGapcloser").SetValue(true));
            Config.SubMenu("Gapcloser").AddItem(new MenuItem("RInterrupetSpell", "R InterruptSpell").SetValue(true));
            Config.SubMenu("Gapcloser").AddItem(new MenuItem("RAntiGapcloserMinHealth", "R AntiGapcloser Min Health").SetValue(new Slider(60, 0, 100)));

            Config.AddSubMenu(new Menu("Misc", "Misc"));
            Config.SubMenu("Misc").AddItem(new MenuItem("PacketCast", "No-Face Exploit (PacketCast)").SetValue(true));

            Config.AddSubMenu(new Menu("Ultimate", "Ultimate"));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("UseAssistedUlt", "Use AssistedUlt").SetValue(true));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("AssistedUltKey", "Assisted Ult Key").SetValue((new KeyBind("R".ToCharArray()[0], KeyBindType.Press))));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("BlockUlt", "Block Ult will Not Hit").SetValue(true));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("UseUltUnderTower", "Ult Enemy Under Tower").SetValue(true));
            
            Config.SubMenu("Ultimate").AddItem(new MenuItem("UltRange", "Ultimate Range").SetValue(new Slider(700, 0, 850)));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("RMinHit", "Min Enemies Hit").SetValue(new Slider(2, 1, 5)));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("RMinHitFacing", "Min Enemies Facing").SetValue(new Slider(1, 1, 5)));

            Config.AddSubMenu(new Menu("Drawings", "Drawings"));
            Config.SubMenu("Drawings").AddItem(new MenuItem("QRange", "Q Range").SetValue(new Circle(true, System.Drawing.Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("WRange", "W Range").SetValue(new Circle(false, System.Drawing.Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("ERange", "E Range").SetValue(new Circle(false, System.Drawing.Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("RRange", "R Range").SetValue(new Circle(false, System.Drawing.Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("EDamage", "Show E Damage on HPBar").SetValue(true));

            SkinManager.AddToMenu(ref Config);

            Config.AddToMainMenu();

            if (mustDebug)
                Game.PrintChat("InitializeMainMenu Finish");
        }
    }
}