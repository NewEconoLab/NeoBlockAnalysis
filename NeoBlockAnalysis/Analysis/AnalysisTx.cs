﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading;

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
                var maxBlockIndex = 999999999;
                if (Program.handlermaxblockindex != -1)
                    maxBlockIndex = Program.handlermaxblockindex;
                //删除这个高度tx的所有数据
                var count1 = mongoHelper.GetDataCount(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx");
                Console.WriteLine("删之前的数据个数："+ count1);
                mongoHelper.DelData(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", "{\"isNep5\":false,\"blockindex\":"+blockindex+"}");
                var count2 = mongoHelper.GetDataCount(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx");
                Console.WriteLine("删之后的数据个数：" + count2);
                while (true)
                {
                    if (blockindex >= maxBlockIndex)
                    {
                        Console.WriteLine("已经处理到预期高度");
                        return;
                    }
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
                MyJson.JsonNode_Array JAvin_utxo = new MyJson.JsonNode_Array();

                //获取这个tx的vin并根据vin中的txid来获取输入的utxo的信息
                MyJson.JsonNode_Array JAvin = jo_raw["vin"] as MyJson.JsonNode_Array;


                //过滤一些疑似交易所的复杂构造
                if (scripts.Count > 1 && JAvin.Count > 200)
                    return;


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
                        MyJson.JsonNode_Object vin = (joresult_vin["vout"] as MyJson.JsonNode_Array)[n] as MyJson.JsonNode_Object;
                        JAvin_utxo.Add(vin);
                        if (vin["address"].ToString() == address_tx.addr)
                        {
                            string value;
                            if (!address_tx.detail.ContainsKey(vin["asset"].ToString()))
                            {
                                //从asset表中获取这个资产的详情
                                result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "asset", "{\"id\":\"" + vin["asset"].ToString() + "\"}");
                                MyJson.JsonNode_Object assetInfo = result[0].AsDict();
                                Detail detail = new Detail();
                                detail.assetType = "UTXO";
                                detail.assetName = assetInfo["name"].AsList()[0].AsDict()["name"].ToString();
                                detail.assetSymbol = detail.assetName;
                                detail.assetDecimals = "";
                                detail.fromOrTo = "from";
                                detail.value = "0";
                                address_tx.detail[vin["asset"].ToString()] = detail.toMyJson();
                                value = "0";
                            }
                            else
                            {
                                value = address_tx.detail[vin["asset"].ToString()].AsDict()["value"].ToString();
                            }
                            address_tx.detail[vin["asset"].ToString()].AsDict()["value"] = new MyJson.JsonNode_ValueString((decimal.Parse(value) - decimal.Parse(vin["value"].ToString())).ToString());
                        }
                    }
                }

                MyJson.JsonNode_Array JAvout_utxo = jo_raw["vout"] as MyJson.JsonNode_Array;
                foreach (MyJson.JsonNode_Object jo_vout in JAvout_utxo)
                {
                    if (jo_vout["address"].ToString() == address_tx.addr)
                    {
                        string value;
                        if (!address_tx.detail.ContainsKey(jo_vout["asset"].ToString()))
                        {
                            //从asset表中获取这个资产的详情
                            result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "asset", "{\"id\":\"" + jo_vout["asset"].ToString() + "\"}");
                            MyJson.JsonNode_Object assetInfo = result[0].AsDict();
                            Detail detail = new Detail();
                            detail.assetType = "UTXO";
                            detail.assetName = assetInfo["name"].AsList()[0].AsDict()["name"].ToString();
                            detail.assetSymbol = detail.assetName;
                            detail.assetDecimals = "";
                            detail.fromOrTo = "from";
                            detail.value = "0";
                            address_tx.detail[jo_vout["asset"].ToString()] = detail.toMyJson();
                            value = "0";
                        }
                        else
                        {
                            value = address_tx.detail[jo_vout["asset"].ToString()].AsDict()["value"].ToString();
                        }
                        var r = decimal.Parse(value) + decimal.Parse(jo_vout["value"].ToString());
                        if (r > 0)
                            address_tx.detail[jo_vout["asset"].ToString()].AsDict()["fromOrTo"] = new MyJson.JsonNode_ValueString("to");
                        else
                            address_tx.detail[jo_vout["asset"].ToString()].AsDict()["fromOrTo"] = new MyJson.JsonNode_ValueString("from");
                        address_tx.detail[jo_vout["asset"].ToString()].AsDict()["value"] = new MyJson.JsonNode_ValueString(r.ToString());

                    }
                }
                //如果这个地址在这个交易里面没有资产变化就不入库
                if (address_tx.detail.Keys.Count == 0)
                    return;
                address_tx.vin = JAvin_utxo;
                address_tx.vout = JAvout_utxo;
                address_tx.isNep5 = false;
                mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", address_tx.toMyJson().ToString());
            }
        }
        bool isFirstHandlerBlockindex = true;
    }
}