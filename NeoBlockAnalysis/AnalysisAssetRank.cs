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
                    DateTime start = DateTime.Now;

                    StartAnalysis();

                    DateTime end = DateTime.Now;
                    var doTime = (end - start).TotalMilliseconds;
                    Console.WriteLine("总共处理了："+ doTime);
                    Thread.Sleep(Program.sleepTime);
                }
            });
            task_StoragTx.Start();
        }

        private void StartAnalysis()
        {
            MyJson.JsonNode_Array result = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "asset", "{}");
            for (var i = 0; i < result.Count; i++)
            {
                HandlerAssetRank((result[i] as MyJson.JsonNode_Object)["id"].ToString());
            }
        }

        private void HandlerAssetRank(string assetId)
        {
            //从assetRank中获取这个资产的所有地址的钱
            var Ja_result = mongoHelper.GetData(Program.mongodbConnStr, Program.mongodbDatabase, "assetrank", "{\"asset\":\""+assetId+"\"}");
            var count = Ja_result.Count;
            for (var i = 0; i < count; i++)
            {
                var Jo_result = Ja_result[i]  as MyJson.JsonNode_Object;
                var value = double.Parse(Jo_result["value_pre"].ToString())+ double.Parse(Jo_result["value_cur"].ToString());
                var addr = Jo_result["addr"].ToString();
                MyJson.JsonNode_Object jo_assetNew = new MyJson.JsonNode_Object();
                mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "hadAnalysisAssetRank", "{addr:\"" + addr + "\",asset:\"" + assetId + "\"}", jo_assetNew.ToString());
            }
        }
    }
}
