using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeoBlockAnalysis
{
    class AnalysisAssetByHeight : IAnalysis
    {
        public int height = 0;
        public string assetid = "0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b";
        public void StartTask()
        {
            Task task_StoragTx = new Task(() => {
                StartAnalysis();
            });
            task_StoragTx.Start();
        }

        private void StartAnalysis()
        {
            try
            {
                HandlerUtxo();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void HandlerUtxo()
        {
            //获取已有的所有的地址 （分段）
            var count = mongoHelper.GetDataCount(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "address");
            count = count / 1000 + 1;
            for (var i = 1; i < count + 1; i++)
            {
                Console.WriteLine("总共要循环：" + count + "~~现在循环到：" + i);
                MyJson.JsonNode_Array Ja_addressInfo = mongoHelper.GetDataPages(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "address", "{}", 1000, i);
                for (var ii = 0; ii < Ja_addressInfo.Count; ii++)
                {
                    var address = (Ja_addressInfo[ii] as MyJson.JsonNode_Object)["addr"].ToString();
                    //获取这个address的所有的utxo
                    var findFliter = "{addr:\"" + address+"\",asset:\""+assetid + "\"}";
                    MyJson.JsonNode_Array utxos = mongoHelper.GetData(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "utxo", findFliter);
                    decimal value = 0;
                    foreach (MyJson.JsonNode_Object j in utxos)
                    {
                        if(int.Parse(j["createHeight"].ToString())<= height && (int.Parse(j["useHeight"].ToString())>height|| int.Parse(j["useHeight"].ToString())==-1))
                            value += toolHelper.DecimalParse(j["value"].ToString());
                    }
                    if (value > 0)
                    {
                        MyJson.JsonNode_Object json = new MyJson.JsonNode_Object();
                        json["balance"] = new MyJson.JsonNode_ValueNumber((double)value);
                        json["addr"] = new MyJson.JsonNode_ValueString(address);
                        mongoHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "NeoSnapshot", "{addr:\"" + address + "\"}", json.ToString());
                    }
                }
            }
        }
    }
}
