using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading;
using MongoDB.Bson;
using System.Globalization;

namespace NeoBlockAnalysis
{
    class AnalysisTx : IAnalysis
    {
        public void StartTask()
        {
            Task task_StoragTx = new Task(() => {
                //先获取tx已经获取到的高度
                int blockindex = 0;
                blockindex = mongoHelper.GetMaxIndex(Program.mongodbConnStr,Program.mongodbDatabase,"address_tx", "blockindex","{\"isNep5\":false}");
                if (Program.handlerminblockindex != -1)
                { blockindex = Program.handlerminblockindex; }
                //删除这个高度tx的所有数据
                var count1 = mongoHelper.GetDataCount(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx");
                Console.WriteLine("删之前的数据个数："+ count1);
                mongoHelper.DelData(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", "{\"isNep5\":false,\"blockindex\":"+blockindex+"}");
                var count2 = mongoHelper.GetDataCount(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx");
                Console.WriteLine("删之后的数据个数：" + count2);
                while (true)
                {
                    var cli_blockindex = mongoHelper.Getblockheight(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block");
                    if (blockindex < cli_blockindex)//处理的数据要比库中的高度少一 
                    {
                        StorageTx(blockindex);
                        blockindex++;
                    }
                    Thread.Sleep(Program.sleepTime);
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
            Console.WriteLine("address_tx:" + blockindex);

            var findFliter = "{blockindex:" + blockindex + "}";
            MyJson.JsonNode_Array result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "address_tx", findFliter);
            isFirstHandlerBlockindex = true;
            for (var i = 0; i < result.Count; i++)
            {
                Console.WriteLine("blockindex:"+ blockindex+"~~~~~~~~~~~总数:"+result.Count+"!!!!!!!i:"+i);
                HandlerAddressTx(result[i] as MyJson.JsonNode_Object);
            }
        }

        void HandlerAddressTx(MyJson.JsonNode_Object jo)
        {
            Address_Tx address_tx = new Address_Tx();
            address_tx.addr = jo["addr"].ToString();
            address_tx.blockindex = int.Parse(jo["blockindex"].ToString());

            string txid = jo["txid"].ToString();
            address_tx.txid = txid;

            var findFliter = "{txid:'" + txid + "'}";
            MyJson.JsonNode_Array result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx", findFliter);
            if (result.Count>0)
            {
                //一个txid获取到的信息应该只会存在一条
                MyJson.JsonNode_Object jo_raw = result[0] as MyJson.JsonNode_Object;

                var scripts = jo_raw["scripts"] as MyJson.JsonNode_Array;


                //获取区块所在时间
                var blocktime = ((MyJson.JsonNode_Object)mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block", "{index:" + address_tx.blockindex + "}")[0])["time"].ToString();
                address_tx.blocktime = blocktime;

                address_tx.txType = jo_raw["type"].ToString();
                address_tx.sysfee = jo_raw["sys_fee"].ToString();
                address_tx.netfee = jo_raw["net_fee"].ToString();
                //定义一个输入包含utxo
                List<Utxo> list_vin_utxo = new List<Utxo>();
                //定义一个输出入包含utxo
                List<Utxo> list_vout_utxo = new List<Utxo>();
                //获取这个tx的vin并根据vin中的txid来获取输入的utxo的信息
                MyJson.JsonNode_Array JAvin = jo_raw["vin"] as MyJson.JsonNode_Array;


                //过滤一些疑似交易所的复杂构造
                if (scripts.Count > 1 && JAvin.Count > 200)
                    return;

                Dictionary<string, Detail> dic_detail = new Dictionary<string, Detail>();
                List<string> list_fromAddress = new List<string>();
                List<string> list_toAddress = new List<string>();

                for (var i =0;i<JAvin.Count;i++)
                {
                    Console.WriteLine("blockinde:"+ address_tx.blockindex + "   vin:"+i);
                    MyJson.JsonNode_Object jo_vin = JAvin[i] as MyJson.JsonNode_Object;
                    findFliter = "{txid:'" + jo_vin["txid"].ToString() + "'}";
                    result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx", findFliter);

                    if (result.Count>0)
                    {
                        int n = int.Parse(jo_vin["vout"].ToString());
                        MyJson.JsonNode_Object joresult_vin = result[0] as MyJson.JsonNode_Object;
                        //MyJson.JsonNode_Object vin = (joresult_vin["vout"] as MyJson.JsonNode_Array)[n] as MyJson.JsonNode_Object;
                        Utxo vin = new Utxo();
                        vin.n = (uint)joresult_vin["vout"].AsList()[n].AsDict()["n"].AsInt();
                        vin.asset = joresult_vin["vout"].AsList()[n].AsDict()["asset"].ToString();
                        vin.value = toolHelper.DecimalParse(joresult_vin["vout"].AsList()[n].AsDict()["value"].ToString());
                        vin.address = joresult_vin["vout"].AsList()[n].AsDict()["address"].ToString();
                        list_vin_utxo.Add(vin);
                        if (vin.address == address_tx.addr)
                        {
                            string value;
                            if (!dic_detail.ContainsKey(vin.asset))
                            {
                                //从asset表中获取这个资产的详情
                                result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "asset", "{\"id\":\"" + vin.asset + "\"}");
                                MyJson.JsonNode_Object assetInfo = result[0].AsDict();
                                Detail detail = new Detail();
                                detail.assetId = vin.asset;
                                detail.assetType = "UTXO";
                                detail.assetName = assetInfo["name"].AsList()[0].AsDict()["name"].ToString();
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

                MyJson.JsonNode_Array JAvout_utxo = jo_raw["vout"] as MyJson.JsonNode_Array;
                for (var i = 0; i < JAvout_utxo.Count; i++)
                {
                    MyJson.JsonNode_Object jo_vout = JAvout_utxo[i] as MyJson.JsonNode_Object;
                    Utxo vout = new Utxo();
                    vout.n = (uint)jo_vout["n"].AsInt();
                    vout.asset = jo_vout["asset"].ToString();
                    vout.value = toolHelper.DecimalParse(jo_vout["value"].ToString());
                    vout.address = jo_vout["address"].ToString();
                    list_vout_utxo.Add(vout);
                    if (jo_vout["address"].ToString() == address_tx.addr)
                    {
                        string value;
                        if (!dic_detail.ContainsKey(jo_vout["asset"].ToString()))
                        {
                            //从asset表中获取这个资产的详情
                            result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "asset", "{\"id\":\"" + jo_vout["asset"].ToString() + "\"}");
                            MyJson.JsonNode_Object assetInfo = result[0].AsDict();
                            Detail detail = new Detail();
                            detail.assetId = vout.asset;
                            detail.assetType = "UTXO";
                            detail.assetName = assetInfo["name"].AsList()[0].AsDict()["name"].ToString();
                            detail.assetSymbol = detail.assetName;
                            detail.assetDecimals = "";
                            detail.isSender = true;
                            detail.value = BsonDecimal128.Create("0");
                            dic_detail[jo_vout["asset"].ToString()] = detail;
                            value = "0";
                        }
                        else
                        {
                            value = dic_detail[jo_vout["asset"].ToString()].value.ToString();
                        }
                        var r = toolHelper.DecimalParse(value) + vout.value;
                        if (r > 0)
                            dic_detail[jo_vout["asset"].ToString()].isSender = false;
                        else
                            dic_detail[jo_vout["asset"].ToString()].isSender = false;
                        dic_detail[jo_vout["asset"].ToString()].value = BsonDecimal128.Create(r.ToString());

                    }
                    else
                    {
                        if (!list_toAddress.Contains(jo_vout["address"].ToString()))
                            list_toAddress.Add(jo_vout["address"].ToString());
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
                    mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", address_tx);
                }
            }
        }
        bool isFirstHandlerBlockindex = true;
    }
}
