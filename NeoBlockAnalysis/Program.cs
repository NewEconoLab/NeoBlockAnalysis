using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace NeoBlockAnalysis
{
    class Program
    {
        public static string mongodbConnStr = string.Empty;
        public static string mongodbDatabase = string.Empty;
        public static string neo_mongodbConnStr = string.Empty;
        public static string neo_mongodbDatabase = string.Empty;
        public static string NeoCliJsonRPCUrl = string.Empty;
        public static int sleepTime = 0;
        public static int serverType = 0;
        public static int handlerminblockindex = -1;
        public static int handlermaxblockindex = -1;
        public static int isDoTx = 1; 
        public static int isDoNep5 = 1; 
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
                    isDoTx = int.Parse(config["isDoTx"]);
                    isDoNep5 = int.Parse(config["isDoNep5"]);
                    handlerminblockindex = config["min_blockindex"] == null ? -1 : int.Parse(config["min_blockindex"]);
                    handlermaxblockindex = config["max_blockindex"] == null ? -1 : int.Parse(config["max_blockindex"]);
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
                    isDoTx = int.Parse(config["isDoTx"]);
                    isDoNep5 = int.Parse(config["isDoNep5"]);
                    handlerminblockindex = config["min_blockindex"] == null ? -1 : int.Parse(config["min_blockindex"]);
                    handlermaxblockindex = config["max_blockindex"]==null?-1: int.Parse(config["max_blockindex"]);
                    serverType = 2;
                    break;
                }
            }
            if(isDoNep5 ==1 )
                new AnalysisNep5Transfer().StartTask();
            if( isDoTx ==1 )
                new AnalysisTx().StartTask();
            //int blockindex = 2000000;
            //while (true)
            //{
            //    new AnalysisNep5Transfer().Start(blockindex);
            //    new AnalysisTx().Start(blockindex);
            //    blockindex++;
            //    //Thread.Sleep(1000);
            //}
            while (true)
            {
                Thread.Sleep(100);
            }
        }

        static void ShowMenu()
        {
            Console.WriteLine("输入1开始测试网；输入2开始主网");
        }
    }
}
