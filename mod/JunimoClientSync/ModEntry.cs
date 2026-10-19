using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using JunimoClientSync.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace JunimoClientSync;

public class ModEntry : Mod
{
    public static ModEntry? Instance { get; private set; }
    public ModConfig Config { get; private set; } = new();

    private bool _isSyncing;
    private string _syncStatus = "";
    private string _syncDetail = "";
    private float _progress;
    private bool _showModal;
    private bool _syncCompleted;
    private bool _restartNeeded;
    private int _updatedCount;
    private Rectangle _lobbyButtonBounds = Rectangle.Empty;
    private Rectangle _modalOkButtonBounds = Rectangle.Empty;
    private Rectangle _modalQuitButtonBounds = Rectangle.Empty;
    private bool _isHoveringLobbyButton;

    private Rectangle _professionHudButtonBounds = Rectangle.Empty;
    private bool _isHoveringProfessionHudButton;
    private bool _hasAutoRequestedProfession;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += (s, ev) => _hasAutoRequestedProfession = false;
        helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Input.CursorMoved += OnCursorMoved;
        helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;

        Monitor.Log($"[JunimoClientSync] Loaded v{ModManifest.Version} (Chunked Range & Signed). Ready to sync.", LogLevel.Info);

        helper.ConsoleCommands.Add(
            "sync_mods",
            "Synchronize installed mods with the dedicated server. Usage: sync_mods",
            (cmd, args) =>
            {
                Monitor.Log("[JunimoClientSync] Manual sync triggered via console command.", LogLevel.Info);
                _ = TriggerSyncWorkflowAsync();
            }
        );

        helper.ConsoleCommands.Add(
            "profesi",
            "Buka menu pemilihan profesi RPG Lembah Pelikan. Usage: profesi",
            (cmd, args) => OpenProfessionMenu()
        );

        helper.ConsoleCommands.Add(
            "role",
            "Buka menu pemilihan peran RPG Lembah Pelikan. Usage: role",
            (cmd, args) => OpenProfessionMenu()
        );
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        // GMCM Integration
        var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm != null)
        {
            gmcm.Register(
                ModManifest,
                () => Config = new ModConfig(),
                () => Helper.WriteConfig(Config)
            );

            gmcm.AddSectionTitle(ModManifest, () => "Junimo Client Sync Settings");
            gmcm.AddTextOption(
                ModManifest,
                () => Config.ServerUrl,
                value => Config.ServerUrl = value,
                () => "Server API URL",
                () => "Address of the dedicated server host."
            );
            gmcm.AddBoolOption(
                ModManifest,
                () => Config.ShowLobbyButton,
                value => Config.ShowLobbyButton = value,
                () => "Show Lobby Button",
                () => "Display 'Sync Server Mods' button in Title & Co-op menu."
            );
        }
    }

    private void OnCursorMoved(object? sender, CursorMovedEventArgs e)
    {
        var mousePos = new Point((int)e.NewPosition.ScreenPixels.X, (int)e.NewPosition.ScreenPixels.Y);
        bool wasHovering = _isHoveringLobbyButton;
        _isHoveringLobbyButton = _lobbyButtonBounds.Contains(mousePos);

        if (!_isHoveringLobbyButton && wasHovering)
        {
            // Mouse exited
        }
        else if (_isHoveringLobbyButton && !wasHovering)
        {
            Game1.playSound("Cowboy_gunshot");
        }

        bool wasHoveringProf = _isHoveringProfessionHudButton;
        _isHoveringProfessionHudButton = _professionHudButtonBounds.Contains(mousePos);
        if (_isHoveringProfessionHudButton && !wasHoveringProf)
        {
            Game1.playSound("shiny4");
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button != SButton.MouseLeft)
        {
            return;
        }

        var mousePos = new Point((int)e.Cursor.ScreenPixels.X, (int)e.Cursor.ScreenPixels.Y);

        if (_showModal)
        {
            if (_modalOkButtonBounds.Contains(mousePos))
            {
                Game1.playSound("drumkit6");
                _showModal = false;
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (_restartNeeded && _modalQuitButtonBounds.Contains(mousePos))
            {
                Game1.playSound("select");
                Game1.quit = true;
                Helper.Input.Suppress(e.Button);
                return;
            }

            // Click outside or modal absorbed
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (Context.IsWorldReady && Context.IsMultiplayer && Game1.activeClickableMenu == null)
        {
            if (_professionHudButtonBounds.Contains(mousePos))
            {
                Helper.Input.Suppress(e.Button);
                Game1.playSound("bigSelect");
                OpenProfessionMenu();
                return;
            }
        }

        if (Config.ShowLobbyButton && IsInTitleOrLobbyMenu())
        {
            if (_lobbyButtonBounds.Contains(mousePos))
            {
                Helper.Input.Suppress(e.Button);
                if (!_isSyncing)
                {
                    Game1.playSound("select");
                    _ = TriggerSyncWorkflowAsync();
                }
            }
        }
    }

    private bool IsInTitleOrLobbyMenu()
    {
        var menu = Game1.activeClickableMenu;
        if (menu == null)
        {
            return false;
        }

        string typeName = menu.GetType().Name;
        return typeName.Contains("Title") || typeName.Contains("Coop") || menu is TitleMenu || menu is CoopMenu;
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        var b = e.SpriteBatch;

        if (Config.ShowLobbyButton && IsInTitleOrLobbyMenu() && !_showModal)
        {
            DrawLobbySyncButton(b);
        }

        if (_showModal)
        {
            DrawSyncModal(b);
        }
    }

    private void DrawLobbySyncButton(SpriteBatch b)
    {
        int btnWidth = 220;
        int btnHeight = 44;
        int btnX = Game1.uiViewport.Width - btnWidth - 16;
        int btnY = 16;

        _lobbyButtonBounds = new Rectangle(btnX, btnY, btnWidth, btnHeight);

        // Draw background box
        Color boxColor = _isHoveringLobbyButton ? Color.Wheat : Color.White;
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            btnX,
            btnY,
            btnWidth,
            btnHeight,
            boxColor,
            1f,
            drawShadow: true
        );

        // Button text
        string label = _isSyncing ? "Syncing..." : "Sync Server Mods";
        var textSize = Game1.smallFont.MeasureString(label);
        var textPos = new Vector2(
            btnX + (btnWidth - textSize.X) / 2f,
            btnY + (btnHeight - textSize.Y) / 2f
        );

        Color textColor = _isHoveringLobbyButton ? Color.DarkSlateGray : Game1.textColor;
        Utility.drawTextWithShadow(b, label, Game1.smallFont, textPos, textColor);

        if (_isHoveringLobbyButton)
        {
            Game1.mouseCursorTransparency = 1f;
            b.Draw(
                Game1.mouseCursors,
                new Vector2(Game1.getMouseX(), Game1.getMouseY()),
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 0, 16, 16),
                Color.White,
                0f,
                Vector2.Zero,
                4f + Game1.dialogueButtonScale / 150f,
                SpriteEffects.None,
                1f
            );
        }
    }

    private void DrawSyncModal(SpriteBatch b)
    {
        // Darken background
        b.Draw(
            Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.55f
        );

        int modalW = 540;
        int modalH = 280;
        int modalX = (Game1.uiViewport.Width - modalW) / 2;
        int modalY = (Game1.uiViewport.Height - modalH) / 2;

        // Modal Frame
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            modalX,
            modalY,
            modalW,
            modalH,
            Color.White,
            1f,
            drawShadow: true
        );

        // Header Title
        string title = _syncCompleted ? $"Junimo Mod Sync v{ModManifest.Version} (Completed)" : $"Junimo Mod Sync v{ModManifest.Version}";
        var titleSize = Game1.dialogueFont.MeasureString(title);
        var titlePos = new Vector2(modalX + (modalW - titleSize.X) / 2f, modalY + 24);
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont, titlePos, Game1.textColor);

        // Status Line
        var statusSize = Game1.smallFont.MeasureString(_syncStatus);
        var statusPos = new Vector2(modalX + 32, modalY + 85);
        b.DrawString(Game1.smallFont, _syncStatus, statusPos, Color.DarkSlateGray);

        // Detail Line
        if (!string.IsNullOrWhiteSpace(_syncDetail))
        {
            var detailPos = new Vector2(modalX + 32, modalY + 115);
            b.DrawString(Game1.smallFont, _syncDetail, detailPos, Color.DimGray);
        }

        // Progress Bar
        if (_isSyncing)
        {
            int barX = modalX + 32;
            int barY = modalY + 155;
            int barW = modalW - 64;
            int barH = 22;

            // Background border
            b.Draw(Game1.staminaRect, new Rectangle(barX - 2, barY - 2, barW + 4, barH + 4), Color.Black * 0.4f);
            b.Draw(Game1.staminaRect, new Rectangle(barX, barY, barW, barH), Color.SlateGray * 0.3f);

            // Fill
            int fillW = (int)Math.Clamp(barW * _progress, 0, barW);
            if (fillW > 0)
            {
                b.Draw(Game1.staminaRect, new Rectangle(barX, barY, fillW, barH), new Color(74, 144, 226));
            }

            string pctStr = $"{(_progress * 100):0}%";
            var pctSize = Game1.smallFont.MeasureString(pctStr);
            b.DrawString(Game1.smallFont, pctStr, new Vector2(barX + (barW - pctSize.X) / 2, barY + (barH - pctSize.Y) / 2), Color.White);
        }

        // Action Buttons
        if (!_isSyncing)
        {
            int btnH = 44;
            int btnY = modalY + modalH - btnH - 24;

            if (_restartNeeded)
            {
                int btnW = 180;
                int gap = 24;
                int totalBtnsW = btnW * 2 + gap;
                int startX = modalX + (modalW - totalBtnsW) / 2;

                _modalQuitButtonBounds = new Rectangle(startX, btnY, btnW, btnH);
                _modalOkButtonBounds = new Rectangle(startX + btnW + gap, btnY, btnW, btnH);

                // Exit Game Button
                DrawDialogButton(b, _modalQuitButtonBounds, "Exit Game", Color.IndianRed);
                // Continue Later Button
                DrawDialogButton(b, _modalOkButtonBounds, "Continue", Color.DarkSlateGray);
            }
            else
            {
                int btnW = 160;
                int btnX = modalX + (modalW - btnW) / 2;
                _modalOkButtonBounds = new Rectangle(btnX, btnY, btnW, btnH);

                DrawDialogButton(b, _modalOkButtonBounds, "OK", Color.DarkSlateGray);
            }
        }
    }

    private static void DrawDialogButton(SpriteBatch b, Rectangle bounds, string text, Color textColor)
    {
        bool isHover = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            isHover ? Color.Wheat : Color.White,
            1f,
            drawShadow: true
        );

        var textSize = Game1.smallFont.MeasureString(text);
        var textPos = new Vector2(
            bounds.X + (bounds.Width - textSize.X) / 2f,
            bounds.Y + (bounds.Height - textSize.Y) / 2f
        );

        Utility.drawTextWithShadow(b, text, Game1.smallFont, textPos, textColor);
    }

    public async Task TriggerSyncWorkflowAsync()
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;
        _showModal = true;
        _restartNeeded = false;
        _progress = 0.05f;
        _syncStatus = "Connecting to dedicated server...";
        _syncDetail = Config.ServerUrl;

        try
        {
            string baseUrl = Config.ServerUrl.TrimEnd('/');
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

            _syncStatus = "Fetching server mod manifest...";
            _progress = 0.15f;

            var manifestResp = await client.GetAsync($"{baseUrl}/api/mods/sync/manifest");
            if (!manifestResp.IsSuccessStatusCode)
            {
                throw new Exception($"Server returned HTTP {(int)manifestResp.StatusCode}: {manifestResp.ReasonPhrase}");
            }

            var json = await manifestResp.Content.ReadAsStringAsync();
            var manifest = JsonConvert.DeserializeObject<SyncManifest>(json);

            if (manifest == null || manifest.Mods == null)
            {
                throw new Exception("Received empty or invalid manifest payload from server.");
            }

            _progress = 0.3f;
            _syncStatus = "Analyzing local mods directory...";

            // Find client Mods directory
            var modsDir = Directory.GetParent(Helper.DirectoryPath)?.FullName ?? Helper.DirectoryPath;
            var installedMods = Helper.ModRegistry.GetAll().ToList();

            var neededMods = new List<SyncModItem>();

            foreach (var serverMod in manifest.Mods)
            {
                var local = installedMods.FirstOrDefault(m =>
                    string.Equals(m.Manifest.UniqueID, serverMod.UniqueId, StringComparison.OrdinalIgnoreCase)
                );

                if (local == null)
                {
                    neededMods.Add(serverMod);
                }
                else
                {
                    // Check version difference
                    string localVer = local.Manifest.Version.ToString();
                    if (IsVersionOlder(localVer, serverMod.Version))
                    {
                        neededMods.Add(serverMod);
                    }
                }
            }

            if (neededMods.Count == 0)
            {
                _progress = 1.0f;
                _syncStatus = "All mods are up to date!";
                _syncDetail = $"Verified {manifest.ModCount} mod(s) matching server v{manifest.ServerVersion}.";
                _isSyncing = false;
                _syncCompleted = true;
                _restartNeeded = false;
                Monitor.Log("[JunimoClientSync] All mods are verified and match the server.", LogLevel.Info);
                return;
            }

            Monitor.Log($"[JunimoClientSync] Found {neededMods.Count} mod(s) requiring download/update.", LogLevel.Info);
            _updatedCount = neededMods.Count;
            float totalModSlice = 0.65f / neededMods.Count;

            // Download and extract each mod
            for (int i = 0; i < neededMods.Count; i++)
            {
                var mod = neededMods[i];
                float baseProg = 0.3f + (0.65f * ((float)i / neededMods.Count));
                _progress = baseProg;
                _syncStatus = $"Downloading {mod.Name}...";
                _syncDetail = $"{i + 1}/{neededMods.Count} (v{mod.Version})";

                string downloadUrl = mod.DownloadUrl.StartsWith("http")
                    ? mod.DownloadUrl
                    : $"{baseUrl}{mod.DownloadUrl}";

                var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }).Distinct().ToArray();
                string safeBaseName = string.Join("_", mod.FolderName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
                string tempZipPath = Path.Combine(Path.GetTempPath(), $"junimo_sync_{safeBaseName}_{Guid.NewGuid():N}.zip");
                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); } catch { }
                }

                // Probe ContentLength and Accept-Ranges support
                long totalBytes = mod.SizeBytes;
                bool supportsRanges = false;
                try
                {
                    using var headReq = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
                    using var headResp = await client.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead);
                    if (headResp.IsSuccessStatusCode)
                    {
                        if (headResp.Content.Headers.ContentLength.HasValue && headResp.Content.Headers.ContentLength.Value > 0)
                        {
                            totalBytes = headResp.Content.Headers.ContentLength.Value;
                        }
                        supportsRanges = headResp.Headers.AcceptRanges.Contains("bytes") || headResp.Headers.Contains("Accept-Ranges");
                    }
                }
                catch
                {
                    // Fallback to default streaming
                }

                const long ChunkSize = 5 * 1024 * 1024; // 5 MB chunks to prevent tunnel timeouts

                if (supportsRanges && totalBytes > ChunkSize)
                {
                    // 1. Chunked Range Download
                    using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        long downloaded = 0;
                        while (downloaded < totalBytes)
                        {
                            long start = downloaded;
                            long end = Math.Min(start + ChunkSize - 1, totalBytes - 1);

                            bool chunkSuccess = false;
                            int retries = 0;
                            while (!chunkSuccess && retries < 3)
                            {
                                try
                                {
                                    using var chunkReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                                    chunkReq.Headers.Range = new RangeHeaderValue(start, end);
                                    using var chunkResp = await client.SendAsync(chunkReq, HttpCompletionOption.ResponseHeadersRead);
                                    chunkResp.EnsureSuccessStatusCode();

                                    using var stream = await chunkResp.Content.ReadAsStreamAsync();
                                    byte[] buf = new byte[65536];
                                    int read;
                                    long chunkRead = 0;
                                    while ((read = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
                                    {
                                        await fileStream.WriteAsync(buf, 0, read);
                                        chunkRead += read;
                                        long currentTotal = downloaded + chunkRead;
                                        float frac = (float)currentTotal / totalBytes;
                                        _progress = baseProg + (totalModSlice * frac);
                                        _syncDetail = $"{i + 1}/{neededMods.Count} • {FormatBytes(currentTotal)} / {FormatBytes(totalBytes)} ({(int)(frac * 100)}%)";
                                    }

                                    downloaded += chunkRead;
                                    chunkSuccess = true;
                                }
                                catch (Exception ex)
                                {
                                    retries++;
                                    Monitor.Log($"[JunimoClientSync] Chunk {start}-{end} failed (attempt {retries}/3): {ex.Message}. Retrying...", LogLevel.Warn);
                                    if (retries >= 3)
                                    {
                                        throw;
                                    }
                                    await Task.Delay(1000);
                                    fileStream.Seek(start, SeekOrigin.Begin);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 2. Streamed download with real-time 64KB buffer progress
                    using var resp = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();

                    if (totalBytes <= 0 && resp.Content.Headers.ContentLength.HasValue)
                    {
                        totalBytes = resp.Content.Headers.ContentLength.Value;
                    }

                    using var downloadStream = await resp.Content.ReadAsStreamAsync();
                    using var fileStream = File.Create(tempZipPath);
                    byte[] buffer = new byte[65536];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            float frac = Math.Min(1f, (float)totalRead / totalBytes);
                            _progress = baseProg + (totalModSlice * frac);
                            _syncDetail = $"{i + 1}/{neededMods.Count} • {FormatBytes(totalRead)} / {FormatBytes(totalBytes)} ({(int)(frac * 100)}%)";
                        }
                        else
                        {
                            _syncDetail = $"{i + 1}/{neededMods.Count} • {FormatBytes(totalRead)} downloaded";
                        }
                    }
                }

                _progress = baseProg + totalModSlice;
                _syncStatus = $"Extracting {mod.Name}...";
                _syncDetail = $"{i + 1}/{neededMods.Count} (Decompressing files)";

                // Extract zip directly into client Mods folder
                try
                {
                    ZipFile.ExtractToDirectory(tempZipPath, modsDir, overwriteFiles: true);
                }
                finally
                {
                    if (File.Exists(tempZipPath))
                    {
                        try { File.Delete(tempZipPath); } catch { }
                    }
                }
            }

            _progress = 1.0f;
            _isSyncing = false;
            _syncCompleted = true;
            _restartNeeded = true;
            _syncStatus = $"Sync Complete! {_updatedCount} mod(s) updated.";
            _syncDetail = "Please restart Stardew Valley once to load the newly synchronized mods.";
            Monitor.Log($"[JunimoClientSync] Successfully synchronized {_updatedCount} mod(s) to {modsDir}.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _isSyncing = false;
            _restartNeeded = false;
            _syncStatus = "Sync failed: " + ex.Message;
            _syncDetail = "Check SMAPI console log or verify server connection.";
            Monitor.Log($"[JunimoClientSync] Sync failed: {ex}", LogLevel.Error);
        }
    }

    private static bool IsVersionOlder(string current, string required)
    {
        if (string.IsNullOrWhiteSpace(required))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        try
        {
            var curParts = current.Split('-', '+')[0].Split('.').Select(int.Parse).ToArray();
            var reqParts = required.Split('-', '+')[0].Split('.').Select(int.Parse).ToArray();

            int len = Math.Max(curParts.Length, reqParts.Length);
            for (int i = 0; i < len; i++)
            {
                int cur = i < curParts.Length ? curParts[i] : 0;
                int req = i < reqParts.Length ? reqParts[i] : 0;
                if (cur < req)
                {
                    return true;
                }
                if (cur > req)
                {
                    return false;
                }
            }
            return false;
        }
        catch
        {
            return !string.Equals(current, required, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024)
        {
            return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
        }
        if (bytes >= 1024 * 1024)
        {
            return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
        }
        if (bytes >= 1024)
        {
            return $"{(bytes / 1024.0):F0} KB";
        }
        return $"{bytes} B";
    }

    private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (Context.IsWorldReady && Context.IsMultiplayer && !_hasAutoRequestedProfession)
        {
            _hasAutoRequestedProfession = true;
            if (!Game1.player.modData.ContainsKey("svserver/profession"))
            {
                try
                {
                    Helper.Multiplayer.SendMessage("REQ", "RequestProfessionUI", modIDs: new[] { "JunimoHost.Server" });
                }
                catch { }
            }
        }
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsMultiplayer || Game1.activeClickableMenu != null)
        {
            _professionHudButtonBounds = Rectangle.Empty;
            return;
        }

        bool hasProfession = Game1.player.modData.ContainsKey("svserver/profession");

        if (!hasProfession)
        {
            DrawProfessionHudButton(e.SpriteBatch);
        }
        else
        {
            _professionHudButtonBounds = Rectangle.Empty;
        }
    }

    private void DrawProfessionHudButton(SpriteBatch b)
    {
        int btnW = 190;
        int btnH = 44;
        int btnX = 16;
        int btnY = 16;

        _professionHudButtonBounds = new Rectangle(btnX, btnY, btnW, btnH);

        Color boxColor = _isHoveringProfessionHudButton ? Color.Wheat : Color.White;
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            btnX,
            btnY,
            btnW,
            btnH,
            boxColor,
            1f,
            drawShadow: true
        );

        string label = "Pilih Profesi RPG";
        var textSize = Game1.smallFont.MeasureString(label);
        var textPos = new Vector2(
            btnX + (btnW - textSize.X) / 2f,
            btnY + (btnH - textSize.Y) / 2f
        );

        Color textColor = _isHoveringProfessionHudButton ? Color.DarkSlateBlue : Game1.textColor;
        Utility.drawTextWithShadow(b, label, Game1.smallFont, textPos, textColor);

        if (_isHoveringProfessionHudButton)
        {
            Game1.mouseCursorTransparency = 1f;
            b.Draw(
                Game1.mouseCursors,
                new Vector2(Game1.getMouseX(), Game1.getMouseY()),
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 0, 16, 16),
                Color.White,
                0f,
                Vector2.Zero,
                4f + Game1.dialogueButtonScale / 150f,
                SpriteEffects.None,
                1f
            );
        }
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != "JunimoHost.Server")
        {
            return;
        }

        if (e.Type == "ShowProfessionUI")
        {
            string payload = e.ReadAs<string>();
            var parts = payload.Split('|');
            string currentProf = parts.Length > 0 ? parts[0] : "";
            int rancherCount = (parts.Length > 1 && int.TryParse(parts[1], out int rc)) ? rc : 0;
            int rancherMax = (parts.Length > 2 && int.TryParse(parts[2], out int rm)) ? rm : 2;

            Monitor.Log($"[JunimoClientSync] Received ShowProfessionUI modal request (current='{currentProf}', rancher={rancherCount}/{rancherMax})", LogLevel.Info);
            Game1.activeClickableMenu = new ProfessionSelectionMenu(Helper, Monitor, rancherCount, rancherMax, currentProf);
        }
        else if (e.Type == "ProfessionConfirmed")
        {
            string chosen = e.ReadAs<string>();
            Game1.player.modData["svserver/profession"] = chosen;
            _professionHudButtonBounds = Rectangle.Empty;
            Game1.playSound("reward");
            Monitor.Log($"[JunimoClientSync] Profession confirmed as '{chosen}'.", LogLevel.Info);
        }
    }

    public void OpenProfessionMenu()
    {
        int rancherCount = 0;
        int rancherMax = 2;
        string currentProf = Game1.player.modData.TryGetValue("svserver/profession", out var p) ? p : "";

        // Also ask server for latest quota
        try
        {
            Helper.Multiplayer.SendMessage("REQ", "RequestProfessionUI", modIDs: new[] { "JunimoHost.Server" });
        }
        catch { }

        Game1.activeClickableMenu = new ProfessionSelectionMenu(Helper, Monitor, rancherCount, rancherMax, currentProf);
    }
}

public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
    void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
    void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
}
