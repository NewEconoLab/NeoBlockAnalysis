using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using MongoDB.Bson;
using NEL.Simple.SDK.Helper;
using Newtonsoft.Json.Linq;

namespace NeoBlockAnalysis
{
    class AnalysisTx : IAnalysis
    {
        public void StartTask()
        {
            Task task_StoragTx = new Task(() => {
                try
                {
                    int blockindex = 0;
                    //先获取tx已经获取到的高度
                    JArray query = MongoDBHelper.Get(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", 0, 1, "{\"isNep5\":false}", "{\"blockindex\":-1}");
                    if (query.Count > 0)
                        blockindex = (int)query[0]["blockindex"];
                    //删除这个高度tx的所有数据
                    MongoDBHelper.DeleteData(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", "{\"isNep5\":false,\"blockindex\":" + blockindex + "}");
                    while (true)
                    {
                        var cli_TxIndex = (UInt32)MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "system_counter", 0, 1, "{counter:\"tx\"}")[0]["lastBlockindex"];
                        if (blockindex <= cli_TxIndex)
                        {
                            StorageTx(blockindex);
                            blockindex++;
                        }
                        Thread.Sleep(Program.sleepTime);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
            task_StoragTx.Start();
        }

        public void Start(int blockindex)
        {
            StorageTx(blockindex);
        }

        void StorageTx(Int64 blockindex)
        {
            var findFliter = "{useHeight:" + blockindex + "}";
            JArray result = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "utxo", findFliter);
            Dictionary<string, JObject> dic = new Dictionary<string, JObject>();
            for (var i = 0; i < result.Count; i++)
            {
                var key = result[i]["addr"].ToString() + result[i]["used"].ToString() + result[i]["useHeight"].ToString();
                if (dic.ContainsKey(key))
                    continue;
                dic.Add(key,new JObject() { { "addr", result[i]["addr"].ToString() }, { "blockindex", result[i]["useHeight"].ToString() }, { "txid", result[i]["used"].ToString() } });
            }

            findFliter = "{createHeight:" + blockindex + "}";
            result = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "utxo", findFliter);
            for (var i = 0; i < result.Count; i++)
            {
                var key = result[i]["addr"].ToString() + result[i]["txid"].ToString() + result[i]["createHeight"].ToString();
                if (dic.ContainsKey(key))
                    continue;
                dic.Add(key, new JObject() { { "addr", result[i]["addr"].ToString() }, { "blockindex", result[i]["createHeight"].ToString() }, { "txid", result[i]["txid"].ToString() } });
            }


            foreach (var k in dic.Keys)
            {
                HandlerAddressTx(dic[k] as JObject);
            }
        }

        void HandlerAddressTx(JObject jo)
        {
            Address_Tx address_tx = new Address_Tx();
            address_tx.addr = jo["addr"].ToString();
            address_tx.blockindex = int.Parse(jo["blockindex"].ToString());

            string txid = jo["txid"].ToString();
            address_tx.txid = txid;

            var findFliter = "{txid:'" + txid + "'}";
            JArray result = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx", findFliter);
            if (result.Count>0)
            {
                //一个txid获取到的信息应该只会存在一条
                JObject jo_raw = result[0] as JObject;

                JArray scripts = jo_raw["scripts"] as JArray;


                //获取区块所在时间
                string blocktime = (string)MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block", "{index:" + address_tx.blockindex + "}")[0]["time"];
                address_tx.blocktime = blocktime;

                address_tx.txType = jo_raw["type"].ToString();
                address_tx.sysfee = jo_raw["sys_fee"].ToString();
                address_tx.netfee = jo_raw["net_fee"].ToString();
                //定义一个输入包含utxo
                List<Utxo> list_vin_utxo = new List<Utxo>();
                //定义一个输出入包含utxo
                List<Utxo> list_vout_utxo = new List<Utxo>();
                //获取这个tx的vin并根据vin中的txid来获取输入的utxo的信息
                JArray JAvin = jo_raw["vin"] as JArray;


                //过滤一些疑似交易所的复杂构造
                if (scripts.Count > 1 && JAvin.Count > 100)
                    return;

                Dictionary<string, Detail> dic_detail = new Dictionary<string, Detail>();
                List<string> list_fromAddress = new List<string>();
                List<string> list_toAddress = new List<string>();

                for (var i =0;i<JAvin.Count;i++)
                {
                    JObject jo_vin = JAvin[i] as JObject;
                    findFliter = "{txid:'" + jo_vin["txid"].ToString() + "'}";
                    result = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx", findFliter);

                    if (result.Count>0)
                    {
                        int n = int.Parse(jo_vin["vout"].ToString());
                        JObject joresult_vin = result[0] as JObject;
                        Utxo vin = new Utxo();
                        vin.n = (uint)joresult_vin["vout"][n]["n"];
                        vin.asset = (string)joresult_vin["vout"][n]["asset"];
                        vin.value = toolHelper.DecimalParse((string)joresult_vin["vout"][n]["value"]);
                        vin.address = (string)joresult_vin["vout"][n]["address"];
                        list_vin_utxo.Add(vin);
                        if (vin.address == address_tx.addr)
                        {
                            string value;
                            if (!dic_detail.ContainsKey(vin.asset))
                            {
                                //从asset表中获取这个资产的详情
                                result = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "asset", "{\"id\":\"" + vin.asset + "\"}");
                                if (result.Count == 0)
                                    continue;
                                JObject assetInfo = result[0] as JObject;
                                Detail detail = new Detail();
                                detail.assetId = vin.asset;
                                detail.assetType = "UTXO";
                                detail.assetName = (string)assetInfo["name"][0]["name"];
                                detail.assetSymbol = detail.assetName;
                                detail.assetDecimals = "";
                                detail.isSender = true;
                                detail.value = BsonDecimal128.Create("0");
                                dic_detail[vin.asset] = detail;
                                value = "0";
                            }
                            else
                            {
                                value = dic_detail[vin.asset].value.ToString();
                            }
                            dic_detail[vin.asset].value = BsonDecimal128.Create((toolHelper.DecimalParse(value) - toolHelper.DecimalParse(vin.value.ToString())).ToString());
                        }
                        else
                        {

                        }
                        if (!list_fromAddress.Contains(vin.address))
                            list_fromAddress.Add(vin.address);
                    }
                }

                JArray JAvout_utxo = jo_raw["vout"] as JArray;
                for (var i = 0; i < JAvout_utxo.Count; i++)
                {
                    JObject jo_vout = JAvout_utxo[i] as JObject;
                    Utxo vout = new Utxo();
                    vout.n = (uint)jo_vout["n"];
                    vout.asset = (string)jo_vout["asset"];
                    vout.value = toolHelper.DecimalParse((string)jo_vout["value"]);
                    vout.address = (string)jo_vout["address"];
                    list_vout_utxo.Add(vout);
                    if (jo_vout["address"].ToString() == address_tx.addr)
                    {
                        string value;
                        if (!dic_detail.ContainsKey(jo_vout["asset"].ToString()))
                        {
                            //从asset表中获取这个资产的详情
                            result = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "asset", "{\"id\":\"" + jo_vout["asset"].ToString() + "\"}");
                            if (result.Count == 0)
                                continue;
                            JObject assetInfo = result[0] as JObject;
                            Detail detail = new Detail();
                            detail.assetId = vout.asset;
                            detail.assetType = "UTXO";
                            detail.assetName = (string)assetInfo["name"][0]["name"];
                            detail.assetSymbol = detail.assetName;
                            detail.assetDecimals = "";
                            detail.isSender = true;
                            detail.value = BsonDecimal128.Create("0");
                            dic_detail[(string)jo_vout["asset"]] = detail;
                            value = "0";
                        }
                        else
                        {
                            value = dic_detail[(string)jo_vout["asset"]].value.ToString();
                        }
                        var r = toolHelper.DecimalParse(value) + vout.value;
                        if (r > 0)
                            dic_detail[(string)jo_vout["asset"]].isSender = false;
                        else
                            dic_detail[(string)jo_vout["asset"]].isSender = false;
                        dic_detail[(string)jo_vout["asset"]].value = BsonDecimal128.Create(r.ToString());

                    }
                    else
                    {
                        if (!list_toAddress.Contains((string)jo_vout["address"]))
                            list_toAddress.Add((string)jo_vout["address"]);
                    }
                }
                //如果这个地址在这个交易里面没有资产变化就不入库
                if (dic_detail.Count == 0)
                    return;
                address_tx.vin = list_vin_utxo.ToArray();
                address_tx.vout = list_vout_utxo.ToArray();
                address_tx.isNep5 = false;
                List<string> keys = new List<string>(dic_detail.Keys);
                for (var i = 0; i < keys.Count; i++)
                {
                    dic_detail[keys[i]].from = list_fromAddress.ToArray();
                    dic_detail[keys[i]].to = list_toAddress.ToArray();
                    address_tx.detail = dic_detail[keys[i]];
                    MongoDBHelper.InsertOne<Address_Tx>(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", address_tx);
                }
            }
        }
    }
}
