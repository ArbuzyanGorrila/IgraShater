using System;
using System.IO;
using CraftMine;
using NUnit.Framework;
using UnityEngine;

namespace CraftMineTests
{
    public sealed class GameModelTests
    {
        private CraftMineSettings settings;
        private RoomDefinition room;
        [SetUp] public void SetUp() { settings = ScriptableObject.CreateInstance<CraftMineSettings>(); }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(settings); if (room != null) UnityEngine.Object.DestroyImmediate(room); }
        private GameModel Make(PlayerProfile profile, params string[] rows)
        {
            room = ScriptableObject.CreateInstance<RoomDefinition>(); room.rows = rows;
            return new GameModel(room,settings,profile);
        }
        private GameModel Basic(PlayerProfile profile = null) => Make(profile ?? new PlayerProfile(),
            "#######", "#.....#", "#..H..#", "#.@...#", "#.....#", "#.....#", "#######");

        [Test] public void StepDigsAndAwardsExperience()
        {
            var m = Basic(); var r = m.Step(Vector2Int.right);
            Assert.IsTrue(r.accepted); Assert.AreEqual(1,m.State.experience); Assert.AreEqual(1,m.State.moves);
            Assert.AreEqual(CellType.Air,m.Cell(m.State.player));
        }
        [Test] public void BlockedStepFacesWithoutSpendingMove()
        {
            var m = Make(new PlayerProfile(),"#####","#H..#","#@#.#","#####");
            var before = m.State.player; var r = m.Step(Vector2Int.right);
            Assert.IsFalse(r.accepted); Assert.AreEqual(before,m.State.player); Assert.AreEqual(0,m.State.moves);
            Assert.AreEqual(Vector2Int.right,m.State.facing);
        }
        [TestCase(CellType.Copper,1,0,true)]
        [TestCase(CellType.Iron,2,1,false)]
        [TestCase(CellType.Iron,3,0,false)]
        [TestCase(CellType.Iron,3,1,true)]
        [TestCase(CellType.Gold,5,1,false)]
        [TestCase(CellType.Gold,5,2,true)]
        [TestCase(CellType.Diamond,8,2,false)]
        [TestCase(CellType.Diamond,8,3,true)]
        public void OreNeedsBothLevelAndPickaxe(CellType ore,int level,int tier,bool expected)
        {
            var m = Basic(new PlayerProfile { totalExperience = settings.Threshold(level),pickaxeTier = tier });
            Assert.AreEqual(expected,m.CanMine(ore));
        }
        [TestCase(1,1)] [TestCase(6,6)] [TestCase(10,9)]
        public void MiningFootprintHasExpectedSize(int level,int count)
        {
            var m = Basic(new PlayerProfile { totalExperience = settings.Threshold(level) });
            m.State.player = new Vector2Int(1,3); m.Face(Vector2Int.right);
            m.State.cells[2,3] = CellType.Dirt;
            Assert.AreEqual(count,m.MiningArea().Count);
        }
        [TestCase(0,1)] [TestCase(1,0)] [TestCase(0,-1)] [TestCase(-1,0)]
        public void MiningNeverTouchesBehindHero(int dx,int dy)
        {
            var m = Basic(new PlayerProfile { totalExperience = settings.Threshold(10) });
            var facing = new Vector2Int(dx,dy); m.Face(facing);
            foreach (var p in m.MiningArea()) { var delta = p-m.State.player; Assert.Greater(delta.x*facing.x+delta.y*facing.y,0); }
        }
        [Test] public void WallBlocksAreaPropagation()
        {
            var m = Basic(new PlayerProfile { totalExperience = settings.Threshold(10) });
            for (int y = 0; y < m.Height; y++) m.State.cells[3,y] = CellType.Bedrock;
            Assert.IsEmpty(m.MiningArea());
        }
        [Test] public void AreaMiningDoesNotCollectOrDestroyHearts()
        {
            var m = Basic(); m.Face(Vector2Int.up); m.State.hearts[2,4] = true;
            m.Mine(); Assert.IsTrue(m.State.hearts[2,4]); Assert.AreEqual(0,m.State.collected);
        }
        [Test] public void MiningAreaEligibilityIsFrozenBeforeLevelUp()
        {
            settings.firstLevelExperience = 1; settings.experienceStep = 0;
            var m = Basic(); m.State.cells[3,3] = CellType.Copper; m.State.cells[4,3] = CellType.Copper;
            var r = m.Mine(); Assert.AreEqual(1,r.dug); Assert.AreEqual(6,m.Level); Assert.AreEqual(1,m.State.charges);
            Assert.AreEqual(CellType.Copper,m.State.cells[4,3]);
        }
        [Test] public void RockColumnSettlesWithoutOverlap()
        {
            var m = Make(new PlayerProfile(),"#######","##O#  #","##O#  #","##O#  #","##O#  #","##O#  #","## #  #","## #@H#","#######");
            m.ResolveGravity(new ActionResult());
            for (int y = 1; y <= 5; y++) Assert.AreEqual(CellType.Boulder,m.State.cells[2,y]);
            Assert.AreEqual(CellType.Air,m.State.cells[2,6]); Assert.AreEqual(RunStatus.Playing,m.State.status);
        }
        [Test] public void BoulderRollsLeftFirstWhenBothSidesAreOpen()
        {
            var m = Make(new PlayerProfile(),"#######","# O   #","# O   #","##O#@H#","#######");
            m.ResolveGravity(new ActionResult());
            Assert.AreEqual(CellType.Boulder,m.State.cells[1,2]);
            Assert.AreEqual(CellType.Air,m.State.cells[2,3]);
            var settled = new ActionResult(); m.ResolveGravity(settled); Assert.AreEqual(0,settled.falls);
        }
        [Test] public void DiggingAirCannotAwardExperienceAgain()
        {
            var m = Basic(); m.Mine(); int experience = m.State.experience;
            Assert.IsFalse(m.Mine().accepted); Assert.AreEqual(experience,m.State.experience);
        }
        [Test] public void StartingANewAttemptDoesNotKeepPendingExperienceOrOre()
        {
            var profile = new PlayerProfile(); var m = Basic(profile); m.State.cells[3,3] = CellType.Copper; m.Mine();
            var reset = new GameModel(room,settings,profile);
            Assert.AreEqual(0,reset.State.experience); Assert.AreEqual(0,reset.State.loot[0]); Assert.AreEqual(0,profile.inventory[0]);
        }
        [Test] public void BoulderHitWithoutShieldLoses()
        {
            var m = Make(new PlayerProfile(),"#####","# O #","# . #","# @H#","#####");
            m.Step(Vector2Int.up); Assert.AreEqual(RunStatus.Lost,m.State.status);
        }
        [Test] public void ShieldDestroysRockAndConsumesExactlyOneCharge()
        {
            var m = Make(new PlayerProfile { totalExperience = settings.Threshold(4) },"#####","# O #","# . #","# @H#","#####");
            var r = m.Step(Vector2Int.up); Assert.AreEqual(RunStatus.Playing,m.State.status);
            Assert.AreEqual(0,m.State.charges); Assert.AreEqual(1,r.shields); Assert.AreEqual(CellType.Air,m.Cell(m.State.player));
        }
        [Test] public void TwoRocksConsumeTwoProtectionCharges()
        {
            var m = Make(new PlayerProfile { totalExperience = settings.Threshold(9) },"#####","# O #","# O #","# . #","# @H#","#####");
            var r = m.Step(Vector2Int.up); Assert.AreEqual(RunStatus.Playing,m.State.status); Assert.AreEqual(2,r.shields); Assert.AreEqual(0,m.State.charges);
        }
        [Test] public void UndoRestoresMiningXPLevelInventoryGridAndTrail()
        {
            var m = Basic(new PlayerProfile { totalExperience = settings.Threshold(2)-1 });
            var before = m.State.Clone(); m.State.cells[3,3] = CellType.Copper; before.cells[3,3] = CellType.Copper;
            m.Step(Vector2Int.right); Assert.AreEqual(2,m.Level); Assert.AreEqual(1,m.State.loot[0]);
            Assert.IsTrue(m.Undo()); Assert.AreEqual(before.experience,m.State.experience); Assert.AreEqual(before.player,m.State.player);
            Assert.AreEqual(0,m.State.loot[0]); Assert.AreEqual(CellType.Copper,m.State.cells[3,3]); Assert.IsFalse(m.State.trail[3,3]);
        }
        [Test] public void UndoRestoresLostRockAndProtection()
        {
            var m = Make(new PlayerProfile { totalExperience = settings.Threshold(4) },"#####","# O #","# . #","# @H#","#####");
            m.Step(Vector2Int.up); m.Undo(); Assert.AreEqual(1,m.State.charges); Assert.AreEqual(0,m.State.protectionUsed);
            Assert.AreEqual(CellType.Boulder,m.State.cells[2,3]); Assert.AreEqual(CellType.Dirt,m.State.cells[2,2]);
        }
        [Test] public void UndoBudgetCannotBeRefilledByMovingAgain()
        {
            var m = Basic();
            for (int i = 0; i < 3; i++) { m.Step(Vector2Int.right); Assert.IsTrue(m.Undo()); }
            m.Step(Vector2Int.right); Assert.IsFalse(m.Undo()); Assert.AreEqual(0,m.UndoRemaining);
        }
        [Test] public void StrictTrailPreventsReturning()
        {
            var m = Basic(); room.strictTrail = true; m.Step(Vector2Int.right);
            Assert.IsFalse(m.Step(Vector2Int.left).accepted);
        }
        [Test] public void AreaMiningRemainsAnActionWhenAdjacentTrailBlocksWalking()
        {
            var m = Basic(new PlayerProfile { totalExperience = settings.Threshold(6) }); room.strictTrail = true;
            foreach (var d in GameModel.Directions)
            { var p = m.State.player+d; m.State.cells[p.x,p.y] = CellType.Air; m.State.trail[p.x,p.y] = true; }
            Assert.IsTrue(m.Mine().accepted); Assert.AreEqual(RunStatus.Playing,m.State.status);
        }
        [Test] public void LastHeartDoesNotWinIfRockKillsHero()
        {
            var m = Make(new PlayerProfile(),"#####","# O #","# H #","# @ #","#####");
            m.Step(Vector2Int.up); Assert.AreEqual(1,m.State.collected); Assert.AreEqual(RunStatus.Lost,m.State.status);
        }
        [Test] public void VictoryNeedsEveryHeart()
        {
            var m = Make(new PlayerProfile(),"######","#@HH #","######");
            m.Step(Vector2Int.right); Assert.AreEqual(RunStatus.Playing,m.State.status);
            m.Step(Vector2Int.right); Assert.AreEqual(RunStatus.Won,m.State.status);
        }
        [Test] public void CraftingConsumesResourcesAndCannotRepeat()
        {
            var r = ScriptableObject.CreateInstance<RecipeDefinition>(); r.pickaxeTier = 1; r.requiredLevel = 1; r.cost = new[] {8,0,0,0};
            var p = new PlayerProfile { inventory = new[] {9,0,0,0} };
            Assert.IsTrue(Crafting.TryCraft(p,settings,r)); Assert.AreEqual(1,p.inventory[0]); Assert.AreEqual(1,p.pickaxeTier);
            Assert.IsFalse(Crafting.TryCraft(p,settings,r)); Assert.AreEqual(1,p.inventory[0]); UnityEngine.Object.DestroyImmediate(r);
        }
        [Test] public void CraftingCannotSpendResourcesBeforeRequiredLevel()
        {
            var r = ScriptableObject.CreateInstance<RecipeDefinition>(); r.pickaxeTier = 2; r.requiredLevel = 3; r.cost = new[] {4,10,0,0};
            var p = new PlayerProfile { pickaxeTier = 1,inventory = new[] {10,20,0,0} };
            Assert.IsFalse(Crafting.TryCraft(p,settings,r)); Assert.AreEqual(20,p.inventory[1]); UnityEngine.Object.DestroyImmediate(r);
        }
        [Test] public void SaveRoundTripAndCorruptPrimaryRecovery()
        {
            string directory = System.IO.Path.Combine(Directory.GetCurrentDirectory(),"TestResults","save-"+Guid.NewGuid());
            Directory.CreateDirectory(directory); string path = System.IO.Path.Combine(directory,"profile.json");
            try
            {
                var s = new SaveService(path); var p = new PlayerProfile { totalExperience = 90,inventory = new[] {8,4,0,0} };
                Assert.IsTrue(s.Save(p)); Assert.AreEqual(90,s.Load().totalExperience);
                p.totalExperience = 140; Assert.IsTrue(s.Save(p)); File.WriteAllText(path,"broken");
                Assert.AreEqual(90,s.Load().totalExperience); Assert.IsNotNull(s.Notice);
            }
            finally { Directory.Delete(directory,true); }
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        public void AuthoredRoomWitnessWinsWithStartingEquipment(int number)
        {
            var definition = Resources.Load<RoomDefinition>("Rooms/Room"+number.ToString("00"));
            Assert.IsNotNull(definition,"Run CraftMine Setup before content tests");
            var m = new GameModel(definition,settings,new PlayerProfile());
            foreach (char c in definition.solution)
            {
                var r = m.Step(c == 'W' ? Vector2Int.up : c == 'S' ? Vector2Int.down : c == 'A' ? Vector2Int.left : Vector2Int.right);
                Assert.IsTrue(r.accepted,"Rejected witness move " + m.State.moves + " in room " + number + ": " + r.message);
            }
            Assert.AreEqual(RunStatus.Won,m.State.status); Assert.LessOrEqual(m.State.moves,definition.par);
        }
    }
}
