using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;


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
                    int blockindex = mongoHelper.GetMaxIndex(Program.mongodbConnStr, Program.mongodbDatabase, "NEP5transfer", "blockindex");

                    if (Program.handlerminblockindex != -1)
                    { blockindex = Program.handlerminblockindex; }
                    var maxBlockIndex = 999999999;
                    if (Program.handlermaxblockindex != -1)
                        maxBlockIndex = Program.handlermaxblockindex;

                    mongoHelper.DelData(Program.mongodbConnStr, Program.mongodbDatabase, "NEP5transfer", "{\"blockindex\":" + blockindex + "}");
                    mongoHelper.DelData(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", "{\"isNep5\":true,\"blockindex\":" + blockindex + "}");

                    while (true)
                    {
                        if (blockindex >= maxBlockIndex)
                        {
                            Console.WriteLine("已经处理到预期高度");
                            return;
                        }
                        var cli_blockindex = mongoHelper.GetNEP5transferheight(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "NEP5transfer");
                        if (blockindex < cli_blockindex)
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

        public void Start(int blockindex)
        {
            StorageNep5Transfer(blockindex);
        }

        bool isFirstHandlerBlockindex = true;

        void StorageNep5Transfer(Int64 blockindex)
        {
            Console.WriteLine("nep5transfer:"+blockindex);
            var findFliter = "{blockindex:" + blockindex + "}";
            MyJson.JsonNode_Array result=  mongoHelper.GetData(Program.neo_mongodbConnStr,Program.neo_mongodbDatabase, "NEP5transfer", findFliter);
            isFirstHandlerBlockindex = true;
            for (var i = 0; i < result.Count; i++)
            {
                Console.WriteLine("nep5transfer: "+blockindex+"~~~~~~~~~i:"+i);
                MyJson.JsonNode_Object jo = result[i] as MyJson.JsonNode_Object;
                mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "NEP5transfer",BsonDocument.Parse(jo.ToString()));
                //对nep5数据进行分析处理
                HandlerNep5Transfer(jo);
            }
        }

        void HandlerNep5Transfer(MyJson.JsonNode_Object jo)
        {
            string asset = jo["asset"].ToString(); //资产id
            string from = jo["from"].ToString();
            string to = jo["to"].ToString();
            string  str_value = jo["value"].ToString();

            decimal value = toolHelper.DecimalParse(str_value);
            int blockindex_cur = int.Parse(jo["blockindex"].ToString());

            if (!string.IsNullOrEmpty(from))
            {
                //获取这个资产 from地址的资产
                var jo_assetfrom = mongoHelper.FindOne(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + from + "\",asset:\"" + asset + "\"}");
                decimal value_cur = jo_assetfrom!=null ? toolHelper.DecimalParse(jo_assetfrom["value_cur"].ToString()) : 0;
                decimal value_pre = jo_assetfrom!=null ? toolHelper.DecimalParse(jo_assetfrom["value_pre"].ToString()) : 0;

                int blockindex_cur_from = jo_assetfrom != null ? int.Parse(jo_assetfrom["blockindex"].ToString()) : blockindex_cur;

                MyJson.JsonNode_Object jo_assetfromNew = new MyJson.JsonNode_Object();

                if (blockindex_cur == blockindex_cur_from)
                {
                    if (isFirstHandlerBlockindex)
                    {
                        value_cur = 0;
                    }
                    jo_assetfromNew["value_pre"] = new MyJson.JsonNode_ValueNumber((double)value_pre);
                }
                else
                {
                    jo_assetfromNew["value_pre"] = new MyJson.JsonNode_ValueNumber((double)value_cur + (double)value_pre);
                    value_cur = 0 ;
                }

                isFirstHandlerBlockindex = false;
                //from 就是要减少的值
                value_cur -= value;
                jo_assetfromNew["value_cur"] = new MyJson.JsonNode_ValueNumber((double)value_cur);
                jo_assetfromNew["asset"] = new MyJson.JsonNode_ValueString(asset);
                jo_assetfromNew["addr"] = new MyJson.JsonNode_ValueString(from);
                jo_assetfromNew["blockindex"] = new MyJson.JsonNode_ValueNumber(blockindex_cur);  //当前高度
                jo_assetfromNew["lastused"] = new MyJson.JsonNode_ValueString(jo["txid"].ToString());
                //存入新的from地址的钱
                mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + from + "\",asset:\"" + asset + "\"}", jo_assetfromNew.ToString());
            }

            if (!string.IsNullOrEmpty(to))
            {
                //获取这个资产 to地址的资产
                var jo_assetto = mongoHelper.FindOne(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + to + "\",asset:\"" + asset + "\"}");
                decimal value_cur = jo_assetto != null ? toolHelper.DecimalParse(jo_assetto["value_cur"].ToString()) : 0;
                decimal value_pre = jo_assetto != null ? toolHelper.DecimalParse(jo_assetto["value_pre"].ToString()) : 0;

                int blockindex_cur_to = jo_assetto != null ? int.Parse(jo_assetto["blockindex"].ToString()) : blockindex_cur;

                MyJson.JsonNode_Object jo_assettoNew = new MyJson.JsonNode_Object();

                if (blockindex_cur == blockindex_cur_to)
                {
                    if (isFirstHandlerBlockindex)
                    {
                        value_cur = 0;
                    }
                    jo_assettoNew["value_pre"] = new MyJson.JsonNode_ValueNumber((double)value_pre);
                }
                else
                {
                    jo_assettoNew["value_pre"] = new MyJson.JsonNode_ValueNumber((double)value_cur + (double)value_pre);
                    value_cur = 0;

                }
                isFirstHandlerBlockindex = false;
                //to 就是要++的值
                value_cur += value;
                jo_assettoNew["value_cur"] = new MyJson.JsonNode_ValueNumber((double)value_cur);
                jo_assettoNew["asset"] = new MyJson.JsonNode_ValueString(asset);
                jo_assettoNew["addr"] = new MyJson.JsonNode_ValueString(to);
                jo_assettoNew["blockindex"] = new MyJson.JsonNode_ValueNumber(blockindex_cur); //所在高度
                jo_assettoNew["lastused"] = new MyJson.JsonNode_ValueString(jo["txid"].ToString());
                //存入新的to地址的钱
                mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + to + "\",asset:\"" + asset + "\"}", jo_assettoNew.ToString());
            }



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
            var blocktime = ((MyJson.JsonNode_Object)mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block", "{index:" + blockindex_cur + "}")[0])["time"].ToString();

            //获取交易详情
            var txinfo = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx", "{\"txid\":\"" + txid + "\"}");
            var txType = "InvocationTransaction";
            var sysfee = "0";
            var netfee = "0";
            if (txinfo.Count > 0)
            {
                txType = txinfo[0].AsDict()["type"].ToString();
                sysfee = txinfo[0].AsDict()["sys_fee"].ToString();
                netfee = txinfo[0].AsDict()["net_fee"].ToString();
            }

            //获取资产详情
            Detail detail = new Detail();
            var assetInfo = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "NEP5asset", "{assetid:\"" + asset + "\"}");
            //from 构造
            detail.assetId = asset;
            detail.assetName = assetInfo[0].AsDict()["name"].ToString();
            detail.assetDecimals = assetInfo[0].AsDict()["decimals"].ToString();
            detail.assetSymbol = assetInfo[0].AsDict()["symbol"].ToString();
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
            addressTx.blockindex = blockindex_cur;
            addressTx.blocktime = blocktime;
            addressTx.isNep5 = true;
            addressTx.txType = txType;

            MyJson.JsonNode_Object JOvalue = new MyJson.JsonNode_Object();
            mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", addressTx);
            detail.value = BsonDecimal128.Create((0 + value).ToString());
            detail.isSender = false;
            addressTx.addr = to;
            addressTx.detail = detail;
            mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", addressTx);

        }
    }
}
