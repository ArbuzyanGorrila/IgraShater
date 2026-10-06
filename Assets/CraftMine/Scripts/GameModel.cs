using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftMine
{
    public sealed class RunState
    {
        public CellType[,] cells;
        public bool[,] hearts;
        public bool[,] trail;
        public Vector2Int player;
        public Vector2Int facing = Vector2Int.right;
        public int experience, collected, heartCount, moves, charges, protectionUsed;
        public int[] loot = new int[4];
        public RunStatus status;
        public RunState Clone()
        {
            var copy = (RunState)MemberwiseClone();
            copy.cells = (CellType[,])cells.Clone();
            copy.hearts = (bool[,])hearts.Clone();
            copy.trail = (bool[,])trail.Clone();
            copy.loot = (int[])loot.Clone();
            return copy;
        }
    }

    public sealed class ActionResult
    {
        public bool accepted;
        public string message;
        public int dug, hearts, falls, shields, levelsGained;
        public Vector2Int previousPlayer;
        public readonly List<Vector2Int> excavated = new List<Vector2Int>();
    }

    // All gameplay changes live here. Presentation never writes the board.
    public sealed class GameModel
    {
        public RunState State { get; private set; }
        public readonly CraftMineSettings Settings;
        public readonly RoomDefinition Room;
        public readonly int PickaxeTier;
        public int UndoRemaining { get; private set; } = 3;
        public int UndoUsed => 3 - UndoRemaining;
        public string ReachabilityWarning { get; private set; }
        public int Width => State.cells.GetLength(0);
        public int Height => State.cells.GetLength(1);
        public int Level => Settings.Level(State.experience);
        private readonly List<RunState> history = new List<RunState>();

        public GameModel(RoomDefinition room, CraftMineSettings settings, PlayerProfile profile)
        {
            Room = room; Settings = settings; PickaxeTier = profile.pickaxeTier;
            int w = room.rows[0].Length, h = room.rows.Length;
            State = new RunState { cells = new CellType[w,h], hearts = new bool[w,h],
                trail = new bool[w,h], experience = profile.totalExperience };
            int starts = 0;
            for (int y = 0; y < h; y++)
            {
                if (room.rows[h-1-y].Length != w) throw new ArgumentException("Room rows have different widths");
                for (int x = 0; x < w; x++)
                {
                    char c = room.rows[h-1-y][x];
                    switch (c)
                    {
                        case '#': State.cells[x,y] = CellType.Bedrock; break;
                        case 'O': State.cells[x,y] = CellType.Boulder; break;
                        case 'c': State.cells[x,y] = CellType.Copper; break;
                        case 'i': State.cells[x,y] = CellType.Iron; break;
                        case 'g': State.cells[x,y] = CellType.Gold; break;
                        case 'd': State.cells[x,y] = CellType.Diamond; break;
                        case '.': State.cells[x,y] = CellType.Dirt; break;
                        case 'H': State.hearts[x,y] = true; State.heartCount++; State.cells[x,y] = CellType.Dirt; break;
                        case '@': State.player = new Vector2Int(x,y); starts++; break;
                        case ' ': break;
                        default: throw new ArgumentException("Unknown room cell: " + c);
                    }
                }
            }
            if (starts != 1 || State.heartCount == 0) throw new ArgumentException("Room needs one start and hearts");
            State.trail[State.player.x, State.player.y] = true;
            State.charges = Settings.Protection(Level);
        }

        public bool Inside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;
        public CellType Cell(Vector2Int p) => Inside(p) ? State.cells[p.x,p.y] : CellType.Bedrock;
        public bool CanMine(CellType cell)
        {
            if (cell == CellType.Dirt) return true;
            int ore = CraftMineSettings.OreIndex(cell);
            return ore >= 0 && Level >= CraftMineSettings.OreLevels[ore] && PickaxeTier >= ore;
        }
        public bool CanEnter(Vector2Int p) => Inside(p) && (Cell(p) == CellType.Air || CanMine(Cell(p))) &&
            (!Room.strictTrail || !State.trail[p.x,p.y]);
        public void Face(Vector2Int direction)
        {
            if (Math.Abs(direction.x) + Math.Abs(direction.y) == 1) State.facing = direction;
        }
        public List<Vector2Int> MiningArea()
        {
            var result = new List<Vector2Int>();
            var reached = new HashSet<Vector2Int> { State.player };
            var side = new Vector2Int(-State.facing.y, State.facing.x);
            int radius = Settings.Radius(Level), half = radius > 1 ? 1 : 0;
            // Grow each row from reachable preceding/neighbor cells; never tunnel through an obstacle.
            for (int depth = 1; depth <= radius; depth++)
                for (int pass = 0; pass < 3; pass++)
                    for (int offset = -half; offset <= half; offset++)
                    {
                        var p = State.player + State.facing * depth + side * offset;
                        if (!Inside(p) || reached.Contains(p) || (Cell(p) != CellType.Air && !CanMine(Cell(p)))) continue;
                        if (!(reached.Contains(p - State.facing) || reached.Contains(p - side) || reached.Contains(p + side))) continue;
                        reached.Add(p);
                        if (CanMine(Cell(p))) result.Add(p);
                    }
            return result;
        }
        public string BlockReason(Vector2Int p)
        {
            if (!Inside(p) || Cell(p) == CellType.Bedrock) return "Неразрушимая порода";
            if (Cell(p) == CellType.Boulder) return "Валун преграждает путь";
            if (Room.strictTrail && State.trail[p.x,p.y]) return "Испытание: нельзя пересекать свой след";
            int ore = CraftMineSettings.OreIndex(Cell(p));
            if (ore >= 0 && !CanMine(Cell(p))) return "Нужно: уровень " + CraftMineSettings.OreLevels[ore] +
                ", кирка «" + CraftMineSettings.PickaxeNames[ore] + "» или лучше";
            return "Здесь нечего копать";
        }

        public ActionResult Step(Vector2Int direction)
        {
            var result = new ActionResult { previousPlayer = State.player };
            if (State.status != RunStatus.Playing) return result;
            if (Math.Abs(direction.x) + Math.Abs(direction.y) != 1) return result;
            var target = State.player + direction;
            if (!CanEnter(target)) { Face(direction); result.message = BlockReason(target); return result; }
            Snapshot(); Face(direction);
            Excavate(target, result);
            State.player = target; State.trail[target.x,target.y] = true;
            if (State.hearts[target.x,target.y])
            { State.hearts[target.x,target.y] = false; State.collected++; result.hearts++; }
            Finish(result);
            return result;
        }
        public ActionResult Mine()
        {
            var result = new ActionResult { previousPlayer = State.player };
            if (State.status != RunStatus.Playing) return result;
            var area = MiningArea();
            if (area.Count == 0) { result.message = BlockReason(State.player + State.facing); return result; }
            Snapshot();
            // Eligibility and footprint are frozen before XP from this action can unlock more ore.
            foreach (var p in area) Excavate(p, result);
            Finish(result);
            return result;
        }
        private void Snapshot()
        {
            if (history.Count == 3) history.RemoveAt(0);
            history.Add(State.Clone());
        }
        private void Excavate(Vector2Int p, ActionResult result)
        {
            var cell = Cell(p);
            if (cell == CellType.Air) return;
            int oldLevel = Level, ore = CraftMineSettings.OreIndex(cell);
            State.cells[p.x,p.y] = CellType.Air;
            State.experience += Settings.miningExperience[ore + 1];
            if (ore >= 0) State.loot[ore]++;
            int newLevel = Level;
            State.charges += Settings.Protection(newLevel) - Settings.Protection(oldLevel);
            result.levelsGained += newLevel - oldLevel;
            result.dug++; result.excavated.Add(p);
        }
        private void Finish(ActionResult result)
        {
            result.accepted = true; State.moves++;
            ResolveGravity(result);
            if (State.status == RunStatus.Playing && State.collected == State.heartCount) State.status = RunStatus.Won;
            UpdateReachability();
            if (State.status == RunStatus.Playing && !HasAction()) State.status = RunStatus.Trapped;
        }
        public void ResolveGravity(ActionResult result)
        {
            bool moved;
            do
            {
                moved = false;
                for (int y = 1; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        var source = new Vector2Int(x,y);
                        if (Cell(source) != CellType.Boulder) continue;
                        var target = source + Vector2Int.down;
                        if (Cell(target) != CellType.Air)
                        {
                            if (Cell(target) != CellType.Boulder) continue;
                            bool rolled = false;
                            // Fixed left-first ordering makes replays and Undo reproducible.
                            foreach (var lateral in new[] { Vector2Int.left, Vector2Int.right })
                                if (Cell(source + lateral) == CellType.Air && Cell(target + lateral) == CellType.Air)
                                { target += lateral; rolled = true; break; }
                            if (!rolled) continue;
                        }
                        State.cells[x,y] = CellType.Air; moved = true; result.falls++;
                        if (target == State.player)
                        {
                            if (State.charges > 0)
                            { State.charges--; State.protectionUsed++; result.shields++; continue; }
                            State.cells[target.x,target.y] = CellType.Boulder;
                            State.status = RunStatus.Lost; return;
                        }
                        State.cells[target.x,target.y] = CellType.Boulder;
                    }
            } while (moved);
        }
        private bool HasAction()
        {
            foreach (var d in Directions)
            {
                if (CanEnter(State.player + d)) return true;
                // Area mining requires a clear/minable first cell in the facing direction.
                if (CanMine(Cell(State.player + d))) return true;
            }
            return false;
        }
        public bool Undo()
        {
            if (UndoRemaining <= 0 || history.Count == 0 || State.status == RunStatus.Won) return false;
            State = history[history.Count-1]; history.RemoveAt(history.Count-1); UndoRemaining--;
            UpdateReachability(); return true;
        }
        public void UpdateReachability()
        {
            ReachabilityWarning = null;
            var seen = new HashSet<Vector2Int> { State.player };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(State.player);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in Directions)
                {
                    var n = p + d;
                    // Optimistic: future mining XP can unlock ore. This is only a warning, not a solver.
                    if (!Inside(n) || seen.Contains(n) || Cell(n) == CellType.Bedrock || Cell(n) == CellType.Boulder ||
                        (Room.strictTrail && State.trail[n.x,n.y])) continue;
                    seen.Add(n); queue.Enqueue(n);
                }
            }
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
                if (State.hearts[x,y] && !seen.Contains(new Vector2Int(x,y)))
                { ReachabilityWarning = "Сердечко отрезано. Попробуй отменить ход или изменить завал."; return; }
        }
        public List<Vector2Int> DangerCells()
        {
            var result = new List<Vector2Int>();
            for (int y = 1; y < Height; y++) for (int x = 0; x < Width; x++)
                if (State.cells[x,y] == CellType.Boulder)
                    for (int below = y-1; below >= 0 && (State.cells[x,below] == CellType.Air || CanMine(State.cells[x,below])); below--)
                        result.Add(new Vector2Int(x,below));
            return result;
        }
        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    }
}
