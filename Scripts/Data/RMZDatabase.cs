// 從 StreamingAssets/rmz/data 讀取 MZ 的 JSON。
// 桌面平台用 File.ReadAllText 直讀；之後要出 Android/WebGL 需改成 UnityWebRequest。
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Godot;

namespace RMZ
{
    public static class RMZDatabase
    {
        public static SystemData System { get; private set; }
        public static List<TilesetData> Tilesets { get; private set; }
        public static List<MapInfo> MapInfos { get; private set; }
        public static List<ActorData> Actors { get; private set; }
        public static List<ClassData> Classes { get; private set; }
        public static List<EquipItemData> Items { get; private set; }
        public static List<EquipItemData> Weapons { get; private set; }
        public static List<EquipItemData> Armors { get; private set; }
        public static List<AnimationData> Animations { get; private set; }
        public static List<CommonEventData> CommonEvents { get; private set; }

        static readonly Dictionary<int, MapData> MapCache = new Dictionary<int, MapData>();
        public static bool Loaded { get; private set; }

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public static void LoadCore()
        {
            if (Loaded) return;

            System = ReadJson<SystemData>("System.json");
            Tilesets = ReadJson<List<TilesetData>>("Tilesets.json");
            MapInfos = ReadJson<List<MapInfo>>("MapInfos.json");
            Actors = ReadJson<List<ActorData>>("Actors.json");
            Classes = ReadJson<List<ClassData>>("Classes.json");
            Items = ReadJson<List<EquipItemData>>("Items.json");
            Weapons = ReadJson<List<EquipItemData>>("Weapons.json");
            Armors = ReadJson<List<EquipItemData>>("Armors.json");
            Animations = ReadJson<List<AnimationData>>("Animations.json");
            CommonEvents = ReadJson<List<CommonEventData>>("CommonEvents.json");

            Loaded = System != null && Tilesets != null;
            if (!Loaded)
                GD.PushError($"[RMZ] 核心資料載入失敗。請確認 {RMZConfig.DataPath} 底下有 System.json / Tilesets.json。");
        }

        public static MapData LoadMap(int mapId)
        {
            if (MapCache.TryGetValue(mapId, out var cached)) return cached;

            var map = ReadJson<MapData>($"Map{mapId:D3}.json");
            if (map == null)
            {
                GD.PushError($"[RMZ] 找不到地圖 Map{mapId:D3}.json");
                return null;
            }

            MapCache[mapId] = map;
            return map;
        }

        public static void ClearMapCache() => MapCache.Clear();

        public static AnimationData Animation(int id) =>
            (Animations != null && id > 0 && id < Animations.Count) ? Animations[id] : null;

        public static EquipItemData Item(int id) =>
            (Items != null && id > 0 && id < Items.Count) ? Items[id] : null;

        public static EquipItemData Weapon(int id) =>
            (Weapons != null && id > 0 && id < Weapons.Count) ? Weapons[id] : null;

        public static EquipItemData Armor(int id) =>
            (Armors != null && id > 0 && id < Armors.Count) ? Armors[id] : null;

        public static ActorData Actor(int id)
        {
            if (Actors == null || id <= 0 || id >= Actors.Count) return null;
            return Actors[id];
        }

        public static ClassData Class(int id) =>
            (Classes != null && id > 0 && id < Classes.Count) ? Classes[id] : null;

        public static CommonEventData CommonEvent(int id)
        {
            if (CommonEvents == null || id <= 0 || id >= CommonEvents.Count) return null;
            return CommonEvents[id];
        }

        public static TilesetData Tileset(int id)
        {
            if (Tilesets == null || id <= 0 || id >= Tilesets.Count) return null;
            return Tilesets[id];
        }

        public static string MapName(int mapId)
        {
            if (MapInfos == null) return $"Map{mapId:D3}";
            foreach (var info in MapInfos)
                if (info != null && info.id == mapId) return info.name;
            return $"Map{mapId:D3}";
        }

        static T ReadJson<T>(string fileName) where T : class
        {
            string path = Path.Combine(RMZConfig.DataPath, fileName);
            if (!File.Exists(path))
            {
                GD.PushError($"[RMZ] 檔案不存在：{path}");
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Settings);
            }
            catch (Exception e)
            {
                GD.PushError($"[RMZ] 解析 {fileName} 失敗：{e.Message}");
                return null;
            }
        }
    }
}
