﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace DevCommom
{
    public static class DevHelper
    {

        public static List<Obj_AI_Hero> GetEnemyList()
        {
            return ObjectManager.Get<Obj_AI_Hero>()
                .Where(x => x.IsEnemy && x.IsValid)
                .OrderBy(x => ObjectManager.Player.ServerPosition.Distance(x.ServerPosition))
                .ToList();
        }

        public static List<Obj_AI_Hero> GetAllyList()
        {
            return ObjectManager.Get<Obj_AI_Hero>()
                .Where(x => x.IsAlly && x.IsValid)
                .OrderBy(x => ObjectManager.Player.ServerPosition.Distance(x.ServerPosition))
                .ToList();
        }

        public static Obj_AI_Hero GetNearestEnemy(this Obj_AI_Base unit)
        {
            return ObjectManager.Get<Obj_AI_Hero>()
                .Where(x => x.IsEnemy && x.IsValid)
                .OrderBy(x => unit.ServerPosition.Distance(x.ServerPosition))
                .FirstOrDefault();
        }

        public static Obj_AI_Hero GetNearestEnemyFromUnit(this Obj_AI_Base unit)
        {
            return ObjectManager.Get<Obj_AI_Hero>()
                .Where(x => x.IsEnemy && x.IsValid)
                .OrderBy(x => unit.ServerPosition.Distance(x.ServerPosition))
                .FirstOrDefault();
        }

        public static float GetHealthPerc(this Obj_AI_Base unit)
        {
            return unit.Health * 100 / unit.MaxHealth;
        }

        public static float GetManaPerc(this Obj_AI_Base unit)
        {
            return unit.Mana * 100 / unit.MaxMana;
        }

        public static void SendMovePacket(this Obj_AI_Base v, Vector2 point)
        {
            Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(point.X, point.Y)).Send();
        }

        public static bool IsUnderEnemyTurret(this Obj_AI_Base unit)
        {
            IEnumerable<Obj_AI_Turret> query;

            if (unit.IsEnemy)
            {
                query = ObjectManager.Get<Obj_AI_Turret>()
                    .Where(x => x.IsAlly && x.IsValid && !x.IsDead && unit.ServerPosition.Distance(x.ServerPosition) < x.AttackRange);
            }
            else
            {
                query = ObjectManager.Get<Obj_AI_Turret>()
                    .Where(x => x.IsEnemy && x.IsValid && !x.IsDead && unit.ServerPosition.Distance(x.ServerPosition) < x.AttackRange);
            }

            return (query.Count() > 0);
        }

        public static void Ping(Vector3 pos)
        {
            Packet.S2C.Ping.Encoded(new Packet.S2C.Ping.Struct(pos.X, pos.Y, 0, 0, Packet.PingType.NormalSound)).Process();
        }

        public static float GetDistanceSqr(Obj_AI_Base source, Obj_AI_Base target)
        {
            return Vector2.DistanceSquared(source.ServerPosition.To2D(), target.ServerPosition.To2D());
        }

        public static bool IsFacing(this Obj_AI_Base enemy)
        {
            if (enemy.Path.Count() > 0 && enemy.Path[0].Distance(ObjectManager.Player.ServerPosition) > ObjectManager.Player.Distance(enemy))
                return false;
            else 
                return true;
        }

        public static bool IsKillable(Obj_AI_Hero source, Obj_AI_Base target, IEnumerable<SpellSlot> spellCombo)
        {
            return Damage.GetComboDamage(source, target, spellCombo) > target.Health;
        }

        public static int CountEnemyInTargetRange(Obj_AI_Hero target, float range)
        {
            return GetEnemyList().Where(x => target.Distance(x) <= range).Count();
        }
    }
}
