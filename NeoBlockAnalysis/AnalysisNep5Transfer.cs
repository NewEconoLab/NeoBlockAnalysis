using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeoBlockAnalysis
{
    class AnalysisNep5Transfer
    {
        public void StartTask()
        {
            Task task_StorageNep5Transfer = new Task(() => {
                //先获取Nep5Transfer已经获取到的高度
                int blockindex = mongoHelper.GetMaxIndex(Program.mongodbConnStr, Program.mongodbDatabase, "NEP5transfer", "blockindex");

                if (Program.handlerminblockindex != -1)
                { blockindex = Program.handlerminblockindex; }
                var maxBlockIndex = 999999999;
                if (Program.handlermaxblockindex != -1)
                    maxBlockIndex = Program.handlermaxblockindex;

                mongoHelper.DelData(Program.mongodbConnStr, Program.mongodbDatabase, "NEP5transfer", "{\"blockindex\":" + blockindex + "}");
                mongoHelper.DelData(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", "{\"assetType\":\"nep5\",\"blockindex\":" + blockindex + "}");

                while (true)
                {
                    if (blockindex >= maxBlockIndex)
                    {
                        Console.WriteLine("已经处理到预期高度");
                        return;
                    }
                    var cli_blockindex = mongoHelper.Getblockheight(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block");
                    if (blockindex < cli_blockindex)
                    {
                        StorageNep5Transfer(blockindex);
                        blockindex++;
                    }
                    Thread.Sleep(Program.sleepTime);
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
                mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "NEP5transfer", jo.ToString());
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

            double value = double.Parse(str_value);
            int blockindex_cur = int.Parse(jo["blockindex"].ToString());

            if (!string.IsNullOrEmpty(from))
            {
                //获取这个资产 from地址的资产
                var jo_assetfrom = mongoHelper.FindOne(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + from + "\",asset:\"" + asset + "\"}");
                double value_cur = jo_assetfrom!=null ? double.Parse(jo_assetfrom["value_cur"].ToString()) : 0;
                double value_pre = jo_assetfrom!=null ? double.Parse(jo_assetfrom["value_pre"].ToString()) : 0;

                int blockindex_cur_from = jo_assetfrom != null ? int.Parse(jo_assetfrom["blockindex"].ToString()) : blockindex_cur;

                MyJson.JsonNode_Object jo_assetfromNew = new MyJson.JsonNode_Object();

                if (blockindex_cur == blockindex_cur_from)
                {
                    if (isFirstHandlerBlockindex)
                    {
                        value_cur = 0;
                    }
                    jo_assetfromNew["value_pre"] = new MyJson.JsonNode_ValueNumber(value_pre);
                }
                else
                {
                    jo_assetfromNew["value_pre"] = new MyJson.JsonNode_ValueNumber(value_cur + value_pre);
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
                mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + from + "\"}", jo_assetfromNew.ToString());
            }

            if (!string.IsNullOrEmpty(to))
            {
                //获取这个资产 to地址的资产
                var jo_assetto = mongoHelper.FindOne(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + to + "\",asset:\"" + asset + "\"}");
                double value_cur = jo_assetto != null ? double.Parse(jo_assetto["value_cur"].ToString()) : 0;
                double value_pre = jo_assetto != null ? double.Parse(jo_assetto["value_pre"].ToString()) : 0;

                int blockindex_cur_to = jo_assetto != null ? int.Parse(jo_assetto["blockindex"].ToString()) : blockindex_cur;

                MyJson.JsonNode_Object jo_assettoNew = new MyJson.JsonNode_Object();

                if (blockindex_cur == blockindex_cur_to)
                {
                    if (isFirstHandlerBlockindex)
                    {
                        value_cur = 0;
                    }
                    jo_assettoNew["value_pre"] = new MyJson.JsonNode_ValueNumber(value_pre);
                }
                else
                {
                    jo_assettoNew["value_pre"] = new MyJson.JsonNode_ValueNumber(value_cur + value_pre);
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
                mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + to + "\"}", jo_assettoNew.ToString());
            }



            //往mongo里address_tx存入相关的地址交易信息
            //存from地址
            MyJson.JsonNode_Array JAvin = new MyJson.JsonNode_Array();
            MyJson.JsonNode_Object JOvin = new MyJson.JsonNode_Object();
            JOvin["n"] = new MyJson.JsonNode_ValueNumber(0);
            JOvin["asset"] = new MyJson.JsonNode_ValueString(asset);
            JOvin["value"] = new MyJson.JsonNode_ValueString(value.ToString());
            JOvin["address"] = new MyJson.JsonNode_ValueString(from);
            JAvin.Add(JOvin);

            MyJson.JsonNode_Array JAvout = new MyJson.JsonNode_Array();
            MyJson.JsonNode_Object JOvout = new MyJson.JsonNode_Object();
            JOvout["n"] = new MyJson.JsonNode_ValueNumber(0);
            JOvout["asset"] = new MyJson.JsonNode_ValueString(asset);
            JOvout["value"] = new MyJson.JsonNode_ValueString(value.ToString());
            JOvout["address"] = new MyJson.JsonNode_ValueString(to);
            JAvout.Add(JOvout);

            //获取区块所在时间
            var blocktime = ((MyJson.JsonNode_Object)mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block", "{index:" + blockindex_cur + "}")[0])["time"].ToString();

            MyJson.JsonNode_Object JOvalue = new MyJson.JsonNode_Object();
            JOvalue[asset] = new MyJson.JsonNode_ValueString((0 - value).ToString());
            Address_Tx addressTx = new Address_Tx(from, jo["txid"].ToString(), "in", "nep5", "nep5", JAvin, JAvout, JOvalue, blockindex_cur, blocktime);
            mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", addressTx.toMyJson().ToString());
            JOvalue[asset] = new MyJson.JsonNode_ValueString((0 + value).ToString());
            addressTx = new Address_Tx(to, jo["txid"].ToString(), "out", "nep5", "nep5", JAvin, JAvout, JOvalue, blockindex_cur, blocktime);
            mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", addressTx.toMyJson().ToString());

        }
    }
}
