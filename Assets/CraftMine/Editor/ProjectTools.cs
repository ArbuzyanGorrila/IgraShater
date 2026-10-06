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
            string[] titles = { "Первый тоннель", "Под каменным сводом", "Золотая нить", "Развилка рудокопа", "Узкие повороты", "Глубокий удар", "Лестница в темноту", "Каскад", "Нить среди валунов", "Сердце шахты" };
            string[] lessons = {
                "WASD — движение. Сердечки собираются при проходе через клетку. E копает перед собой.",
                "Земля держит валуны. Убирая опору, ты вызываешь обвал. Space показывает линии падения.",
                "Здесь нельзя пересекать собственный след. Планируй маршрут; Z отменяет ход.",
                "Медная кирка и уровень 3 открывают железо. Руды находятся в дополнительных ответвлениях.",
                "Собери сердечки без возвратов. Заблокированное движение всё равно меняет направление копания.",
                "На уровне персонажа 6 удар E расширяется до 3 × 2. Предпросмотр показывает область.",
                "След не держит камни. Копай осторожно рядом с нависающими валунами.",
                "Колонна камней падает каскадом. Заряды защиты спасают от отдельных попаданий.",
                "На уровне 9 персонаж получает второй заряд защиты. Здесь снова действует правило следа.",
                "Последняя экспедиция: все сердечки одним маршрутом. Руда и XP сохраняются после победы."
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
                // Off-route ore pockets are optional; their quantity supports crafting without endless farming.
                int copper = 0,iron = 0,gold = 0,diamond = 0;
                for (int y = 2; y < h-1; y++) for (int x = 2; x < w-1; x++)
                {
                    var p = P(x,y);
                    if (routeSet.Contains(p)) continue;
                    if (copper < 10) { cells[x,y] = 'c'; copper++; }
                    else if (index >= 2 && iron < 12) { cells[x,y] = 'i'; iron++; }
                    else if (index >= 4 && gold < 10) { cells[x,y] = 'g'; gold++; }
                    else if (index >= 7 && diamond < 8) { cells[x,y] = 'd'; diamond++; }
                }
                // Rock supports stay off the witness route. Alternative excavation can trigger hazards.
                int placed = 0;
                for (int y = h-2; y >= 4 && placed < 2+index/2; y--)
                    for (int x = 2; x < w-2 && placed < 2+index/2; x++)
                        if (!routeSet.Contains(P(x,y)) && !routeSet.Contains(P(x,y-1)) && cells[x,y] == '.' && cells[x,y-1] == '.')
                        { cells[x,y] = 'O'; placed++; x++; }
                // Fixed wall piers make pockets and turn decisions clearer without blocking the witness.
                for (int x = 3; x < w-2; x += 4)
                    if (!routeSet.Contains(P(x,h-2)) && cells[x,h-2] == '.') cells[x,h-2] = '#';
                var room = ScriptableObject.CreateInstance<RoomDefinition>(); room.title = titles[index]; room.lesson = lessons[index];
                room.strictTrail = index == 2 || index == 4 || index == 6 || index == 8 || index == 9;
                room.solution = solution; room.par = solution.Length; room.rows = new string[h];
                for (int y = 0; y < h; y++) { var row = new char[w]; for (int x = 0; x < w; x++) row[x] = cells[x,h-1-y]; room.rows[y] = new string(row); }
                AssetDatabase.CreateAsset(room,path);
            }
        }
        private static Vector2Int P(int x,int y) => new Vector2Int(x,y);
    }
}
