﻿using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using xSLx_Orbwalker;
using xSLx_Activator;

namespace Ultimate_Carry_Prevolution
{
    internal class Champion
    {
        //public Spell R2;

        //items & Summoners
        public static Items.Item DFG = Utility.Map.GetMap()._MapType == Utility.Map.MapType.TwistedTreeline
            ? new Items.Item(3188, 750)
            : new Items.Item(3128, 750);

        public static Items.Item Botrk = new Items.Item(3153, 450);
        public static Items.Item Bilge = new Items.Item(3144, 450);
        public static Items.Item Hex = new Items.Item(3146, 700);

        public static SpellSlot Ignite = ObjectManager.Player.GetSpellSlot("SummonerDot");

        public static Menu Menu;
        public IEnumerable<Obj_AI_Hero> AllHeros = ObjectManager.Get<Obj_AI_Hero>();
        public IEnumerable<Obj_AI_Hero> AllHerosEnemy = ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsEnemy);
        public IEnumerable<Obj_AI_Hero> AllHerosFriend = ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsAlly);
        public Spell E;
        public Spell E2;
        public Obj_AI_Hero MyHero = ObjectManager.Player;
        public Spell Q;
        public Spell Q2;
        public Spell R;
        public Spell W;

        public Champion()
        {
            LoadBasics();

            Game.OnGameUpdate += OnGameUpdateModes;
            Game.OnGameSendPacket += OnSendPacket;
            Game.OnGameProcessPacket += OnProcessPacket;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            AntiGapcloser.OnEnemyGapcloser += OnGapClose;
            GameObject.OnDelete += ObjSpellMissileOnOnDelete;
            GameObject.OnCreate += ObjSpellMissileOnOnCreate;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            xSLxOrbwalker.AfterAttack += OnAfterAttack;
            xSLxOrbwalker.OnAttack += OnAttack;
            xSLxOrbwalker.BeforeAttack += OnBeforeAttack;
        }

        private void OnGameUpdateModes(EventArgs args)
        {
            switch (xSLxOrbwalker.CurrentMode)
            {
                case xSLxOrbwalker.Mode.Combo:
                    OnCombo();
                    break;
                case xSLxOrbwalker.Mode.Harass:
                    OnHarass();
                    break;
                case xSLxOrbwalker.Mode.LaneClear:
                    OnLaneClear();
                    break;
                case xSLxOrbwalker.Mode.LaneFreeze:
                    OnLaneFreeze();
                    break;
                case xSLxOrbwalker.Mode.Lasthit:
                    OnLasthit();
                    break;
                case xSLxOrbwalker.Mode.Flee:
                    OnFlee();
                    break;
                case xSLxOrbwalker.Mode.None:
                    OnStandby();
                    break;
            }
            OnPassive();
        }

        private void LoadBasics()
        {
            Menu = new Menu("UC-Prevolution", MyHero.ChampionName + "_UCP", true);

            /*
			//the Team
			Menu.AddSubMenu(new Menu("UC-Team", "Credits"));
			Menu.SubMenu("Credits").AddItem(new MenuItem("Lexxes", "Lexxes (Austria)"));
			Menu.SubMenu("Credits").AddItem(new MenuItem("xSalice", "xSalice (US)"));
			Menu.SubMenu("Credits").AddItem(new MenuItem("InjectionDev", "InjectionDev (???)"));
            */
            //TargetSelector
            var targetselectormenu = new Menu("TargetSelector", "Common_TargetSelector");
            SimpleTs.AddToMenu(targetselectormenu);
            Menu.AddSubMenu(targetselectormenu);

            //PacketMenu
            Menu.AddSubMenu(new Menu("Packet Setting", "Packets"));
            Menu.SubMenu("Packets")
                .AddItem(new MenuItem(MyHero.ChampionName + "usePackets", "Use Packets").SetValue(false));

            //xSLxActivator
            var activatorMenu = new Menu("xSLx Activator", "Activatort1");
            xSLxActivator.AddToMenu(activatorMenu);
            Menu.AddSubMenu(activatorMenu);

            //xSLxOrbwalker (Based on Common)
            var orbwalkerMenu = new Menu("xSLx Orbwalker", "Orbwalkert1");
            xSLxOrbwalker.AddToMenu(orbwalkerMenu);
            Menu.AddSubMenu(orbwalkerMenu);
        }

        public bool UsePackets()
        {
            return Menu.Item(MyHero.ChampionName + "usePackets").GetValue<bool>();
        }

        public void Use_Item(Obj_AI_Hero target, Items.Item item)
        {
            if (target.IsValidTarget(item.Range) && Items.CanUseItem(item.Id))
            {
                Items.UseItem(item.Id);
            }
        }

        public void Use_DFG(Obj_AI_Hero target)
        {
            Use_Item(target, DFG);
        }

        public void Use_Hex(Obj_AI_Hero target)
        {
            Use_Item(target, Hex);
        }

        public void Use_Botrk(Obj_AI_Hero target)
        {
            Use_Item(target, Botrk);
        }

        public void Use_Bilge(Obj_AI_Hero target)
        {
            Use_Item(target, Bilge);
        }

        public void Use_Ignite(Obj_AI_Hero target)
        {
            if (Ignite != SpellSlot.Unknown && MyHero.SummonerSpellbook.CanUseSpell(Ignite) == SpellState.Ready &&
                target.IsValidTarget(650))
            {
                MyHero.SummonerSpellbook.CastSpell(Ignite, target);
            }
        }

        public void Cast_Shield_onFriend(Spell spell, int percent, bool skillshot = false)
        {
            if (!spell.IsReady())
            {
                return;
            }

            foreach (
                var friend in
                    AllHerosFriend.Where(hero => hero.Distance(MyHero) <= spell.Range)
                        .Where(
                            friend => friend.Health / friend.MaxHealth * 100 <= percent && EnemysinRange(600, 1, friend))
                )
            {
                if (skillshot)
                {
                    spell.Cast(spell.GetPrediction(friend).CastPosition, UsePackets());
                }
                else
                {
                    spell.CastOnUnit(friend, UsePackets());
                }
                return;
            }
        }

        public Obj_AI_Hero Cast_BasicSkillshot_Enemy(Spell spell,
            SimpleTs.DamageType prio = SimpleTs.DamageType.True,
            float extrarange = 0,
            HitChance hitchance = HitChance.Medium)
        {

            var target = SimpleTs.GetTarget(spell.Range + extrarange, prio);
         
            if (!spell.IsReady() || target == null || !target.IsValidTarget(spell.Range + extrarange) || spell.GetPrediction(target).Hitchance < hitchance)
            {
                return null;
            }

            spell.UpdateSourcePosition();
            spell.Cast(target, UsePackets());
            return target;
        }

        public void Cast_BasicSkillshot_AOE_Farm(Spell spell, int extrawidth = 0)
        {
            if (!spell.IsReady())
            {
                return;
            }
            var minions = MinionManager.GetMinions(
                MyHero.ServerPosition,
                spell.Type == SkillshotType.SkillshotLine ? spell.Range : spell.Range + ((spell.Width + extrawidth) / 2),
                MinionTypes.All, MinionTeam.NotAlly);

            if (minions.Count == 0)
            {
                return;
            }

            MinionManager.FarmLocation castPostion;

            switch (spell.Type)
            {
                case SkillshotType.SkillshotCircle:
                    castPostion =
                        MinionManager.GetBestCircularFarmLocation(
                            minions.Select(minion => minion.ServerPosition.To2D()).ToList(), spell.Width + extrawidth,
                            spell.Range);
                    break;
                case SkillshotType.SkillshotLine:
                    castPostion =
                        MinionManager.GetBestLineFarmLocation(
                            minions.Select(minion => minion.ServerPosition.To2D()).ToList(), spell.Width, spell.Range);
                    break;
                default:
                    return;
            }

            spell.UpdateSourcePosition();
            
            if (castPostion.MinionsHit >= 2 || minions.Any(x => x.Team == GameObjectTeam.Neutral))
            {
                spell.Cast(castPostion.Position, UsePackets());
            }
        }

        public static bool IsInsideEnemyTower(Vector3 position)
        {
            return
                ObjectManager.Get<Obj_AI_Turret>()
                    .Any(tower => tower.IsEnemy && tower.Health > 0 && tower.Position.Distance(position) < 775);
        }

        public void AddSpelltoMenu(Menu menu, string name, bool state)
        {
            var showname = name;
            if (name == "Q" || name == "W" || name == "E" || name == "R")
            {
                showname = "Use " + name;
            }
            menu.AddItem(
                new MenuItem(
                    MyHero.ChampionName + menu.Name + "_" + name.Replace(" ", "_").Replace("%", "pct"), showname)
                    .SetValue(state));
        }

        public void AddSpelltoMenu(Menu menu, string name, int value, int minValue = 0, int maxValue = 100)
        {
            menu.AddItem(
                new MenuItem(MyHero.ChampionName + menu.Name + "_" + name.Replace(" ", "_").Replace("%", "pct"), name)
                    .SetValue(new Slider(value, minValue, maxValue)));
        }

        public int GetValue(string name)
        {
            try
            {
                return
                    Menu.Item(
                        MyHero.ChampionName + xSLxOrbwalker.CurrentMode + "_" +
                        name.Replace(" ", "_").Replace("%", "pct")).GetValue<Slider>().Value;
            }
            catch
            {
                return 0;
            }
        }

        public bool IsSpellActive(string name)
        {
            try
            {
                return
                    Menu.Item(
                        MyHero.ChampionName + xSLxOrbwalker.CurrentMode + "_" +
                        name.Replace(" ", "_").Replace("%", "pct")).GetValue<bool>();
            }
            catch
            {
                return false;
            }
        }

        public void AddMisc(Menu menu, string name, bool state)
        {
            menu.AddItem(
                new MenuItem(MyHero.ChampionName + menu.Name + "_" + name.Replace(" ", "_").Replace("%", "pct"), name)
                    .SetValue(state));
        }

        public bool GetMiscBool(string name)
        {
            try
            {
                return
                    Menu.Item(MyHero.ChampionName + "Misc_" + name.Replace(" ", "_").Replace("%", "pct"))
                        .GetValue<bool>();
            }
            catch
            {
                return false;
            }
        }

        public void AddManaManagertoMenu(Menu menu, int standard)
        {
            menu.AddItem(
                new MenuItem(MyHero.ChampionName + menu.Name + "_Manamanager", "Mana-Manager").SetValue(
                    new Slider(standard)));
        }

        public bool ManaManagerAllowCast()
        {
            try
            {
                if (GetManaPercent() <
                    Menu.Item(MyHero.ChampionName + xSLxOrbwalker.CurrentMode + "_Manamanager").GetValue<Slider>().Value)
                {
                    return false;
                }
            }
            catch
            {
                return true;
            }
            return true;
        }

        public Vector3 GetReversePosition(Vector3 positionMe, Vector3 positionEnemy)
        {
            float x = positionMe.X - positionEnemy.X;
            float y = positionMe.Y - positionEnemy.Y;
            return new Vector3(positionMe.X + x, positionMe.Y + y, positionMe.Z);
        }

        public float GetManaPercent(Obj_AI_Hero unit = null)
        {
            if (unit == null)
            {
                unit = MyHero;
            }
            return (unit.Mana / unit.MaxMana) * 100f;
        }

        public float GetHealthPercent(Obj_AI_Hero unit)
        {
            if (unit == null)
            {
                unit = MyHero;
            }

            return (unit.Health / unit.MaxHealth) * 100f;
        }

        public string GetSpellName(SpellSlot slot, Obj_AI_Hero unit = null)
        {
            return unit != null ? unit.Spellbook.GetSpell(slot).Name : MyHero.Spellbook.GetSpell(slot).Name;
        }

        public bool EnemysinRange(float range, int min = 1, Obj_AI_Hero unit = null)
        {
            if (unit == null)
            {
                unit = MyHero;
            }

            return min <= AllHerosEnemy.Count(hero => hero.IsValidTarget() && hero.Distance(unit) < range);
        }

        public bool EnemysinRange(float range, int min, Vector3 pos)
        {
            return min <=
                   AllHerosEnemy.Count(
                       hero => hero.Position.Distance(pos) < range && hero.IsValidTarget() && !hero.IsDead);
        }

        public Vector2 V2E(Vector3 from, Vector3 direction, float distance)
        {
            return from.To2D() + distance * Vector3.Normalize(direction - from).To2D();
        }

        public Vector3 V3E(Vector3 from, Vector3 direction, float distance)
        {
            return from + distance * Vector3.Normalize(direction - from);
        }

        public bool HasBuff(Obj_AI_Base target, string buffName)
        {
            return target.Buffs.Any(buff => buff.Name == buffName);
        }

        public bool IsWall(Vector2 pos)
        {
            return (NavMesh.GetCollisionFlags(pos.X, pos.Y) == CollisionFlags.Wall ||
                    NavMesh.GetCollisionFlags(pos.X, pos.Y) == CollisionFlags.Building);
        }

        public bool IsPassWall(Vector3 start, Vector3 end)
        {
            double count = Vector3.Distance(start, end);
            for (uint i = 0; i <= count; i += 10)
            {
                if (IsWall(V2E(start, end, i)))
                {
                    return true;
                }
            }
            return false;
        }

        public virtual void OnDraw(EventArgs args)
        {
            OnDraw();
        }

        public virtual void OnDraw()
        {
            // Virtual OnDraw
        }

        public virtual void OnSendPacket(GamePacketEventArgs args)
        {
            // Virtual OnSendPacket
        }

        public virtual void OnProcessPacket(GamePacketEventArgs args)
        {
            // Virtual OnSendPacket
        }

        public virtual void OnAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            // Virtual OnAttack
        }

        public virtual void OnAfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            // Virtual OnAfterAttack
        }

        public virtual void OnBeforeAttack(xSLxOrbwalker.BeforeAttackEventArgs args)
        {
            // Virtual OnBeforeAttack
        }

        public virtual void OnProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs spell)
        {
            // Virtual Game_OnProcessSpell
        }

        public virtual void ObjSpellMissileOnOnCreate(GameObject sender, EventArgs args)
        {
            // Virtual ObjSpellMissileOnOnCreate
        }

        public virtual void ObjSpellMissileOnOnDelete(GameObject sender, EventArgs args)
        {
            // Virtual ObjSpellMissileOnOnDelete
        }

        public virtual void OnGapClose(ActiveGapcloser gapcloser)
        {
            // Virtual OnGapClose
        }

        public virtual void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            // Virtual OnPossibleToInterrupt
        }

        public virtual void OnStandby()
        {
            // Virtual OnStandby
        }

        public virtual void OnFlee()
        {
            // Virtual OnFlee
        }

        public virtual void OnLasthit()
        {
            // Virtual OnLasthit
        }

        public virtual void OnLaneFreeze()
        {
            // Virtual OnLaneFreeze
        }

        public virtual void OnLaneClear()
        {
            // Virtual OnLaneClear
        }

        public virtual void OnHarass()
        {
            // Virtual OnHarass
        }

        public virtual void OnCombo()
        {
            // Virtual OnCombo
        }

        public virtual void OnGameUpdate(EventArgs args)
        {
            // Virtual OnGameUpdate
        }

        public virtual void OnPassive()
        {
            // Virtual OnPassive
        }
    }
}