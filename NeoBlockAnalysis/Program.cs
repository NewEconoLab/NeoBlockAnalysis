using Microsoft.Extensions.Configuration;
using NEL.Simple.SDK.Helper;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;

namespace NeoBlockAnalysis
{
    class Program
    {
        public static string mongodbConnStr = string.Empty;
        public static string mongodbDatabase = string.Empty;
        public static string neo_mongodbConnStr = string.Empty;
        public static string neo_mongodbDatabase = string.Empty;
        public static string NeoCliJsonRPCUrl = string.Empty;
        public static string[] MongoDbIndex = new string[] { };
        public static int sleepTime = 0;
        public static int serverType = 0;
        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection()    //将配置文件的数据加载到内存中
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())   //指定配置文件所在的目录
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)  //指定加载的配置文件
                .Build();    //编译成对象  

            while (true)
            {
                ShowMenu();
                string input = Console.ReadLine();
                if (input == "1")
                {
                    mongodbConnStr = config["mongodbConnStr_testnet"];
                    mongodbDatabase = config["mongodbDatabase_testnet"];
                    neo_mongodbConnStr = config["neo_mongodbConnStr_testnet"];
                    neo_mongodbDatabase = config["neo_mongodbDatabase_testnet"];
                    NeoCliJsonRPCUrl = config["NeoCliJsonRPCUrl_testnet"];
                    sleepTime = int.Parse(config["sleepTime"]);
                    MongoDbIndex = config.GetSection("MongoDbIndexs").GetChildren().Select(p => p.Value).ToArray();
                    serverType = 1;
                    break;
                }
                else if (input == "2")
                {
                    mongodbConnStr = config["mongodbConnStr_mainnet"];
                    mongodbDatabase = config["mongodbDatabase_mainnet"];
                    neo_mongodbConnStr = config["neo_mongodbConnStr_mainnet"];
                    neo_mongodbDatabase = config["neo_mongodbDatabase_mainnet"];
                    NeoCliJsonRPCUrl = config["NeoCliJsonRPCUrl_mainnet"];
                    sleepTime = int.Parse(config["sleepTime"]);
                    MongoDbIndex = config.GetSection("MongoDbIndexs").GetChildren().Select(p => p.Value).ToArray();
                    serverType = 2;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(mongodbConnStr) && !string.IsNullOrEmpty(mongodbDatabase))
            {
                //创建索引
                for (var i = 0; i < MongoDbIndex.Length; i++)
                {
                    SetMongoDbIndex(MongoDbIndex[i]);
                }
            }
            new AnalysisDumpInfos().StartTask();
            new AnalysisAssetRank().StartTask();
            new AnalysisTx().StartTask();
            new AnalysisNep5Transfer().StartTask();

            while (true)
            {
                Thread.Sleep(100);
            }
        }

        public static void SetMongoDbIndex(string mongoDbIndex)
        {
            JObject joIndex = JObject.Parse(mongoDbIndex);
            string collName = (string)joIndex["collName"];
            JArray indexs = (JArray)joIndex["indexs"];
            for (var i = 0; i < indexs.Count; i++)
            {
                string indexName = (string)indexs[i]["indexName"];
                string indexDefinition = indexs[i]["indexDefinition"].ToString();
                bool isUnique = false;
                if (indexs[i]["isUnique"] != null)
                    isUnique = (bool)indexs[i]["isUnique"];
                MongoDBHelper.CreateIndex(mongodbConnStr, mongodbDatabase, collName, indexDefinition, indexName, isUnique);
            }
        }

        static void ShowMenu()
        {
            Console.WriteLine("输入1开始测试网；输入2开始主网");
        }

        static void ShowMenu2()
        {
            Console.WriteLine("输入1开始执行分析工程；输入2执行入库工程;输入3全部执行;输入4获取某个高度的neo快照");
        }
    }
}
