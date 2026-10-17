using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CraftMine
{
    public sealed class GridView : MonoBehaviour
    {
        private GameController game;
        private Camera boardCamera;
        private Camera backgroundCamera;
        private Material mineralMaterial;
        private Tilemap terrain;
        private GameObject boardRoot, itemRoot, overlayRoot;
        private SpriteRenderer player, playerMarker;
        private readonly Dictionary<int, Sprite> platformSprites = new Dictionary<int, Sprite>();
        private readonly Dictionary<(Sprite, Color32), Tile> terrainTiles = new Dictionary<(Sprite, Color32), Tile>();
        private readonly Dictionary<Vector2Int, SpriteRenderer> boulderSprites = new Dictionary<Vector2Int, SpriteRenderer>();
        private Texture2D platformSheet, characterSheet, dungeonSheet;
        private Sprite hero;
        private bool menu;
        private int lastWidth, lastHeight;
        private bool gameplayViewport;
        public static readonly Color[] OreColors = {
            new Color(1f,.66f,.35f), new Color(.8f,.9f,1f), new Color(1f,.86f,.24f), new Color(.3f,.93f,1f)
        };
        public Material MineralMaterial => mineralMaterial;
        public void Initialize(GameController controller)
        {
            game = controller;
            platformSheet = Resources.Load<Texture2D>("Art/KenneyPixelPlatformer");
            characterSheet = Resources.Load<Texture2D>("Art/KenneyCharacters");
            dungeonSheet = Resources.Load<Texture2D>("Art/KenneyTinyDungeon");
            mineralMaterial = new Material(Resources.Load<Shader>("Art/MineralTint"));
            mineralMaterial.SetFloat("_Brightness",1.65f);
            platformSheet.filterMode = characterSheet.filterMode = dungeonSheet.filterMode = FilterMode.Point;
            hero = Sprite.Create(characterSheet, new Rect(6*24, characterSheet.height-24,24,24),new Vector2(.5f,.5f),24);
            boardCamera = Camera.main;
            if (boardCamera == null)
            {
                var cameraObject = new GameObject("Mine camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera"; boardCamera = cameraObject.GetComponent<Camera>();
            }
            boardCamera.orthographic = true;
            boardCamera.allowHDR = false; boardCamera.allowMSAA = false; boardCamera.allowDynamicResolution = false;
            // A viewport camera cannot clear pixels beneath the UI outside its viewport.
            // Clear the full frame first so removed labels and panels leave no stale pixels.
            backgroundCamera = new GameObject("Frame background",typeof(Camera)).GetComponent<Camera>();
            backgroundCamera.depth = -10; backgroundCamera.cullingMask = 0;
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(.067f,.094f,.133f);
            boardCamera.backgroundColor = new Color(.067f,.094f,.133f);
            boardCamera.clearFlags = CameraClearFlags.SolidColor;
        }
        public Sprite Icon(int index)
        {
            if (!platformSprites.TryGetValue(index, out var sprite))
            {
                // Terrain fills all 18 pixels of the source cell; keep its edges intact.
                sprite = Sprite.Create(platformSheet,new Rect(index%20*18,platformSheet.height-(index/20+1)*18,18,18),new Vector2(.5f,.5f),18);
                platformSprites[index] = sprite;
            }
            return sprite;
        }
        private Sprite DungeonIcon(int index)
        {
            int key = 1000+index;
            if (!platformSprites.TryGetValue(key,out var sprite))
            {
                sprite = Sprite.Create(dungeonSheet,new Rect(index%12*16,dungeonSheet.height-(index/12+1)*16,16,16),
                    new Vector2(.5f,.5f),16);
                platformSprites[key] = sprite;
            }
            return sprite;
        }
        // Copper nugget, square iron, round gold and diamond use different silhouettes.
        public Sprite OreIcon(int ore) => ore == 1 ? DungeonIcon(102) : Icon(ore == 2 ? 151 : ore == 3 ? 67 : 8);
        public void LoadBoard()
        {
            if (boardRoot != null) { boardRoot.SetActive(false); Destroy(boardRoot); }
            boardRoot = new GameObject("Mine board");
            var grid = boardRoot.AddComponent<Grid>();
            var terrainObject = new GameObject("Terrain", typeof(Tilemap),typeof(TilemapRenderer));
            terrainObject.transform.SetParent(grid.transform,false); terrain = terrainObject.GetComponent<Tilemap>();
            terrainObject.GetComponent<TilemapRenderer>().sortingOrder = 0;
            terrainObject.GetComponent<TilemapRenderer>().sharedMaterial = mineralMaterial;
            itemRoot = new GameObject("Items and boulders"); itemRoot.transform.SetParent(boardRoot.transform);
            overlayRoot = new GameObject("Mining and danger preview"); overlayRoot.transform.SetParent(boardRoot.transform);
            player = Render("Кубик", hero,game.Model.State.player,4,Color.white,1.05f,boardRoot.transform);
            playerMarker = new GameObject("Метка героя",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            playerMarker.transform.SetParent(player.transform,false); playerMarker.transform.localScale = Vector3.one*1.08f;
            playerMarker.sprite = DungeonIcon(60); playerMarker.sortingOrder = 3;
            playerMarker.color = new Color(.4f,.95f,1f);
            DrawState(); FitCamera(); RefreshOverlays();
        }
        private SpriteRenderer Render(string label, Sprite sprite, Vector2 position, int order, Color color, float scale, Transform parent)
        {
            var obj = new GameObject(label); obj.transform.SetParent(parent,false);
            obj.transform.position = new Vector3(position.x+.5f,position.y+.5f,0);
            obj.transform.localScale = Vector3.one*scale;
            var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order; renderer.color = color;
            return renderer;
        }
        private Tile GetTile(Sprite sprite, Color color)
        {
            var key = (sprite,(Color32)color);
            if (!terrainTiles.TryGetValue(key,out var tile))
            { tile = ScriptableObject.CreateInstance<Tile>(); tile.sprite = sprite; tile.color = color; terrainTiles[key] = tile; }
            return tile;
        }
        private void Clear(Transform parent)
        {
            for (int i = parent.childCount-1; i >= 0; i--)
            { var child = parent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
        }
        private void DrawState(bool updatePlayer = true, bool drawBoulders = true)
        {
            Clear(itemRoot.transform);
            boulderSprites.Clear();
            var model = game.Model; var state = model.State;
            for (int y = 0; y < model.Height; y++) for (int x = 0; x < model.Width; x++)
            {
                var p = new Vector2Int(x,y); var cell = state.cells[x,y];
                bool wall = cell == CellType.Bedrock, excavated = cell == CellType.Air || cell == CellType.Boulder;
                // The chosen ground variant is stable across moves and Undo.
                int variation = (x*7+y*11)%4;
                Sprite tileSprite = wall ? DungeonIcon(40) : excavated ? DungeonIcon(0) :
                    Icon(variation == 0 ? 25 : variation == 1 ? 120 : variation == 2 ? 140 : 141);
                Color tint = wall ? new Color(.56f,.66f,.78f) : excavated ? new Color(.12f,.18f,.25f) : new Color(.66f,.43f,.28f);
                terrain.SetTile(new Vector3Int(x,y,0),GetTile(tileSprite,tint));
                if (state.trail[x,y] && p != state.player)
                    Render("След",Icon(157),p,1,new Color(1,.8f,.4f,.6f),.4f,itemRoot.transform);
                int ore = CraftMineSettings.OreIndex(cell);
                if (ore >= 0)
                    Render(CraftMineSettings.OreNames[ore],OreIcon(ore),p,2,OreColors[ore],.72f,itemRoot.transform).sharedMaterial = mineralMaterial;
                if (state.hearts[x,y]) Render("Сердечко",Icon(44),p,2,Color.white,.68f,itemRoot.transform);
                if (cell == CellType.Boulder && drawBoulders)
                {
                    boulderSprites[p] = Render("Камень",Icon(7),p,3,new Color(.67f,.76f,.83f),.96f,itemRoot.transform);
                    boulderSprites[p].sharedMaterial = mineralMaterial;
                }
            }
            if (updatePlayer) player.transform.position = new Vector3(state.player.x+.5f,state.player.y+.5f,0);
            player.flipX = state.facing == Vector2Int.left;
            player.color = state.status == RunStatus.Lost ? new Color(1,.35f,.3f) : Color.white;
            playerMarker.color = state.status == RunStatus.Lost ? new Color(1,.4f,.3f) : new Color(.4f,.95f,1f);
        }
        public IEnumerator Animate(ActionResult result, float seconds)
        {
            // Retain visual starting positions while interpolating to the already-settled logical state.
            var start = new Vector3(result.previousPlayer.x+.5f,result.previousPlayer.y+.5f,0);
            var target = new Vector3(game.Model.State.player.x+.5f,game.Model.State.player.y+.5f,0);
            Clear(overlayRoot.transform);
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed/seconds), eased = 1-Mathf.Pow(1-t,3);
                player.transform.position = Vector3.Lerp(start,target,eased);
                if (start == target) player.transform.localScale = Vector3.one * (1.05f + Mathf.Sin(t*Mathf.PI)*.12f);
                yield return null;
            }
            player.transform.localScale = Vector3.one*1.05f;
            if (result.boulders.Count > 0)
            {
                var originalPositions = new List<Vector2Int>(boulderSprites.Keys);
                DrawState(true,false);
                foreach (var p in originalPositions)
                {
                    boulderSprites[p] = Render("Падающий камень",Icon(7),p,3,new Color(.67f,.76f,.83f),.96f,itemRoot.transform);
                    boulderSprites[p].sharedMaterial = mineralMaterial;
                }
                int index = 0;
                while (index < result.boulders.Count)
                {
                    int tick = result.boulders[index].tick;
                    var flights = new List<KeyValuePair<BoulderMotion,SpriteRenderer>>();
                    while (index < result.boulders.Count && result.boulders[index].tick == tick)
                    {
                        var motion = result.boulders[index++];
                        if (boulderSprites.TryGetValue(motion.from,out var rock)) flights.Add(new KeyValuePair<BoulderMotion,SpriteRenderer>(motion,rock));
                    }
                    for (float t = 0; t < .065f; t += Time.unscaledDeltaTime)
                    {
                        float fraction = Mathf.Clamp01(t/.065f);
                        foreach (var flight in flights)
                        {
                            var motion = flight.Key;
                            flight.Value.transform.position = Vector2.Lerp((Vector2)motion.from+Vector2.one*.5f,(Vector2)motion.to+Vector2.one*.5f,fraction);
                            if (motion.destroyed) flight.Value.color = new Color(1,1,1,1-fraction);
                        }
                        yield return null;
                    }
                    foreach (var flight in flights) boulderSprites.Remove(flight.Key.from);
                    foreach (var flight in flights)
                    {
                        if (flight.Key.destroyed) flight.Value.enabled = false;
                        else { boulderSprites[flight.Key.to] = flight.Value; flight.Value.transform.position = (Vector2)flight.Key.to+Vector2.one*.5f; }
                    }
                }
            }
            DrawState();
            if (result.falls > 0)
            {
                var cameraStart = boardCamera.transform.position;
                float duration = .12f;
                for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
                { boardCamera.transform.position = cameraStart + new Vector3(Mathf.Sin(t*100)*.035f,0,0); yield return null; }
                boardCamera.transform.position = cameraStart;
            }
        }
        public void RefreshOverlays()
        {
            if (overlayRoot == null) return;
            Clear(overlayRoot.transform);
            if (menu || game.Model.State.status != RunStatus.Playing) return;
            foreach (var p in game.Model.MiningArea())
                Render("Область копания E",DungeonIcon(60),p,5,new Color(1,.85f,.3f,.9f),.96f,overlayRoot.transform);
            var facing = game.Model.State.facing;
            var arrow = Render("Направление",Icon(88),game.Model.State.player+(Vector2)facing*.43f,6,new Color(1,.85f,.4f),.34f,overlayRoot.transform);
            arrow.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(facing.y,facing.x)*Mathf.Rad2Deg);
            if (game.ShowDanger)
                foreach (var p in game.Model.DangerCells())
                    Render("Возможное падение",DungeonIcon(61),p,5,new Color(1,.3f,.25f,.75f),.8f,overlayRoot.transform);
        }
        public void SetMenuAppearance(bool enabled)
        { menu = enabled; RefreshOverlays(); }
        private void LateUpdate()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height || gameplayViewport != (game.Screen == ScreenMode.Playing)) FitCamera();
        }
        private void FitCamera()
        {
            if (game.Model == null) return;
            lastWidth = Screen.width; lastHeight = Screen.height;
            gameplayViewport = game.Screen == ScreenMode.Playing;
            boardCamera.pixelRect = InterfaceLayout.BoardPixels(Screen.width, Screen.height,gameplayViewport);
            boardCamera.orthographicSize = Mathf.Max(game.Model.Height/2f+.3f,(game.Model.Width/2f+.3f)/boardCamera.aspect);
            boardCamera.transform.position = new Vector3(game.Model.Width/2f,game.Model.Height/2f,-10);
        }
        private void OnDestroy()
        {
            if (boardRoot != null) Destroy(boardRoot);
            foreach (var sprite in platformSprites.Values) Destroy(sprite);
            foreach (var tile in terrainTiles.Values) Destroy(tile);
            if (hero != null) Destroy(hero);
            if (mineralMaterial != null) Destroy(mineralMaterial);
            if (backgroundCamera != null) Destroy(backgroundCamera.gameObject);
        }
    }
}
