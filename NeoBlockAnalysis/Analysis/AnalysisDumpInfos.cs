using NEL.Simple.SDK.Helper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using System.Numerics;
using System.Linq;
using Newtonsoft.Json.Linq;
using ThinNeo.Cryptography;

namespace NeoBlockAnalysis
{
    class AnalysisDumpInfos : IAnalysis
    {
        public void StartTask()
        {
            Task task = new Task(()=> 
            {
                try
                {
                    //获取已经处理到了哪个高度
                    var query = MongoDBHelper.Get(Program.mongodbConnStr, Program.mongodbDatabase, "system_counter", 0, 1, "{\"counter\":\"dumpInfos\"}", "{}");
                    int handlerHeight = -1;
                    if (query.Count > 0)
                        handlerHeight = (int)query[0]["lastBlockindex"];
                    while (true)
                    {
                        Thread.Sleep(Program.sleepTime);
                        //处理的高度必须小于节点同步dumpinfo到的高度
                        query = MongoDBHelper.Get(Program.neo_mongodbConnStr,Program.neo_mongodbDatabase,"dumpinfos",0,1,"{}","{\"blockIndex\":-1}");
                        int height = (int)query[0]["blockIndex"];
                        if (handlerHeight >= height)
                            continue;
                        HandlerDumpInfos(handlerHeight);
                        handlerHeight++;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            });
            task.Start();
        }

        void HandlerDumpInfos(int handlerHeight)
        {
            var query = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "dumpinfos", "{\"blockIndex\":\""+ handlerHeight + "\"}", "{}");
            for (var i = 0; i < query.Count; i++)
            {
                var txid = (string)query[i]["txid"];
                var str = (string)query[i]["dimpInfo"];
                var bts = ThinNeo.Helper.HexString2Bytes(str);
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream(bts))
                {
                    var outms = llvm.QuickFile.FromFile(ms);
                    var text = System.Text.Encoding.UTF8.GetString(outms.ToArray());
                    var json = JObject.Parse(text);
                    if (!json.ContainsKey("VMState")|| !json.ContainsKey("script") || !((string)json["VMState"]).Contains("HALT"))
                        continue;
                    foreach (var op in json["script"]["ops"].ToList())
                    {
                        if (op["op"].ToString() == "APPCALL" || op["op"].ToString() == "TAILCALL")
                        {
                            //取到的值是小端序的，要转换成大端序
                            var contractHash =new ThinNeo.Hash160(ThinNeo.Helper.HexString2Bytes(op["param"].ToString()));
                            //查询这个交易的发起人,用tx中的scripts中的verification来获得
                            var txinfo = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx",0,1, "{\"txid\":\""+ txid + "\"}", "{}")[0];
                            var scripts = txinfo["scripts"] as JArray;
                            var sys_fee = (string)txinfo["sys_fee"];
                            var net_fee = (string)txinfo["net_fee"];
                            var vout = txinfo["vout"] as JArray;
                            var blockindex = (uint)txinfo["blockindex"];
                            decimal neoAmount = 0;
                            decimal gasAmount = 0;
                            //根据高度获取时间戳
                            var blockInfo = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block", 0, 1, "{\"index\":" + blockindex + "}", "{}")[0];
                            var time = (string)blockInfo["time"];
                            foreach (var v in vout)
                            {
                                if (v["address"].ToString() == ThinNeo.Helper_NEO.GetAddress_FromScriptHash(contractHash))
                                {
                                    if (v["asset"].ToString() == "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7")
                                        gasAmount += decimal.Parse(v["value"].ToString(), System.Globalization.NumberStyles.Float);
                                    else if (v["asset"].ToString() == "0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b")
                                        neoAmount += decimal.Parse(v["value"].ToString(), System.Globalization.NumberStyles.Float);
                                }
                            }
                            foreach (var script in scripts)
                            {
                                var verification = script["verification"].ToString();
                                var address = Verification2Address(verification);
                                ContractCallInfo c = new ContractCallInfo() {
                                    txid = txid,
                                    address = address,
                                    neoAmount = BsonDecimal128.Create(neoAmount),
                                    gasAmount = BsonDecimal128.Create(gasAmount),
                                    contractHash = contractHash.ToString(),
                                    time = time,
                                    sys_fee = BsonDecimal128.Create(sys_fee),
                                    net_fee = BsonDecimal128.Create(net_fee),
                                    blockIndex = blockindex
                                };
                                //存进数据库
                                MongoDBHelper.InsertOne(Program.mongodbConnStr, Program.mongodbDatabase, "contract_call_info", c);
                            }
                            break;
                        }
                    }
                }
            }

            MongoDBHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "system_counter", "{\"counter\":\"dumpInfos\"}", MongoDB.Bson.BsonDocument.Parse("{counter:\"dumpInfos\",lastBlockindex:" + handlerHeight + "}"));
        }

        string Verification2Address(string verification)
        {
            var bts = ThinNeo.Helper.HexString2Bytes(verification);
            if (bts.Length == 22)//这种情况一般是正常的地址
            {
                var pubKey = bts.Skip(1).Take(20).ToArray();
                return ThinNeo.Helper_NEO.GetAddress_FromPublicKey(pubKey);
            }
            else//鉴权构造
            {
                RIPEMD160Managed ripemd160 = new RIPEMD160Managed();
                var scripthash = ThinNeo.Helper.Sha256.ComputeHash(ThinNeo.Helper.HexString2Bytes(verification));
                scripthash = ripemd160.ComputeHash(scripthash);
                return ThinNeo.Helper_NEO.GetAddress_FromScriptHash(scripthash);
            }
        }
    }

    class ContractCallInfo
    {
        public string txid;
        public string address;
        public BsonDecimal128 neoAmount;
        public BsonDecimal128 gasAmount;
        public string contractHash;
        public string time;
        public BsonDecimal128 sys_fee;
        public BsonDecimal128 net_fee;
        public uint blockIndex ;
    }
}
