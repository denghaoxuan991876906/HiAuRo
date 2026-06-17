using System.Numerics;
using System.Reflection;
using Dalamud.Interface.ManagedFontAtlas;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 图标辅助 — 从 EmbeddedResource TTF 加载自定义 Game-Icon-Pack 字体，支持双字号
/// </summary>
public static class IconHelper
{
    /// <summary>Game-Icon-Pack 图标 Unicode 码点 (Private Use Area U+EA00+) — 共 504 个图标</summary>
    public static class Icons
    {
        /// <summary>Editing_Tools_align-bottom</summary>
        public const string EditingToolsAlignBottom = "\uea01";
        /// <summary>Editing_Tools_align-bottom2</summary>
        public const string EditingToolsAlignBottom2 = "\uea02";
        /// <summary>Editing_Tools_align-horizontal-centers</summary>
        public const string EditingToolsAlignHorizontalCenters = "\uea03";
        /// <summary>Editing_Tools_align-horizontal-centers2</summary>
        public const string EditingToolsAlignHorizontalCenters2 = "\uea04";
        /// <summary>Editing_Tools_align-left</summary>
        public const string EditingToolsAlignLeft = "\uea05";
        /// <summary>Editing_Tools_align-left2</summary>
        public const string EditingToolsAlignLeft2 = "\uea06";
        /// <summary>Editing_Tools_align-right</summary>
        public const string EditingToolsAlignRight = "\uea07";
        /// <summary>Editing_Tools_align-right2</summary>
        public const string EditingToolsAlignRight2 = "\uea08";
        /// <summary>Editing_Tools_align-top</summary>
        public const string EditingToolsAlignTop = "\uea09";
        /// <summary>Editing_Tools_align-top2</summary>
        public const string EditingToolsAlignTop2 = "\uea0a";
        /// <summary>Editing_Tools_align-vertical-centers</summary>
        public const string EditingToolsAlignVerticalCenters = "\uea0b";
        /// <summary>Editing_Tools_align-vertical-centers2</summary>
        public const string EditingToolsAlignVerticalCenters2 = "\uea0c";
        /// <summary>Editing_Tools_bold</summary>
        public const string EditingToolsBold = "\uea0d";
        /// <summary>Editing_Tools_brush</summary>
        public const string EditingToolsBrush = "\uea0e";
        /// <summary>Editing_Tools_copy</summary>
        public const string EditingToolsCopy = "\uea0f";
        /// <summary>Editing_Tools_cursor-alternate-select</summary>
        public const string EditingToolsCursorAlternateSelect = "\uea10";
        /// <summary>Editing_Tools_cursor-busy</summary>
        public const string EditingToolsCursorBusy = "\uea11";
        /// <summary>Editing_Tools_cursor-busy2</summary>
        public const string EditingToolsCursorBusy2 = "\uea12";
        /// <summary>Editing_Tools_cursor-busy3</summary>
        public const string EditingToolsCursorBusy3 = "\uea13";
        /// <summary>Editing_Tools_cursor-default</summary>
        public const string EditingToolsCursorDefault = "\uea14";
        /// <summary>Editing_Tools_cursor-default2</summary>
        public const string EditingToolsCursorDefault2 = "\uea15";
        /// <summary>Editing_Tools_cursor-diagonal-resize</summary>
        public const string EditingToolsCursorDiagonalResize = "\uea16";
        /// <summary>Editing_Tools_cursor-diagonal-resize2</summary>
        public const string EditingToolsCursorDiagonalResize2 = "\uea17";
        /// <summary>Editing_Tools_cursor-help</summary>
        public const string EditingToolsCursorHelp = "\uea18";
        /// <summary>Editing_Tools_cursor-help2</summary>
        public const string EditingToolsCursorHelp2 = "\uea19";
        /// <summary>Editing_Tools_cursor-horizontal-resize</summary>
        public const string EditingToolsCursorHorizontalResize = "\uea1a";
        /// <summary>Editing_Tools_cursor-link</summary>
        public const string EditingToolsCursorLink = "\uea1b";
        /// <summary>Editing_Tools_cursor-loading</summary>
        public const string EditingToolsCursorLoading = "\uea1c";
        /// <summary>Editing_Tools_cursor-loading2</summary>
        public const string EditingToolsCursorLoading2 = "\uea1d";
        /// <summary>Editing_Tools_cursor-move</summary>
        public const string EditingToolsCursorMove = "\uea1e";
        /// <summary>Editing_Tools_cursor-pen</summary>
        public const string EditingToolsCursorPen = "\uea1f";
        /// <summary>Editing_Tools_cursor-precision-select</summary>
        public const string EditingToolsCursorPrecisionSelect = "\uea20";
        /// <summary>Editing_Tools_cursor-text-select</summary>
        public const string EditingToolsCursorTextSelect = "\uea21";
        /// <summary>Editing_Tools_cursor-unavailable</summary>
        public const string EditingToolsCursorUnavailable = "\uea22";
        /// <summary>Editing_Tools_cursor-unavailable2</summary>
        public const string EditingToolsCursorUnavailable2 = "\uea23";
        /// <summary>Editing_Tools_cursor-vertical-resize</summary>
        public const string EditingToolsCursorVerticalResize = "\uea24";
        /// <summary>Editing_Tools_eraser</summary>
        public const string EditingToolsEraser = "\uea25";
        /// <summary>Editing_Tools_eyedropper</summary>
        public const string EditingToolsEyedropper = "\uea26";
        /// <summary>Editing_Tools_fill</summary>
        public const string EditingToolsFill = "\uea27";
        /// <summary>Editing_Tools_font</summary>
        public const string EditingToolsFont = "\uea28";
        /// <summary>Editing_Tools_font2</summary>
        public const string EditingToolsFont2 = "\uea29";
        /// <summary>Editing_Tools_frame</summary>
        public const string EditingToolsFrame = "\uea2a";
        /// <summary>Editing_Tools_italic</summary>
        public const string EditingToolsItalic = "\uea2b";
        /// <summary>Editing_Tools_outline</summary>
        public const string EditingToolsOutline = "\uea2c";
        /// <summary>Editing_Tools_paintbrush</summary>
        public const string EditingToolsPaintbrush = "\uea2d";
        /// <summary>Editing_Tools_palette</summary>
        public const string EditingToolsPalette = "\uea2e";
        /// <summary>Editing_Tools_pen</summary>
        public const string EditingToolsPen = "\uea2f";
        /// <summary>Editing_Tools_pencil</summary>
        public const string EditingToolsPencil = "\uea30";
        /// <summary>Editing_Tools_redo</summary>
        public const string EditingToolsRedo = "\uea31";
        /// <summary>Editing_Tools_select</summary>
        public const string EditingToolsSelect = "\uea32";
        /// <summary>Editing_Tools_split</summary>
        public const string EditingToolsSplit = "\uea33";
        /// <summary>Editing_Tools_strikethrough</summary>
        public const string EditingToolsStrikethrough = "\uea34";
        /// <summary>Editing_Tools_text</summary>
        public const string EditingToolsText = "\uea35";
        /// <summary>Editing_Tools_underline</summary>
        public const string EditingToolsUnderline = "\uea36";
        /// <summary>Editing_Tools_undo</summary>
        public const string EditingToolsUndo = "\uea37";
        /// <summary>Game_bank</summary>
        public const string GameBank = "\uea38";
        /// <summary>Game_boss</summary>
        public const string GameBoss = "\uea39";
        /// <summary>Game_card</summary>
        public const string GameCard = "\uea3a";
        /// <summary>Game_cards</summary>
        public const string GameCards = "\uea3b";
        /// <summary>Game_character</summary>
        public const string GameCharacter = "\uea3c";
        /// <summary>Game_club-card</summary>
        public const string GameClubCard = "\uea3d";
        /// <summary>Game_clubs</summary>
        public const string GameClubs = "\uea3e";
        /// <summary>Game_death</summary>
        public const string GameDeath = "\uea3f";
        /// <summary>Game_diamond-card</summary>
        public const string GameDiamondCard = "\uea40";
        /// <summary>Game_diamonds</summary>
        public const string GameDiamonds = "\uea41";
        /// <summary>Game_dice-pair</summary>
        public const string GameDicePair = "\uea42";
        /// <summary>Game_dice</summary>
        public const string GameDice = "\uea43";
        /// <summary>Game_dice2</summary>
        public const string GameDice2 = "\uea44";
        /// <summary>Game_dice3</summary>
        public const string GameDice3 = "\uea45";
        /// <summary>Game_dice4</summary>
        public const string GameDice4 = "\uea46";
        /// <summary>Game_dice5</summary>
        public const string GameDice5 = "\uea47";
        /// <summary>Game_dice6</summary>
        public const string GameDice6 = "\uea48";
        /// <summary>Game_double</summary>
        public const string GameDouble = "\uea49";
        /// <summary>Game_experience</summary>
        public const string GameExperience = "\uea4a";
        /// <summary>Game_female</summary>
        public const string GameFemale = "\uea4b";
        /// <summary>Game_ghost</summary>
        public const string GameGhost = "\uea4c";
        /// <summary>Game_halloween</summary>
        public const string GameHalloween = "\uea4d";
        /// <summary>Game_heart-card</summary>
        public const string GameHeartCard = "\uea4e";
        /// <summary>Game_heart</summary>
        public const string GameHeart = "\uea4f";
        /// <summary>Game_hearts</summary>
        public const string GameHearts = "\uea50";
        /// <summary>Game_hit-effect</summary>
        public const string GameHitEffect = "\uea51";
        /// <summary>Game_house</summary>
        public const string GameHouse = "\uea52";
        /// <summary>Game_house2</summary>
        public const string GameHouse2 = "\uea53";
        /// <summary>Game_house3</summary>
        public const string GameHouse3 = "\uea54";
        /// <summary>Game_house4</summary>
        public const string GameHouse4 = "\uea55";
        /// <summary>Game_i-tetris-i</summary>
        public const string GameITetrisI = "\uea56";
        /// <summary>Game_j-tetris-j</summary>
        public const string GameJTetrisJ = "\uea57";
        /// <summary>Game_l-tetris-l</summary>
        public const string GameLTetrisL = "\uea58";
        /// <summary>Game_level</summary>
        public const string GameLevel = "\uea59";
        /// <summary>Game_male</summary>
        public const string GameMale = "\uea5a";
        /// <summary>Game_o-tetris-o</summary>
        public const string GameOTetrisO = "\uea5b";
        /// <summary>Game_onefold</summary>
        public const string GameOnefold = "\uea5c";
        /// <summary>Game_puzzle</summary>
        public const string GamePuzzle = "\uea5d";
        /// <summary>Game_puzzle10</summary>
        public const string GamePuzzle10 = "\uea5e";
        /// <summary>Game_puzzle11</summary>
        public const string GamePuzzle11 = "\uea5f";
        /// <summary>Game_puzzle12</summary>
        public const string GamePuzzle12 = "\uea60";
        /// <summary>Game_puzzle2</summary>
        public const string GamePuzzle2 = "\uea61";
        /// <summary>Game_puzzle3</summary>
        public const string GamePuzzle3 = "\uea62";
        /// <summary>Game_puzzle4</summary>
        public const string GamePuzzle4 = "\uea63";
        /// <summary>Game_puzzle5</summary>
        public const string GamePuzzle5 = "\uea64";
        /// <summary>Game_puzzle6</summary>
        public const string GamePuzzle6 = "\uea65";
        /// <summary>Game_puzzle7</summary>
        public const string GamePuzzle7 = "\uea66";
        /// <summary>Game_puzzle8</summary>
        public const string GamePuzzle8 = "\uea67";
        /// <summary>Game_puzzle9</summary>
        public const string GamePuzzle9 = "\uea68";
        /// <summary>Game_random-dice</summary>
        public const string GameRandomDice = "\uea69";
        /// <summary>Game_ranking</summary>
        public const string GameRanking = "\uea6a";
        /// <summary>Game_s-tetris-s</summary>
        public const string GameSTetrisS = "\uea6b";
        /// <summary>Game_shop</summary>
        public const string GameShop = "\uea6c";
        /// <summary>Game_six-sided-dice</summary>
        public const string GameSixSidedDice = "\uea6d";
        /// <summary>Game_six-sided-dice2</summary>
        public const string GameSixSidedDice2 = "\uea6e";
        /// <summary>Game_six-sided-dice3</summary>
        public const string GameSixSidedDice3 = "\uea6f";
        /// <summary>Game_six-sided-dice4</summary>
        public const string GameSixSidedDice4 = "\uea70";
        /// <summary>Game_six-sided-dice5</summary>
        public const string GameSixSidedDice5 = "\uea71";
        /// <summary>Game_six-sided-dice6</summary>
        public const string GameSixSidedDice6 = "\uea72";
        /// <summary>Game_skull</summary>
        public const string GameSkull = "\uea73";
        /// <summary>Game_spade-card</summary>
        public const string GameSpadeCard = "\uea74";
        /// <summary>Game_spades</summary>
        public const string GameSpades = "\uea75";
        /// <summary>Game_stamina</summary>
        public const string GameStamina = "\uea76";
        /// <summary>Game_t-tetris-t</summary>
        public const string GameTTetrisT = "\uea77";
        /// <summary>Game_tent</summary>
        public const string GameTent = "\uea78";
        /// <summary>Game_tower</summary>
        public const string GameTower = "\uea79";
        /// <summary>Game_tower2</summary>
        public const string GameTower2 = "\uea7a";
        /// <summary>Game_tower3</summary>
        public const string GameTower3 = "\uea7b";
        /// <summary>Game_tower4</summary>
        public const string GameTower4 = "\uea7c";
        /// <summary>Game_tower5</summary>
        public const string GameTower5 = "\uea7d";
        /// <summary>Game_tower6</summary>
        public const string GameTower6 = "\uea7e";
        /// <summary>Game_town</summary>
        public const string GameTown = "\uea7f";
        /// <summary>Game_triple</summary>
        public const string GameTriple = "\uea80";
        /// <summary>Game_trophy</summary>
        public const string GameTrophy = "\uea81";
        /// <summary>Game_village</summary>
        public const string GameVillage = "\uea82";
        /// <summary>Game_z-tetris-z</summary>
        public const string GameZTetrisZ = "\uea83";
        /// <summary>Items_anchor</summary>
        public const string ItemsAnchor = "\uea84";
        /// <summary>Items_anvil</summary>
        public const string ItemsAnvil = "\uea85";
        /// <summary>Items_arrow</summary>
        public const string ItemsArrow = "\uea86";
        /// <summary>Items_attach</summary>
        public const string ItemsAttach = "\uea87";
        /// <summary>Items_axe</summary>
        public const string ItemsAxe = "\uea88";
        /// <summary>Items_backpack</summary>
        public const string ItemsBackpack = "\uea89";
        /// <summary>Items_bomb</summary>
        public const string ItemsBomb = "\uea8a";
        /// <summary>Items_bone</summary>
        public const string ItemsBone = "\uea8b";
        /// <summary>Items_book</summary>
        public const string ItemsBook = "\uea8c";
        /// <summary>Items_boot</summary>
        public const string ItemsBoot = "\uea8d";
        /// <summary>Items_boots</summary>
        public const string ItemsBoots = "\uea8e";
        /// <summary>Items_bow</summary>
        public const string ItemsBow = "\uea8f";
        /// <summary>Items_broadsword</summary>
        public const string ItemsBroadsword = "\uea90";
        /// <summary>Items_broadsword2</summary>
        public const string ItemsBroadsword2 = "\uea91";
        /// <summary>Items_bullet</summary>
        public const string ItemsBullet = "\uea92";
        /// <summary>Items_bullhorn</summary>
        public const string ItemsBullhorn = "\uea93";
        /// <summary>Items_chest</summary>
        public const string ItemsChest = "\uea94";
        /// <summary>Items_coin</summary>
        public const string ItemsCoin = "\uea95";
        /// <summary>Items_compass</summary>
        public const string ItemsCompass = "\uea96";
        /// <summary>Items_crown</summary>
        public const string ItemsCrown = "\uea97";
        /// <summary>Items_crystal-ball</summary>
        public const string ItemsCrystalBall = "\uea98";
        /// <summary>Items_cuirass</summary>
        public const string ItemsCuirass = "\uea99";
        /// <summary>Items_dagger</summary>
        public const string ItemsDagger = "\uea9a";
        /// <summary>Items_diamond</summary>
        public const string ItemsDiamond = "\uea9b";
        /// <summary>Items_fishhook</summary>
        public const string ItemsFishhook = "\uea9c";
        /// <summary>Items_fishing-rod</summary>
        public const string ItemsFishingRod = "\uea9d";
        /// <summary>Items_funnel</summary>
        public const string ItemsFunnel = "\uea9e";
        /// <summary>Items_gravestone</summary>
        public const string ItemsGravestone = "\uea9f";
        /// <summary>Items_greave</summary>
        public const string ItemsGreave = "\ueaa0";
        /// <summary>Items_hammer</summary>
        public const string ItemsHammer = "\ueaa1";
        /// <summary>Items_hammer2</summary>
        public const string ItemsHammer2 = "\ueaa2";
        /// <summary>Items_hayfork</summary>
        public const string ItemsHayfork = "\ueaa3";
        /// <summary>Items_helmet</summary>
        public const string ItemsHelmet = "\ueaa4";
        /// <summary>Items_helmet2</summary>
        public const string ItemsHelmet2 = "\ueaa5";
        /// <summary>Items_helmet3</summary>
        public const string ItemsHelmet3 = "\ueaa6";
        /// <summary>Items_ingot</summary>
        public const string ItemsIngot = "\ueaa7";
        /// <summary>Items_key</summary>
        public const string ItemsKey = "\ueaa8";
        /// <summary>Items_lantern</summary>
        public const string ItemsLantern = "\ueaa9";
        /// <summary>Items_lantern2</summary>
        public const string ItemsLantern2 = "\ueaaa";
        /// <summary>Items_lantern3</summary>
        public const string ItemsLantern3 = "\ueaab";
        /// <summary>Items_lantern4</summary>
        public const string ItemsLantern4 = "\ueaac";
        /// <summary>Items_lantern5</summary>
        public const string ItemsLantern5 = "\ueaad";
        /// <summary>Items_magnet</summary>
        public const string ItemsMagnet = "\ueaae";
        /// <summary>Items_map</summary>
        public const string ItemsMap = "\ueaaf";
        /// <summary>Items_medical-kit</summary>
        public const string ItemsMedicalKit = "\ueab0";
        /// <summary>Items_mirror</summary>
        public const string ItemsMirror = "\ueab1";
        /// <summary>Items_missile</summary>
        public const string ItemsMissile = "\ueab2";
        /// <summary>Items_nail</summary>
        public const string ItemsNail = "\ueab3";
        /// <summary>Items_nails</summary>
        public const string ItemsNails = "\ueab4";
        /// <summary>Items_paint-roller</summary>
        public const string ItemsPaintRoller = "\ueab5";
        /// <summary>Items_petroleum</summary>
        public const string ItemsPetroleum = "\ueab6";
        /// <summary>Items_pickaxe</summary>
        public const string ItemsPickaxe = "\ueab7";
        /// <summary>Items_pill</summary>
        public const string ItemsPill = "\ueab8";
        /// <summary>Items_pistol</summary>
        public const string ItemsPistol = "\ueab9";
        /// <summary>Items_potion</summary>
        public const string ItemsPotion = "\ueaba";
        /// <summary>Items_potion2</summary>
        public const string ItemsPotion2 = "\ueabb";
        /// <summary>Items_potion3</summary>
        public const string ItemsPotion3 = "\ueabc";
        /// <summary>Items_pushpin</summary>
        public const string ItemsPushpin = "\ueabd";
        /// <summary>Items_radiation</summary>
        public const string ItemsRadiation = "\ueabe";
        /// <summary>Items_rifle</summary>
        public const string ItemsRifle = "\ueabf";
        /// <summary>Items_saw</summary>
        public const string ItemsSaw = "\ueac0";
        /// <summary>Items_scissors</summary>
        public const string ItemsScissors = "\ueac1";
        /// <summary>Items_screwdriver</summary>
        public const string ItemsScrewdriver = "\ueac2";
        /// <summary>Items_shield</summary>
        public const string ItemsShield = "\ueac3";
        /// <summary>Items_shield2</summary>
        public const string ItemsShield2 = "\ueac4";
        /// <summary>Items_shield3</summary>
        public const string ItemsShield3 = "\ueac5";
        /// <summary>Items_shovel</summary>
        public const string ItemsShovel = "\ueac6";
        /// <summary>Items_sickle</summary>
        public const string ItemsSickle = "\ueac7";
        /// <summary>Items_spear</summary>
        public const string ItemsSpear = "\ueac8";
        /// <summary>Items_star-coin</summary>
        public const string ItemsStarCoin = "\ueac9";
        /// <summary>Items_star-coin2</summary>
        public const string ItemsStarCoin2 = "\ueaca";
        /// <summary>Items_stick</summary>
        public const string ItemsStick = "\ueacb";
        /// <summary>Items_sword</summary>
        public const string ItemsSword = "\ueacc";
        /// <summary>Items_sycee</summary>
        public const string ItemsSycee = "\ueacd";
        /// <summary>Items_toilet-sucker</summary>
        public const string ItemsToiletSucker = "\ueace";
        /// <summary>Items_tool-kit</summary>
        public const string ItemsToolKit = "\ueacf";
        /// <summary>Items_torch</summary>
        public const string ItemsTorch = "\uead0";
        /// <summary>Items_trident</summary>
        public const string ItemsTrident = "\uead1";
        /// <summary>Items_wand</summary>
        public const string ItemsWand = "\uead2";
        /// <summary>Items_wand2</summary>
        public const string ItemsWand2 = "\uead3";
        /// <summary>Items_wand3</summary>
        public const string ItemsWand3 = "\uead4";
        /// <summary>Items_wrench</summary>
        public const string ItemsWrench = "\uead5";
        /// <summary>Media_Technology_audio-waves</summary>
        public const string MediaTechnologyAudioWaves = "\uead6";
        /// <summary>Media_Technology_battery-negative</summary>
        public const string MediaTechnologyBatteryNegative = "\uead7";
        /// <summary>Media_Technology_battery-positive</summary>
        public const string MediaTechnologyBatteryPositive = "\uead8";
        /// <summary>Media_Technology_bookmark</summary>
        public const string MediaTechnologyBookmark = "\uead9";
        /// <summary>Media_Technology_bt</summary>
        public const string MediaTechnologyBt = "\ueada";
        /// <summary>Media_Technology_bug</summary>
        public const string MediaTechnologyBug = "\ueadb";
        /// <summary>Media_Technology_calendar</summary>
        public const string MediaTechnologyCalendar = "\ueadc";
        /// <summary>Media_Technology_camera</summary>
        public const string MediaTechnologyCamera = "\ueadd";
        /// <summary>Media_Technology_classic-tv</summary>
        public const string MediaTechnologyClassicTv = "\ueade";
        /// <summary>Media_Technology_clock</summary>
        public const string MediaTechnologyClock = "\ueadf";
        /// <summary>Media_Technology_cloud-download</summary>
        public const string MediaTechnologyCloudDownload = "\ueae0";
        /// <summary>Media_Technology_cloud-upload</summary>
        public const string MediaTechnologyCloudUpload = "\ueae1";
        /// <summary>Media_Technology_code</summary>
        public const string MediaTechnologyCode = "\ueae2";
        /// <summary>Media_Technology_computer-host</summary>
        public const string MediaTechnologyComputerHost = "\ueae3";
        /// <summary>Media_Technology_connection</summary>
        public const string MediaTechnologyConnection = "\ueae4";
        /// <summary>Media_Technology_credit-card</summary>
        public const string MediaTechnologyCreditCard = "\ueae5";
        /// <summary>Media_Technology_dislike</summary>
        public const string MediaTechnologyDislike = "\ueae6";
        /// <summary>Media_Technology_display</summary>
        public const string MediaTechnologyDisplay = "\ueae7";
        /// <summary>Media_Technology_document</summary>
        public const string MediaTechnologyDocument = "\ueae8";
        /// <summary>Media_Technology_download</summary>
        public const string MediaTechnologyDownload = "\ueae9";
        /// <summary>Media_Technology_earth</summary>
        public const string MediaTechnologyEarth = "\ueaea";
        /// <summary>Media_Technology_export</summary>
        public const string MediaTechnologyExport = "\ueaeb";
        /// <summary>Media_Technology_fast-forward</summary>
        public const string MediaTechnologyFastForward = "\ueaec";
        /// <summary>Media_Technology_fast-rewind</summary>
        public const string MediaTechnologyFastRewind = "\ueaed";
        /// <summary>Media_Technology_file</summary>
        public const string MediaTechnologyFile = "\ueaee";
        /// <summary>Media_Technology_film</summary>
        public const string MediaTechnologyFilm = "\ueaef";
        /// <summary>Media_Technology_first-frame</summary>
        public const string MediaTechnologyFirstFrame = "\ueaf0";
        /// <summary>Media_Technology_flashdisk</summary>
        public const string MediaTechnologyFlashdisk = "\ueaf1";
        /// <summary>Media_Technology_folder</summary>
        public const string MediaTechnologyFolder = "\ueaf2";
        /// <summary>Media_Technology_gamepad</summary>
        public const string MediaTechnologyGamepad = "\ueaf3";
        /// <summary>Media_Technology_headset</summary>
        public const string MediaTechnologyHeadset = "\ueaf4";
        /// <summary>Media_Technology_image</summary>
        public const string MediaTechnologyImage = "\ueaf5";
        /// <summary>Media_Technology_import</summary>
        public const string MediaTechnologyImport = "\ueaf6";
        /// <summary>Media_Technology_internet</summary>
        public const string MediaTechnologyInternet = "\ueaf7";
        /// <summary>Media_Technology_keyboard</summary>
        public const string MediaTechnologyKeyboard = "\ueaf8";
        /// <summary>Media_Technology_laptop</summary>
        public const string MediaTechnologyLaptop = "\ueaf9";
        /// <summary>Media_Technology_last-frame</summary>
        public const string MediaTechnologyLastFrame = "\ueafa";
        /// <summary>Media_Technology_light</summary>
        public const string MediaTechnologyLight = "\ueafb";
        /// <summary>Media_Technology_like</summary>
        public const string MediaTechnologyLike = "\ueafc";
        /// <summary>Media_Technology_link</summary>
        public const string MediaTechnologyLink = "\ueafd";
        /// <summary>Media_Technology_live</summary>
        public const string MediaTechnologyLive = "\ueafe";
        /// <summary>Media_Technology_live2</summary>
        public const string MediaTechnologyLive2 = "\ueaff";
        /// <summary>Media_Technology_location</summary>
        public const string MediaTechnologyLocation = "\ueb00";
        /// <summary>Media_Technology_mail</summary>
        public const string MediaTechnologyMail = "\ueb01";
        /// <summary>Media_Technology_memory-card</summary>
        public const string MediaTechnologyMemoryCard = "\ueb02";
        /// <summary>Media_Technology_mention</summary>
        public const string MediaTechnologyMention = "\ueb03";
        /// <summary>Media_Technology_message</summary>
        public const string MediaTechnologyMessage = "\ueb04";
        /// <summary>Media_Technology_message2</summary>
        public const string MediaTechnologyMessage2 = "\ueb05";
        /// <summary>Media_Technology_mic</summary>
        public const string MediaTechnologyMic = "\ueb06";
        /// <summary>Media_Technology_microchip</summary>
        public const string MediaTechnologyMicrochip = "\ueb07";
        /// <summary>Media_Technology_mobile-data</summary>
        public const string MediaTechnologyMobileData = "\ueb08";
        /// <summary>Media_Technology_mobile-phone</summary>
        public const string MediaTechnologyMobilePhone = "\ueb09";
        /// <summary>Media_Technology_mouse</summary>
        public const string MediaTechnologyMouse = "\ueb0a";
        /// <summary>Media_Technology_music</summary>
        public const string MediaTechnologyMusic = "\ueb0b";
        /// <summary>Media_Technology_music2</summary>
        public const string MediaTechnologyMusic2 = "\ueb0c";
        /// <summary>Media_Technology_mute</summary>
        public const string MediaTechnologyMute = "\ueb0d";
        /// <summary>Media_Technology_next-frame</summary>
        public const string MediaTechnologyNextFrame = "\ueb0e";
        /// <summary>Media_Technology_next</summary>
        public const string MediaTechnologyNext = "\ueb0f";
        /// <summary>Media_Technology_no-music</summary>
        public const string MediaTechnologyNoMusic = "\ueb10";
        /// <summary>Media_Technology_no-music2</summary>
        public const string MediaTechnologyNoMusic2 = "\ueb11";
        /// <summary>Media_Technology_notification</summary>
        public const string MediaTechnologyNotification = "\ueb12";
        /// <summary>Media_Technology_pause</summary>
        public const string MediaTechnologyPause = "\ueb13";
        /// <summary>Media_Technology_phone</summary>
        public const string MediaTechnologyPhone = "\ueb14";
        /// <summary>Media_Technology_play</summary>
        public const string MediaTechnologyPlay = "\ueb15";
        /// <summary>Media_Technology_power-switch</summary>
        public const string MediaTechnologyPowerSwitch = "\ueb16";
        /// <summary>Media_Technology_previous-frame</summary>
        public const string MediaTechnologyPreviousFrame = "\ueb17";
        /// <summary>Media_Technology_previous</summary>
        public const string MediaTechnologyPrevious = "\ueb18";
        /// <summary>Media_Technology_protect</summary>
        public const string MediaTechnologyProtect = "\ueb19";
        /// <summary>Media_Technology_qr</summary>
        public const string MediaTechnologyQr = "\ueb1a";
        /// <summary>Media_Technology_record</summary>
        public const string MediaTechnologyRecord = "\ueb1b";
        /// <summary>Media_Technology_recording</summary>
        public const string MediaTechnologyRecording = "\ueb1c";
        /// <summary>Media_Technology_share</summary>
        public const string MediaTechnologyShare = "\ueb1d";
        /// <summary>Media_Technology_share2</summary>
        public const string MediaTechnologyShare2 = "\ueb1e";
        /// <summary>Media_Technology_silent</summary>
        public const string MediaTechnologySilent = "\ueb1f";
        /// <summary>Media_Technology_stop</summary>
        public const string MediaTechnologyStop = "\ueb20";
        /// <summary>Media_Technology_tablet</summary>
        public const string MediaTechnologyTablet = "\ueb21";
        /// <summary>Media_Technology_tag</summary>
        public const string MediaTechnologyTag = "\ueb22";
        /// <summary>Media_Technology_time</summary>
        public const string MediaTechnologyTime = "\ueb23";
        /// <summary>Media_Technology_trash</summary>
        public const string MediaTechnologyTrash = "\ueb24";
        /// <summary>Media_Technology_upload</summary>
        public const string MediaTechnologyUpload = "\ueb25";
        /// <summary>Media_Technology_video</summary>
        public const string MediaTechnologyVideo = "\ueb26";
        /// <summary>Media_Technology_volume</summary>
        public const string MediaTechnologyVolume = "\ueb27";
        /// <summary>Media_Technology_watch</summary>
        public const string MediaTechnologyWatch = "\ueb28";
        /// <summary>Media_Technology_webcam</summary>
        public const string MediaTechnologyWebcam = "\ueb29";
        /// <summary>Media_Technology_wifi</summary>
        public const string MediaTechnologyWifi = "\ueb2a";
        /// <summary>Shapes_Symbol_123</summary>
        public const string ShapesSymbol123 = "\ueb2b";
        /// <summary>Shapes_Symbol_a</summary>
        public const string ShapesSymbolA = "\ueb2c";
        /// <summary>Shapes_Symbol_abc</summary>
        public const string ShapesSymbolAbc = "\ueb2d";
        /// <summary>Shapes_Symbol_acute-triangle</summary>
        public const string ShapesSymbolAcuteTriangle = "\ueb2e";
        /// <summary>Shapes_Symbol_approximately-equal</summary>
        public const string ShapesSymbolApproximatelyEqual = "\ueb2f";
        /// <summary>Shapes_Symbol_arrow-down</summary>
        public const string ShapesSymbolArrowDown = "\ueb30";
        /// <summary>Shapes_Symbol_arrow-down2</summary>
        public const string ShapesSymbolArrowDown2 = "\ueb31";
        /// <summary>Shapes_Symbol_arrow-down3</summary>
        public const string ShapesSymbolArrowDown3 = "\ueb32";
        /// <summary>Shapes_Symbol_arrow-left</summary>
        public const string ShapesSymbolArrowLeft = "\ueb33";
        /// <summary>Shapes_Symbol_arrow-left2</summary>
        public const string ShapesSymbolArrowLeft2 = "\ueb34";
        /// <summary>Shapes_Symbol_arrow-left3</summary>
        public const string ShapesSymbolArrowLeft3 = "\ueb35";
        /// <summary>Shapes_Symbol_arrow-right</summary>
        public const string ShapesSymbolArrowRight = "\ueb36";
        /// <summary>Shapes_Symbol_arrow-right2</summary>
        public const string ShapesSymbolArrowRight2 = "\ueb37";
        /// <summary>Shapes_Symbol_arrow-right3</summary>
        public const string ShapesSymbolArrowRight3 = "\ueb38";
        /// <summary>Shapes_Symbol_arrow-up</summary>
        public const string ShapesSymbolArrowUp = "\ueb39";
        /// <summary>Shapes_Symbol_arrow-up2</summary>
        public const string ShapesSymbolArrowUp2 = "\ueb3a";
        /// <summary>Shapes_Symbol_arrow-up3</summary>
        public const string ShapesSymbolArrowUp3 = "\ueb3b";
        /// <summary>Shapes_Symbol_b</summary>
        public const string ShapesSymbolB = "\ueb3c";
        /// <summary>Shapes_Symbol_bitcoin</summary>
        public const string ShapesSymbolBitcoin = "\ueb3d";
        /// <summary>Shapes_Symbol_c</summary>
        public const string ShapesSymbolC = "\ueb3e";
        /// <summary>Shapes_Symbol_capsule</summary>
        public const string ShapesSymbolCapsule = "\ueb3f";
        /// <summary>Shapes_Symbol_circle</summary>
        public const string ShapesSymbolCircle = "\ueb40";
        /// <summary>Shapes_Symbol_cny-jpy</summary>
        public const string ShapesSymbolCnyJpy = "\ueb41";
        /// <summary>Shapes_Symbol_d</summary>
        public const string ShapesSymbolD = "\ueb42";
        /// <summary>Shapes_Symbol_division</summary>
        public const string ShapesSymbolDivision = "\ueb43";
        /// <summary>Shapes_Symbol_division2</summary>
        public const string ShapesSymbolDivision2 = "\ueb44";
        /// <summary>Shapes_Symbol_e</summary>
        public const string ShapesSymbolE = "\ueb45";
        /// <summary>Shapes_Symbol_ease-in-out</summary>
        public const string ShapesSymbolEaseInOut = "\ueb46";
        /// <summary>Shapes_Symbol_ease-in</summary>
        public const string ShapesSymbolEaseIn = "\ueb47";
        /// <summary>Shapes_Symbol_ease-out</summary>
        public const string ShapesSymbolEaseOut = "\ueb48";
        /// <summary>Shapes_Symbol_eight</summary>
        public const string ShapesSymbolEight = "\ueb49";
        /// <summary>Shapes_Symbol_ellipse</summary>
        public const string ShapesSymbolEllipse = "\ueb4a";
        /// <summary>Shapes_Symbol_emoji</summary>
        public const string ShapesSymbolEmoji = "\ueb4b";
        /// <summary>Shapes_Symbol_emoji10</summary>
        public const string ShapesSymbolEmoji10 = "\ueb4c";
        /// <summary>Shapes_Symbol_emoji11</summary>
        public const string ShapesSymbolEmoji11 = "\ueb4d";
        /// <summary>Shapes_Symbol_emoji12</summary>
        public const string ShapesSymbolEmoji12 = "\ueb4e";
        /// <summary>Shapes_Symbol_emoji13</summary>
        public const string ShapesSymbolEmoji13 = "\ueb4f";
        /// <summary>Shapes_Symbol_emoji14</summary>
        public const string ShapesSymbolEmoji14 = "\ueb50";
        /// <summary>Shapes_Symbol_emoji15</summary>
        public const string ShapesSymbolEmoji15 = "\ueb51";
        /// <summary>Shapes_Symbol_emoji16</summary>
        public const string ShapesSymbolEmoji16 = "\ueb52";
        /// <summary>Shapes_Symbol_emoji17</summary>
        public const string ShapesSymbolEmoji17 = "\ueb53";
        /// <summary>Shapes_Symbol_emoji18</summary>
        public const string ShapesSymbolEmoji18 = "\ueb54";
        /// <summary>Shapes_Symbol_emoji19</summary>
        public const string ShapesSymbolEmoji19 = "\ueb55";
        /// <summary>Shapes_Symbol_emoji2</summary>
        public const string ShapesSymbolEmoji2 = "\ueb56";
        /// <summary>Shapes_Symbol_emoji20</summary>
        public const string ShapesSymbolEmoji20 = "\ueb57";
        /// <summary>Shapes_Symbol_emoji3</summary>
        public const string ShapesSymbolEmoji3 = "\ueb58";
        /// <summary>Shapes_Symbol_emoji4</summary>
        public const string ShapesSymbolEmoji4 = "\ueb59";
        /// <summary>Shapes_Symbol_emoji5</summary>
        public const string ShapesSymbolEmoji5 = "\ueb5a";
        /// <summary>Shapes_Symbol_emoji6</summary>
        public const string ShapesSymbolEmoji6 = "\ueb5b";
        /// <summary>Shapes_Symbol_emoji7</summary>
        public const string ShapesSymbolEmoji7 = "\ueb5c";
        /// <summary>Shapes_Symbol_emoji8</summary>
        public const string ShapesSymbolEmoji8 = "\ueb5d";
        /// <summary>Shapes_Symbol_emoji9</summary>
        public const string ShapesSymbolEmoji9 = "\ueb5e";
        /// <summary>Shapes_Symbol_equals</summary>
        public const string ShapesSymbolEquals = "\ueb5f";
        /// <summary>Shapes_Symbol_equilateral-triangle</summary>
        public const string ShapesSymbolEquilateralTriangle = "\ueb60";
        /// <summary>Shapes_Symbol_eur</summary>
        public const string ShapesSymbolEur = "\ueb61";
        /// <summary>Shapes_Symbol_f</summary>
        public const string ShapesSymbolF = "\ueb62";
        /// <summary>Shapes_Symbol_five-pointed-star</summary>
        public const string ShapesSymbolFivePointedStar = "\ueb63";
        /// <summary>Shapes_Symbol_five</summary>
        public const string ShapesSymbolFive = "\ueb64";
        /// <summary>Shapes_Symbol_four-pointed-star</summary>
        public const string ShapesSymbolFourPointedStar = "\ueb65";
        /// <summary>Shapes_Symbol_four</summary>
        public const string ShapesSymbolFour = "\ueb66";
        /// <summary>Shapes_Symbol_function</summary>
        public const string ShapesSymbolFunction = "\ueb67";
        /// <summary>Shapes_Symbol_g</summary>
        public const string ShapesSymbolG = "\ueb68";
        /// <summary>Shapes_Symbol_gbp</summary>
        public const string ShapesSymbolGbp = "\ueb69";
        /// <summary>Shapes_Symbol_greater-than-or-equal</summary>
        public const string ShapesSymbolGreaterThanOrEqual = "\ueb6a";
        /// <summary>Shapes_Symbol_greater-than</summary>
        public const string ShapesSymbolGreaterThan = "\ueb6b";
        /// <summary>Shapes_Symbol_h</summary>
        public const string ShapesSymbolH = "\ueb6c";
        /// <summary>Shapes_Symbol_heptagon</summary>
        public const string ShapesSymbolHeptagon = "\ueb6d";
        /// <summary>Shapes_Symbol_hexagon</summary>
        public const string ShapesSymbolHexagon = "\ueb6e";
        /// <summary>Shapes_Symbol_i</summary>
        public const string ShapesSymbolI = "\ueb6f";
        /// <summary>Shapes_Symbol_infinity</summary>
        public const string ShapesSymbolInfinity = "\ueb70";
        /// <summary>Shapes_Symbol_inr</summary>
        public const string ShapesSymbolInr = "\ueb71";
        /// <summary>Shapes_Symbol_j</summary>
        public const string ShapesSymbolJ = "\ueb72";
        /// <summary>Shapes_Symbol_k</summary>
        public const string ShapesSymbolK = "\ueb73";
        /// <summary>Shapes_Symbol_krw</summary>
        public const string ShapesSymbolKrw = "\ueb74";
        /// <summary>Shapes_Symbol_l</summary>
        public const string ShapesSymbolL = "\ueb75";
        /// <summary>Shapes_Symbol_less-than-or-equal</summary>
        public const string ShapesSymbolLessThanOrEqual = "\ueb76";
        /// <summary>Shapes_Symbol_less-than</summary>
        public const string ShapesSymbolLessThan = "\ueb77";
        /// <summary>Shapes_Symbol_linear</summary>
        public const string ShapesSymbolLinear = "\ueb78";
        /// <summary>Shapes_Symbol_m</summary>
        public const string ShapesSymbolM = "\ueb79";
        /// <summary>Shapes_Symbol_minus</summary>
        public const string ShapesSymbolMinus = "\ueb7a";
        /// <summary>Shapes_Symbol_multiplication</summary>
        public const string ShapesSymbolMultiplication = "\ueb7b";
        /// <summary>Shapes_Symbol_multiplication2</summary>
        public const string ShapesSymbolMultiplication2 = "\ueb7c";
        /// <summary>Shapes_Symbol_multiplication3</summary>
        public const string ShapesSymbolMultiplication3 = "\ueb7d";
        /// <summary>Shapes_Symbol_n</summary>
        public const string ShapesSymbolN = "\ueb7e";
        /// <summary>Shapes_Symbol_ngn</summary>
        public const string ShapesSymbolNgn = "\ueb7f";
        /// <summary>Shapes_Symbol_nine</summary>
        public const string ShapesSymbolNine = "\ueb80";
        /// <summary>Shapes_Symbol_not-equal</summary>
        public const string ShapesSymbolNotEqual = "\ueb81";
        /// <summary>Shapes_Symbol_o</summary>
        public const string ShapesSymbolO = "\ueb82";
        /// <summary>Shapes_Symbol_obtuse-triangle</summary>
        public const string ShapesSymbolObtuseTriangle = "\ueb83";
        /// <summary>Shapes_Symbol_one</summary>
        public const string ShapesSymbolOne = "\ueb84";
        /// <summary>Shapes_Symbol_p</summary>
        public const string ShapesSymbolP = "\ueb85";
        /// <summary>Shapes_Symbol_parallelogram</summary>
        public const string ShapesSymbolParallelogram = "\ueb86";
        /// <summary>Shapes_Symbol_pentagon</summary>
        public const string ShapesSymbolPentagon = "\ueb87";
        /// <summary>Shapes_Symbol_percent</summary>
        public const string ShapesSymbolPercent = "\ueb88";
        /// <summary>Shapes_Symbol_pi</summary>
        public const string ShapesSymbolPi = "\ueb89";
        /// <summary>Shapes_Symbol_plus</summary>
        public const string ShapesSymbolPlus = "\ueb8a";
        /// <summary>Shapes_Symbol_q</summary>
        public const string ShapesSymbolQ = "\ueb8b";
        /// <summary>Shapes_Symbol_quadrilateral</summary>
        public const string ShapesSymbolQuadrilateral = "\ueb8c";
        /// <summary>Shapes_Symbol_quarter-circle</summary>
        public const string ShapesSymbolQuarterCircle = "\ueb8d";
        /// <summary>Shapes_Symbol_r</summary>
        public const string ShapesSymbolR = "\ueb8e";
        /// <summary>Shapes_Symbol_rectangle</summary>
        public const string ShapesSymbolRectangle = "\ueb8f";
        /// <summary>Shapes_Symbol_rhombus</summary>
        public const string ShapesSymbolRhombus = "\ueb90";
        /// <summary>Shapes_Symbol_right-trapezoid</summary>
        public const string ShapesSymbolRightTrapezoid = "\ueb91";
        /// <summary>Shapes_Symbol_rub</summary>
        public const string ShapesSymbolRub = "\ueb92";
        /// <summary>Shapes_Symbol_s</summary>
        public const string ShapesSymbolS = "\ueb93";
        /// <summary>Shapes_Symbol_semicircle</summary>
        public const string ShapesSymbolSemicircle = "\ueb94";
        /// <summary>Shapes_Symbol_seven-pointed-star</summary>
        public const string ShapesSymbolSevenPointedStar = "\ueb95";
        /// <summary>Shapes_Symbol_seven</summary>
        public const string ShapesSymbolSeven = "\ueb96";
        /// <summary>Shapes_Symbol_six-pointed-star</summary>
        public const string ShapesSymbolSixPointedStar = "\ueb97";
        /// <summary>Shapes_Symbol_six</summary>
        public const string ShapesSymbolSix = "\ueb98";
        /// <summary>Shapes_Symbol_square-root</summary>
        public const string ShapesSymbolSquareRoot = "\ueb99";
        /// <summary>Shapes_Symbol_square</summary>
        public const string ShapesSymbolSquare = "\ueb9a";
        /// <summary>Shapes_Symbol_t</summary>
        public const string ShapesSymbolT = "\ueb9b";
        /// <summary>Shapes_Symbol_thb</summary>
        public const string ShapesSymbolThb = "\ueb9c";
        /// <summary>Shapes_Symbol_three-pointed-star</summary>
        public const string ShapesSymbolThreePointedStar = "\ueb9d";
        /// <summary>Shapes_Symbol_three-quarter-circle</summary>
        public const string ShapesSymbolThreeQuarterCircle = "\ueb9e";
        /// <summary>Shapes_Symbol_three</summary>
        public const string ShapesSymbolThree = "\ueb9f";
        /// <summary>Shapes_Symbol_trapezoid</summary>
        public const string ShapesSymbolTrapezoid = "\ueba0";
        /// <summary>Shapes_Symbol_triangle</summary>
        public const string ShapesSymbolTriangle = "\ueba1";
        /// <summary>Shapes_Symbol_try</summary>
        public const string ShapesSymbolTry = "\ueba2";
        /// <summary>Shapes_Symbol_two</summary>
        public const string ShapesSymbolTwo = "\ueba3";
        /// <summary>Shapes_Symbol_u</summary>
        public const string ShapesSymbolU = "\ueba4";
        /// <summary>Shapes_Symbol_usd</summary>
        public const string ShapesSymbolUsd = "\ueba5";
        /// <summary>Shapes_Symbol_usd2</summary>
        public const string ShapesSymbolUsd2 = "\ueba6";
        /// <summary>Shapes_Symbol_v</summary>
        public const string ShapesSymbolV = "\ueba7";
        /// <summary>Shapes_Symbol_w</summary>
        public const string ShapesSymbolW = "\ueba8";
        /// <summary>Shapes_Symbol_x</summary>
        public const string ShapesSymbolX = "\ueba9";
        /// <summary>Shapes_Symbol_y</summary>
        public const string ShapesSymbolY = "\uebaa";
        /// <summary>Shapes_Symbol_yen</summary>
        public const string ShapesSymbolYen = "\uebab";
        /// <summary>Shapes_Symbol_yuan</summary>
        public const string ShapesSymbolYuan = "\uebac";
        /// <summary>Shapes_Symbol_z</summary>
        public const string ShapesSymbolZ = "\uebad";
        /// <summary>Shapes_Symbol_zero</summary>
        public const string ShapesSymbolZero = "\uebae";
        /// <summary>UI_adjustment</summary>
        public const string UiAdjustment = "\uebaf";
        /// <summary>UI_ban</summary>
        public const string UiBan = "\uebb0";
        /// <summary>UI_circle-ring</summary>
        public const string UiCircleRing = "\uebb1";
        /// <summary>UI_cross</summary>
        public const string UiCross = "\uebb2";
        /// <summary>UI_dark-mode</summary>
        public const string UiDarkMode = "\uebb3";
        /// <summary>UI_dark-mode2</summary>
        public const string UiDarkMode2 = "\uebb4";
        /// <summary>UI_exclamation-double</summary>
        public const string UiExclamationDouble = "\uebb5";
        /// <summary>UI_exclamation</summary>
        public const string UiExclamation = "\uebb6";
        /// <summary>UI_expand</summary>
        public const string UiExpand = "\uebb7";
        /// <summary>UI_expand2</summary>
        public const string UiExpand2 = "\uebb8";
        /// <summary>UI_expand3</summary>
        public const string UiExpand3 = "\uebb9";
        /// <summary>UI_expand4</summary>
        public const string UiExpand4 = "\uebba";
        /// <summary>UI_friends</summary>
        public const string UiFriends = "\uebbb";
        /// <summary>UI_grid-add</summary>
        public const string UiGridAdd = "\uebbc";
        /// <summary>UI_grid</summary>
        public const string UiGrid = "\uebbd";
        /// <summary>UI_grid2</summary>
        public const string UiGrid2 = "\uebbe";
        /// <summary>UI_grid3</summary>
        public const string UiGrid3 = "\uebbf";
        /// <summary>UI_grid4</summary>
        public const string UiGrid4 = "\uebc0";
        /// <summary>UI_grid5</summary>
        public const string UiGrid5 = "\uebc1";
        /// <summary>UI_grid6</summary>
        public const string UiGrid6 = "\uebc2";
        /// <summary>UI_grid7</summary>
        public const string UiGrid7 = "\uebc3";
        /// <summary>UI_info</summary>
        public const string UiInfo = "\uebc4";
        /// <summary>UI_invisible</summary>
        public const string UiInvisible = "\uebc5";
        /// <summary>UI_light-mode</summary>
        public const string UiLightMode = "\uebc6";
        /// <summary>UI_light-mode2</summary>
        public const string UiLightMode2 = "\uebc7";
        /// <summary>UI_loading</summary>
        public const string UiLoading = "\uebc8";
        /// <summary>UI_loading2</summary>
        public const string UiLoading2 = "\uebc9";
        /// <summary>UI_loading3</summary>
        public const string UiLoading3 = "\uebca";
        /// <summary>UI_loading4</summary>
        public const string UiLoading4 = "\uebcb";
        /// <summary>UI_loading5</summary>
        public const string UiLoading5 = "\uebcc";
        /// <summary>UI_lock</summary>
        public const string UiLock = "\uebcd";
        /// <summary>UI_menu-add</summary>
        public const string UiMenuAdd = "\uebce";
        /// <summary>UI_menu-open</summary>
        public const string UiMenuOpen = "\uebcf";
        /// <summary>UI_menu</summary>
        public const string UiMenu = "\uebd0";
        /// <summary>UI_menu2</summary>
        public const string UiMenu2 = "\uebd1";
        /// <summary>UI_menu3</summary>
        public const string UiMenu3 = "\uebd2";
        /// <summary>UI_menu4</summary>
        public const string UiMenu4 = "\uebd3";
        /// <summary>UI_menu5</summary>
        public const string UiMenu5 = "\uebd4";
        /// <summary>UI_menu6</summary>
        public const string UiMenu6 = "\uebd5";
        /// <summary>UI_menu7</summary>
        public const string UiMenu7 = "\uebd6";
        /// <summary>UI_progress</summary>
        public const string UiProgress = "\uebd7";
        /// <summary>UI_query</summary>
        public const string UiQuery = "\uebd8";
        /// <summary>UI_refresh</summary>
        public const string UiRefresh = "\uebd9";
        /// <summary>UI_rest</summary>
        public const string UiRest = "\uebda";
        /// <summary>UI_restore</summary>
        public const string UiRestore = "\uebdb";
        /// <summary>UI_restore2</summary>
        public const string UiRestore2 = "\uebdc";
        /// <summary>UI_restore3</summary>
        public const string UiRestore3 = "\uebdd";
        /// <summary>UI_restore4</summary>
        public const string UiRestore4 = "\uebde";
        /// <summary>UI_rotate-left</summary>
        public const string UiRotateLeft = "\uebdf";
        /// <summary>UI_rotate-right</summary>
        public const string UiRotateRight = "\uebe0";
        /// <summary>UI_save</summary>
        public const string UiSave = "\uebe1";
        /// <summary>UI_search</summary>
        public const string UiSearch = "\uebe2";
        /// <summary>UI_settings</summary>
        public const string UiSettings = "\uebe3";
        /// <summary>UI_settings2</summary>
        public const string UiSettings2 = "\uebe4";
        /// <summary>UI_slider</summary>
        public const string UiSlider = "\uebe5";
        /// <summary>UI_tick</summary>
        public const string UiTick = "\uebe6";
        /// <summary>UI_toggle-off</summary>
        public const string UiToggleOff = "\uebe7";
        /// <summary>UI_toggle-on</summary>
        public const string UiToggleOn = "\uebe8";
        /// <summary>UI_toggle</summary>
        public const string UiToggle = "\uebe9";
        /// <summary>UI_ui</summary>
        public const string UiUi = "\uebea";
        /// <summary>UI_unlock</summary>
        public const string UiUnlock = "\uebeb";
        /// <summary>UI_user-add</summary>
        public const string UiUserAdd = "\uebec";
        /// <summary>UI_user-alert</summary>
        public const string UiUserAlert = "\uebed";
        /// <summary>UI_user-avatar</summary>
        public const string UiUserAvatar = "\uebee";
        /// <summary>UI_user-avatar2</summary>
        public const string UiUserAvatar2 = "\uebef";
        /// <summary>UI_user-check</summary>
        public const string UiUserCheck = "\uebf0";
        /// <summary>UI_user-delete</summary>
        public const string UiUserDelete = "\uebf1";
        /// <summary>UI_user-group</summary>
        public const string UiUserGroup = "\uebf2";
        /// <summary>UI_user-list</summary>
        public const string UiUserList = "\uebf3";
        /// <summary>UI_user-remove</summary>
        public const string UiUserRemove = "\uebf4";
        /// <summary>UI_user</summary>
        public const string UiUser = "\uebf5";
        /// <summary>UI_visible</summary>
        public const string UiVisible = "\uebf6";
        /// <summary>UI_zoom-in</summary>
        public const string UiZoomIn = "\uebf7";
        /// <summary>UI_zoom-out</summary>
        public const string UiZoomOut = "\uebf8";

        // ── 向后兼容别名 ──
        public const string Play = MediaTechnologyPlay;
        public const string Stop = MediaTechnologyStop;
        public const string Pause = MediaTechnologyPause;
        public const string Save = UiSave;
        public const string ArrowUp = ShapesSymbolArrowUp;
        public const string ArrowDown = ShapesSymbolArrowDown;
        public const string Cross = UiCross;
        public const string Settings = UiSettings;
        public const string Bug = MediaTechnologyBug;
        public const string Clock = MediaTechnologyClock;
        public const string Puzzle = GamePuzzle;
        public const string Wrench = ItemsWrench;
        public const string DarkMode = UiDarkMode;
        public const string LightMode = UiLightMode;
    }

    private static readonly object _initLock = new();
    private static bool _initialized;
    private static IFontHandle? _fontHandle18;
    private static IFontHandle? _fontHandle24;

    /// <summary>初始化图标字体 — 在 Plugin 构造器中调用一次</summary>
    public static void Init()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;

            try
            {
                // 1. 从 EmbeddedResource 提取 TTF 到配置目录
                var configDir = DService.Instance().PI.ConfigDirectory.FullName;
                var ttfPath = Path.Combine(configDir, "game-icons.ttf");

                // 始终覆盖，确保 TTF 版本与嵌入资源一致
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("HiAuRo.Resources.Fonts.game-icons.ttf");
                if (stream == null)
                {
                    DService.Instance().Log.Warning("[IconHelper] TTF resource not found");
                    return;
                }
                using var fs = File.Create(ttfPath);
                stream.CopyTo(fs);

                // 2. 在游戏字体图集中注册两个字号
                var fontAtlas = DService.Instance().UIBuilder.FontAtlas;
                var iconRange = new ushort[] { 0xEA00, 0xEBF9, 0 };

                _fontHandle18 = fontAtlas.NewDelegateFontHandle(e =>
                {
                    e.OnPreBuild(tk =>
                    {
                        tk.AddFontFromFile(ttfPath, new()
                        {
                            SizePx = 18f,
                            PixelSnapH = true,
                            GlyphRanges = iconRange,
                        });
                    });
                });

                _fontHandle24 = fontAtlas.NewDelegateFontHandle(e =>
                {
                    e.OnPreBuild(tk =>
                    {
                        tk.AddFontFromFile(ttfPath, new()
                        {
                            SizePx = 24f,
                            PixelSnapH = true,
                            GlyphRanges = iconRange,
                        });
                    });
                });

                DService.Instance().Log.Information("[IconHelper] Game-Icon-Pack font registered (18px + 24px)");
                _initialized = true;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[IconHelper] Font init failed: {ex.Message}");
            }
        }
    }

    /// <summary>在指定中心绘制图标文本</summary>
    public static void DrawIcon(ImDrawListPtr dl, Vector2 center, string iconChar, uint color, float sizePx = 18f)
    {
        var fontHandle = sizePx >= 22f ? _fontHandle24 : _fontHandle18;
        if (fontHandle == null) return;

        try
        {
            using var lk = fontHandle.Lock();
            var fontPtr = lk.ImFont;
            using var font = ImRaii.PushFont(fontPtr);
            var size = ImGui.CalcTextSize(iconChar);
            dl.AddText(center - size / 2, color, iconChar);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[IconHelper] DrawIcon font lock failed: {ex.Message}");
        }
    }
}
