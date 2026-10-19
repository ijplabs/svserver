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

    public ProfessionSelectionMenu(IModHelper helper, IMonitor monitor, int rancherCount = 0, int rancherMax = 2, string currentProfession = "")
        : base(0, 0, 960, 560, showUpperRightCloseButton: true)
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
            "Agrikultur",
            "Ahli bercocok tanam otomatis & panen melimpah.",
            new List<string>
            {
                "- 1x Quality Sprinkler (8 petak)",
                "- 15x Bibit Tanaman Musiman",
                "- 15x Basic Fertilizer"
            }
        ));

        _options.Add(new ProfessionOption(
            "miner",
            "Penambang",
            "Tambang & Gua",
            "Ahli penjelajah gua, bebatuan, & bijih logam.",
            new List<string>
            {
                "- 1x Copper Pickaxe (Upgrade)",
                "- 10x Obor Tambang (Torches)",
                "- 3x Cherry Bombs",
                "- 2x Miner's Treat (+1 Mining)"
            }
        ));

        _options.Add(new ProfessionOption(
            "fisherman",
            "Nelayan",
            "Bahari & Sungai",
            "Pemasok ikan & makanan energi untuk tim.",
            new List<string>
            {
                "- 1x Fiberglass Rod (Pancing)",
                "- 20x Umpan Ikan (Bait)",
                "- 2x Crab Pot (Bubu Kepiting)",
                "- 2x Dish o' The Sea (+3 Fishing)"
            }
        ));

        string rancherNote = _rancherCount >= _rancherMax
            ? $"Peternak (Penuh {_rancherCount}/{_rancherMax})"
            : $"Peternak ({_rancherCount}/{_rancherMax})";

        _options.Add(new ProfessionOption(
            "rancher",
            rancherNote,
            "Ternak Hewan",
            "Spesialis kandang, unggas, & produk harian.",
            new List<string>
            {
                "- 1x Kandang Ayam (Coop) di Farm",
                "- 2x Ekor Ayam Penghasil Telur",
                "- 30x Jerami (Hay)",
                "- 1x Milk Pail & 1x Shears"
            }
        ));

        _options.Add(new ProfessionOption(
            "forager",
            "Penebang",
            "Hutan & Alam",
            "Penebang kayu, pembuka lahan & ekspedisi liar.",
            new List<string>
            {
                "- 1x Copper Axe (Upgrade Kapak)",
                "- 1x Charcoal Kiln (Tungku Arang)",
                "- 10x Tree Fertilizer",
                "- 2x Survival Burger (+3 Foraging)"
            }
        ));
    }

    private void Reposition()
    {
        width = Math.Min(960, Game1.uiViewport.Width - 32);
        height = Math.Min(560, Game1.uiViewport.Height - 32);
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

        _closeButton = new ClickableTextureComponent(
            new Rectangle(xPositionOnScreen + width - 40, yPositionOnScreen + 16, 28, 28),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            2.5f
        );

        _cardBounds.Clear();
        _buttonBounds.Clear();

        int numCards = _options.Count;
        int paddingSide = 24;
        int gap = 12;
        int availWidth = width - (paddingSide * 2) - (gap * (numCards - 1));
        int cardWidth = availWidth / numCards;
        int cardHeight = height - 144;
        int startY = yPositionOnScreen + 88;

        for (int i = 0; i < numCards; i++)
        {
            int cardX = xPositionOnScreen + paddingSide + i * (cardWidth + gap);
            _cardBounds.Add(new Rectangle(cardX, startY, cardWidth, cardHeight));

            int btnH = 36;
            int btnW = cardWidth - 24;
            int btnX = cardX + 12;
            int btnY = startY + cardHeight - btnH - 12;
            _buttonBounds.Add(new Rectangle(btnX, btnY, btnW, btnH));
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
            yPositionOnScreen + 20
        );
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont, titlePos, Game1.textColor);

        // Subtitle
        string subtitle = "Pilih 1 peran untuk petualangan Lembah Pelikan (Pilihan permanen 1 kali)";
        var subSize = Game1.smallFont.MeasureString(subtitle);
        var subPos = new Vector2(
            xPositionOnScreen + (width - subSize.X) / 2f,
            yPositionOnScreen + 56
        );
        Utility.drawTextWithShadow(b, subtitle, Game1.smallFont, subPos, new Color(71, 85, 105));

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
            Color cardBg = isCardHovered ? new Color(254, 252, 240) : Color.White;
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
                drawShadow: false
            );

            // Card outline accent
            Color accentColor = opt.Key switch
            {
                "farmer" => new Color(34, 139, 34),
                "miner" => new Color(184, 115, 51),
                "fisherman" => new Color(30, 144, 255),
                "rancher" => new Color(218, 165, 32),
                "forager" => new Color(46, 139, 87),
                _ => Color.DarkSlateGray
            };

            // Category Badge
            string catText = $"[{opt.Category.ToUpper()}]";
            var catSize = Game1.tinyFont.MeasureString(catText);
            var catPos = new Vector2(card.X + (card.Width - catSize.X) / 2f, card.Y + 12);
            Utility.drawTextWithShadow(b, catText, Game1.tinyFont, catPos, accentColor);

            // Profession Title
            string profName = opt.DisplayName;
            var profSize = Game1.smallFont.MeasureString(profName);
            var profPos = new Vector2(card.X + (card.Width - profSize.X) / 2f, card.Y + 28);
            Utility.drawTextWithShadow(b, profName, Game1.smallFont, profPos, Game1.textColor);

            // Divider line
            b.Draw(Game1.staminaRect, new Rectangle(card.X + 12, card.Y + 54, card.Width - 24, 2), new Color(226, 232, 240));

            // Description
            string desc = Game1.parseText(opt.Description, Game1.tinyFont, card.Width - 16);
            var descPos = new Vector2(card.X + 8, card.Y + 62);
            b.DrawString(Game1.tinyFont, desc, descPos, new Color(51, 65, 85));

            // Bonus Label
            var bonusLabelPos = new Vector2(card.X + 8, card.Y + 116);
            b.DrawString(Game1.tinyFont, "Paket Perlengkapan:", bonusLabelPos, new Color(15, 23, 42));

            // Starter item list
            float itemY = card.Y + 134;
            foreach (var itemText in opt.StarterItems)
            {
                string parsedItem = Game1.parseText(itemText, Game1.tinyFont, card.Width - 16);
                b.DrawString(Game1.tinyFont, parsedItem, new Vector2(card.X + 8, itemY), new Color(71, 85, 105));
                itemY += Game1.tinyFont.MeasureString(parsedItem).Y + 4f;
            }

            // Button Box
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

            // Button Text
            string btnText = isRancherFull ? "KUOTA PENUH" : "PILIH ROLE";
            Color btnTextColor = isRancherFull ? Color.DimGray : (isBtnHovered ? Color.DarkSlateBlue : Game1.textColor);
            var btnTextSize = Game1.smallFont.MeasureString(btnText);
            var btnTextPos = new Vector2(
                btn.X + (btn.Width - btnTextSize.X) / 2f,
                btn.Y + (btn.Height - btnTextSize.Y) / 2f
            );
            Utility.drawTextWithShadow(b, btnText, Game1.smallFont, btnTextPos, btnTextColor);
        }

        // Draw Mouse Cursor
        drawMouse(b);
    }
}
