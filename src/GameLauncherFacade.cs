using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using JavaClass = java.lang.Class;
using System.Diagnostics;

namespace Cfvbaibai.Cardfantasy.DotNetFacade
{
    public enum FirstAttack
    {
        /// <summary>
        /// 按规则决定先攻
        /// </summary>
        RuleSet = -1,
        /// <summary>
        /// 玩家1先攻
        /// </summary>
        Player1 = 0,
        /// <summary>
        /// 玩家2先攻
        /// </summary>
        Player2 = 1,
    }
    public enum DeckOrder
    {
        /// <summary>
        /// 随机出牌
        /// </summary>
        Random = 0,
        /// <summary>
        /// 按照预设顺序出牌
        /// </summary>
        Preset = 1,
    }
    public enum BossGuardType
    {
        /// <summary>
        /// 无杂兵
        /// </summary>
        NoGuard = 0,
        /// <summary>
        /// 普通杂兵
        /// </summary>
        NormalGuard = 1,
        /// <summary>
        /// 强杂兵
        /// </summary>
        StrongGuard = 2,
    }
    public enum LilithGameType
    {
        /// <summary>
        /// 清兵模式
        /// </summary>
        ClearGuards = 0,
        /// <summary>
        /// 尾刀模式
        /// </summary>
        RushBoss = 1,
    }
    [DataContract]
    public class ArenaGameResult
    {
        [DataMember(Name = "validationResult")]
        public string ValidationResult { get; set; }
        [DataMember(Name = "p1Win")]
        public int Player1Win { get; set; }
        [DataMember(Name = "p2Win")]
        public int Player2Win { get; set; }
        [DataMember(Name = "timeoutCount")]
        public int TimeoutCount { get; set; }
    }

    [DataContract]
    public class BossGameResult
    {
        [DataMember(Name = "validationResult")]
        public string ValidationResult { get; set; }
        [DataMember(Name = "gameCount")]
        public int GameCount { get; set; }
        [DataMember(Name = "coolDown")]
        public int CoolDown { get; set; }
        [DataMember(Name = "totalCost")]
        public int TotalCost { get; set; }
        [DataMember(Name = "timeoutCount")]
        public int TimeoutCount { get; set; }
        [DataMember(Name = "minDamage")]
        public double MinDamage { get; set; }
        [DataMember(Name = "avgDamage")]
        public double AvgDamage { get; set; }
        [DataMember(Name = "avgDamagePerMinute")]
        public double AvgDamagePerMinute { get; set; }
        [DataMember(Name = "maxDamage")]
        public double MaxDamage { get; set; }
        [DataMember(Name = "cvDamage")]
        public double CvDamage { get; set; }
        [DataMember(Name = "dataItems")]
        public ChartDataItem[] DataItems { get; set; }
    }

    [DataContract]
    public class ChartDataItem
    {
        [DataMember(Name = "label")]
        public string Label { get; set; }
        [DataMember(Name = "count")]
        public int Count { get; set; }
    }

    [DataContract]
    public class MapGameResult
    {
        [DataMember(Name = "validationResult")]
        public string ValidationResult { get; set; }
        [DataMember(Name = "timeoutCount")]
        public int TimeoutCount { get; set; }
        [DataMember(Name = "winCount")]
        public int WinCount { get; set; }
        [DataMember(Name = "advWinCount")]
        public int AdvWinCount { get; set; }
        [DataMember(Name = "lostCount")]
        public int LostCount { get; set; }
        [DataMember(Name = "unknownCount")]
        public int UnknownCount { get; set; }
    }

    [DataContract]
    public class LilithGameResult
    {
        [DataMember(Name = "validationResult")]
        public string ValidationResult { get; set; }
        [DataMember(Name = "avgBattleCount")]
        public double AvgBattleCount { get; set; }
        [DataMember(Name = "avgDamageToLilith")]
        public double AvgDamageToLilith { get; set; }
        [DataMember(Name = "cvBattleCount")]
        public double CvBattleCount { get; set; }
        [DataMember(Name = "cvDamageToLilith")]
        public double CvDamageToLilith { get; set; }
    }

    public class LoggingEventArgs
    {
        public string Message { get; private set; }
        public TraceLevel Level { get; private set; }
        public LoggingEventArgs(TraceLevel level, string message)
        {
            this.Message = message;
            this.Level = level;
        }
    }

    /// <summary>
    /// 魔卡幻想DotNet调用Java版本的Facade。
    /// </summary>
    /// <remarks>
    /// 编译时注意：将jni4net.n-0.8.8.0.dll添加入项目引用
    /// 运行时注意：jni4net.j-0.8.8.0.jar必须和jni4net.n-0.8.8.0.dll在同一目录下
    /// GameLauncherFacade调用方法：见Main函数
    /// </remarks>
    public class GameLauncherFacade
    {
        private JavaClass facadeClass;
        private net.sf.jni4net.jni.JNIEnv jvm;
        private const string sigPlayArenaGame = "(Ljava/lang/String;Ljava/lang/String;IIIIIIIIIILjava/lang/String;I)Ljava/lang/String;";
        private const string sigPlayBossGame = "(Ljava/lang/String;Ljava/lang/String;IIIIIII)Ljava/lang/String;";
        private const string sigPlayMapGame = "(Ljava/lang/String;Ljava/lang/String;II)Ljava/lang/String;";
        private const string sigPlayLilithGame = "(Ljava/lang/String;Ljava/lang/String;IIIILjava/lang/String;I)Ljava/lang/String;";

        private const string coreJarFileName = "mkhx.core-1.0-jar-with-dependencies.jar";
        private const string coreJarVersionFileName = "mkhx.core.version.txt";
        private const string coreJarRemoteBaseUrl = "http://www.mkhx.cc/resources/lib/corejar";

        public delegate void LoggingEventHandler(object sender, LoggingEventArgs args);
        private event LoggingEventHandler Logging;

        private void OnLogging(TraceLevel level, string s)
        {
            if (Logging != null)
            {
                Logging(this, new LoggingEventArgs(level, s));
            }
        }

        public void Initialize(bool tryUpdateCoreJar)
        {
            OnLogging(TraceLevel.Info, string.Format("Initializing GameLauncherFacade...tryUpdateCoreJar = {0}", tryUpdateCoreJar));
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var jarPath = Path.Combine(assemblyDir, coreJarFileName);
            OnLogging(TraceLevel.Info, string.Format("Local CoreJarPath = {0}", jarPath));
            bool jarExists = File.Exists(jarPath);
            OnLogging(TraceLevel.Info, string.Format("Local CoreJarExists = {0}", jarExists));
            var verPath = Path.Combine(assemblyDir, coreJarVersionFileName);
            OnLogging(TraceLevel.Info, string.Format("Local CoreJarVersionFilePath = {0}", verPath));
            var verExists = File.Exists(verPath);
            OnLogging(TraceLevel.Info, string.Format("Local CoreJarVersionFileExists = {0}", verExists));

            long existingVer = 0;
            if (jarExists && verExists)
            {
                var existingVerText = File.ReadAllText(verPath);
                var parseSuccess = long.TryParse(existingVerText, out existingVer);
                if (parseSuccess)
                {
                    OnLogging(TraceLevel.Info, string.Format("Successfully get local core jar version: {0}", existingVer));
                }
                else
                {
                    OnLogging(TraceLevel.Warning, string.Format("Failed to get local core jar version! Invalid version text: {0}", existingVerText));
                }
            }

            if (tryUpdateCoreJar || !jarExists)
            {
                OnLogging(TraceLevel.Info, "Trying to update local core jar with latest one on server.");
                using (WebClient client = new WebClient())
                {
                    try
                    {
                        var remoteVerUrl = coreJarRemoteBaseUrl + "/" + coreJarVersionFileName;
                        OnLogging(TraceLevel.Info, string.Format("Downloading version file from {0}...", remoteVerUrl));
                        var latestVerText = client.DownloadString(remoteVerUrl);
                        long latestVer = int.MaxValue;
                        if (!long.TryParse(latestVerText, out latestVer))
                        {
                            OnLogging(TraceLevel.Warning, string.Format("Server returns an invalid core jar file version: {0}. Force update...", latestVerText));
                        }
                        OnLogging(TraceLevel.Info, string.Format("Remote core jar version: {0}", latestVer));
                        if (!jarExists || existingVer < latestVer)
                        {
                            OnLogging(TraceLevel.Info, "Trying to replace local core jar with remote one...");
                            var remoteCoreJarUrl = coreJarRemoteBaseUrl + "/" + coreJarFileName;
                            OnLogging(TraceLevel.Info, string.Format("Downloading core jar file from {0}...", remoteCoreJarUrl));
                            client.DownloadFile(remoteCoreJarUrl, jarPath);
                            var remoteCoreJarVerFileUrl = coreJarRemoteBaseUrl + "/" + coreJarVersionFileName;
                            OnLogging(TraceLevel.Info, string.Format("Downloading core jar version file from {0}...", remoteCoreJarVerFileUrl));
                            client.DownloadFile(remoteCoreJarVerFileUrl, verPath);
                        }
                        else
                        {
                            OnLogging(TraceLevel.Info, "Local core jar file is already update-to-date.");
                        }
                    }
                    catch (Exception e)
                    {
                        OnLogging(TraceLevel.Error, "Failed to update local core jar with remote one due to error: " + e.ToString());
                        if (!jarExists)
                        {
                            throw new InvalidOperationException("Core jar file does not exists either on server or at local.", e);
                        }
                    }
                }
            }

            OnLogging(TraceLevel.Info, "Initializing JVM...");
            var setup = new net.sf.jni4net.BridgeSetup();
            setup.AddClassPath(jarPath);
            this.jvm = net.sf.jni4net.Bridge.CreateJVM(setup);
            this.facadeClass = this.jvm.FindClass("cfvbaibai/cardfantasy/game/launcher/GameLauncherFacade");
            OnLogging(TraceLevel.Info, "JVM initialized!");
        }

        private void CheckInitialization()
        {
            if (this.jvm == null)
            {
                throw new InvalidOperationException("Facade has not been initialized yet.");
            }
        }
        public ArenaGameResult PlayArenaGame(
            string deck1, string deck2, int heroLv1, int heroLv2,
            int p1CardAtBuff, int p1CardHpBuff, int p1HeroHpBuff,
            int p2CardAtBuff, int p2CardHpBuff, int p2HeroHpBuff,
            FirstAttack firstAttack, DeckOrder deckOrder, string vc1Text, int gameCount)
        {
            CheckInitialization();
            object[] args = new object[]
            {
                deck1, deck2, heroLv1, heroLv2,
                p1CardAtBuff, p1CardHpBuff, p1HeroHpBuff, p2CardAtBuff, p2CardHpBuff, p2HeroHpBuff,
                (int)firstAttack, (int)deckOrder, vc1Text, gameCount,
            };
            var resultText = jvm.CallStaticMethod<java.lang.String>(facadeClass, "playArenaGame", sigPlayArenaGame, args);
            var result = JsonConvert.DeserializeObject<ArenaGameResult>(resultText);
            return result;
        }
        public BossGameResult PlayBossGame(
            string playerDeck, string bossName, int heroLv,
            int kingdomBuff, int forestBuff, int savageBuff, int hellBuff,
            BossGuardType guardType, int gameCount)
        {
            CheckInitialization();
            object[] args = new object[]
            {
                playerDeck, bossName, heroLv, kingdomBuff, forestBuff, savageBuff, hellBuff, (int)guardType, gameCount,
            };
            var resultText = jvm.CallStaticMethod<java.lang.String>(facadeClass, "playBossGame", sigPlayBossGame, args);
            var result = JsonConvert.DeserializeObject<BossGameResult>(resultText);
            return result;
        }

        public MapGameResult playMapGame(string playerDeck, string mapName, int heroLv, int gameCount)
        {
            CheckInitialization();
            object[] args = new object[] { playerDeck, mapName, heroLv, gameCount };
            var resultText = jvm.CallStaticMethod<java.lang.String>(facadeClass, "playMapGame", sigPlayMapGame, args);
            var result = JsonConvert.DeserializeObject<MapGameResult>(resultText);
            return result;
        }

        public LilithGameResult playLilithGame(string playerDeck, string lilithName, int heroLv,
            LilithGameType gameType, int remainingGuard, int remainingHp, string eventCardNames, int gameCount)
        {
            CheckInitialization();
            object[] args = new object[] { playerDeck, lilithName, heroLv, (int)gameType, remainingGuard, remainingHp, eventCardNames, gameCount };
            var resultText = jvm.CallStaticMethod<java.lang.String>(facadeClass, "playLilithGame", sigPlayLilithGame, args);
            var result = JsonConvert.DeserializeObject<LilithGameResult>(resultText);
            return result;
        }

        public static int Main(string[] args)
        {
            try
            {
                var facade = new GameLauncherFacade();
                facade.Logging += (sender, e) => Console.WriteLine("[{0}] {1}", e.Level, e.Message);
                facade.Initialize(true);
                var result = facade.PlayArenaGame(
                    deck1: "凤凰",
                    deck2: "凤凰",
                    heroLv1: 50,
                    heroLv2: 50,
                    p1CardAtBuff: 100,      // 玩家1的卡牌攻击BUFF，百分比数
                    p1CardHpBuff: 100,      // 玩家1的卡牌体力BUFF，百分比数
                    p1HeroHpBuff: 100,      // 玩家1的英雄体力BUFF，百分比数
                    p2CardAtBuff: 100,      // 玩家2的卡牌攻击BUFF，百分比数
                    p2CardHpBuff: 100,      // 玩家2的卡牌体力BUFF，百分比数
                    p2HeroHpBuff: 100,      // 玩家2的英雄体力BUFF，百分比数
                    firstAttack: FirstAttack.Player1,
                    deckOrder: DeckOrder.Random,
                    vc1Text: "Any",         // 玩家1的特殊胜利条件设置，参见http://localhost:8080/mkhx/#help
                    gameCount: 10
                );
                Console.WriteLine(result.ValidationResult);
                Console.WriteLine(result.Player1Win);
                Console.WriteLine(result.Player2Win);
                Console.WriteLine("================");
                var bossGameResult = facade.PlayBossGame(
                    playerDeck: "凤凰*10",
                    bossName: "网页版复仇女神",    // 魔神名字参见http://localhost:8080/mkhx/#boss-battle，包括各个版本的不同魔神
                    heroLv: 50,
                    kingdomBuff: 10,        // 玩家王国军团种族加成等级
                    forestBuff: 10,         // 玩家森林军团种族加成等级
                    savageBuff: 10,         // 玩家蛮荒军团种族加成等级
                    hellBuff: 10,           // 玩家地狱军团种族加成等级
                    guardType: BossGuardType.NormalGuard,
                    gameCount: 20
                );
                Console.WriteLine(bossGameResult.ValidationResult);
                Console.WriteLine(bossGameResult.AvgDamage);
                Console.WriteLine(bossGameResult.CvDamage);
                Console.WriteLine("================");
                var mapGameResult = facade.playMapGame(
                    playerDeck: "精灵法师*10",
                    mapName: "5-5-3",
                    heroLv: 50,
                    gameCount: 100);
                Console.WriteLine(mapGameResult.ValidationResult);
                Console.WriteLine(mapGameResult.WinCount);
                Console.WriteLine(mapGameResult.AdvWinCount);
                Console.WriteLine("================");
                var lilithGameResult = facade.playLilithGame(
                    playerDeck: "凤凰*10",
                    lilithName: "困难莉莉丝+陷阱3",       // 莉莉丝识别名，使用【<难度>莉莉丝+<第四技能>】的形式
                    heroLv: 50,
                    gameType: LilithGameType.RushBoss,
                    remainingGuard: 5,                  // 清兵模式下指定清到还剩几个敌方卡牌为止（包括莉莉丝），设置为0表示清兵+尾刀
                    remainingHp: 5000,                  // 尾刀模式下指定莉莉丝剩余体力
                    eventCardNames: "凤凰,金属巨龙",      // 以半角逗号分隔的活动卡牌名称列表
                    gameCount: 100);
                Console.WriteLine(lilithGameResult.ValidationResult);
                Console.WriteLine(lilithGameResult.AvgBattleCount);
                Console.WriteLine(lilithGameResult.AvgDamageToLilith);
                Console.WriteLine("================");
                return 0;
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e.ToString() + e.StackTrace);
                return 1;
            }
        }
    }
}
