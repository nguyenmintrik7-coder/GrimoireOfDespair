using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Weapons;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Projectiles;
using StardewValley.Tools;

namespace GrimoireOfDespair
{
    public class ModEntry : Mod
    {
        // ID vật phẩm trong game
        private const string BookId = "TriNguyen.Grimoire_Book";
        private const string SwordId = "TriNguyen.Grimoire_Sword";
        private const string SwingsDataKey = "TriNguyen.Grimoire/TotalSwings";
        private const string AwakenedDataKey = "TriNguyen.Grimoire/Awakened";
        private const string BossBonusDmgKey = "TriNguyen.Grimoire/BossBonusDmg";

        // Tài nguyên hình ảnh đã cắt và tách nền
        private Texture2D TexArmor;
        private Texture2D TexSword;
        private Texture2D TexBook;
        private readonly Dictionary<int, Texture2D> TexBosses = new();
        private Texture2D PixelTex;

        // Trạng thái Đồng Hoá Ác Ma & Thanh Ma Pháp
        private bool IsDemonAssimilation;
        private float AssimilationTimer; // Tối đa 60s thực
        private float TransformAwakenFlashTimer; // 1s hiệu ứng Hắc Lôi khi biến thân
        private float CurrentMana = 100f; // 0 -> 100%
        private float ManaBufferTimer; // Đệm 1.5s chống sốc sát thương phép
        private float ManaDrainedThisSecond; // Khóa trần 3.3% / giây
        private float ManaSecondResetTimer;

        // Trạng thái Diệt Ma Kiếm (Khiên 3s hồi 2s & Tụ lực 5s nhân 5x)
        private float ShieldActiveTimer; // 3s miễn sát thương
        private float ShieldCooldownTimer; // 2s hồi chiêu
        private bool IsChargingSword;
        private float SwordChargeTimer; // 0 -> 5s (1x -> 5x)
        private float SwordSwingTimer;
        private int TotalSwordSwings;
        private int BonusBossDamage;

        // Nút ảo trên màn hình cảm ứng Android (Phương án B - có thể kéo thả)
        private Rectangle ChargeBtnRect = new(180, 520, 96, 96);
        private Rectangle ShieldBtnRect = new(300, 520, 88, 88);
        private Rectangle BookBtnRect = new(410, 520, 88, 88);
        private bool IsDraggingChargeBtn;

        // Hệ thống Hạt (Particle System), Tàn ảnh & Kiếm khí
        private readonly List<VfxParticle> Particles = new();
        private readonly List<AfterimageGhost> Afterimages = new();
        private readonly List<SwordWaveProjectile> SwordWaves = new();
        private float MagicCircleAngle;
        private float AfterimageSpawnTimer;

        // Quản lý 12 Boss & Sự kiện Tập Kích Thị Trấn
        private ActiveBoss CurrentBoss;
        private bool AwakeningTriggeredThisSession;
        private bool IsTownRaidToday;
        private bool TownRaidCompletedToday;
        private int TownRaidBossIndex = 1;
        private readonly List<NPC> InjuredTownNpcs = new();

        // Thông tin 12 Boss khớp với bộ ảnh thiết kế
        private static readonly Dictionary<int, BossTemplate> BossTemplates = new()
        {
            { 1,  new(1,  "BOSS 1: OÁN MA BẬC CAO",       1800,  28, new Color(255, 50, 30),  "Shockwave_Slow") },
            { 2,  new(2,  "BOSS 2: QUỶ VƯƠNG ĐA SỪNG",    2600,  36, new Color(180, 40, 220), "BlackHole_Orbs") },
            { 3,  new(3,  "BOSS 3: NGUỒN CỘI NGUYỀN RỦA", 3500,  42, new Color(160, 20, 255), "Tentacle_PoisonFog") },
            { 4,  new(4,  "BOSS 4: THỨC TỈNH MA TỘC",     4600,  50, new Color(200, 30, 80),  "DarkBlizzard_Blink") },
            { 5,  new(5,  "BOSS 5: ĐOẠN HỒN ĐẾ VƯƠNG",    5800,  60, new Color(255, 60, 20),  "SoulSlash_ArmorBreak") },
            { 6,  new(6,  "BOSS 6: MẠNG MA PHÁP SƯ",      7000,  68, new Color(190, 50, 255), "ManaWeb_ShadowClones") },
            { 7,  new(7,  "BOSS 7: QUỶ LỆNH ĐỊA NGỤC",    8500,  78, new Color(255, 90, 0),   "HellChains_LavaPillar") },
            { 8,  new(8,  "BOSS 8: MÊ CUNG MA VŨ",        10200, 88, new Color(220, 180, 255),"QuadMaskLaser_Invert") },
            { 9,  new(9,  "BOSS 9: KỴ SĨ DIỆT VONG",      12000, 98, new Color(170, 0, 255),  "PurpleFlameDash_Spin") },
            { 10, new(10, "BOSS 10: VUA CỦA TỐI TĂM",     14500, 112,new Color(255, 40, 0),   "MeteorRain_DarkAura") },
            { 11, new(11, "BOSS 11: TỬ THẦN ĐOẠT MỆNH",   17000, 128,new Color(140, 30, 190), "StealthTwinBlade_DeathMark") },
            { 12, new(12, "BOSS 12: MÃNH THÚ CUỒNG LOẠN", 22000, 150,new Color(255, 20, 20),  "BerserkQuake_Roar") }
        };

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonReleased += OnButtonReleased;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        // =====================================================================
        // 1. TỰ ĐỘNG CẮT ẢNH & XÓA NỀN LƯỚI ĐEN TỪ 3 ẢNH GỐC (12224, 12227, 12228)
        // =====================================================================
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            PixelTex = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            PixelTex.SetData(new[] { Color.White });

            LoadAndSliceAllSprites();
        }

        private void LoadAndSliceAllSprites()
        {
            try
            {
                // 1. Cắt ảnh 12224.png (ARMOR, SWORD, BOOK)
                var sheetEquip = LoadRawSheet("assets/12224.png");
                if (sheetEquip != null)
                {
                    TexArmor = CropAndRemoveGrid(sheetEquip, 0.01f, 0.14f, 0.32f, 0.64f);
                    TexSword = CropAndRemoveGrid(sheetEquip, 0.34f, 0.14f, 0.32f, 0.64f);
                    TexBook  = CropAndRemoveGrid(sheetEquip, 0.67f, 0.14f, 0.32f, 0.64f);
                }

                // 2. Cắt ảnh 12227.png (BOSS 1, BOSS 2, BOSS 3)
                var sheetBoss1To3 = LoadRawSheet("assets/12227.png");
                if (sheetBoss1To3 != null)
                {
                    TexBosses[1] = CropAndRemoveGrid(sheetBoss1To3, 0.01f, 0.07f, 0.32f, 0.71f);
                    TexBosses[2] = CropAndRemoveGrid(sheetBoss1To3, 0.33f, 0.07f, 0.34f, 0.71f);
                    TexBosses[3] = CropAndRemoveGrid(sheetBoss1To3, 0.67f, 0.07f, 0.32f, 0.71f);
                }

                // 3. Cắt ảnh 12228.png (BOSS 4 đến BOSS 12 - Lưới 3x3)
                var sheetBoss4To12 = LoadRawSheet("assets/12228.png");
                if (sheetBoss4To12 != null)
                {
                    float[] colX = { 0.01f, 0.34f, 0.67f };
                    float[] rowY = { 0.01f, 0.34f, 0.66f };
                    int bossIdx = 4;
                    for (int r = 0; r < 3; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            TexBosses[bossIdx++] = CropAndRemoveGrid(sheetBoss4To12, colX[c], rowY[r], 0.32f, 0.26f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Cảnh báo khi cắt ảnh tự động: {ex.Message}. Đang dùng texture dự phòng.", LogLevel.Warn);
            }

            // Đảm bảo không bao giờ null nếu thiếu ảnh
            TexArmor ??= PixelTex;
            TexSword ??= PixelTex;
            TexBook  ??= PixelTex;
            for (int i = 1; i <= 12; i++)
                if (!TexBosses.ContainsKey(i) || TexBosses[i] == null)
                    TexBosses[i] = PixelTex;
        }

        private Texture2D LoadRawSheet(string relativePath)
        {
            string fullPath = Path.Combine(Helper.DirectoryPath, relativePath);
            if (!File.Exists(fullPath))
                return null;
            return Helper.ModContent.Load<Texture2D>(relativePath);
        }

        /// <summary>
        /// Cắt vùng ảnh theo tỷ lệ và dùng thuật toán Flood-Fill từ viền ngoài
        /// để xóa sạch nền đen + ô lưới xám nhưng giữ nguyên màu đen bên trong trang bị/Boss.
        /// </summary>
        private Texture2D CropAndRemoveGrid(Texture2D source, float normX, float normY, float normW, float normH)
        {
            int x = Math.Clamp((int)(source.Width * normX), 0, source.Width - 1);
            int y = Math.Clamp((int)(source.Height * normY), 0, source.Height - 1);
            int w = Math.Clamp((int)(source.Width * normW), 1, source.Width - x);
            int h = Math.Clamp((int)(source.Height * normH), 1, source.Height - y);

            Color[] fullData = new Color[source.Width * source.Height];
            source.GetData(fullData);

            Color[] cropData = new Color[w * h];
            for (int row = 0; row < h; row++)
                for (int col = 0; col < w; col++)
                    cropData[row * w + col] = fullData[(y + row) * source.Width + (x + col)];

            // Flood-fill từ 4 cạnh viền để xóa nền tối & đường kẻ ô lưới
            bool[] visited = new bool[w * h];
            Queue<Point> queue = new();

            for (int col = 0; col < w; col++)
            {
                queue.Enqueue(new Point(col, 0));
                queue.Enqueue(new Point(col, h - 1));
            }
            for (int row = 0; row < h; row++)
            {
                queue.Enqueue(new Point(0, row));
                queue.Enqueue(new Point(w - 1, row));
            }

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                if (p.X < 0 || p.X >= w || p.Y < 0 || p.Y >= h)
                    continue;
                int idx = p.Y * w + p.X;
                if (visited[idx])
                    continue;
                visited[idx] = true;

                Color c = cropData[idx];
                // Nhận diện màu nền đen/xám xanh và đường lưới (#14141E -> #46485A)
                bool isDarkGridBg = c.R < 78 && c.G < 78 && c.B < 92 &&
                                    Math.Abs(c.R - c.G) < 20 && Math.Abs(c.G - c.B) < 25;

                if (isDarkGridBg)
                {
                    cropData[idx] = Color.Transparent;
                    for (int d = 0; d < 4; d++)
                        queue.Enqueue(new Point(p.X + dx[d], p.Y + dy[d]));
                }
            }

            Texture2D result = new(Game1.graphics.GraphicsDevice, w, h);
            result.SetData(cropData);
            return result;
        }

        // =====================================================================
        // 2. ĐĂNG KÝ MA ĐẠO THƯ TUYỆT VỌNG & DIỆT MA KIẾM VÀO DATA GAME 1.6
        // =====================================================================
        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Mods/TriNguyen.Grimoire/BookTex"))
                e.LoadFrom(() => TexBook ?? PixelTex, AssetLoadPriority.Medium);

            if (e.NameWithoutLocale.IsEquivalentTo("Mods/TriNguyen.Grimoire/SwordTex"))
                e.LoadFrom(() => TexSword ?? PixelTex, AssetLoadPriority.Medium);

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, ObjectData>().Data;
                    data[BookId] = new ObjectData
                    {
                        Name = BookId,
                        DisplayName = "Ma Đạo Thư Tuyệt Vọng",
                        Description = "Quyển sách phép thuật tối thượng của loài quỷ. Giữ trong túi để triệu hồi Diệt Ma Kiếm. Sử dụng để kích hoạt Đồng Hoá Ác Ma trong 60 giây thực.",
                        Type = "Basic",
                        Category = -2,
                        Price = 99999,
                        Texture = "Mods/TriNguyen.Grimoire/BookTex",
                        SpriteIndex = 0
                    };
                });
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Weapons"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, WeaponData>().Data;
                    data[SwordId] = new WeaponData
                    {
                        Name = SwordId,
                        DisplayName = "Diệt Ma Kiếm",
                        Description = "Thanh đại kiếm khắc chế mọi năng lực quái vật. Đỡ đòn tạo Khiên bất tử 3s (hồi 2s). Giữ nút Tụ Lực 5s để nhân 5 lần sát thương.",
                        MinDamage = 60,
                        MaxDamage = 100,
                        Knockback = 1.5f,
                        Speed = 12,
                        AddedPrecision = 10,
                        AddedDefense = 20,
                        Type = 3,
                        CritChance = 0.25f,
                        CritMultiplier = 3.0f,
                        Texture = "Mods/TriNguyen.Grimoire/SwordTex",
                        SpriteIndex = 0
                    };
                });
            }
        }

        // =====================================================================
        // 3. QUẢN LÝ SAVE, NGÀY MỚI & SỰ KIỆN ÁC MA TẬP KÍCH THỊ TRẤN (1-2 LẦN/MÙA)
        // =====================================================================
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (Game1.player.modData.TryGetValue(SwingsDataKey, out string swingsStr) && int.TryParse(swingsStr, out int s))
                TotalSwordSwings = s;
            if (Game1.player.modData.TryGetValue(BossBonusDmgKey, out string dmgStr) && int.TryParse(dmgStr, out int b))
                BonusBossDamage = b;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            CurrentMana = 100f;
            IsDemonAssimilation = false;
            CurrentBoss = null;
            InjuredTownNpcs.Clear();
            TownRaidCompletedToday = false;

            // Mỗi mùa (28 ngày) xuất hiện Boss tập kích thị trấn vào ngày 10 và ngày 22 (2 lần/mùa)
            int day = Game1.dayOfMonth;
            IsTownRaidToday = (day == 10 || day == 22) && Game1.player.modData.ContainsKey(AwakenedDataKey);

            if (IsTownRaidToday)
            {
                // Độ khó Boss tăng dần theo tầng hầm mỏ sâu nhất đã đi hoặc số mùa
                int deepestMine = Math.Max(10, Game1.player.deepestMineLevel);
                TownRaidBossIndex = Math.Clamp(deepestMine / 10, 1, 12);

                // Hộp thoại Thị trưởng Lewis cầu cứu kèm nút dịch chuyển khẩn cấp
                Response[] responses =
                {
                    new Response("TeleportTown", "Tôi đến Quảng trường ngay lập tức! (Dịch chuyển)"),
                    new Response("Later", "Để tôi chuẩn bị trang bị rồi sẽ ra sau.")
                };

                Game1.currentLocation.createQuestionDialogue(
                    $"Thị trưởng Lewis: \"{Game1.player.Name}! Nguy to rồi! Đại ác ma [{BossTemplates[TownRaidBossIndex].Name}] vừa trồi lên tàn phá Quảng trường Thị trấn! Nhiều người dân đang bị thương nặng, xin hãy cứu họ!\"",
                    responses,
                    (who, answer) =>
                    {
                        if (answer == "TeleportTown")
                            Game1.warpFarmer("Town", 28, 67, 0);
                    }
                );
            }
        }

        // =====================================================================
        // 4. VÒNG LẶP CẬP NHẬT: SÁCH, KIẾM (200ms->80ms), MA PHÁP & 12 BOSS
        // =====================================================================
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            float dt = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f || dt > 0.5f)
                dt = 1f / 60f;

            // 1. Khi Ma Đạo Thư trong túi đồ -> Tự động cấp Diệt Ma Kiếm nếu chưa có
            bool hasGrimoire = HasItemInInventory(BookId);
            if (hasGrimoire && !HasItemInInventory(SwordId))
            {
                Item sword = ItemRegistry.Create($"(W){SwordId}");
                Game1.player.addItemToMenuIfNecessary(sword);
            }

            // 2. Cập nhật Thanh Ma Pháp (Hồi đầy sau 120s thực = 0.8333% / giây)
            ManaSecondResetTimer += dt;
            if (ManaSecondResetTimer >= 1.0f)
            {
                ManaSecondResetTimer = 0f;
                ManaDrainedThisSecond = 0f; // Reset trần 3.3% mỗi giây
            }

            if (ManaBufferTimer > 0f)
                ManaBufferTimer -= dt;

            if (!IsDemonAssimilation && CurrentMana < 100f)
                CurrentMana = Math.Min(100f, CurrentMana + (100f / 120f) * dt);
            else if (IsDemonAssimilation && CurrentBoss != null && CurrentMana < 100f)
                CurrentMana = Math.Min(100f, CurrentMana + (100f / 120f) * dt * 0.5f); // Hồi nhẹ trong giao tranh

            // 3. Cập nhật trạng thái Đồng Hoá Ác Ma (Tồn tại 60s thời gian thực)
            if (TransformAwakenFlashTimer > 0f)
                TransformAwakenFlashTimer -= dt;

            if (IsDemonAssimilation)
            {
                AssimilationTimer -= dt;
                MagicCircleAngle += dt * 2.5f;

                // Xóa mọi debuff trên người chơi ngay lập tức
                Game1.player.buffs.Remove("13"); // Slow
                Game1.player.buffs.Remove("18"); // Fear/Darkness
                Game1.player.buffs.Remove("19"); // Frozen
                Game1.player.buffs.Remove("25"); // Nausea

                // Phát sáng hào quang đỏ thẫm trong hầm mỏ tối
                Utility.addLightSource(Game1.player.UniqueMultiplayerID.GetHashCode() ^ 999,
                    Game1.player.Position, 3.5f, new Color(30, 220, 220)); // Cyan-inverted = Red glow

                // Tạo tàn ảnh (Afterimage) và lông vũ đen + tàn lửa đỏ
                AfterimageSpawnTimer += dt;
                if (AfterimageSpawnTimer >= 0.07f && Game1.player.isMoving())
                {
                    AfterimageSpawnTimer = 0f;
                    Afterimages.Add(new AfterimageGhost(Game1.player.Position, 0.35f));
                }

                if (Game1.random.NextDouble() < 0.45)
                {
                    Vector2 offset = new(Game1.random.Next(-36, 36), Game1.random.Next(-64, 16));
                    bool isFeather = Game1.random.NextBool();
                    Particles.Add(new VfxParticle(
                        Game1.player.Position + offset,
                        new Vector2(Game1.random.Next(-25, 25), Game1.random.Next(-70, -20)),
                        isFeather ? new Color(35, 10, 45) : new Color(255, 30, 50),
                        isFeather ? 7f : 4f,
                        0.75f
                    ));
                }

                // Với quái thường: Miễn nhiễm 100% sát thương & không tốn Ma Pháp
                if (CurrentBoss == null)
                    Game1.player.temporarilyInvincible = true;

                if (AssimilationTimer <= 0f || CurrentMana <= 0f)
    
