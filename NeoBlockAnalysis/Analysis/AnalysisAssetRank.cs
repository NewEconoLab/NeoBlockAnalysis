using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeoBlockAnalysis
{
    class AnalysisAssetRank:IAnalysis
    {
        public void StartTask()
        {
            Task task_StoragTx = new Task(() => {

                while (true)
                {
                    StartAnalysis();
                    Thread.Sleep(Program.sleepTime);
                }
            });
            task_StoragTx.Start();
        }

        private void StartAnalysis()
        {
            try
            {
                HandlerNep5();
                HandlerUtxo();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void HandlerNep5()
        {
            MyJson.JsonNode_Array result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "NEP5asset", "{}");
            for (var i = 0; i < result.Count; i++)
            {
                string assetId = (result[i] as MyJson.JsonNode_Object)["assetid"].ToString();
                //从assetRank中获取这个资产的所有地址的钱
                var Ja_result = mongoHelper.GetData(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{\"asset\":\"" + assetId + "\"}");
                var count = Ja_result.Count;
                for (var ii = 0; ii < count; ii++)
                {
                    var Jo_result = Ja_result[ii] as MyJson.JsonNode_Object;
                    var value = double.Parse(Jo_result["value_pre"].ToString()) + double.Parse(Jo_result["value_cur"].ToString());
                    var addr = Jo_result["addr"].ToString();
                    MyJson.JsonNode_Object j = new MyJson.JsonNode_Object();
                    j["asset"] = new MyJson.JsonNode_ValueString(assetId);
                    j["balance"] = new MyJson.JsonNode_ValueNumber((double)value);
                    j["addr"] = new MyJson.JsonNode_ValueString(addr);
                    mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "allAssetRank", "{addr:\"" + addr + "\",asset:\"" + assetId + "\"}", j.ToString());
                }
            }
        }

        private void HandlerUtxo()
        {
            //获取已有的所有的地址 （分段）
            var count = mongoHelper.GetDataCount(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "address");
            count = count/1000 +1;
            for (var i = 1; i < count+1; i++)
            {
                MyJson.JsonNode_Array Ja_addressInfo =mongoHelper.GetDataPages(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "address", "{}",1000,i);
                for (var ii = 0; ii < Ja_addressInfo.Count; ii++)
                {
                    var address = (Ja_addressInfo[ii] as MyJson.JsonNode_Object)["addr"].ToString();
                    //获取这个address的所有的utxo
                    var findFliter = "{addr:\"" + address + "\",used:''}";
                    MyJson.JsonNode_Array utxos = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "utxo", findFliter);
                    Dictionary<string, decimal> balance = new Dictionary<string, decimal>();
                    foreach (MyJson.JsonNode_Object j in utxos)
                    {
                        if (!balance.ContainsKey(j["asset"].ToString()))
                        {
                            balance.Add(j["asset"].ToString(),decimal.Parse(j["value"].ToString()));
                        }
                        else
                        {
                            balance[j["asset"].ToString()] += decimal.Parse(j["value"].ToString());
                        }
                    }
                    foreach (KeyValuePair<string, decimal> kv in balance)
                    {
                        MyJson.JsonNode_Object j = new MyJson.JsonNode_Object();
                        j["asset"] = new MyJson.JsonNode_ValueString(kv.Key);
                        j["balance"] = new MyJson.JsonNode_ValueNumber((double)kv.Value);
                        j["addr"] = new MyJson.JsonNode_ValueString(address);

                        mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "allAssetRank", "{addr:\"" + address + "\",asset:\"" + kv.Key + "\"}", j.ToString());
                    }

                }
            }
        }
    }
}
