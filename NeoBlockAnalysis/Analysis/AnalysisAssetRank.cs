using NEL.Simple.SDK.Helper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;

namespace NeoBlockAnalysis
{
    class AnalysisAssetRank : IAnalysis
    {
        public void StartTask()
        {
            Task task_HandlerUtxo = new Task(() => {
                HandlerUtxo();
            });
            Task task_HandlerNep5 = new Task(() => {
                HandlerNep5();
            });
            task_HandlerUtxo.Start();
            task_HandlerNep5.Start();
        }


        private void HandlerNep5()
        {
            //获取nep5资产拍卖分析到了什么高度
            var query = MongoDBHelper.Get(Program.mongodbConnStr, Program.mongodbDatabase, "system_counter", 0, 1, "{\"counter\":\"nep5Balance\"}", "{}");
            int handlerHeight = -1;
            if (query.Count > 0)
                handlerHeight = (int)query[0]["lastBlockindex"];

            Dictionary<string, AddressAssetBalance> dic = new Dictionary<string, AddressAssetBalance>();
            while (true)
            {
                Thread.Sleep(Program.sleepTime);
                //handlerHeight不能超过block数据库中的nep5的高度
                var block_utxo_height = (int)MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "system_counter", "{counter:\"NEP5\"}")[0]["lastBlockindex"];
                if (handlerHeight >= block_utxo_height)
                    continue;
                handlerHeight++;
                query = MongoDBHelper.Get(Program.neo_mongodbConnStr,Program.neo_mongodbDatabase, "Nep5State","{LastUpdatedBlock:"+handlerHeight+"}");
                for (var i = 0; i < query.Count; i++)
                {
                    var address = (string)query[i]["Address"];
                    var assetid = (string)query[i]["AssetHash"];

                    //获取这个资产的精度
                    var assetInfo = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "NEP5asset", "{assetid:\"" + assetid + "\"}");
                    if (assetInfo.Count == 0)
                    {
                        Console.WriteLine("没有获取到"+ assetid+"的资产信息");
                        handlerHeight--;
                        goto c;
                    }
                    int decimals = (int)assetInfo[0]["decimals"];
                    //除以精度
                    var balance = BigInteger.Parse((string)query[i]["Balance"]) / BigInteger.Parse(Math.Pow(10,decimals).ToString());

                    var addressAssetBalance = new AddressAssetBalance() { Address = address,AssetHash = assetid,Balance = int.Parse(balance.ToString()),LastUpdatedBlock = handlerHeight};
                    var addressAssetBalacnes = MongoDBHelper.Get<AddressAssetBalance>(Program.mongodbConnStr, Program.mongodbDatabase, "address_assetid_balance", "{Address:\"" + address + "\",AssetHash:\"" + assetid + "\"}");
                    if (addressAssetBalacnes.Count == 1)
                    {
                        if (addressAssetBalacnes[0].LastUpdatedBlock == handlerHeight)
                            continue;
                        MongoDBHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "address_assetid_balance", "{Address:\"" + address + "\",AssetHash:\"" + assetid + "\"}", MongoDB.Bson.BsonDocument.Parse(addressAssetBalance.ToJson().ToString()));
                    }
                    else if (addressAssetBalacnes.Count == 0)
                        MongoDBHelper.InsertOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_assetid_balance",addressAssetBalance);
                    else
                        throw new Exception("完蛋");
                }
                MongoDBHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "system_counter", "{\"counter\":\"nep5Balance\"}", MongoDB.Bson.BsonDocument.Parse("{counter:\"nep5Balance\",lastBlockindex:" + handlerHeight + "}"));
                c:;
            }
        }

        private void HandlerUtxo()
        {
            //获取utxo资产拍卖分析到了什么高度
            var query = MongoDBHelper.Get(Program.mongodbConnStr, Program.mongodbDatabase, "system_counter", 0, 1, "{\"counter\":\"utxoBalance\"}", "{}");
            int handlerHeight = -1;
            if (query.Count > 0)
                handlerHeight = (int)query[0]["lastBlockindex"];

            Dictionary<string, AddressAssetBalance> dic = new Dictionary<string, AddressAssetBalance>();
            while (true)
            {
                Thread.Sleep(Program.sleepTime);
                //handlerHeight不能超过block数据库中的utxo的高度
                var block_utxo_height = (int)MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "system_counter","{counter:\"utxo\"}")[0]["lastBlockindex"];
                if (handlerHeight >= block_utxo_height)
                    continue;
                handlerHeight++;
                //获取下一个高度的所有utxo进行分析   
                //先获取所有的创建
                dic.Clear();
                query = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "utxo", "{createHeight:" + handlerHeight + "}");
                //所有的创建都是++
                for (var i = 0; i < query.Count; i++)
                {
                    string address = (string)query[i]["addr"];
                    string assetid = (string)query[i]["asset"];
                    int value = int.Parse((string)query[i]["value"]);
                    string key = address + assetid;
                    if (dic.ContainsKey(key))
                    {
                        dic[key].Balance += value;
                    }
                    else
                    {
                        dic[key] = new AddressAssetBalance() { Address = address,AssetHash = assetid,Balance = value, LastUpdatedBlock = handlerHeight };
                    }
                }
                //所有的使用都是--
                query = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "utxo", "{useHeight:" + handlerHeight + "}");
                for (var i = 0; i < query.Count; i++)
                {
                    string address = (string)query[i]["addr"];
                    string assetid = (string)query[i]["asset"];
                    int value = int.Parse((string)query[i]["value"]);
                    string key = address + assetid;
                    if (dic.ContainsKey(key))
                    {
                        dic[key].Balance -= value;
                    }
                    else
                    {
                        dic[key] = new AddressAssetBalance() { Address = address, AssetHash = assetid, Balance = -value , LastUpdatedBlock = handlerHeight};
                    }
                }
                //上面计算好了  某个高度  某些地址资产的变动  现在开始并入
                foreach (var key in dic.Keys)
                {
                    var address = dic[key].Address;
                    var assetid = dic[key].AssetHash;
                    var addressAssetBalacnes = MongoDBHelper.Get<AddressAssetBalance>(Program.mongodbConnStr,Program.mongodbDatabase, "address_assetid_balance","{Address:\""+ address + "\",AssetHash:\""+ assetid + "\"}");
                    if (addressAssetBalacnes.Count == 1)
                    {
                        if (addressAssetBalacnes[0].LastUpdatedBlock == handlerHeight)
                            continue;

                        dic[key].Balance = addressAssetBalacnes[0].Balance + dic[key].Balance;
                        MongoDBHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "address_assetid_balance", "{Address:\"" + address + "\",AssetHash:\"" + assetid + "\"}", MongoDB.Bson.BsonDocument.Parse(dic[key].ToJson().ToString()));
                    }
                    else if (addressAssetBalacnes.Count == 0)
                        MongoDBHelper.InsertOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_assetid_balance", dic[key]);
                    else
                        throw new Exception("完蛋");

                }

                //更新system_counter
                MongoDBHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "system_counter", "{\"counter\":\"utxoBalance\"}",MongoDB.Bson.BsonDocument.Parse("{counter:\"utxoBalance\",lastBlockindex:"+ handlerHeight + "}"));
            }
        }
    }



    public class AddressAssetBalance
    {
        public MongoDB.Bson.ObjectId _id;
        public string Address;
        public string AssetHash;
        public int Balance;
        public int LastUpdatedBlock;

        public JObject ToJson()
        {
            var jo = new JObject();
            jo.Add("Address", Address);
            jo.Add("AssetHash", AssetHash);
            jo.Add("Balance", Balance);
            jo.Add("LastUpdatedBlock", LastUpdatedBlock);
            return jo;
        }
    }
}
