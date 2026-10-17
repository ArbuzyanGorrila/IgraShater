using System;
using System.Collections.Generic;
using System.IO;
using CraftMine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraftMineEditor
{
    public static class ProjectTools
    {
        private const string Root = "Assets/CraftMine/Resources";
        public const string ScenePath = "Assets/CraftMine/Scenes/CraftMine.unity";

        [MenuItem("Tools/CraftMine/Prepare project")]
        public static void Setup()
        {
            Directory.CreateDirectory(Root + "/Rooms"); Directory.CreateDirectory(Root + "/Recipes");
            Directory.CreateDirectory("Assets/CraftMine/Scenes");
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<CraftMineSettings>(Root+"/Balance.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CraftMineSettings>(),Root+"/Balance.asset");
            CreateRooms(); CreateRecipes();
            foreach (string path in new[] { Root+"/Art/KenneyTinyDungeon.png", Root+"/Art/KenneyPixelPlatformer.png", Root+"/Art/KenneyCharacters.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default; importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None; importer.SaveAndReimport();
            }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                new GameObject("CraftMine",typeof(GameController));
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath,true) };
            PlayerSettings.productName = "CraftMine";
            PlayerSettings.companyName = "CraftMineCourse";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("CraftMine prepared: 10 rooms, 4 recipes and playable scene.");
        }
        [MenuItem("Tools/CraftMine/Build Windows")]
        public static void BuildWindows()
        {
            Setup();
            string output = "Builds/Windows/CraftMine.exe";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length-1; i++) if (args[i] == "--craftmine-output") output = Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("CraftMine build failed: " + report.summary.result);
            Debug.Log("CraftMine Windows build succeeded: " + report.summary.totalSize + " bytes.");
        }
        private static void CreateRecipes()
        {
            int[][] costs = { new[] {8,0,0,0},new[] {4,10,0,0},new[] {0,6,8,0},new[] {0,0,8,6} };
            int[] levels = {1,3,5,8};
            for (int i = 0; i < 4; i++)
            {
                string path = Root+"/Recipes/Pickaxe"+(i+1)+".asset";
                if (File.Exists(path)) continue;
                var r = ScriptableObject.CreateInstance<RecipeDefinition>(); r.pickaxeTier = i+1;
                r.requiredLevel = levels[i]; r.cost = costs[i]; AssetDatabase.CreateAsset(r,path);
            }
        }
        private static void CreateRooms()
        {
            string[] titles = { "Первый тоннель", "Под каменным сводом", "Золотая нить", "Развилка рудокопа", "Узкие повороты", "Глубокий удар", "Лестница в темноту", "Падающие камни", "Путь среди камней", "Сердце шахты" };
            string[] lessons = {
                "WASD — идти. E — копать. Пройди по всем сердечкам, чтобы завершить уровень.",
                "Уберёшь землю под камнем — он упадёт. Space показывает, куда могут упасть камни.",
                "На этом уровне нельзя возвращаться на пройденные клетки. Z — отменить ход.",
                "Для железа нужны уровень героя 3 и медная кирка. Руду собирать необязательно.",
                "Собери сердечки без возвратов. Шаг в стену поворачивает героя и направляет кирку.",
                "На уровне героя 6 кирка копает сразу 3 × 2 клетки. Жёлтая рамка показывает область.",
                "Пройденные клетки не держат камни. Не копай прямо под ними.",
                "Следи за камнями над головой. Один щит спасает от одного падающего камня.",
                "На уровне героя 9 доступны два щита. На этой карте нельзя возвращаться на свои следы.",
                "Последний уровень: собери все сердечки. Опыт и ресурсы сохранятся после победы."
            };
            int[] widths = {10,12,12,14,14,14,14,16,16,16};
            int[] heights = {10,10,10,10,12,12,12,12,12,12};
            // These fixed waypoints author each room's witness route. No random generation at runtime.
            Vector2Int[][] points = {
                new[] {P(1,1),P(8,1),P(8,3),P(2,3),P(2,5),P(8,5)},
                new[] {P(1,1),P(10,1),P(10,3),P(1,3),P(1,5),P(10,5)},
                new[] {P(1,1),P(5,1),P(5,3),P(10,3),P(10,5),P(2,5),P(2,7),P(9,7)},
                new[] {P(1,1),P(12,1),P(12,3),P(6,3),P(6,5),P(1,5),P(1,7),P(11,7)},
                new[] {P(1,1),P(11,1),P(11,3),P(3,3),P(3,5),P(11,5),P(11,7),P(3,7),P(3,9),P(11,9)},
                new[] {P(1,1),P(12,1),P(12,3),P(1,3),P(1,5),P(12,5),P(12,7),P(1,7),P(1,9),P(12,9)},
                new[] {P(1,1),P(4,1),P(4,3),P(7,3),P(7,5),P(10,5),P(10,7),P(12,7),P(12,9),P(2,9)},
                new[] {P(1,1),P(14,1),P(14,3),P(2,3),P(2,5),P(14,5),P(14,7),P(2,7),P(2,9),P(14,9)},
                new[] {P(1,1),P(7,1),P(7,3),P(14,3),P(14,5),P(1,5),P(1,7),P(14,7),P(14,9),P(2,9)},
                new[] {P(1,1),P(14,1),P(14,3),P(1,3),P(1,5),P(14,5),P(14,7),P(1,7),P(1,9),P(14,9)}
            };
            for (int index = 0; index < 10; index++)
            {
                string path = Root+"/Rooms/Room"+(index+1).ToString("00")+".asset";
                if (File.Exists(path)) continue;
                int w = widths[index],h = heights[index]; var cells = new char[w,h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    cells[x,y] = x == 0 || x == w-1 || y == 0 || y == h-1 ? '#' : '.';
                var route = new List<Vector2Int> { points[index][0] }; string solution = "";
                for (int n = 1; n < points[index].Length; n++)
                {
                    var current = route[route.Count-1]; var end = points[index][n];
                    while (current != end)
                    {
                        Vector2Int d = current.x != end.x ? P(Math.Sign(end.x-current.x),0) : P(0,Math.Sign(end.y-current.y));
                        current += d; route.Add(current); solution += d.x > 0 ? "D" : d.x < 0 ? "A" : d.y > 0 ? "W" : "S";
                    }
                }
                var routeSet = new HashSet<Vector2Int>(route);
                for (int n = 5; n < route.Count-1; n += 7) cells[route[n].x,route[n].y] = 'H';
                var final = route[route.Count-1]; cells[final.x,final.y] = 'H';
                var start = route[0]; cells[start.x,start.y] = '@';
                // Fixed wall piers make pockets and turn decisions clearer without blocking the witness.
                for (int x = 3; x < w-2; x += 4)
                    if (!routeSet.Contains(P(x,h-2)) && cells[x,h-2] == '.') cells[x,h-2] = '#';
                PlaceDeposits(cells,routeSet,index);
                var room = ScriptableObject.CreateInstance<RoomDefinition>(); room.title = titles[index]; room.lesson = lessons[index];
                room.strictTrail = index == 2 || index == 4 || index == 6 || index == 8 || index == 9;
                room.solution = solution; room.par = solution.Length; room.rows = new string[h];
                for (int y = 0; y < h; y++) { var row = new char[w]; for (int x = 0; x < w; x++) row[x] = cells[x,h-1-y]; room.rows[y] = new string(row); }
                AssetDatabase.CreateAsset(room,path);
            }
        }
        // Stable per-room ordering scatters optional deposits without changing the authored route.
        private static void PlaceDeposits(char[,] cells,HashSet<Vector2Int> route,int index)
        {
            int w = cells.GetLength(0),h = cells.GetLength(1);
            var candidates = new List<Vector2Int>();
            for (int y = 2; y < h-1; y++) for (int x = 1; x < w-1; x++)
                if (cells[x,y] == '.' && !route.Contains(P(x,y))) candidates.Add(P(x,y));
            candidates.Sort((a,b) => PlacementKey(a,index).CompareTo(PlacementKey(b,index)));

            var rocks = new List<Vector2Int>();
            foreach (var p in candidates)
            {
                if (rocks.Count >= 2+index/2) break;
                if (route.Contains(p+Vector2Int.down) || cells[p.x,p.y-1] != '.') continue;
                if (rocks.Exists(other => Math.Abs(other.x-p.x)+Math.Abs(other.y-p.y) < 4)) continue;
                cells[p.x,p.y] = 'O'; rocks.Add(p);
            }

            char[] kinds = {'c','i','g','d'};
            int[] remaining = {10,index >= 2 ? 12 : 0,index >= 4 ? 10 : 0,index >= 7 ? 8 : 0};
            int nextKind = index%4;
            foreach (var p in candidates)
            {
                if (cells[p.x,p.y] != '.' || rocks.Contains(p+Vector2Int.up) || !CanPlaceDeposit(cells,p)) continue;
                for (int n = 0; n < kinds.Length; n++)
                {
                    int kind = (nextKind+n)%kinds.Length;
                    if (remaining[kind] == 0) continue;
                    cells[p.x,p.y] = kinds[kind]; remaining[kind]--; nextKind = (kind+1)%kinds.Length;
                    break;
                }
            }
        }
        private static uint PlacementKey(Vector2Int p,int index)
        {
            unchecked
            {
                uint value = (uint)(p.x*73856093 ^ p.y*19349663 ^ (index+1)*83492791);
                value ^= value >> 13; return value*1274126177u;
            }
        }
        private static bool IsDeposit(char cell) => cell == 'c' || cell == 'i' || cell == 'g' || cell == 'd' || cell == 'O';
        private static bool CanPlaceDeposit(char[,] cells,Vector2Int p)
        {
            // Leave dirt between small pockets; never create a line of three ores or rocks.
            foreach (var axis in new[] {Vector2Int.right,Vector2Int.up})
            {
                int length = 1;
                foreach (int sign in new[] {-1,1})
                {
                    var next = p+axis*sign;
                    while (next.x > 0 && next.x < cells.GetLength(0)-1 && next.y > 0 && next.y < cells.GetLength(1)-1 && IsDeposit(cells[next.x,next.y]))
                    { length++; next += axis*sign; }
                }
                if (length > 2) return false;
            }
            return true;
        }
        private static Vector2Int P(int x,int y) => new Vector2Int(x,y);
    }
}
