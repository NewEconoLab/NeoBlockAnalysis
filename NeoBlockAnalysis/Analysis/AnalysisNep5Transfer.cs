using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using NEL.Simple.SDK.Helper;
using Newtonsoft.Json.Linq;

namespace NeoBlockAnalysis
{
    class AnalysisNep5Transfer:IAnalysis
    {
        public void StartTask()
        {
            Task task_StorageNep5Transfer = new Task(() => {
                try
                {
                    //先获取Nep5Transfer已经获取到的高度
                    int blockindex = 0;
                    var query = MongoDBHelper.Get(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx",0,1, "{\"isNep5\":true}","{\"blockindex\":-1}");
                    if (query.Count > 0)
                        blockindex = (int)query[0]["blockindex"];

                    //移除这个高度的所有交易重新入库
                    MongoDBHelper.DeleteData(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", "{\"isNep5\":true,\"blockindex\":" + blockindex + "}");

                    while (true)
                    {
                        int cli_blockindex = (int)MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "system_counter", 0, 1, "{counter:\"NEP5\"}")[0]["lastBlockindex"];
                        if (blockindex <= cli_blockindex)
                        {
                            StorageNep5Transfer(blockindex);
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
            task_StorageNep5Transfer.Start();
        }


        void StorageNep5Transfer(Int64 blockindex)
        {
            var findFliter = "{blockindex:" + blockindex + "}";
            JArray result=  MongoDBHelper.Get(Program.neo_mongodbConnStr,Program.neo_mongodbDatabase, "NEP5transfer", findFliter);
            for (var i = 0; i < result.Count; i++)
            {
                //对nep5数据进行分析处理
                HandlerNep5Transfer(result[i] as JObject);
            }
        }

        void HandlerNep5Transfer(JObject jo)
        {
            string asset = jo["asset"].ToString(); //资产id
            string from = jo["from"].ToString();
            string to = jo["to"].ToString();
            string  str_value = jo["value"].ToString();

            decimal value = toolHelper.DecimalParse(str_value);
            int blockindex = int.Parse(jo["blockindex"].ToString());


            //往mongo里address_tx存入相关的地址交易信息
            //存from地址
            List<Utxo> list_vin_utxo = new List<Utxo>();
            Utxo vin = new Utxo();
            vin.n = 0;
            vin.asset = asset;
            vin.value = value;
            vin.address = from;
            list_vin_utxo.Add(vin);

            List<Utxo> list_vout_utxo = new List<Utxo>();
            Utxo vout = new Utxo();
            vout.n = 0;
            vout.asset = asset;
            vout.value = value;
            vout.address = to;
            list_vout_utxo.Add(vout);

            var txid = jo["txid"].ToString();
            //获取区块所在时间
            string blocktime = (string)MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block", "{index:" + blockindex + "}")[0]["time"];

            //获取交易详情
            var txinfo = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx", "{\"txid\":\"" + txid + "\"}");
            var txType = "InvocationTransaction";
            var sysfee = "0";
            var netfee = "0";
            if (txinfo.Count > 0)
            {
                txType = (string)txinfo[0]["type"];
                sysfee = (string)txinfo[0]["sys_fee"];
                netfee = (string)txinfo[0]["net_fee"];
            }

            //获取资产详情
            Detail detail = new Detail();
            var assetInfo = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "NEP5asset", "{assetid:\"" + asset + "\"}");
            //from 构造
            detail.assetId = asset;
            detail.assetName = (string)assetInfo[0]["name"];
            detail.assetDecimals = (string)assetInfo[0]["decimals"];
            detail.assetSymbol = (string)assetInfo[0]["symbol"];
            detail.value = BsonDecimal128.Create((0-value).ToString());
            detail.isSender = false;
            detail.from = new string[] {from };
            detail.to = new string[] { to };

            Address_Tx addressTx = new Address_Tx();
            addressTx.detail = detail;
            addressTx.addr = from;
            addressTx.txid = txid;
            addressTx.netfee = netfee;
            addressTx.sysfee = sysfee;
            addressTx.vin = list_vin_utxo.ToArray();
            addressTx.vout = list_vout_utxo.ToArray();
            addressTx.blockindex = blockindex;
            addressTx.blocktime = blocktime;
            addressTx.isNep5 = true;
            addressTx.txType = txType;

            MongoDBHelper.InsertOne<Address_Tx>(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", addressTx);
            detail.value = BsonDecimal128.Create((0 + value).ToString());
            detail.isSender = false;
            addressTx.addr = to;
            addressTx.detail = detail;
            MongoDBHelper.InsertOne<Address_Tx>(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", addressTx);

        }
    }
}
