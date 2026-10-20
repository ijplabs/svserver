using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace JunimoClientSync;

public class ProfessionOption
{
    public string Key { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string Description { get; }
    public List<string> StarterItems { get; }

    public ProfessionOption(string key, string displayName, string category, string description, List<string> starterItems)
    {
        Key = key;
        DisplayName = displayName;
        Category = category;
        Description = description;
        StarterItems = starterItems;
    }
}

public class ProfessionSelectionMenu : IClickableMenu
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly int _rancherCount;
    private readonly int _rancherMax;
    private readonly string _currentProfession;

    private readonly List<ProfessionOption> _options = new();
    private readonly List<Rectangle> _cardBounds = new();
    private readonly List<Rectangle> _buttonBounds = new();

    private ClickableTextureComponent? _closeButton;
    private int _hoveredCardIndex = -1;
    private int _hoveredButtonIndex = -1;
    private bool _isTwoTier;

    public ProfessionSelectionMenu(IModHelper helper, IMonitor monitor, int rancherCount = 0, int rancherMax = 2, string currentProfession = "")
        : base(0, 0, 1060, 620, showUpperRightCloseButton: true)
    {
        _helper = helper;
        _monitor = monitor;
        _rancherCount = rancherCount;
        _rancherMax = rancherMax;
        _currentProfession = currentProfession;

        InitOptions();
        Reposition();
    }

    private void InitOptions()
    {
        _options.Clear();

        _options.Add(new ProfessionOption(
            "farmer",
            "Petani",
            "PETANI",
            "Pakar budidaya tanaman & sprinkler siram otomatis.",
            new List<string>
            {
                "1x Quality Sprinkler (8 petak)",
                "15x Bibit Tanaman Musiman",
                "15x Basic Fertilizer"
            }
        ));

        _options.Add(new ProfessionOption(
            "miner",
            "Penambang",
            "TAMBANG",
            "Ahli eksplorasi gua, bebatuan, & bijih logam.",
            new List<string>
            {
                "1x Copper Pickaxe (Level 1)",
                "10x Obor Tambang (Torches)",
                "3x Cherry Bombs",
                "2x Miner's Treat (+1 Mining)"
            }
        ));

        _options.Add(new ProfessionOption(
            "fisherman",
            "Nelayan",
            "BAHARI",
            "Penyedia ikan segar, tangkapan laut, & energi tim.",
            new List<string>
            {
                "1x Fiberglass Rod (Pancing)",
                "20x Umpan Ikan (Bait)",
                "2x Bubu Kepiting (Crabpot)",
                "2x Dish o' The Sea (+3 Fish)"
            }
        ));

        string rancherNote = _rancherCount >= _rancherMax
            ? $"Peternak ({_rancherCount}/{_rancherMax})"
            : $"Peternak ({_rancherCount}/{_rancherMax})";

        _options.Add(new ProfessionOption(
            "rancher",
            rancherNote,
            "TERNAK",
            "Spesialis kandang, unggas, & produk harian.",
            new List<string>
            {
                "1x Kandang Ayam (Coop Instan)",
                "2x Ekor Ayam Penghasil Telur",
                "30x Jerami Makanan (Hay)",
                "1x Milk Pail & 1x Shears"
            }
        ));

        _options.Add(new ProfessionOption(
            "forager",
            "Penebang",
            "HUTAN",
            "Penebang kayu, pembuka lahan, & ekspedisi alam.",
            new List<string>
            {
                "1x Copper Axe (Level 1)",
                "1x Charcoal Kiln (Tungku)",
                "10x Tree Fertilizer",
                "2x Survival Burger (+3 Forage)"
            }
        ));
    }

    private void Reposition()
    {
        _isTwoTier = Game1.uiViewport.Width < 1000;

        _cardBounds.Clear();
        _buttonBounds.Clear();

        if (!_isTwoTier)
        {
            width = Math.Min(1060, Game1.uiViewport.Width - 32);
            height = Math.Min(620, Game1.uiViewport.Height - 32);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            _closeButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + width - 42, yPositionOnScreen + 16, 28, 28),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                2.5f
            );

            int numCards = _options.Count; // 5
            int paddingSide = 20;
            int gap = 12;
            int availWidth = width - (paddingSide * 2) - (gap * (numCards - 1));
            int cardWidth = Math.Max(180, availWidth / numCards);
            int cardHeight = height - 120;
            int startY = yPositionOnScreen + 84;

            for (int i = 0; i < numCards; i++)
            {
                int cardX = xPositionOnScreen + paddingSide + i * (cardWidth + gap);
                var cardRect = new Rectangle(cardX, startY, cardWidth, cardHeight);
                _cardBounds.Add(cardRect);

                int btnH = 38;
                int btnW = cardWidth - 20;
                int btnX = cardX + 10;
                int btnY = cardRect.Bottom - btnH - 12;
                _buttonBounds.Add(new Rectangle(btnX, btnY, btnW, btnH));
            }
        }
        else
        {
            // Dynamic Two-Tier Layout: 3 cards top row, 2 cards bottom row
            width = Math.Min(840, Game1.uiViewport.Width - 24);
            height = Math.Min(660, Game1.uiViewport.Height - 24);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            _closeButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + width - 40, yPositionOnScreen + 14, 28, 28),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                2.5f
            );

            int paddingSide = 16;
            int gap = 12;
            int availWidth = width - (paddingSide * 2) - (gap * 2);
            int cardWidth = Math.Max(180, availWidth / 3);
            int headerH = 68;
            int rowGap = 10;
            int cardHeight = Math.Max(210, (height - headerH - rowGap - 24) / 2);
            int row1Y = yPositionOnScreen + headerH;
            int row2Y = row1Y + cardHeight + rowGap;

            // Row 1: 3 cards (Petani, Penambang, Nelayan)
            for (int i = 0; i < 3; i++)
            {
                int cardX = xPositionOnScreen + paddingSide + i * (cardWidth + gap);
                var cardRect = new Rectangle(cardX, row1Y, cardWidth, cardHeight);
                _cardBounds.Add(cardRect);

                int btnH = 34;
                int btnW = cardWidth - 16;
                int btnX = cardX + 8;
                int btnY = cardRect.Bottom - btnH - 8;
                _buttonBounds.Add(new Rectangle(btnX, btnY, btnW, btnH));
            }

            // Row 2: 2 cards (Peternak, Penebang) - centered horizontally
            int row2TotalWidth = (cardWidth * 2) + gap;
            int row2StartX = xPositionOnScreen + (width - row2TotalWidth) / 2;
            for (int i = 0; i < 2; i++)
            {
                int cardX = row2StartX + i * (cardWidth + gap);
                var cardRect = new Rectangle(cardX, row2Y, cardWidth, cardHeight);
                _cardBounds.Add(cardRect);

                int btnH = 34;
                int btnW = cardWidth - 16;
                int btnX = cardX + 8;
                int btnY = cardRect.Bottom - btnH - 8;
                _buttonBounds.Add(new Rectangle(btnX, btnY, btnW, btnH));
            }
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        Reposition();
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);

        _hoveredCardIndex = -1;
        _hoveredButtonIndex = -1;

        if (_closeButton != null)
        {
            _closeButton.tryHover(x, y);
        }

        for (int i = 0; i < _cardBounds.Count; i++)
        {
            if (_cardBounds[i].Contains(x, y))
            {
                _hoveredCardIndex = i;
            }
            if (_buttonBounds[i].Contains(x, y))
            {
                _hoveredButtonIndex = i;
            }
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        if (_closeButton != null && _closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            exitThisMenu();
            return;
        }

        for (int i = 0; i < _buttonBounds.Count; i++)
        {
            if (_buttonBounds[i].Contains(x, y) || _cardBounds[i].Contains(x, y))
            {
                var opt = _options[i];

                if (opt.Key == "rancher" && _rancherCount >= _rancherMax && !string.Equals(_currentProfession, "rancher", StringComparison.OrdinalIgnoreCase))
                {
                    Game1.playSound("cancel");
                    Game1.showRedMessage("Kuota Peternak sudah penuh (maksimal 2).");
                    return;
                }

                SelectProfession(opt);
                return;
            }
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            Game1.playSound("bigDeSelect");
            exitThisMenu();
            return;
        }

        base.receiveKeyPress(key);
    }

    private void SelectProfession(ProfessionOption opt)
    {
        Game1.playSound("coin");

        try
        {
            // Send SMAPI mod message directly to server
            _helper.Multiplayer.SendMessage(
                opt.Key,
                "SelectProfession",
                modIDs: new[] { "JunimoHost.Server" }
            );

            // Display chat confirmation feedback
            Game1.chatBox?.addInfoMessage($"[Profesi] Anda memilih peran {opt.DisplayName}. Mengirim ke server...");

            _monitor.Log($"[JunimoClientSync] Selected profession '{opt.Key}'. Sent to server.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _monitor.Log($"[JunimoClientSync] Error sending profession selection: {ex.Message}", LogLevel.Error);
        }

        exitThisMenu();
    }

    private static void DrawScaledText(SpriteBatch b, string text, SpriteFont font, Vector2 position, Color color, float scale = 1f, bool shadow = true)
    {
        if (shadow)
        {
            b.DrawString(font, text, position + new Vector2(1f, 1f), Game1.textShadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
        b.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    public override void draw(SpriteBatch b)
    {
        // Background dim
        b.Draw(
            Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.65f
        );

        // Main Menu Texture Box
        drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            xPositionOnScreen,
            yPositionOnScreen,
            width,
            height,
            Color.White,
            1f,
            drawShadow: true
        );

        // Title
        string title = "PILIH PROFESI SPESIALISASI RPG";
        var titleSize = Game1.dialogueFont.MeasureString(title);
        var titlePos = new Vector2(
            xPositionOnScreen + (width - titleSize.X) / 2f,
            yPositionOnScreen + 18
        );
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont, titlePos, Game1.textColor);

        // Subtitle
        string subtitle = "Pilih 1 peran untuk petualangan Lembah Pelikan (Pilihan permanen 1 kali)";
        float subScale = 0.85f;
        var subSize = Game1.smallFont.MeasureString(subtitle) * subScale;
        var subPos = new Vector2(
            xPositionOnScreen + (width - subSize.X) / 2f,
            yPositionOnScreen + 52
        );
        DrawScaledText(b, subtitle, Game1.smallFont, subPos, new Color(71, 85, 105), subScale, shadow: false);

        // Draw Close Button
        _closeButton?.draw(b);

        // Draw 5 Profession Cards
        for (int i = 0; i < _options.Count; i++)
        {
            var opt = _options[i];
            var card = _cardBounds[i];
            var btn = _buttonBounds[i];

            bool isCardHovered = _hoveredCardIndex == i;
            bool isBtnHovered = _hoveredButtonIndex == i;
            bool isRancherFull = opt.Key == "rancher" && _rancherCount >= _rancherMax && !string.Equals(_currentProfession, "rancher", StringComparison.OrdinalIgnoreCase);

            // Card box
            Color cardBg = isCardHovered ? new Color(255, 253, 245) : Color.White;
            drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                card.X,
                card.Y,
                card.Width,
                card.Height,
                cardBg,
                1f,
                drawShadow: isCardHovered
            );

            // Accent color
            Color accentColor = opt.Key switch
            {
                "farmer" => new Color(46, 125, 50),
                "miner" => new Color(194, 65, 12),
                "fisherman" => new Color(2, 132, 199),
                "rancher" => new Color(180, 83, 9),
                "forager" => new Color(13, 148, 136),
                _ => Color.DarkSlateGray
            };

            // 1. Category Tag (Short, uppercase, non-overlapping)
            string catText = $"[{opt.Category}]";
            float catScale = _isTwoTier ? 0.68f : 0.72f;
            float catW = Game1.tinyFont.MeasureString(catText).X * catScale;
            Vector2 catPos = new Vector2(card.X + (card.Width - catW) / 2f, card.Y + (_isTwoTier ? 8 : 12));
            DrawScaledText(b, catText, Game1.tinyFont, catPos, accentColor, catScale, shadow: false);

            // 2. Profession Title
            string profName = opt.DisplayName;
            float profScale = _isTwoTier ? 0.80f : 0.85f;
            float profW = Game1.smallFont.MeasureString(profName).X * profScale;
            Vector2 profPos = new Vector2(card.X + (card.Width - profW) / 2f, card.Y + (_isTwoTier ? 24 : 30));
            DrawScaledText(b, profName, Game1.smallFont, profPos, Game1.textColor, profScale, shadow: true);

            // 3. Top Divider
            int topDivY = card.Y + (_isTwoTier ? 46 : 56);
            b.Draw(Game1.staminaRect, new Rectangle(card.X + 8, topDivY, card.Width - 16, 1), new Color(226, 232, 240));

            // 4. Role Summary (1-2 lines, nicely scaled)
            float descScale = _isTwoTier ? 0.66f : 0.72f;
            int maxDescWidth = (int)((card.Width - 16) / descScale);
            string desc = Game1.parseText(opt.Description, Game1.tinyFont, maxDescWidth);
            Vector2 descPos = new Vector2(card.X + 8, topDivY + (_isTwoTier ? 4 : 8));
            DrawScaledText(b, desc, Game1.tinyFont, descPos, new Color(71, 85, 105), descScale, shadow: false);
            float descH = Game1.tinyFont.MeasureString(desc).Y * descScale;

            // 5. Middle Divider
            int midDivY = (int)(descPos.Y + descH + (_isTwoTier ? 4 : 6));
            b.Draw(Game1.staminaRect, new Rectangle(card.X + 8, midDivY, card.Width - 16, 1), new Color(226, 232, 240));

            // 6. Bonus Header Label
            float bonusHdrScale = _isTwoTier ? 0.66f : 0.72f;
            Vector2 bonusLabelPos = new Vector2(card.X + 8, midDivY + (_isTwoTier ? 4 : 8));
            DrawScaledText(b, "Paket Hadiah Starter:", Game1.smallFont, bonusLabelPos, new Color(30, 41, 59), bonusHdrScale, shadow: false);
            float bonusHdrH = Game1.smallFont.MeasureString("Paket").Y * bonusHdrScale;

            // 7. Starter Item List (strictly bounded to stop before the button)
            float itemY = bonusLabelPos.Y + bonusHdrH + (_isTwoTier ? 3 : 6);
            float itemScale = _isTwoTier ? 0.64f : 0.70f;
            int maxItemWidth = (int)((card.Width - 16) / itemScale);

            foreach (var itemText in opt.StarterItems)
            {
                string bullet = $"• {itemText}";
                string parsedItem = Game1.parseText(bullet, Game1.tinyFont, maxItemWidth);
                float itemH = Game1.tinyFont.MeasureString(parsedItem).Y * itemScale;

                // Stop drawing item text if it reaches the button threshold
                if (itemY + itemH > btn.Y - 4)
                {
                    break;
                }

                DrawScaledText(b, parsedItem, Game1.tinyFont, new Vector2(card.X + 8, itemY), new Color(51, 65, 85), itemScale, shadow: false);
                itemY += itemH + (_isTwoTier ? 2f : 4f);
            }

            // 8. Button Box (Fixed at bottom)
            Color btnColor = isRancherFull ? Color.LightGray : (isBtnHovered ? Color.Wheat : Color.White);
            drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                btn.X,
                btn.Y,
                btn.Width,
                btn.Height,
                btnColor,
                1f,
                drawShadow: !isRancherFull
            );

            // 9. Button Text (Centered inside button)
            string btnText = isRancherFull ? "KUOTA PENUH" : "PILIH ROLE";
            Color btnTextColor = isRancherFull ? Color.DimGray : (isBtnHovered ? Color.DarkSlateBlue : Game1.textColor);
            float btnScale = 0.80f;
            var btnTextSize = Game1.smallFont.MeasureString(btnText) * btnScale;
            var btnTextPos = new Vector2(
                btn.X + (btn.Width - btnTextSize.X) / 2f,
                btn.Y + (btn.Height - btnTextSize.Y) / 2f
            );
            DrawScaledText(b, btnText, Game1.smallFont, btnTextPos, btnTextColor, btnScale, shadow: !isRancherFull);
        }

        // Draw Mouse Cursor
        drawMouse(b);
    }
}
