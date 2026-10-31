using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GameServer;
using Il2Cpp;
using MelonLoader;
using TinyJSON;
using UnityEngine;

namespace SkyCoop;

public class MPSaveManager
{
	public class KnockData
	{
		public Vector3 m_Position;

		public string m_Scene = "";

		public int m_ClientID;

		public string m_ToScene = "";

		public int m_Timeout = 30;
	}

	public class LocksmithBlank
	{
		public int m_State;

		public string m_GearName = "";

		public string m_Scene = "";

		public string m_Dropper = "";

		public LocksmithBlank(int State, string Name, string Scene, string Dropper)
		{
			m_State = State;
			m_GearName = Name;
			m_Scene = Scene;
			m_Dropper = Dropper;
		}
	}

	public enum UseKeyStatus
	{
		KeyUsed,
		DoorAlreadyLocked,
		Done
	}

	public enum SaveKeyTemplateType
	{
		Container,
		DropsVisual,
		DropsData,
		Openables,
		Furns,
		PickedGears,
		LootedContainers,
		HarvestedPlants
	}

	public static bool NoSaveAndLoad = false;

	public static Dictionary<string, Dictionary<int, DataStr.DroppedGearItemDataPacket>> RecentVisual = new Dictionary<string, Dictionary<int, DataStr.DroppedGearItemDataPacket>>();

	public static Dictionary<string, Dictionary<int, DataStr.SlicedJsonDroppedGear>> RecentData = new Dictionary<string, Dictionary<int, DataStr.SlicedJsonDroppedGear>>();

	public static Dictionary<string, Dictionary<string, bool>> RecentOpenableThings = new Dictionary<string, Dictionary<string, bool>>();

	public static Dictionary<string, long> UsersSaveHashs = new Dictionary<string, long>();

	public static Dictionary<string, Dictionary<string, string>> LockedDoors = new Dictionary<string, Dictionary<string, string>>();

	public static Dictionary<string, bool> UsedKeys = new Dictionary<string, bool>();

	public static Dictionary<int, LocksmithBlank> Blanks = new Dictionary<int, LocksmithBlank>();

	public static bool SaveHashsChanged = false;

	public static bool LockedDoorsChanged = false;

	public static bool AnimalsKilledChanged = false;

	public static int SaveRecentTimer = 5;

	public static bool Diagnostic = false;

	public static bool SaveLogging = false;

	public static int Seed = 0;

	public static string OldIdsJson = "{\"0\":\"gear_accelerant\",\"1\":\"gear_accelerantgunpowder\",\"2\":\"gear_accelerantkerosene\",\"3\":\"gear_accelerantmedium\",\"4\":\"gear_airlinefoodchick\",\"5\":\"gear_airlinefoodveg\",\"6\":\"gear_arrow\",\"7\":\"gear_arrowhead\",\"8\":\"gear_arrowshaft\",\"9\":\"gear_astridbackpack_hangar\",\"10\":\"gear_astridboots\",\"11\":\"gear_astridgloves\",\"12\":\"gear_astridjacket\",\"13\":\"gear_astridjeans\",\"14\":\"gear_astridsweater\",\"15\":\"gear_astridtoque\",\"16\":\"gear_aurorahatchcode\",\"17\":\"gear_auroraobservationnote\",\"18\":\"gear_backernote1a\",\"19\":\"gear_backernote1b\",\"20\":\"gear_backernote1c\",\"21\":\"gear_backernote2a\",\"22\":\"gear_backernote2b\",\"23\":\"gear_backernote2c\",\"24\":\"gear_backernote3a\",\"25\":\"gear_backernote3b\",\"26\":\"gear_backernote3c\",\"27\":\"gear_backernote4a\",\"28\":\"gear_backernote4b\",\"29\":\"gear_backernote4c\",\"30\":\"gear_balaclava\",\"31\":\"gear_ballisticvest\",\"32\":\"gear_bankmanagerhousekey\",\"33\":\"gear_bankmanagerhousekey\",\"34\":\"gear_bankvaultcode\",\"35\":\"gear_barktinder\",\"36\":\"gear_baseballcap\",\"37\":\"gear_basicboots\",\"38\":\"gear_basicgloves\",\"39\":\"gear_basicshoes\",\"40\":\"gear_basicwintercoat\",\"41\":\"gear_basicwoolhat\",\"42\":\"gear_basicwoolscarf\",\"43\":\"gear_bear_spear_broken\",\"44\":\"gear_bear_spear_complete\",\"45\":\"gear_bear_spear_head\",\"46\":\"gear_bearear\",\"47\":\"gear_bearhide\",\"48\":\"gear_bearhidedried\",\"49\":\"gear_bearquarter\",\"50\":\"gear_bearskinbedroll\",\"51\":\"gear_bearskincoat\",\"52\":\"gear_bearspear\",\"53\":\"gear_bearspearbroken\",\"54\":\"gear_bearspearbrokenstory\",\"55\":\"gear_bearspearrelic\",\"56\":\"gear_bearspearstory\",\"57\":\"gear_bedroll\",\"58\":\"gear_beefjerky\",\"59\":\"gear_birchbarkprepared\",\"60\":\"gear_birchbarktea\",\"61\":\"gear_birchsapling\",\"62\":\"gear_birchsaplingdried\",\"63\":\"gear_blackrockadminnote\",\"64\":\"gear_blackrockammoroomnote\",\"65\":\"gear_blackrockcodenote\",\"66\":\"gear_blackrockconvictnote1\",\"67\":\"gear_blackrockconvictnote2\",\"68\":\"gear_blackrockconvictnote3\",\"69\":\"gear_blackrockmemo\",\"70\":\"gear_blackrocksecuritynote\",\"71\":\"gear_blackrocktowernote\",\"72\":\"gear_blueflare\",\"73\":\"gear_body_dummy\",\"74\":\"gear_boltcutters\",\"75\":\"gear_booka\",\"76\":\"gear_bookarchery\",\"77\":\"gear_bookb\",\"78\":\"gear_bookbopen\",\"79\":\"gear_bookc\",\"80\":\"gear_bookcarcassharvesting\",\"81\":\"gear_bookcooking\",\"82\":\"gear_bookd\",\"83\":\"gear_booke\",\"84\":\"gear_bookeopen\",\"85\":\"gear_bookf\",\"86\":\"gear_bookfirestarting\",\"87\":\"gear_bookfopen\",\"88\":\"gear_bookg\",\"89\":\"gear_bookgunsmithing\",\"90\":\"gear_bookh\",\"91\":\"gear_bookhopen\",\"92\":\"gear_booki\",\"93\":\"gear_bookicefishing\",\"94\":\"gear_booklabe_01\",\"95\":\"gear_booklabe_02\",\"96\":\"gear_booklabe_03\",\"97\":\"gear_booklabe_open_01\",\"98\":\"gear_bookmanual\",\"99\":\"gear_bookmending\",\"100\":\"gear_bookrevolverfirearm\",\"101\":\"gear_bookriflefirearm\",\"102\":\"gear_bookriflefirearmadvanced\",\"103\":\"gear_booktalltalesfishing\",\"104\":\"gear_booktalltalesglowcave\",\"105\":\"gear_booktalltalesstag\",\"106\":\"gear_booktalltalesyeti\",\"107\":\"gear_bottleantibiotics\",\"108\":\"gear_bottlehydrogenperoxide\",\"109\":\"gear_bottlepainkillers\",\"110\":\"gear_bow\",\"111\":\"gear_bowstring\",\"112\":\"gear_bowwood\",\"113\":\"gear_brand\",\"114\":\"gear_brokenarrow\",\"115\":\"gear_brokenrifle\",\"116\":\"gear_bullet\",\"117\":\"gear_cachenote_churchmarshdir\",\"118\":\"gear_campofficecollectible\",\"119\":\"gear_candybar\",\"120\":\"gear_cannedbeans\",\"121\":\"gear_cannedsardines\",\"122\":\"gear_cannerycodenote\",\"123\":\"gear_cannerymemo\",\"124\":\"gear_cannerysurvivalpath\",\"125\":\"gear_canopener\",\"126\":\"gear_canyonclimberscavenote\",\"127\":\"gear_canyondeadclimbernote\",\"128\":\"gear_canyonfishinghutjournal\",\"129\":\"gear_canyonminersnote\",\"130\":\"gear_carbattery\",\"131\":\"gear_cargopants\",\"132\":\"gear_cashbundle\",\"133\":\"gear_cattailplant\",\"134\":\"gear_cattailstalk\",\"135\":\"gear_cattailtinder\",\"136\":\"gear_cavecachenote\",\"137\":\"gear_charcoal\",\"138\":\"gear_churchhymn\",\"139\":\"gear_churchnoteep1\",\"140\":\"gear_climbersjournal\",\"141\":\"gear_climbingsocks\",\"142\":\"gear_cloth\",\"143\":\"gear_clothsheet\",\"144\":\"gear_coal\",\"145\":\"gear_coffeecup\",\"146\":\"gear_coffeetin\",\"147\":\"gear_combatboots\",\"148\":\"gear_combatpants\",\"149\":\"gear_compressionbandage\",\"150\":\"gear_condensedmilk\",\"151\":\"gear_condensedmilk_open\",\"152\":\"gear_convictcorpsenote\",\"153\":\"gear_cookedbigmouthbass_talltales\",\"154\":\"gear_cookedcohosalmon\",\"155\":\"gear_cookedlakewhitefish\",\"156\":\"gear_cookedmeatbear\",\"157\":\"gear_cookedmeatdeer\",\"158\":\"gear_cookedmeatmoose\",\"159\":\"gear_cookedmeatrabbit\",\"160\":\"gear_cookedmeatwolf\",\"161\":\"gear_cookedrainbowtrout\",\"162\":\"gear_cookedsmallmouthbass\",\"163\":\"gear_cookingpot\",\"164\":\"gear_cookingpotdummy\",\"165\":\"gear_cottonhoodie\",\"166\":\"gear_cottonscarf\",\"167\":\"gear_cottonshirt\",\"168\":\"gear_cottonsocks\",\"169\":\"gear_cottonsocksstart\",\"170\":\"gear_cowichansweater\",\"171\":\"gear_crackers\",\"172\":\"gear_crampons\",\"173\":\"gear_crowfeather\",\"174\":\"gear_damcollectible1\",\"175\":\"gear_damcontrolroomcodenote\",\"176\":\"gear_damelevatornotice\",\"177\":\"gear_damexitcodenote\",\"178\":\"gear_damfencekey\",\"179\":\"gear_damgarbageletter\",\"180\":\"gear_damlockerkey\",\"181\":\"gear_damofficekey\",\"182\":\"gear_damtrailerbcrewnote\",\"183\":\"gear_darkwalkerdiary1\",\"184\":\"gear_darkwalkerdiary10\",\"185\":\"gear_darkwalkerdiary11\",\"186\":\"gear_darkwalkerdiary2\",\"187\":\"gear_darkwalkerdiary3\",\"188\":\"gear_darkwalkerdiary4\",\"189\":\"gear_darkwalkerdiary5\",\"190\":\"gear_darkwalkerdiary6\",\"191\":\"gear_darkwalkerdiary7\",\"192\":\"gear_darkwalkerdiary8\",\"193\":\"gear_darkwalkerdiary9\",\"194\":\"gear_darkwalkerid\",\"195\":\"gear_deadmannote1\",\"196\":\"gear_deadmannote2\",\"197\":\"gear_deadmannote3\",\"198\":\"gear_deadmannote4\",\"199\":\"gear_deadmannote5\",\"200\":\"gear_deerskinboots\",\"201\":\"gear_deerskinpants\",\"202\":\"gear_depositboxkey_dk1\",\"203\":\"gear_depositboxkey_dk2\",\"204\":\"gear_depositboxkey_dk3\",\"205\":\"gear_depositboxkey_dk_farmer\",\"206\":\"gear_detonator\",\"207\":\"gear_dogfood\",\"208\":\"gear_dogfood_open\",\"209\":\"gear_downparka\",\"210\":\"gear_downskijacket\",\"211\":\"gear_downvest\",\"212\":\"gear_dustingsulfur\",\"213\":\"gear_earmuffs\",\"214\":\"gear_elevatorcrank\",\"215\":\"gear_elevatorparts\",\"216\":\"gear_elevatorpartsdead\",\"217\":\"gear_emergencykitnote\",\"218\":\"gear_emergencystim\",\"219\":\"gear_energybar\",\"220\":\"gear_ep3_churchartifact\",\"221\":\"gear_ep3_churchflyer\",\"222\":\"gear_ep3_churchmotelletter\",\"223\":\"gear_ep3_churchnewsclipping\",\"224\":\"gear_ep3_churchthankyouletter\",\"225\":\"gear_ep3_churchtheftreport\",\"226\":\"gear_ep3_joplinbunkernote\",\"227\":\"gear_ep3_joplinbunkernote2\",\"228\":\"gear_ep3_joplincachenote\",\"229\":\"gear_ep3_patienthistory01\",\"230\":\"gear_ep3_patienthistory02\",\"231\":\"gear_ep3_patienthistory03\",\"232\":\"gear_ep3_patienthistory04\",\"233\":\"gear_ep3_patienthistory05\",\"234\":\"gear_ep3_patienthistory06\",\"235\":\"gear_ep3foresttalkers_ftcollectible1\",\"236\":\"gear_ep3foresttalkers_ftcollectible2\",\"237\":\"gear_ep3foresttalkers_ftcollectible3\",\"238\":\"gear_ep3foresttalkers_pvcollectible1\",\"239\":\"gear_ep3foresttalkers_pvcollectible2\",\"240\":\"gear_ep3foresttalkers_pvcollectible3\",\"241\":\"gear_ep3hallflyer\",\"242\":\"gear_ep3rosary\",\"243\":\"gear_ep3tomsmap\",\"244\":\"gear_ep4_blackrockescapenote1\",\"245\":\"gear_ep4_blackrocknoisemakernote\",\"246\":\"gear_ep4_blackrockrationsnote\",\"247\":\"gear_ep4_foresttalkerplans\",\"248\":\"gear_ep4_infirmarymeds\",\"249\":\"gear_ep4_infirmarymedsnote\",\"250\":\"gear_ep4_jacecarcollectible\",\"251\":\"gear_ep4_letterforesttalker1\",\"252\":\"gear_ep4_letterforesttalker2\",\"253\":\"gear_ep4_letterforesttalker3\",\"254\":\"gear_ep4_letterforesttalkerfinal\",\"255\":\"gear_ep4_minedangernote\",\"256\":\"gear_ep4_rooftopkey\",\"257\":\"gear_ep4_substationmemo\",\"258\":\"gear_ep4convictcachenote1\",\"259\":\"gear_ep4goldnugget\",\"260\":\"gear_ep4lockerkey1\",\"261\":\"gear_ep4lockerkey2\",\"262\":\"gear_ep4lockerkey3\",\"263\":\"gear_ep4lockerkey8\",\"264\":\"gear_ep4lostworkernote\",\"265\":\"gear_ep4minegatekey\",\"266\":\"gear_ep4powerworkerid1\",\"267\":\"gear_ep4powerworkerid2\",\"268\":\"gear_ep4prisongatekey\",\"269\":\"gear_ep4prosepectornote\",\"270\":\"gear_ep4rumourlateemployee\",\"271\":\"gear_ep4rumourshatchlocation\",\"272\":\"gear_ep4rumourshermitreport\",\"273\":\"gear_ep4rumourslockboxkey\",\"274\":\"gear_ep4rumoursprisonrelease\",\"275\":\"gear_ep4rumoursramblings1\",\"276\":\"gear_ep4rumourswolfreport\",\"277\":\"gear_ep4rumourswolfstudy\",\"278\":\"gear_ep4sawfarmnote\",\"279\":\"gear_ep4steamtunnelcampnote\",\"280\":\"gear_farmerdepositboxkey\",\"281\":\"gear_fireaxe\",\"282\":\"gear_firelog\",\"283\":\"gear_firestriker\",\"284\":\"gear_fishermansweater\",\"285\":\"gear_fishingline\",\"286\":\"gear_fixedrifle\",\"287\":\"gear_flarea\",\"288\":\"gear_flaregun\",\"289\":\"gear_flaregunammosingle\",\"290\":\"gear_flaregunammosingle_survivormission\",\"291\":\"gear_flareguncase_hangar\",\"292\":\"gear_flashlight\",\"293\":\"gear_fleecemittens\",\"294\":\"gear_fleecesweater\",\"295\":\"gear_flintandsteel\",\"296\":\"gear_foodsupplies_hangar\",\"297\":\"gear_foresttalkerbloodyitem\",\"298\":\"gear_foresttalkerdamnote\",\"299\":\"gear_foresttalkerflyer\",\"300\":\"gear_foresttalkerhiddenitem\",\"301\":\"gear_foresttalkermap\",\"302\":\"gear_foresttalkerthankyou\",\"303\":\"gear_foresttalkerthankyou2\",\"304\":\"gear_forgeblueprints\",\"305\":\"gear_forgecachenote\",\"306\":\"gear_fuse_dead_prefab\",\"307\":\"gear_fuse_live_prefab\",\"308\":\"gear_gauntlets\",\"309\":\"gear_gmextrasuppliesnote\",\"310\":\"gear_granolabar\",\"311\":\"gear_greenteacup\",\"312\":\"gear_greenteapackage\",\"313\":\"gear_greymotherboots\",\"314\":\"gear_greymotherpearls\",\"315\":\"gear_greymothertrunkkey\",\"316\":\"gear_greymothertrunkkey\",\"317\":\"gear_gunpowdercan\",\"318\":\"gear_gut\",\"319\":\"gear_gutdried\",\"320\":\"gear_hacksaw\",\"321\":\"gear_hammer\",\"322\":\"gear_handheldshortwave\",\"323\":\"gear_hankhatchcode\",\"324\":\"gear_hankjournal1\",\"325\":\"gear_hankjournal2\",\"326\":\"gear_hanklockboxkey\",\"327\":\"gear_hankneiceletter\",\"328\":\"gear_hardcase\",\"329\":\"gear_hardcase_hangar\",\"330\":\"gear_hardwood\",\"331\":\"gear_hatchet\",\"332\":\"gear_hatchetimprovised\",\"333\":\"gear_hc_ep1_fm_treeroots_dir\",\"334\":\"gear_hc_ep1_ml_alanscave_dir\",\"335\":\"gear_hc_ep1_ml_clearcut_dir\",\"336\":\"gear_hc_ep1_ml_tracksent_dir\",\"337\":\"gear_hc_ep1_rw_hunterlodge_dir\",\"338\":\"gear_hc_ep1_rw_ravineend_dir\",\"339\":\"gear_heavybandage\",\"340\":\"gear_heavyparka\",\"341\":\"gear_heavywoolsweater\",\"342\":\"gear_highqualitytools\",\"343\":\"gear_homemadesoup\",\"344\":\"gear_hook\",\"345\":\"gear_hookandline\",\"346\":\"gear_improvisedhat\",\"347\":\"gear_improvisedmittens\",\"348\":\"gear_insulatedboots\",\"349\":\"gear_insulatedpants\",\"350\":\"gear_insulatedvest\",\"351\":\"gear_insulin\",\"352\":\"gear_jeans\",\"353\":\"gear_jeremiahknife\",\"354\":\"gear_jeremiahscoat\",\"355\":\"gear_jerrycanrusty\",\"356\":\"gear_kerosenelampb\",\"357\":\"gear_ketchupchips\",\"358\":\"gear_knife\",\"359\":\"gear_knifeimprovised\",\"360\":\"gear_knifescrapmetal\",\"361\":\"gear_knifescrapmetal\",\"362\":\"gear_knifescrapmetal_clean\",\"363\":\"gear_knowledgebrbook1\",\"364\":\"gear_knowledgebrbook2\",\"365\":\"gear_knowledgebrbook3\",\"366\":\"gear_knowledgecarterdam\",\"367\":\"gear_knowledgecollapse1\",\"368\":\"gear_knowledgecollapse2\",\"369\":\"gear_knowledgecollapse3\",\"370\":\"gear_knowledgecollapse4\",\"371\":\"gear_knowledgemilton\",\"372\":\"gear_knowledgemysterylake\",\"373\":\"gear_knowledgepvbook1\",\"374\":\"gear_knowledgepvbook2\",\"375\":\"gear_knowledgepvbook3\",\"376\":\"gear_lakecabinkey1\",\"377\":\"gear_lakecabinkey2\",\"378\":\"gear_lakecabinkey3\",\"379\":\"gear_lakecabinkey8\",\"380\":\"gear_lakedeerhuntnote\",\"381\":\"gear_lakeincidentnote\",\"382\":\"gear_lakeletter1\",\"383\":\"gear_lakesflarecachenote\",\"384\":\"gear_laketrailerkey1\",\"385\":\"gear_lakewolfcullnote\",\"386\":\"gear_lampfuel\",\"387\":\"gear_lampfuelfull\",\"388\":\"gear_leather\",\"389\":\"gear_leatherdried\",\"390\":\"gear_leatherhide\",\"391\":\"gear_leatherhidedried\",\"392\":\"gear_leathershoes\",\"393\":\"gear_leatherstrips\",\"394\":\"gear_letterbundle\",\"395\":\"gear_lightparka\",\"396\":\"gear_lilyclimbmap\",\"397\":\"gear_line\",\"398\":\"gear_longunderwear\",\"399\":\"gear_longunderwearstart\",\"400\":\"gear_longunderwearwool\",\"401\":\"gear_lorenotea\",\"402\":\"gear_lorenoteb\",\"403\":\"gear_lorenotec\",\"404\":\"gear_lorenoted\",\"405\":\"gear_mackinawjacket\",\"406\":\"gear_magnifyinglens\",\"407\":\"gear_maplesapling\",\"408\":\"gear_maplesaplingdried\",\"409\":\"gear_maplesyrup\",\"410\":\"gear_medicalsupplies\",\"411\":\"gear_medicalsupplies_hangar\",\"412\":\"gear_militaryparka\",\"413\":\"gear_miltoncaveletter\",\"414\":\"gear_miltondepositboxkey1\",\"415\":\"gear_miltondepositboxkey2\",\"416\":\"gear_miltondepositboxkey3\",\"417\":\"gear_miltonflaregunnote\",\"418\":\"gear_miltongardencache\",\"419\":\"gear_miltonletter1\",\"420\":\"gear_miltonletter2\",\"421\":\"gear_miltonparknotice\",\"422\":\"gear_miltonpostofficecollectible1\",\"423\":\"gear_miltonstorenotice\",\"424\":\"gear_minepipevalve\",\"425\":\"gear_mittens\",\"426\":\"gear_moosehide\",\"427\":\"gear_moosehidebag\",\"428\":\"gear_moosehidecloak\",\"429\":\"gear_moosehidedried\",\"430\":\"gear_moosequarter\",\"431\":\"gear_mountaintownfarmkey\",\"432\":\"gear_mountaintownfarmkey\",\"433\":\"gear_mountaintownfarmnote\",\"434\":\"gear_mountaintownlockboxkey\",\"435\":\"gear_mountaintownlockboxkey\",\"436\":\"gear_mre\",\"437\":\"gear_muklukboots\",\"438\":\"gear_newsprint\",\"439\":\"gear_newsprintbootstuffing\",\"440\":\"gear_newsprintinsulation\",\"441\":\"gear_newsprintroll\",\"442\":\"gear_noisemaker\",\"443\":\"gear_notemcu\",\"444\":\"gear_nylon\",\"445\":\"gear_oldladystolenitem\",\"446\":\"gear_oldmansbearddressing\",\"447\":\"gear_oldmansbeardharvested\",\"448\":\"gear_packmatches\",\"449\":\"gear_paperstack\",\"450\":\"gear_passengermanifest\",\"451\":\"gear_peanutbutter\",\"452\":\"gear_picturefamily_prefab\",\"453\":\"gear_pinnaclecanpeaches\",\"454\":\"gear_plaidshirt\",\"455\":\"gear_planecrashid1\",\"456\":\"gear_planecrashid10\",\"457\":\"gear_planecrashid2\",\"458\":\"gear_planecrashid3\",\"459\":\"gear_planecrashid4\",\"460\":\"gear_planecrashid5\",\"461\":\"gear_planecrashid6\",\"462\":\"gear_planecrashid7\",\"463\":\"gear_planecrashid8\",\"464\":\"gear_planecrashid9\",\"465\":\"gear_plantsurvivalnote\",\"466\":\"gear_postcard_ac_centralspire\",\"467\":\"gear_postcard_ac_topshelf\",\"468\":\"gear_postcard_bi_echoone-radiotower\",\"469\":\"gear_postcard_br_canyon\",\"470\":\"gear_postcard_br_prison\",\"471\":\"gear_postcard_cr_abandonedlookout\",\"472\":\"gear_postcard_fm_muskegoverlook\",\"473\":\"gear_postcard_fm_shortwavetower\",\"474\":\"gear_postcard_ml_forestrylookout\",\"475\":\"gear_postcard_ml_lakeoverlook\",\"476\":\"gear_postcard_mt_radiotower\",\"477\":\"gear_postcard_pv_signalhill\",\"478\":\"gear_postcard_rv_pensive\",\"479\":\"gear_postcard_tm_andrespeak\",\"480\":\"gear_postcard_tm_tailsection\",\"481\":\"gear_premiumwintercoat\",\"482\":\"gear_prisonbusnote\",\"483\":\"gear_prybar\",\"484\":\"gear_pumpkinpie\",\"485\":\"gear_pumpkinpiedarkwalker\",\"486\":\"gear_qualitywintercoat\",\"487\":\"gear_rabbitcarcass\",\"488\":\"gear_rabbitpelt\",\"489\":\"gear_rabbitpeltdried\",\"490\":\"gear_rabbitskinhat\",\"491\":\"gear_rabbitskinmittens\",\"492\":\"gear_radioparts\",\"493\":\"gear_rawbigbass\",\"494\":\"gear_rawbigmouthbass_talltales\",\"495\":\"gear_rawcohosalmon\",\"496\":\"gear_rawlakewhitefish\",\"497\":\"gear_rawmeatbear\",\"498\":\"gear_rawmeatdeer\",\"499\":\"gear_rawmeatmoose\",\"500\":\"gear_rawmeatrabbit\",\"501\":\"gear_rawmeatwolf\",\"502\":\"gear_rawrainbowtrout\",\"503\":\"gear_rawsmallmouthbass\",\"504\":\"gear_reclaimedwoodb\",\"505\":\"gear_recycledcan\",\"506\":\"gear_reishimushroom\",\"507\":\"gear_reishiprepared\",\"508\":\"gear_reishitea\",\"509\":\"gear_revolver\",\"510\":\"gear_revolverammobox\",\"511\":\"gear_revolverammocasing\",\"512\":\"gear_revolverammosingle\",\"513\":\"gear_rifle\",\"514\":\"gear_rifleammobox\",\"515\":\"gear_rifleammocasing\",\"516\":\"gear_rifleammosingle\",\"517\":\"gear_riflecleaningkit\",\"518\":\"gear_riflehuntinglodge\",\"519\":\"gear_rockcache_prefab\",\"520\":\"gear_rope\",\"521\":\"gear_rosehip\",\"522\":\"gear_rosehipsprepared\",\"523\":\"gear_rosehiptea\",\"524\":\"gear_ruralregionfarmkey\",\"525\":\"gear_scarftorn\",\"526\":\"gear_scraplead\",\"527\":\"gear_scrapmetal\",\"528\":\"gear_sewingkit\",\"529\":\"gear_sharpeningstone\",\"530\":\"gear_shedcodenote\",\"531\":\"gear_shovel\",\"532\":\"gear_simpletools\",\"533\":\"gear_skiboots\",\"534\":\"gear_skigloves\",\"535\":\"gear_skijacket\",\"536\":\"gear_snare\",\"537\":\"gear_soda\",\"538\":\"gear_sodaenergy\",\"539\":\"gear_sodagrape\",\"540\":\"gear_sodaorange\",\"541\":\"gear_softwood\",\"542\":\"gear_spearhead\",\"543\":\"gear_spraypaintcan\",\"544\":\"gear_spraypaintcanglypha\",\"545\":\"gear_stagquarter\",\"546\":\"gear_steampipevalve\",\"547\":\"gear_stick\",\"548\":\"gear_stone\",\"549\":\"gear_stumpremover\",\"550\":\"gear_survivalelevatorcrank\",\"551\":\"gear_survivalschoolclothing\",\"552\":\"gear_survivalschooldeerhunt\",\"553\":\"gear_survivalschoolfishing\",\"554\":\"gear_survivalschoolplants\",\"555\":\"gear_survivalschoolrabbits\",\"556\":\"gear_survivalschoolwolfhunt\",\"557\":\"gear_technicalbackpack\",\"558\":\"gear_teeshirt\",\"559\":\"gear_timberwolfquarter\",\"560\":\"gear_tinder\",\"561\":\"gear_tomatosoupcan\",\"562\":\"gear_toque\",\"563\":\"gear_torch\",\"564\":\"gear_tornscarf\",\"565\":\"gear_tornscarffp\",\"566\":\"gear_trailersupplies\",\"567\":\"gear_transponderparts\",\"568\":\"gear_utilitiesbill\",\"569\":\"gear_water1000ml\",\"570\":\"gear_water500ml\",\"571\":\"gear_waterpumpcrank\",\"572\":\"gear_waterpurificationtablets\",\"573\":\"gear_watersupplynotpotable\",\"574\":\"gear_watersupplypotable\",\"575\":\"gear_watertowernote\",\"576\":\"gear_whiskeybottle_a_prefab\",\"577\":\"gear_willboots\",\"578\":\"gear_willbootsstart\",\"579\":\"gear_willpants\",\"580\":\"gear_willpantsstart\",\"581\":\"gear_willparka\",\"582\":\"gear_willparka_table\",\"583\":\"gear_willparka_tossed\",\"584\":\"gear_willparkahangar\",\"585\":\"gear_willshirt\",\"586\":\"gear_willshirtstart\",\"587\":\"gear_willsweater\",\"588\":\"gear_willsweaterstart\",\"589\":\"gear_willtoque\",\"590\":\"gear_wolfcarcass\",\"591\":\"gear_wolfpelt\",\"592\":\"gear_wolfpeltdried\",\"593\":\"gear_wolfquarter\",\"594\":\"gear_wolfskincape\",\"595\":\"gear_wolfskincapetalltales\",\"596\":\"gear_woodmatches\",\"597\":\"gear_woolshirt\",\"598\":\"gear_woolsocks\",\"599\":\"gear_woolsweater\",\"600\":\"gear_woolwrap\",\"601\":\"gear_woolwrapcap\",\"602\":\"gear_workboots\",\"603\":\"gear_workgloves\",\"604\":\"gear_workpants\"}";

	public static Dictionary<int, string> OldIdDict = new Dictionary<int, string>();

	public static bool OldDictReady = false;

	public static float LockpickChance = 0.022f;

	public static float LeadKeyBrokeChance = 0.13f;

	public static Dictionary<string, Dictionary<string, DataStr.BrokenFurnitureSync>> RecentBrokenFurns = new Dictionary<string, Dictionary<string, DataStr.BrokenFurnitureSync>>();

	public static Dictionary<string, Dictionary<long, DataStr.PickedGearSync>> RecentPickedGears = new Dictionary<string, Dictionary<long, DataStr.PickedGearSync>>();

	public static Dictionary<string, Dictionary<string, int>> RecentlyLootedContainers = new Dictionary<string, Dictionary<string, int>>();

	public static Dictionary<string, Dictionary<string, int>> RecentlyHarvastedPlants = new Dictionary<string, Dictionary<string, int>>();

	public static List<DataStr.DeathContainerData> DeathCreates = new List<DataStr.DeathContainerData>();

	public static Dictionary<string, string> BannedUsers = new Dictionary<string, string>();

	public static List<KnockData> DoorEnterRequested = new List<KnockData>();

	public static string MyMAC = "";

	public static Dictionary<string, Dictionary<string, DataStr.FakeRockCacheVisualData>> RockCaches = new Dictionary<string, Dictionary<string, DataStr.FakeRockCacheVisualData>>();

	public static bool RockCachesChanged = false;

	public static Dictionary<string, Dictionary<string, DataStr.UniversalSyncableObject>> UniversalSyncableObjects = new Dictionary<string, Dictionary<string, DataStr.UniversalSyncableObject>>();

	public static bool UniversalSyncableObjectsChanged = false;

	public static Dictionary<string, int> FestiveGearsSpawnCooldown = new Dictionary<string, int>();

	public static void Log(string LOG)
	{
		MelonLogger.Msg(ConsoleColor.Blue, "[MPSaveManager] " + LOG);
	}

	public static void Error(string LOG)
	{
		MelonLogger.Msg(ConsoleColor.Red, "[MPSaveManager] " + LOG);
	}

	public static int GetSeed()
	{
		return GameManager.m_SceneTransitionData.m_GameRandomSeed;
	}

	public static void SaveGlobalData()
	{
		Log("Dedicated server saving...");
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		dictionary.Add("ropes", JSON.Dump(MyMod.DeployedRopes));
		dictionary.Add("shelters", JSON.Dump(MyMod.ShowSheltersBuilded));
		int[] data = new int[1] { MyMod.MinutesFromStartServer };
		dictionary.Add("rtt", JSON.Dump(data));
		dictionary.Add("deathcreates", JSON.Dump(MyMod.DeathCreates));
		string[] data2 = new string[1] { MyMod.OveridedTime };
		dictionary.Add("gametime", JSON.Dump(data2));
		dictionary.Add("festivegears", JSON.Dump(FestiveGearsSpawnCooldown));
		string content = JSON.Dump(dictionary);
		SaveData("GlobalServerData", content, GetSeed());
		Log("Save is done! Next save " + MyMod.DsSavePerioud + " seconds later");
	}

	public static void AddKnockDoorRequest(int ClientID, string ToScene)
	{
		Log("AddKnockDoorRequest ClinetID " + ClientID + " ToScene " + ToScene);
		foreach (KnockData item in DoorEnterRequested)
		{
			if (item.m_ClientID == ClientID && item.m_ToScene == ToScene)
			{
				item.m_Timeout = 30;
				return;
			}
		}
		KnockData knockData = new KnockData();
		knockData.m_ClientID = ClientID;
		knockData.m_ToScene = ToScene;
		if (ClientID != 0)
		{
			if (MyMod.playersData[ClientID] == null)
			{
				return;
			}
			knockData.m_Scene = MyMod.playersData[ClientID].m_LevelGuid;
			knockData.m_Position = MyMod.playersData[ClientID].m_Position;
		}
		else
		{
			knockData.m_Scene = MyMod.level_guid;
			knockData.m_Position = GameManager.GetPlayerTransform().position;
		}
		DoorEnterRequested.Add(knockData);
		Log("AddKnock.m_Scene " + knockData.m_Scene);
		Log("AddKnock.m_ToScene " + knockData.m_ToScene);
		ServerSend.KNOCKKNOCK(ToScene);
		if (MyMod.iAmHost && MyMod.level_guid == ToScene)
		{
			HUDMessage.AddMessage("Somebody's knocking on the door");
		}
	}

	public static void UpdateKnockDoorRequests()
	{
		for (int num = DoorEnterRequested.Count - 1; num > -1; num--)
		{
			KnockData knockData = DoorEnterRequested[num];
			int clientID = knockData.m_ClientID;
			string text = MyMod.level_guid;
			Vector3 vector = new Vector3(0f, 0f, 0f);
			if (clientID != 0)
			{
				if (!Server.clients[clientID].IsBusy())
				{
					DoorEnterRequested.RemoveAt(num);
					continue;
				}
				text = MyMod.playersData[clientID].m_LevelGuid;
				vector = MyMod.playersData[clientID].m_Position;
			}
			else
			{
				vector = GameManager.GetPlayerTransform().position;
			}
			if (knockData.m_Timeout <= 0 || knockData.m_Scene != text || Vector3.Distance(vector, knockData.m_Position) > 15f)
			{
				DoorEnterRequested.RemoveAt(num);
				Log("Knock Removed");
			}
			else
			{
				knockData.m_Timeout--;
			}
		}
	}

	public static List<int> GetKnocksOnScene(string Scene)
	{
		List<int> list = new List<int>();
		foreach (KnockData item in DoorEnterRequested)
		{
			if (item.m_ToScene == Scene)
			{
				list.Add(item.m_ClientID);
			}
		}
		Log("GetKnocksOnScene " + Scene + "  Knockers " + list.Count);
		return list;
	}

	public static void ApplyEnterFromKnock(int ClientID, string ToScene)
	{
		Log("ApplyEnterFromKnock ClientID " + ClientID + "  ToScene " + ToScene);
		foreach (KnockData item in DoorEnterRequested)
		{
			if (item.m_ClientID == ClientID && item.m_ToScene == ToScene)
			{
				DoorEnterRequested.Remove(item);
				if (ClientID != 0)
				{
					ServerSend.KNOCKENTER(ClientID, ToScene);
				}
				else
				{
					MyMod.EnterDoorsByScene(ToScene);
				}
				break;
			}
		}
	}

	public static void ChangeBlankState(int hash, int newState)
	{
		if (Blanks.ContainsKey(hash))
		{
			LocksmithBlank blank = GetBlank(hash);
			if (blank != null)
			{
				blank.m_State = newState;
				Blanks.Remove(hash);
				Blanks.Add(hash, blank);
			}
		}
	}

	public static void AlignKey(GearItem key, string KeySeed, string Name)
	{
		if ((bool)key)
		{
			if (key.m_ObjectGuid == null)
			{
				key.m_ObjectGuid = key.gameObject.AddComponent<ObjectGuid>();
			}
			if (Shared.HasNonASCIIChars(Name) || Name.Contains("_"))
			{
				Name = "Incorrectly named Key";
			}
			else if (string.IsNullOrEmpty(Name))
			{
				Name = "Nameless key";
			}
			if (Shared.HasNonASCIIChars(KeySeed) || KeySeed.Contains("_") || string.IsNullOrEmpty(KeySeed))
			{
				KeySeed = "broken";
			}
			key.m_ObjectGuid.Set(Name + "_" + KeySeed.ToLower());
			key.m_DisplayNameOverrideLocID = Name;
		}
	}

	public static void ApplyToolOnBlank(int hash, int tool, string KeyName = "", string KeySeed = "")
	{
		LocksmithBlank blank = GetBlank(hash);
		if (blank == null)
		{
			return;
		}
		string lockSmithProduct = Shared.GetLockSmithProduct(blank.m_GearName.ToLower(), tool);
		Log("[ApplyToolOnBlank] " + hash + " " + blank.m_GearName + " Tool " + tool);
		Vector3 vector = new Vector3(0f, 0f, 0f);
		Quaternion quaternion = new Quaternion(0f, 0f, 0f, 0f);
		string scene = blank.m_Scene;
		if (lockSmithProduct != blank.m_GearName)
		{
			Log("[ApplyToolOnBlank] Going to replace " + blank.m_GearName + " on " + lockSmithProduct);
			Dictionary<int, DataStr.SlicedJsonDroppedGear> dictionary = LoadDropData(scene);
			Dictionary<int, DataStr.DroppedGearItemDataPacket> dictionary2 = LoadDropVisual(scene);
			DataStr.DroppedGearItemDataPacket value;
			if (dictionary == null || dictionary2 == null)
			{
				Log("Wasn't able to load drop directory");
			}
			else if (dictionary2.TryGetValue(hash, out value))
			{
				vector = value.m_Position;
				quaternion = value.m_Rotation;
				DataStr.SlicedJsonDroppedGear slicedJsonDroppedGear = new DataStr.SlicedJsonDroppedGear();
				slicedJsonDroppedGear.m_GearName = lockSmithProduct.ToLower();
				slicedJsonDroppedGear.m_Extra.m_DroppedTime = MyMod.MinutesFromStartServer;
				slicedJsonDroppedGear.m_Extra.m_Dropper = blank.m_Dropper;
				slicedJsonDroppedGear.m_Extra.m_GearName = slicedJsonDroppedGear.m_GearName;
				slicedJsonDroppedGear.m_Extra.m_Variant = 4;
				if (slicedJsonDroppedGear.m_GearName == "gear_scwoodformb")
				{
					slicedJsonDroppedGear.m_Extra.m_GoalTime = MyMod.MinutesFromStartServer + 60;
				}
				GameObject gearItemObject = MyMod.GetGearItemObject(slicedJsonDroppedGear.m_GearName);
				if (gearItemObject != null)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, vector, quaternion);
					GearItem component = gameObject.GetComponent<GearItem>();
					if (!string.IsNullOrEmpty(KeyName) && !string.IsNullOrEmpty(KeySeed))
					{
						AlignKey(component, KeySeed, KeyName);
					}
					component.SkipSpawnChanceRollInitialDecayAndAutoEvolve();
					gameObject.name = blank.m_GearName;
					string json = gameObject.GetComponent<GearItem>().SerializeToString();
					int vectorHash = Shared.GetVectorHash(vector);
					int quaternionHash = Shared.GetQuaternionHash(quaternion);
					int hashCode = Shared.StableHash(scene);
					int hash2 = vectorHash + quaternionHash + hashCode;
					UnityEngine.Object.Destroy(gameObject);
					DataStr.DroppedGearItemDataPacket droppedGearItemDataPacket = new DataStr.DroppedGearItemDataPacket();
					droppedGearItemDataPacket.m_Extra = slicedJsonDroppedGear.m_Extra;
					droppedGearItemDataPacket.m_GearID = -1;
					droppedGearItemDataPacket.m_Hash = hash2;
					droppedGearItemDataPacket.m_LevelGUID = scene;
					droppedGearItemDataPacket.m_Position = vector;
					droppedGearItemDataPacket.m_Rotation = quaternion;
					slicedJsonDroppedGear.m_Json = json;
					RemovSpecificGear(hash, scene);
					ServerSend.PICKDROPPEDGEAR(0, hash, toEveryOne: true);
					if (!MyMod.DedicatedServerAppMode)
					{
						MyMod.DroppedGearsObjs.TryGetValue(hash, out var value2);
						if (value2 != null)
						{
							MyMod.DroppedGearsObjs.Remove(hash);
							MyMod.TrackableDroppedGearsObjs.Remove(hash);
							UnityEngine.Object.Destroy(value2);
						}
					}
					Log("Removed " + hash);
					AddGearData(scene, hash2, slicedJsonDroppedGear);
					AddGearVisual(scene, droppedGearItemDataPacket);
					AddBlank(hash2, lockSmithProduct, scene, blank.m_Dropper);
					Shared.FakeDropItem(droppedGearItemDataPacket, JustLoad: true);
					ServerSend.DROPITEM(0, droppedGearItemDataPacket, toEveryOne: true);
					Log("Added " + hash2);
				}
				else
				{
					Log("Can't load reference for blank");
					ChangeBlankState(hash, 0);
				}
			}
			else
			{
				Log("Wasn't able to find old item");
			}
		}
		else
		{
			ChangeBlankState(hash, 0);
		}
	}

	public static void AddBlank(int hash, string name, string scene, string Dropper)
	{
		if (!Blanks.ContainsKey(hash))
		{
			Blanks.Add(hash, new LocksmithBlank(0, name, scene, Dropper));
		}
	}

	public static LocksmithBlank GetBlank(int hash)
	{
		if (Blanks.ContainsKey(hash) && Blanks.TryGetValue(hash, out var value))
		{
			return value;
		}
		return null;
	}

	public static bool CanWorkOnBlank(int hash)
	{
		LocksmithBlank blank = GetBlank(hash);
		if (blank != null && blank.m_State != -1)
		{
			return true;
		}
		return false;
	}

	public static void LoadNonUnloadables()
	{
		ExpeditionBuilder.Init();
		ExpeditionManager.InitClues();
		int seed = GetSeed();
		Log("LoadNonUnloadables Seed " + seed);
		string text = LoadData("LockedDoors", seed);
		string text2 = LoadData("UsersSaveHashes", seed);
		string text3 = LoadData("KilledAnimals", seed);
		string text4 = LoadData("BanList");
		string text5 = LoadData("RockCaches", seed);
		string text6 = LoadData("UniversalSyncables", seed);
		string jSONString = LoadData("ExpeditionManager", seed);
		if (!string.IsNullOrEmpty(text))
		{
			LockedDoors = JSON.Load(text).Make<Dictionary<string, Dictionary<string, string>>>();
			foreach (KeyValuePair<string, Dictionary<string, string>> lockedDoor in LockedDoors)
			{
				foreach (KeyValuePair<string, string> item in lockedDoor.Value)
				{
					UsedKeys.Add(item.Value, value: true);
				}
			}
		}
		if (!string.IsNullOrEmpty(text2))
		{
			UsersSaveHashs = JSON.Load(text2).Make<Dictionary<string, long>>();
		}
		if (!string.IsNullOrEmpty(text3))
		{
			Shared.AnimalsKilled = JSON.Load(text3).Make<Dictionary<string, DataStr.AnimalKilled>>();
		}
		if (!string.IsNullOrEmpty(text4))
		{
			BannedUsers = JSON.Load(text4).Make<Dictionary<string, string>>();
		}
		if (!string.IsNullOrEmpty(text5))
		{
			RockCaches = JSON.Load(text5).Make<Dictionary<string, Dictionary<string, DataStr.FakeRockCacheVisualData>>>();
		}
		if (!string.IsNullOrEmpty(text6))
		{
			UniversalSyncableObjects = JSON.Load(text6).Make<Dictionary<string, Dictionary<string, DataStr.UniversalSyncableObject>>>();
		}
		ExpeditionManager.Load(jSONString);
	}

	public static string GenerateSeededGUID(int gameSeed, Vector3 v3)
	{
		int num = (int)v3.x;
		int num2 = (int)v3.y;
		int num3 = (int)v3.z;
		int num4 = num + num2 + num3;
		int seed = gameSeed + num4;
		string text = "abcdefghijklmnopqrstuvwxyz1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		System.Random random = new System.Random(seed);
		string text2 = "";
		for (int i = 1; i < 36; i++)
		{
			if (i == 9 || i == 14 || i == 19 || i == 24)
			{
				text2 += "-";
			}
			int index = random.Next(0, text.Length);
			text2 += text[index];
		}
		return text2;
	}

	public static string GetNewUGUID()
	{
		System.Random random = new System.Random();
		int gameSeed = random.Next(1, 999999);
		int num = random.Next(-7000, 999999);
		int num2 = random.Next(-3000, 999999);
		int num3 = random.Next(-5370, 999999);
		return GenerateSeededGUID(gameSeed, new Vector3(num, num2, num3));
	}

	public static void SetSaveHash(string UGUID, long hash)
	{
		if (UsersSaveHashs.ContainsKey(UGUID))
		{
			UsersSaveHashs.Remove(UGUID);
		}
		UsersSaveHashs.Add(UGUID, hash);
		SaveHashsChanged = true;
	}

	public static bool VerifySaveHash(string UGUID, long hash)
	{
		if (UsersSaveHashs.ContainsKey(UGUID))
		{
			if (UsersSaveHashs.TryGetValue(UGUID, out var value))
			{
				Log("GUID " + UGUID + " Provided  hash: " + hash + " expected " + value);
				if (value == hash)
				{
					Log("Verified GUID " + UGUID);
					return true;
				}
				Log("Incorrect hash");
				UsersSaveHashs.Remove(UGUID);
				return false;
			}
			Log("No");
			return false;
		}
		Log("There no saves for GUID " + UGUID);
		return false;
	}

	public static Dictionary<string, string> GetDoorsOnScene(string Scene)
	{
		Dictionary<string, string> value;
		if (!LockedDoors.ContainsKey(Scene))
		{
			value = new Dictionary<string, string>();
			LockedDoors.Add(Scene, value);
			LockedDoorsChanged = true;
		}
		else
		{
			LockedDoors.TryGetValue(Scene, out value);
		}
		return value;
	}

	public static bool TryUseLeadKey()
	{
		return new System.Random().NextDouble() > (double)LeadKeyBrokeChance;
	}

	public static bool TryUseKey(string Scene, string DoorKey, string KeySeed)
	{
		Dictionary<string, string> doorsOnScene = GetDoorsOnScene(Scene);
		if (doorsOnScene.ContainsKey(DoorKey))
		{
			string value = "";
			doorsOnScene.TryGetValue(DoorKey, out value);
			if (value == KeySeed)
			{
				return true;
			}
			return false;
		}
		return true;
	}

	public static UseKeyStatus AddLockedDoor(string Scene, string DoorKey, string KeySeed)
	{
		Dictionary<string, string> doorsOnScene = GetDoorsOnScene(Scene);
		if (UsedKeys.ContainsKey(KeySeed))
		{
			return UseKeyStatus.KeyUsed;
		}
		if (!doorsOnScene.ContainsKey(DoorKey))
		{
			doorsOnScene.Add(DoorKey, KeySeed);
			UsedKeys.Add(KeySeed, value: true);
			LockedDoorsChanged = true;
			return UseKeyStatus.Done;
		}
		return UseKeyStatus.DoorAlreadyLocked;
	}

	public static void RemoveLockedDoor(string Scene, string DoorKey)
	{
		Dictionary<string, string> doorsOnScene = GetDoorsOnScene(Scene);
		if (doorsOnScene.ContainsKey(DoorKey))
		{
			if (doorsOnScene.TryGetValue(DoorKey, out var value))
			{
				UsedKeys.Remove(value);
			}
			doorsOnScene.Remove(DoorKey);
			LockedDoorsChanged = true;
		}
	}

	public static void TryLockPick(string Scene, string DoorKey, int Picker)
	{
		System.Random random = new System.Random();
		bool flag = true;
		if (random.NextDouble() < (double)LockpickChance)
		{
			flag = false;
			RemoveLockedDoor(Scene, DoorKey);
			string text = DoorKey.Split('_')[1];
			ServerSend.REMOVEDOORLOCK(-1, text, Scene);
			if (!MyMod.DedicatedServerAppMode && MyMod.level_guid == Scene)
			{
				MyMod.RemoveLocksFromDoorsByGUID(text);
			}
		}
		if (Picker == 0)
		{
			MyMod.SwearOnLockpickingDone = flag;
		}
		else
		{
			ServerSend.LOCKPICK(Picker, flag);
		}
	}

	public static string GetBaseDirectory()
	{
		return "";
	}

	public static void SaveRecentStuff()
	{
		Stopwatch stopwatch = null;
		if (Diagnostic)
		{
			stopwatch = Stopwatch.StartNew();
		}
		int seed = GetSeed();
		ValidateRootExits();
		if (SaveHashsChanged)
		{
			SaveHashsChanged = false;
			SaveData("UsersSaveHashes", JSON.Dump(UsersSaveHashs), seed);
		}
		if (LockedDoorsChanged)
		{
			LockedDoorsChanged = false;
			SaveData("LockedDoors", JSON.Dump(LockedDoors), seed);
		}
		if (AnimalsKilledChanged)
		{
			AnimalsKilledChanged = false;
			SaveData("KilledAnimals", JSON.Dump(Shared.AnimalsKilled), seed);
		}
		if (RockCachesChanged)
		{
			RockCachesChanged = false;
			SaveData("RockCaches", JSON.Dump(RockCaches), seed);
		}
		if (UniversalSyncableObjectsChanged)
		{
			UniversalSyncableObjectsChanged = false;
			SaveData("UniversalSyncables", JSON.Dump(UniversalSyncableObjects), seed);
		}
		foreach (KeyValuePair<string, Dictionary<int, DataStr.DroppedGearItemDataPacket>> item in RecentVisual.ToList())
		{
			SaveData(GetKeyTemplate(SaveKeyTemplateType.DropsVisual, item.Key), JSON.Dump(item.Value), seed);
		}
		foreach (KeyValuePair<string, Dictionary<int, DataStr.SlicedJsonDroppedGear>> item2 in RecentData.ToList())
		{
			SaveData(GetKeyTemplate(SaveKeyTemplateType.DropsData, item2.Key), JSON.Dump(item2.Value), seed);
		}
		foreach (KeyValuePair<string, Dictionary<string, bool>> item3 in RecentOpenableThings.ToList())
		{
			SaveData(GetKeyTemplate(SaveKeyTemplateType.Openables, item3.Key), JSON.Dump(item3.Value), seed);
		}
		foreach (KeyValuePair<string, Dictionary<string, DataStr.BrokenFurnitureSync>> item4 in RecentBrokenFurns.ToList())
		{
			SaveData(GetKeyTemplate(SaveKeyTemplateType.Furns, item4.Key), JSON.Dump(item4.Value), seed);
		}
		foreach (KeyValuePair<string, Dictionary<long, DataStr.PickedGearSync>> item5 in RecentPickedGears.ToList())
		{
			SaveData(GetKeyTemplate(SaveKeyTemplateType.PickedGears, item5.Key), JSON.Dump(item5.Value), seed);
		}
		foreach (KeyValuePair<string, Dictionary<string, int>> item6 in RecentlyLootedContainers.ToList())
		{
			SaveData(GetKeyTemplate(SaveKeyTemplateType.LootedContainers, item6.Key), JSON.Dump(item6.Value), seed);
		}
		foreach (KeyValuePair<string, Dictionary<string, int>> item7 in RecentlyHarvastedPlants.ToList())
		{
			SaveData(GetKeyTemplate(SaveKeyTemplateType.HarvestedPlants, item7.Key), JSON.Dump(item7.Value), seed);
		}
		RecentVisual = new Dictionary<string, Dictionary<int, DataStr.DroppedGearItemDataPacket>>();
		RecentData = new Dictionary<string, Dictionary<int, DataStr.SlicedJsonDroppedGear>>();
		RecentOpenableThings = new Dictionary<string, Dictionary<string, bool>>();
		RecentBrokenFurns = new Dictionary<string, Dictionary<string, DataStr.BrokenFurnitureSync>>();
		RecentPickedGears = new Dictionary<string, Dictionary<long, DataStr.PickedGearSync>>();
		RecentlyLootedContainers = new Dictionary<string, Dictionary<string, int>>();
		RecentlyHarvastedPlants = new Dictionary<string, Dictionary<string, int>>();
		SaveData("ExpeditionManager", ExpeditionManager.Save(), GetSeed());
		if (stopwatch != null)
		{
			stopwatch.Stop();
			Log("SaveRecentStuff() Took " + stopwatch.ElapsedMilliseconds + "ms");
		}
	}

	public static string LoadData(string name, int Seed = 0, bool Compressed = false)
	{
		if (NoSaveAndLoad)
		{
			return "";
		}
		if (SaveLogging)
		{
			Log("Attempt to load " + name);
		}
		Stopwatch stopwatch = null;
		if (Diagnostic)
		{
			stopwatch = Stopwatch.StartNew();
		}
		string text = "";
		string pathForName = GetPathForName(name, Seed);
		if (!File.Exists(pathForName))
		{
			if (stopwatch != null)
			{
				stopwatch.Stop();
				Log("LoadData() Took " + stopwatch.ElapsedMilliseconds + "ms");
			}
			if (SaveLogging)
			{
				Log("File " + pathForName + " not exist");
			}
		}
		else
		{
			byte[] bytes = File.ReadAllBytes(pathForName);
			text = Encoding.UTF8.GetString(bytes);
			if (stopwatch != null)
			{
				stopwatch.Stop();
				Log("LoadData() Took " + stopwatch.ElapsedMilliseconds + "ms");
			}
			if (SaveLogging)
			{
				Log("Loaded with no problems");
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			text = UpgradeOldJsonFile(text);
		}
		return text;
	}

	public static string GetSeparator()
	{
		return "/";
	}

	public static string GetPathForName(string name, int Seed = 0)
	{
		if (NoSaveAndLoad)
		{
			return "";
		}
		if (string.IsNullOrEmpty(PersistentDataPath.m_Path))
		{
			PersistentDataPath.Init();
		}
		if (Seed != 0)
		{
			return PersistentDataPath.m_Path + GetSeparator() + Seed + GetSeparator() + name;
		}
		return PersistentDataPath.m_Path + GetSeparator() + name;
	}

	public static bool SaveData(string name, string content, int Seed = 0, string CustomPath = "", string JustAdd = "")
	{
		if (NoSaveAndLoad)
		{
			return false;
		}
		Stopwatch stopwatch = null;
		if (Diagnostic)
		{
			stopwatch = Stopwatch.StartNew();
		}
		if (SaveLogging)
		{
			Log("Attempt to save " + name);
		}
		string text = GetPathForName(name, Seed);
		if (!string.IsNullOrEmpty(CustomPath))
		{
			text = CustomPath;
		}
		if (!string.IsNullOrEmpty(JustAdd))
		{
			text = GetPathForName("", Seed) + GetSeparator() + JustAdd;
		}
		string text2 = text + "_temp";
		if (File.Exists(text2))
		{
			File.Delete(text2);
		}
		Stream stream = new FileStream(text2, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
		if (stream == null)
		{
			Error("Save failed, writing stream wasn't created");
			return false;
		}
		byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(content);
		stream.Write(bytes, 0, bytes.Length);
		stream.Dispose();
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		File.Copy(text2, text, overwrite: true);
		if (File.Exists(text2))
		{
			File.Delete(text2);
		}
		if (stopwatch != null)
		{
			stopwatch.Stop();
			Log("SaveData() Took " + stopwatch.ElapsedMilliseconds + "ms");
		}
		if (SaveLogging)
		{
			Log("Everything alright! File saved!");
		}
		return true;
	}

	public static void DeleteData(string name, int Seed = 0)
	{
		try
		{
			string pathForName = GetPathForName(name, Seed);
			if (File.Exists(pathForName))
			{
				File.Delete(pathForName);
			}
		}
		catch (Exception ex)
		{
			Error(ex.ToString());
		}
	}

	public static string GetKeyTemplate(SaveKeyTemplateType T, string Scene, string GUID = "")
	{
		return T switch
		{
			SaveKeyTemplateType.Container => Scene + "_" + GUID, 
			SaveKeyTemplateType.DropsVisual => Scene + "_DropVisual", 
			SaveKeyTemplateType.Openables => Scene + "_Open", 
			SaveKeyTemplateType.DropsData => Scene + "_DropsData", 
			SaveKeyTemplateType.Furns => Scene + "_Furns", 
			SaveKeyTemplateType.PickedGears => Scene + "_PickedGears", 
			SaveKeyTemplateType.LootedContainers => Scene + "_LootedContainers", 
			SaveKeyTemplateType.HarvestedPlants => Scene + "_HarvestedPlants", 
			_ => "_UNKNOWN", 
		};
	}

	public static bool IsFileExist(string name, int Seed = 0)
	{
		if (NoSaveAndLoad)
		{
			return false;
		}
		return File.Exists(GetPathForName(name, Seed));
	}

	public static void CreateFolderIfNotExist(string path)
	{
		if (!NoSaveAndLoad && !Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
		}
	}

	public static void ValidateRootExits()
	{
		if (!NoSaveAndLoad)
		{
			CreateFolderIfNotExist(GetPathForName(GetSeed().ToString() ?? ""));
		}
	}

	public static void SaveContainer(string scene, string GUID, string Content)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.Container, scene, GUID);
		ValidateRootExits();
		SaveData(keyTemplate, Content, seed);
	}

	public static string LoadContainer(string scene, string GUID)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.Container, scene, GUID);
		ValidateRootExits();
		return LoadData(keyTemplate, seed, Compressed: true);
	}

	public static void RemoveContainer(string scene, string GUID)
	{
		int seed = GetSeed();
		DeleteData(GetKeyTemplate(SaveKeyTemplateType.Container, scene, GUID), seed);
	}

	public static bool ContainerNotEmpty(string Scene, string GUID)
	{
		int seed = GetSeed();
		return IsFileExist(GetKeyTemplate(SaveKeyTemplateType.Container, Scene, GUID), seed);
	}

	public static Dictionary<string, bool> LoadOpenableThings(string scene)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.Openables, scene);
		if (RecentOpenableThings.TryGetValue(scene, out var value))
		{
			return value;
		}
		string text = LoadData(keyTemplate, seed);
		if (text != "")
		{
			return JSON.Load(text).Make<Dictionary<string, bool>>();
		}
		return null;
	}

	public static void ChangeOpenableThingState(string scene, string GUID, bool state)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.Openables, scene);
		if (RecentOpenableThings.TryGetValue(scene, out var value))
		{
			if (value.ContainsKey(GUID))
			{
				value.Remove(GUID);
			}
			value.Add(GUID, state);
			RecentOpenableThings.Remove(scene);
			RecentOpenableThings.Add(scene, value);
			return;
		}
		value = LoadOpenableThings(scene);
		if (value == null)
		{
			value = new Dictionary<string, bool>();
		}
		value.Remove(GUID);
		value.Add(GUID, state);
		RecentOpenableThings.Add(scene, value);
		ValidateRootExits();
		SaveData(keyTemplate, JSON.Dump(value), seed);
	}

	public static void AddHarvestedPlant(string GUID, string Scene, int Client = 0)
	{
		MPStats.AddPlantHarvested(Server.GetMACByID(Client));
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.HarvestedPlants, Scene);
		if (RecentlyHarvastedPlants.TryGetValue(Scene, out var value))
		{
			value.Remove(GUID);
			value.Add(GUID, MyMod.MinutesFromStartServer);
			RecentlyHarvastedPlants.Remove(Scene);
			RecentlyHarvastedPlants.Add(Scene, value);
			return;
		}
		value = LoadHarvestedPlants(Scene);
		if (value == null)
		{
			value = new Dictionary<string, int>();
		}
		value.Remove(GUID);
		value.Add(GUID, MyMod.MinutesFromStartServer);
		ValidateRootExits();
		SaveData(keyTemplate, JSON.Dump(value), seed);
		RecentlyHarvastedPlants.Add(Scene, value);
	}

	public static void RepairBreakdowns(string Scene, List<string> BreakdownsList = null)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.Furns, Scene);
		bool flag = BreakdownsList.Count == 0;
		if (RecentBrokenFurns.ContainsKey(Scene))
		{
			foreach (KeyValuePair<string, DataStr.BrokenFurnitureSync> item in RecentBrokenFurns[Scene].ToList())
			{
				if (flag || BreakdownsList.Contains(RecentBrokenFurns[Scene][item.Key].m_Guid))
				{
					RecentBrokenFurns[Scene][item.Key].m_Broken = false;
					ServerSend.FURNBROKEN(0, RecentBrokenFurns[Scene][item.Key], toEveryOne: true);
					if (Scene == MyMod.level_guid)
					{
						MyMod.RepairBrokenFurniture(RecentBrokenFurns[Scene][item.Key].m_Guid, RecentBrokenFurns[Scene][item.Key].m_ParentGuid);
					}
				}
			}
			return;
		}
		Dictionary<string, DataStr.BrokenFurnitureSync> dictionary = LoadFurnsData(Scene);
		if (dictionary == null)
		{
			return;
		}
		foreach (KeyValuePair<string, DataStr.BrokenFurnitureSync> item2 in dictionary.ToList())
		{
			if (flag || BreakdownsList.Contains(dictionary[item2.Key].m_Guid))
			{
				dictionary[item2.Key].m_Broken = false;
				ServerSend.FURNBROKEN(0, dictionary[item2.Key], toEveryOne: true);
				if (Scene == MyMod.level_guid)
				{
					MyMod.RepairBrokenFurniture(dictionary[item2.Key].m_Guid, dictionary[item2.Key].m_ParentGuid);
				}
			}
		}
		ValidateRootExits();
		SaveData(keyTemplate, JSON.Dump(dictionary), seed);
		RecentBrokenFurns.Remove(Scene);
		RecentBrokenFurns.Add(Scene, dictionary);
	}

	public static void ForceGrowPlants(string Scene, List<string> GrowList = null)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.HarvestedPlants, Scene);
		bool flag = GrowList.Count == 0;
		if (RecentlyHarvastedPlants.ContainsKey(Scene))
		{
			foreach (KeyValuePair<string, int> item in RecentlyHarvastedPlants[Scene].ToList())
			{
				if (flag || GrowList.Contains(item.Key))
				{
					RecentlyHarvastedPlants[Scene][item.Key] = -1;
					ServerSend.LOOTEDHARVESTABLE(0, item.Key, Scene, -1, toEveryOne: true);
					if (Scene == MyMod.level_guid)
					{
						MyMod.AddHarvastedPlant(item.Key);
					}
				}
			}
			return;
		}
		Dictionary<string, int> dictionary = LoadHarvestedPlants(Scene);
		if (dictionary == null)
		{
			return;
		}
		foreach (KeyValuePair<string, int> item2 in dictionary.ToList())
		{
			if (flag || GrowList.Contains(item2.Key))
			{
				dictionary[item2.Key] = -1;
				ServerSend.LOOTEDHARVESTABLE(0, item2.Key, Scene, -1, toEveryOne: true);
				if (Scene == MyMod.level_guid)
				{
					MyMod.AddHarvastedPlant(item2.Key);
				}
			}
		}
		ValidateRootExits();
		SaveData(keyTemplate, JSON.Dump(dictionary), seed);
		RecentlyHarvastedPlants.Add(Scene, dictionary);
	}

	public static Dictionary<string, int> CheckPlantsGrow(Dictionary<string, int> input)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> item in input)
		{
			string key = item.Key;
			int value = item.Value;
			int num = value + 4320;
			if (MyMod.MinutesFromStartServer > num)
			{
				dictionary.Add(key, -1);
			}
			else
			{
				dictionary.Add(key, value);
			}
		}
		return dictionary;
	}

	public static Dictionary<string, int> LoadHarvestedPlants(string scene)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.HarvestedPlants, scene);
		if (RecentlyHarvastedPlants.TryGetValue(scene, out var value))
		{
			return CheckPlantsGrow(value);
		}
		string text = LoadData(keyTemplate, seed);
		if (text != "")
		{
			return CheckPlantsGrow(JSON.Load(text).Make<Dictionary<string, int>>());
		}
		return null;
	}

	public static int GetContainerState(string Scene, string GUID)
	{
		Dictionary<string, int> dictionary = LoadLootedContainersData(Scene);
		if (dictionary == null)
		{
			return -1;
		}
		if (dictionary.TryGetValue(GUID, out var value))
		{
			return value;
		}
		return -1;
	}

	public static void SetConstainerState(string Scene, string GUID, int State, bool DontNotify = false)
	{
		Dictionary<string, int> dictionary = LoadLootedContainersData(Scene);
		if (dictionary == null)
		{
			dictionary = new Dictionary<string, int>();
			dictionary.Add(GUID, State);
			RecentlyLootedContainers.Remove(Scene);
			RecentlyLootedContainers.Add(Scene, dictionary);
			if (!DontNotify)
			{
				ServerSend.CHANGECONTAINERSTATE(0, GUID, State, Scene, toEveryOne: true);
				if (MyMod.level_guid == Scene)
				{
					MyMod.RemoveLootFromContainer(GUID, State);
				}
			}
			return;
		}
		if (dictionary.ContainsKey(GUID))
		{
			dictionary.Remove(GUID);
		}
		dictionary.Add(GUID, State);
		RecentlyLootedContainers.Remove(Scene);
		RecentlyLootedContainers.Add(Scene, dictionary);
		if (!DontNotify)
		{
			ServerSend.CHANGECONTAINERSTATE(0, GUID, State, Scene, toEveryOne: true);
			if (MyMod.level_guid == Scene)
			{
				MyMod.RemoveLootFromContainer(GUID, State);
			}
		}
	}

	public static void AddLootedContainer(DataStr.ContainerOpenSync Box, int State = 0, int Looter = 0)
	{
		string levelGUID = Box.m_LevelGUID;
		string guid = Box.m_Guid;
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.LootedContainers, levelGUID);
		if (RecentlyLootedContainers.TryGetValue(levelGUID, out var value))
		{
			if (value.ContainsKey(guid))
			{
				value.Remove(guid);
			}
			else if (Looter != -1)
			{
				MPStats.AddLootedContainer(Server.GetMACByID(Looter));
			}
			value.Add(guid, State);
			RecentlyLootedContainers.Remove(levelGUID);
			RecentlyLootedContainers.Add(levelGUID, value);
			return;
		}
		value = LoadLootedContainersData(levelGUID);
		if (value == null)
		{
			value = new Dictionary<string, int>();
		}
		if (value.ContainsKey(guid))
		{
			value.Remove(guid);
		}
		else if (Looter != -1)
		{
			MPStats.AddLootedContainer(Server.GetMACByID(Looter));
		}
		value.Add(guid, State);
		ValidateRootExits();
		SaveData(keyTemplate, JSON.Dump(value), seed);
		RecentlyLootedContainers.Add(levelGUID, value);
	}

	public static Dictionary<string, int> LoadLootedContainersData(string scene)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.LootedContainers, scene);
		if (RecentlyLootedContainers.TryGetValue(scene, out var value))
		{
			return value;
		}
		string text = LoadData(keyTemplate, seed);
		if (text != "")
		{
			return JSON.Load(text).Make<Dictionary<string, int>>();
		}
		return null;
	}

	public static int AddLootToScene(string Scene)
	{
		Dictionary<string, int> dictionary = LoadLootedContainersData(Scene);
		if (dictionary == null)
		{
			return 0;
		}
		int num = 0;
		List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
		foreach (KeyValuePair<string, int> item in dictionary)
		{
			list.Add(item);
		}
		for (int i = 0; i < list.Count; i++)
		{
			DataStr.ContainerOpenSync containerOpenSync = new DataStr.ContainerOpenSync();
			containerOpenSync.m_Guid = list[i].Key;
			containerOpenSync.m_LevelGUID = Scene;
			if (list[i].Value != 2)
			{
				AddLootedContainer(containerOpenSync, 2, -1);
				ServerSend.CHANGECONTAINERSTATE(0, list[i].Key, 2, Scene, toEveryOne: true);
				if (MyMod.level_guid == Scene)
				{
					MyMod.RemoveLootFromContainer(list[i].Key, 2);
				}
				num++;
			}
		}
		return num;
	}

	public static bool AddLootToContainerOnScene(string GUID, string Scene)
	{
		Dictionary<string, int> dictionary = LoadLootedContainersData(Scene);
		if (dictionary == null)
		{
			return false;
		}
		if (dictionary.ContainsKey(GUID))
		{
			AddLootedContainer(new DataStr.ContainerOpenSync
			{
				m_Guid = GUID,
				m_LevelGUID = Scene
			}, 2, -1);
			ServerSend.CHANGECONTAINERSTATE(0, GUID, 2, Scene, toEveryOne: true);
			if (MyMod.level_guid == Scene)
			{
				MyMod.RemoveLootFromContainer(GUID, 2);
			}
			return true;
		}
		return false;
	}

	public static long GetPickedGearKey(DataStr.PickedGearSync Gear)
	{
		return GetPickedGearKey(Gear.m_Spawn);
	}

	public static long GetPickedGearKey(Vector3 v3)
	{
		return Shared.GetVectorHashV2(v3);
	}

	public static void AddPickedGear(DataStr.PickedGearSync Gear, int picker)
	{
		MPStats.AddPickedGear(Server.GetMACByID(picker));
		string levelGUID = Gear.m_LevelGUID;
		long pickedGearKey = GetPickedGearKey(Gear);
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.PickedGears, levelGUID);
		if (RecentPickedGears.TryGetValue(levelGUID, out var value))
		{
			value.Remove(pickedGearKey);
			value.Add(pickedGearKey, Gear);
			RecentPickedGears.Remove(levelGUID);
			RecentPickedGears.Add(levelGUID, value);
			return;
		}
		value = LoadPickedGearsData(levelGUID);
		if (value == null)
		{
			value = new Dictionary<long, DataStr.PickedGearSync>();
		}
		value.Remove(pickedGearKey);
		value.Add(pickedGearKey, Gear);
		ValidateRootExits();
		SaveData(keyTemplate, JSON.Dump(value), seed);
		RecentPickedGears.Add(levelGUID, value);
	}

	public static Dictionary<long, DataStr.PickedGearSync> LoadPickedGearsData(string scene)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.PickedGears, scene);
		if (RecentPickedGears.TryGetValue(scene, out var value))
		{
			return value;
		}
		string text = LoadData(keyTemplate, seed);
		if (text != "")
		{
			return JSON.Load(text).Make<Dictionary<long, DataStr.PickedGearSync>>();
		}
		return null;
	}

	public static void AddBrokenFurn(DataStr.BrokenFurnitureSync furn)
	{
		string levelGUID = furn.m_LevelGUID;
		string key = furn.m_LevelGUID + furn.m_ParentGuid;
		Dictionary<string, DataStr.BrokenFurnitureSync> dictionary = LoadFurnsData(levelGUID);
		if (dictionary == null)
		{
			dictionary = new Dictionary<string, DataStr.BrokenFurnitureSync>();
		}
		dictionary.Remove(key);
		dictionary.Add(key, furn);
		RecentBrokenFurns.Remove(levelGUID);
		RecentBrokenFurns.Add(levelGUID, dictionary);
	}

	public static Dictionary<string, DataStr.BrokenFurnitureSync> LoadFurnsData(string scene)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.Furns, scene);
		if (RecentBrokenFurns.TryGetValue(scene, out var value))
		{
			return value;
		}
		string text = LoadData(keyTemplate, seed);
		if (text != "")
		{
			value = JSON.Load(text).Make<Dictionary<string, DataStr.BrokenFurnitureSync>>();
			RecentBrokenFurns.Add(scene, value);
			return value;
		}
		return null;
	}

	public static Dictionary<int, DataStr.DroppedGearItemDataPacket> LoadDropVisual(string scene)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.DropsVisual, scene);
		if (!OldDictReady)
		{
			OldIdDict = JSON.Load(OldIdsJson).Make<Dictionary<int, string>>();
			OldIdsJson = "";
			OldDictReady = true;
		}
		if (RecentVisual.TryGetValue(scene, out var value))
		{
			return value;
		}
		string text = LoadData(keyTemplate, seed);
		if (text != "")
		{
			Dictionary<int, DataStr.DroppedGearItemDataPacket> dictionary = JSON.Load(text).Make<Dictionary<int, DataStr.DroppedGearItemDataPacket>>();
			Dictionary<int, DataStr.DroppedGearItemDataPacket> dictionary2 = new Dictionary<int, DataStr.DroppedGearItemDataPacket>();
			{
				foreach (KeyValuePair<int, DataStr.DroppedGearItemDataPacket> item in dictionary)
				{
					string value2 = "";
					if (item.Value.m_GearID != -1)
					{
						OldIdDict.TryGetValue(item.Value.m_GearID, out value2);
						item.Value.m_Extra.m_GearName = value2;
						item.Value.m_GearID = -1;
					}
					dictionary2.Add(item.Key, item.Value);
				}
				return dictionary2;
			}
		}
		return null;
	}

	public static Dictionary<int, DataStr.SlicedJsonDroppedGear> LoadDropData(string scene)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.DropsData, scene);
		if (RecentData.TryGetValue(scene, out var value))
		{
			return value;
		}
		string text = LoadData(keyTemplate, seed);
		if (text != "")
		{
			Dictionary<int, DataStr.SlicedJsonDroppedGear> dictionary = JSON.Load(text).Make<Dictionary<int, DataStr.SlicedJsonDroppedGear>>();
			Dictionary<int, DataStr.SlicedJsonDroppedGear> dictionary2 = new Dictionary<int, DataStr.SlicedJsonDroppedGear>();
			{
				foreach (KeyValuePair<int, DataStr.SlicedJsonDroppedGear> item in dictionary)
				{
					if (item.Value.m_GearName != "" && item.Value.m_Extra.m_GearName == "")
					{
						item.Value.m_Extra.m_GearName = item.Value.m_GearName;
					}
					else if (item.Value.m_GearName == "" && item.Value.m_Extra.m_GearName != "")
					{
						item.Value.m_GearName = item.Value.m_Extra.m_GearName;
					}
					dictionary2.Add(item.Key, item.Value);
				}
				return dictionary2;
			}
		}
		return null;
	}

	public static void AddGearVisual(string scene, DataStr.DroppedGearItemDataPacket gear)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.DropsVisual, scene);
		if (RecentVisual.TryGetValue(scene, out var value))
		{
			value.Remove(gear.m_Hash);
			value.Add(gear.m_Hash, gear);
			RecentVisual.Remove(scene);
			RecentVisual.Add(scene, value);
			return;
		}
		value = LoadDropVisual(scene);
		if (value == null)
		{
			value = new Dictionary<int, DataStr.DroppedGearItemDataPacket>();
		}
		value.Remove(gear.m_Hash);
		value.Add(gear.m_Hash, gear);
		ValidateRootExits();
		SaveData(keyTemplate, JSON.Dump(value), seed);
		RecentVisual.Add(scene, value);
	}

	public static void AddGearData(string scene, int hash, DataStr.SlicedJsonDroppedGear GearData)
	{
		int seed = GetSeed();
		string keyTemplate = GetKeyTemplate(SaveKeyTemplateType.DropsData, scene);
		if (RecentData.TryGetValue(scene, out var value))
		{
			value.Remove(hash);
			value.Add(hash, GearData);
			RecentData.Remove(scene);
			RecentData.Add(scene, value);
			return;
		}
		value = LoadDropData(scene);
		if (value == null)
		{
			value = new Dictionary<int, DataStr.SlicedJsonDroppedGear>();
		}
		value.Remove(hash);
		value.Add(hash, GearData);
		ValidateRootExits();
		SaveData(keyTemplate, JSON.Dump(value), seed);
		RecentData.Add(scene, value);
	}

	public static void RemovSpecificGear(int Hash, string Scene)
	{
		Log("[RemovSpecificGear] Trying to remove " + Hash);
		if (!RecentData.TryGetValue(Scene, out var value))
		{
			value = LoadDropData(Scene);
		}
		if (value != null)
		{
			value.Remove(Hash);
			RecentData.Remove(Scene);
			RecentData.Add(Scene, value);
		}
		if (!RecentVisual.TryGetValue(Scene, out var value2))
		{
			value2 = LoadDropVisual(Scene);
		}
		if (value2 != null)
		{
			value2.Remove(Hash);
			RecentVisual.Remove(Scene);
			RecentVisual.Add(Scene, value2);
		}
		if (Blanks.ContainsKey(Hash))
		{
			Blanks.Remove(Hash);
		}
		ExpeditionManager.RemoveGearSpawnerGear(Hash);
	}

	public static DataStr.SlicedJsonDroppedGear RequestSpecificGear(int Hash, string Scene, bool Remove = true)
	{
		Dictionary<int, DataStr.SlicedJsonDroppedGear> dictionary = LoadDropData(Scene);
		DataStr.SlicedJsonDroppedGear value = null;
		if (dictionary != null && dictionary.TryGetValue(Hash, out value) && Remove)
		{
			RemovSpecificGear(Hash, Scene);
		}
		return value;
	}

	public static bool SaveServerCFG(DataStr.ServerSettingsData CFG)
	{
		return SaveData("ServerSettingsData", JSON.Dump(CFG));
	}

	public static DataStr.ServerSettingsData RequestServerCFG()
	{
		string text = LoadData("ServerSettingsData");
		if (text != "")
		{
			if (text.Contains("SkyCoop.MyMod+ServerSettingsData"))
			{
				return null;
			}
			return JSON.Load(text).Make<DataStr.ServerSettingsData>();
		}
		return null;
	}

	public static void SaveMyName(string Name)
	{
		SaveData("MultiplayerNickName", Name);
	}

	public static string LoadMyName()
	{
		return LoadData("MultiplayerNickName");
	}

	public static string GetDictionaryString(Dictionary<string, string> Dict, string Key)
	{
		if (Dict.TryGetValue(Key, out var value))
		{
			return value;
		}
		return "";
	}

	public static void LoadGlobalData()
	{
		string text = LoadData("GlobalServerData", GetSeed());
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		if (text != "")
		{
			dictionary = JSON.Load(text).Make<Dictionary<string, string>>();
			MyMod.DeployedRopes = JSON.Load(GetDictionaryString(dictionary, "ropes")).Make<List<DataStr.ClimbingRopeSync>>();
			MyMod.ShowSheltersBuilded = JSON.Load(GetDictionaryString(dictionary, "shelters")).Make<List<DataStr.ShowShelterByOther>>();
			MyMod.MinutesFromStartServer = JSON.Load(GetDictionaryString(dictionary, "rtt")).Make<int[]>()[0];
			MyMod.DeathCreates = JSON.Load(GetDictionaryString(dictionary, "deathcreates")).Make<List<DataStr.DeathContainerData>>();
			MyMod.OveridedTime = JSON.Load(GetDictionaryString(dictionary, "gametime")).Make<string[]>()[0];
			string dictionaryString = GetDictionaryString(dictionary, "festivegears");
			if (!string.IsNullOrEmpty(dictionaryString))
			{
				FestiveGearsSpawnCooldown = JSON.Load(dictionaryString).Make<Dictionary<string, int>>();
			}
		}
	}

	public static void SaveJsonSnapshot(string Alias, string JSON)
	{
		CreateFolderIfNotExist(GetPathForName("Snapshots"));
		CreateFolderIfNotExist(GetPathForName("Snapshots" + GetSeparator() + Alias));
		DateTime now = DateTime.Now;
		string text = now.Hour + "_" + now.Minute + "_" + now.Second + "_" + now.Millisecond;
		SaveData(text, JSON, 0, GetPathForName("Snapshots" + GetSeparator() + Alias + GetSeparator() + text));
	}

	public static string VectorsFixUp(string Json)
	{
		Json = Json.Replace("System.Numerics.Vector3", "UnityEngine.Vector3");
		Json = Json.Replace("System.Numerics.Quaternion", "UnityEngine.Quaternion");
		Json = Json.Replace("\"X\"", "\"x\"");
		Json = Json.Replace("\"Y\"", "\"y\"");
		Json = Json.Replace("\"Z\"", "\"z\"");
		Json = Json.Replace("\"W\"", "\"w\"");
		return Json;
	}

	public static string UpgradeOldJsonFile(string Json, bool Decompress = false)
	{
		if (Decompress)
		{
			Json = Shared.DecompressString(Json);
		}
		Json = Json.Replace("SkyCoop.MyMod", "SkyCoop.DataStr");
		if (Decompress)
		{
			Json = Shared.CompressString(Json);
		}
		Json = VectorsFixUp(Json);
		return Json;
	}

	public static string GetSubNetworkGUID()
	{
		if (string.IsNullOrEmpty(MyMAC))
		{
			MyMAC = Shared.GetMacAddress();
		}
		return MyMAC;
	}

	public static void SaveBanned()
	{
		SaveData("BanList", JSON.Dump(BannedUsers));
	}

	public static Texture2D GetPhotoTexture(string GUID, string GearName)
	{
		Texture2D cachedTexture = GameCompat.GetCachedTexture("Photo_" + GUID);
		if (cachedTexture != null)
		{
			Log("Found cached texture");
			return cachedTexture;
		}
		string text = LoadPhoto(GUID, Cached: true);
		if (!string.IsNullOrEmpty(text))
		{
			Log("Loaded texture from local file");
			DataStr.Vector2Int gearResolution = MyMod.GetGearResolution(GearName);
			cachedTexture = new Texture2D(gearResolution.X, gearResolution.Y);
			cachedTexture.LoadImage(Convert.FromBase64String(text));
			GameCompat.CacheTexture("Photo_" + GUID, cachedTexture);
			return cachedTexture;
		}
		return null;
	}

	public static string AddPhoto(string Base64, bool Cached = false, string GUID = "")
	{
		if (string.IsNullOrEmpty(GUID))
		{
			GUID = GetNewUGUID();
		}
		int seed = GetSeed();
		string text = "Photos";
		if (Cached)
		{
			seed = 0;
			text = "CachedPhotos";
		}
		CreateFolderIfNotExist(GetPathForName(text, seed));
		SaveData(GUID, Base64, 0, GetPathForName(text + GetSeparator() + GUID, seed));
		return GUID;
	}

	public static void AddSpecialItem(string JSON, string Name)
	{
		int seed = GetSeed();
		string text = "SpecialItems";
		CreateFolderIfNotExist(GetPathForName(text, seed));
		SaveData(Name, JSON, 0, GetPathForName(text + GetSeparator() + Name, seed));
	}

	public static void CopyPhotoToExpeditionCache(string GUID)
	{
		string text = LoadPhoto(GUID, Cached: true);
		if (!string.IsNullOrEmpty(text) && !File.Exists("Mods" + GetSeparator() + "ExpeditionTemplates" + GetSeparator() + "CachedPhotos" + GetSeparator() + GUID))
		{
			SaveData(GUID, text, 0, "Mods" + GetSeparator() + "ExpeditionTemplates" + GetSeparator() + "CachedPhotos" + GetSeparator() + GUID);
		}
	}

	public static string LoadPhoto(string GUID, bool Cached = false)
	{
		string text = "Mods";
		if (!string.IsNullOrEmpty(GetBaseDirectory()))
		{
			text = GetBaseDirectory() + GetSeparator() + "Mods";
		}
		CreateFolderIfNotExist(text);
		CreateFolderIfNotExist(text + GetSeparator() + "ExpeditionTemplates");
		CreateFolderIfNotExist(text + GetSeparator() + "ExpeditionTemplates" + GetSeparator() + "CachedPhotos");
		if (File.Exists(text + GetSeparator() + "ExpeditionTemplates" + GetSeparator() + "CachedPhotos" + GetSeparator() + GUID))
		{
			byte[] bytes = File.ReadAllBytes(text + GetSeparator() + "ExpeditionTemplates" + GetSeparator() + "CachedPhotos" + GetSeparator() + GUID);
			return Encoding.UTF8.GetString(bytes);
		}
		int seed = GetSeed();
		string text2 = "Photos";
		if (Cached)
		{
			seed = 0;
			text2 = "CachedPhotos";
		}
		return LoadData(text2 + GetSeparator() + GUID, seed);
	}

	public static void AddRockCach(DataStr.FakeRockCacheVisualData Cache, int FromClient)
	{
		if (!RockCaches.ContainsKey(Cache.m_LevelGUID))
		{
			RockCaches.Add(Cache.m_LevelGUID, new Dictionary<string, DataStr.FakeRockCacheVisualData>());
		}
		if (!RockCaches[Cache.m_LevelGUID].ContainsKey(Cache.m_GUID))
		{
			RockCaches[Cache.m_LevelGUID].Add(Cache.m_GUID, Cache);
		}
		else
		{
			RockCaches[Cache.m_LevelGUID][Cache.m_GUID] = Cache;
		}
		RockCachesChanged = true;
		ServerSend.ADDROCKCACH(FromClient, Cache, Cache.m_LevelGUID);
	}

	public static Dictionary<string, DataStr.FakeRockCacheVisualData> GetRockCaches(string Scene)
	{
		if (RockCaches.ContainsKey(Scene))
		{
			return new Dictionary<string, DataStr.FakeRockCacheVisualData>(RockCaches[Scene]);
		}
		return new Dictionary<string, DataStr.FakeRockCacheVisualData>();
	}

	public static void RemoveRockCach(DataStr.FakeRockCacheVisualData Cache, int FromClient)
	{
		if (RockCaches.ContainsKey(Cache.m_LevelGUID) && RockCaches[Cache.m_LevelGUID].ContainsKey(Cache.m_GUID))
		{
			if (ContainerNotEmpty(Cache.m_LevelGUID, Cache.m_GUID))
			{
				ServerSend.ADDHUDMESSAGE(FromClient, "Rock cache should be empty!");
				return;
			}
			RockCaches[Cache.m_LevelGUID].Remove(Cache.m_GUID);
			RockCachesChanged = true;
		}
	}

	public static void AddUniversalSyncableObject(DataStr.UniversalSyncableObject Data)
	{
		if (UniversalSyncableObjects.TryGetValue(Data.m_Scene, out var value))
		{
			if (Data.m_RemoveTime != 0 && Data.m_RemoveTime < MyMod.MinutesFromStartServer)
			{
				RemoveContainer(Data.m_Scene, Data.m_GUID);
			}
			value.Remove(Data.m_GUID);
			value.Add(Data.m_GUID, Data);
		}
		else
		{
			value = new Dictionary<string, DataStr.UniversalSyncableObject>();
			value.Add(Data.m_GUID, Data);
		}
		UniversalSyncableObjects.Remove(Data.m_Scene);
		UniversalSyncableObjects.Add(Data.m_Scene, value);
		UniversalSyncableObjectsChanged = true;
	}

	public static void RemoveUniversalSyncableObject(string Scene, string GUID)
	{
		if (UniversalSyncableObjects.TryGetValue(Scene, out var value))
		{
			value.Remove(GUID);
			UniversalSyncableObjects.Remove(Scene);
			UniversalSyncableObjects.Add(Scene, value);
			ServerSend.REMOVEUNIVERSALSYNCABLE(GUID, Scene);
			UniversalSyncableObjectsChanged = true;
		}
	}

	public static Dictionary<string, DataStr.UniversalSyncableObject> GetUniversalSyncablesForScene(string Scene)
	{
		if (UniversalSyncableObjects.TryGetValue(Scene, out var value))
		{
			List<DataStr.UniversalSyncableObject> list = value.Values.ToList();
			for (int num = list.Count - 1; num >= 0; num--)
			{
				DataStr.UniversalSyncableObject universalSyncableObject = list[num];
				if (universalSyncableObject.m_RemoveTime != 0 && universalSyncableObject.m_RemoveTime < MyMod.MinutesFromStartServer)
				{
					value.Remove(universalSyncableObject.m_GUID);
					RemoveContainer(Scene, universalSyncableObject.m_GUID);
					ServerSend.REMOVEUNIVERSALSYNCABLE(universalSyncableObject.m_GUID, Scene);
				}
			}
			UniversalSyncableObjects.Remove(Scene);
			UniversalSyncableObjects.Add(Scene, value);
			UniversalSyncableObjectsChanged = true;
			return value;
		}
		return new Dictionary<string, DataStr.UniversalSyncableObject>();
	}

	public static bool NeverSeenExpeditions()
	{
		return !IsFileExist("SeenExpeditions");
	}

	public static void SeenExpeditions()
	{
		SaveData("SeenExpeditions", "");
	}

	public static void SaveLastConnectedServer(string Server)
	{
		SaveData("LastServer", Server);
	}

	public static string GetLastConnectedServer()
	{
		string text = LoadData("LastServer");
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return "127.0.0.1";
	}

	public static DataStr.UniversalSyncableObject GetUniversalSyncable(string Scene, string GUID)
	{
		if (UniversalSyncableObjects.TryGetValue(Scene, out var value))
		{
			if (value.TryGetValue(GUID, out var value2) && value2.m_RemoveTime != 0 && value2.m_RemoveTime < MyMod.MinutesFromStartServer)
			{
				value.Remove(value2.m_GUID);
				RemoveContainer(Scene, GUID);
			}
			UniversalSyncableObjects.Remove(Scene);
			UniversalSyncableObjects.Add(Scene, value);
			UniversalSyncableObjectsChanged = true;
			return value2;
		}
		return null;
	}

	public static void TrySpawnFestives(string Scene)
	{
		if (!ResourceIndependent.GearSpawnersReady || FestiveGearsSpawnCooldown.Count <= 0)
		{
			return;
		}
		List<KeyValuePair<string, int>> list = FestiveGearsSpawnCooldown.ToList();
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (list[num].Value < MyMod.MinutesFromStartServer)
			{
				list.RemoveAt(num);
			}
		}
		FestiveGearsSpawnCooldown = list.ToDictionary((KeyValuePair<string, int> x) => x.Key, (KeyValuePair<string, int> x) => x.Value);
		if (!FestiveGearsSpawnCooldown.ContainsKey(Scene))
		{
			ResourceIndependent.SpawnSomeGearsOnScene(Scene, "", "GEAR_SCPresent");
			ResourceIndependent.SpawnSomeGearsOnScene(Scene, "SANDBOX", "GEAR_SCPresent");
			FestiveGearsSpawnCooldown.Add(Scene, MyMod.MinutesFromStartServer + 1440);
		}
	}
}
