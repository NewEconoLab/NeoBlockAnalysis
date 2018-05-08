using System;
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
                blockindex = mongoHelper.GetMaxIndex(Program.mongodbConnStr,Program.mongodbDatabase,"address_tx", "blockindex","{\"assetType\":\"utxo\"}");
                if (Program.handlerminblockindex != -1)
                { blockindex = Program.handlerminblockindex; }
                var maxBlockIndex = 999999999;
                if (Program.handlermaxblockindex != -1)
                    maxBlockIndex = Program.handlermaxblockindex;
                //删除这个高度tx的所有数据
                var count1 = mongoHelper.GetDataCount(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx");
                Console.WriteLine("删之前的数据个数："+ count1);
                mongoHelper.DelData(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", "{\"assetType\":\"utxo\",\"blockindex\":"+blockindex+"}");
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

            //获取区块所在时间
            var blocktime = ((MyJson.JsonNode_Object)mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "block", "{index:" + address_tx.blockindex + "}")[0])["time"].ToString();
            address_tx.blocktime = blocktime;

            address_tx.type = "out";
            address_tx.assetType = "utxo";
            string txid = jo["txid"].ToString();
            address_tx.txid = txid;

            var findFliter = "{txid:'" + txid + "'}";
            MyJson.JsonNode_Array result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx", findFliter);
            if (result.Count>0)
            {
                //一个txid获取到的信息应该只会存在一条
                MyJson.JsonNode_Object jo_raw = result[0] as MyJson.JsonNode_Object;


                var scripts = jo_raw["scripts"] as MyJson.JsonNode_Array;
                //先不处理多签的情况
                if (scripts.Count > 1)
                    return;


                address_tx.txType = jo_raw["type"].ToString();

                //定义一个输入包含utxo
                MyJson.JsonNode_Array JAvin_utxo = new MyJson.JsonNode_Array();

                //获取这个tx的vin并根据vin中的txid来获取输入的utxo的信息
                MyJson.JsonNode_Array JAvin = jo_raw["vin"] as MyJson.JsonNode_Array;
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
                            address_tx.type = "in";
                            string value = address_tx.value.ContainsKey(vin["asset"].ToString()) ? address_tx.value[vin["asset"].ToString()].ToString() : "0";
                            address_tx.value[vin["asset"].ToString()] = new MyJson.JsonNode_ValueString((decimal.Parse(value) - decimal.Parse(vin["value"].ToString())).ToString());
                        }
                    }
                }

                MyJson.JsonNode_Array JAvout_utxo = jo_raw["vout"] as MyJson.JsonNode_Array;
                foreach (MyJson.JsonNode_Object jo_vout in JAvout_utxo)
                {
                    if (jo_vout["address"].ToString() == address_tx.addr)
                    {
                        string value = address_tx.value.ContainsKey(jo_vout["asset"].ToString()) ? address_tx.value[jo_vout["asset"].ToString()].ToString() : "0";
                        address_tx.value[jo_vout["asset"].ToString()] = new MyJson.JsonNode_ValueString((decimal.Parse(value) + decimal.Parse(jo_vout["value"].ToString())).ToString());
                    }
                }

                foreach (var key in address_tx.value.Keys)
                {
                    HandlerAddressAsset(address_tx.addr, key, double.Parse(address_tx.value[key].ToString()),txid, int.Parse(jo["blockindex"].ToString()));
                }

                address_tx.vin = JAvin_utxo;
                address_tx.vout = JAvout_utxo;


                mongoHelper.InsetOne(Program.mongodbConnStr, Program.mongodbDatabase, "address_tx", address_tx.toMyJson().ToString());
            }
        }
        bool isFirstHandlerBlockindex = true;
        void HandlerAddressAsset(string addr,string asset ,double value,string txid,int blockindex)
        {
            var jo_asset = mongoHelper.FindOne(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + addr + "\",asset:\""+ asset + "\"}");
            double value_cur = jo_asset != null ? double.Parse(jo_asset["value_cur"].ToString()) : 0;
            double value_pre = jo_asset != null ? double.Parse(jo_asset["value_pre"].ToString()) : 0;
            int blockindex_cur = blockindex;
            int blockindex_cur_from = jo_asset != null ? int.Parse(jo_asset["blockindex"].ToString()) : blockindex_cur;

            MyJson.JsonNode_Object jo_assetNew = new MyJson.JsonNode_Object();

            if (blockindex_cur == blockindex_cur_from)
            {
                if (isFirstHandlerBlockindex)
                {
                    value_cur = 0;
                }
                jo_assetNew["value_pre"] = new MyJson.JsonNode_ValueNumber(value_pre);
            }
            else
            {
                jo_assetNew["value_pre"] = new MyJson.JsonNode_ValueNumber(value_cur + value_pre);
                value_cur = 0;

            }
            isFirstHandlerBlockindex = false;
            //to 就是要++的值
            value_cur += value;
            jo_assetNew["value_cur"] = new MyJson.JsonNode_ValueNumber((double)value_cur);
            jo_assetNew["asset"] = new MyJson.JsonNode_ValueString(asset);
            jo_assetNew["addr"] = new MyJson.JsonNode_ValueString(addr);
            jo_assetNew["blockindex"] = new MyJson.JsonNode_ValueNumber(blockindex_cur); //所在高度
            jo_assetNew["lastused"] = new MyJson.JsonNode_ValueString(txid);

            mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{addr:\"" + addr + "\",asset:\"" + asset + "\"}", jo_assetNew.ToString());
        }
    }


    class Address_Tx
    {
        public string addr;
        public string txid;
        public string type;
        public string assetType;
        public string txType;
        public MyJson.JsonNode_Array vin;
        public MyJson.JsonNode_Array vout;
        public MyJson.JsonNode_Object value = new MyJson.JsonNode_Object();
        public int blockindex;
        public string blocktime;

        public Address_Tx()
        {

        }
        public Address_Tx(string _addr, string _txid,string _type,string _assetType,string _txType, MyJson.JsonNode_Array _vin, MyJson.JsonNode_Array _vout, MyJson.JsonNode_Object _value, int _blockindex, string _blocktime)
        {
            this.addr = _addr;
            this.txid = _txid;
            this.type = _type;
            this.assetType = _assetType;
            this.txType = _txType;
            this.vin = _vin;
            this.vout = _vout;
            this.value = _value;
            this.blockindex = _blockindex;
            this.blocktime = _blocktime;
        }


        public MyJson.JsonNode_Object toMyJson()
        {
            MyJson.JsonNode_Object jo = new MyJson.JsonNode_Object();
            jo["addr"] = new MyJson.JsonNode_ValueString(addr);
            jo["txid"] = new MyJson.JsonNode_ValueString(txid); 
            jo["type"] = new MyJson.JsonNode_ValueString(type); 
            jo["assetType"] = new MyJson.JsonNode_ValueString(assetType);
            jo["txType"] = new MyJson.JsonNode_ValueString(txType);
            jo["vin"] = vin;
            jo["vout"] = vout;
            jo["value"] = value; 
            jo["blockindex"] = new MyJson.JsonNode_ValueNumber(blockindex); 
            jo["blocktime"] = new MyJson.JsonNode_ValueString(blocktime);

            return jo;
        }
    }
}
