using NEL.Simple.SDK.Helper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using System.Numerics;
using System.Linq;
using Newtonsoft.Json.Linq;
using ThinNeo.Cryptography;

namespace NeoBlockAnalysis
{
    class AnalysisDumpInfosNew : IAnalysis
    {
        public void StartTask()
        {
            Task task = new Task(() =>
            {
                try
                {
                    //获取已经处理到了哪个高度
                    var query = MongoDBHelper.Get(Program.mongodbConnStr, Program.mongodbDatabase, "system_counter", 0, 1, "{\"counter\":\"dumpInfosNew\"}", "{}");
                    int handlerHeight = -1;
                    if (query.Count > 0)
                        handlerHeight = (int)query[0]["lastBlockindex"];
                    while (true)
                    {
                        Thread.Sleep(Program.sleepTime);
                        //处理的高度必须小于节点同步dumpinfo到的高度
                        query = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "dumpinfos", 0, 1, "{}", "{\"blockIndex\":-1}");
                        int height = (int)query[0]["blockIndex"];
                        if (handlerHeight >= height)
                            continue;
                        try
                        {
                            HandlerDumpInfos(handlerHeight);
                            handlerHeight++;
                        }
                        catch (Exception e)
                        {
                           Console.WriteLine(handlerHeight+"   error:" + e);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            });
            task.Start();
        }

        void HandlerDumpInfos(int handlerHeight)
        {
            var query = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "dumpinfos", "{\"blockIndex\":\"" + handlerHeight + "\"}", "{}");
            for (var i = 0; i < query.Count; i++)
            {
                var txid = (string)query[i]["txid"];
                var str = (string)query[i]["dimpInfo"]; 
                var bts = ThinNeo.Helper.HexString2Bytes(str);
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream(bts))
                {
                    var outms = llvm.QuickFile.FromFile(ms);
                    var text = System.Text.Encoding.UTF8.GetString(outms.ToArray());
                    var json = JObject.Parse(text);
                    if (!json.ContainsKey("VMState") || !json.ContainsKey("script") || json["VMState"].ToString() == "FAULT")
                        continue;
                    //查询这个交易的发起人,用tx中的scripts中的verification来获得
                    var txinfo = MongoDBHelper.Get(Program.neo_mongodbConnStr, Program.neo_mongodbDatabase, "tx", 0, 1, "{\"txid\":\"" + txid + "\"}", "{}")[0];
                    var scripts = txinfo["scripts"] as JArray;
                    if (scripts.Count == 0)
                        continue;
                    List<string> froms= new List<string>();
                    var verification = scripts[0]["verification"].ToString();
                    var sender = Verification2Hash(verification);
                    froms.Add(sender);
                    uint index = 0;
                    uint level = 0;
                    execOps(json["script"]["ops"] as JArray,txid, handlerHeight, froms,ref index,ref level, sender);
                }
            }

            MongoDBHelper.ReplaceData(Program.mongodbConnStr, Program.mongodbDatabase, "system_counter", "{\"counter\":\"dumpInfosNew\"}", MongoDB.Bson.BsonDocument.Parse("{counter:\"dumpInfosNew\",lastBlockindex:" + handlerHeight + "}"));
        }

        void execOps(JArray ops,string txid,int blockIndex,List<string> froms,ref uint index,ref uint level,string sender)
        {
            for (var n = 0; n < ops.Count; n++)
            {
                var op = ops[n];
                if (op["op"].ToString() == "APPCALL" || op["op"].ToString() == "TAILCALL")
                {
                    //取到的值是小端序的，要转换成大端序
                    var to = (new ThinNeo.Hash160(ThinNeo.Helper.HexString2Bytes(op["param"].ToString()))).ToString();
                    var l = (int)level > froms.Count ? froms.Count - 1 : (int)level;
                    InvoeInfo info = new InvoeInfo() { from = froms[l], txid = txid, to = to, type = InvokeType.Call, index = index,level = level,blockIndex = blockIndex};
                    MongoDBHelper.InsertOne(Program.mongodbConnStr, Program.mongodbDatabase, "contract_exec_detail", info);
                    index++;
                    level++;
                    froms.Add(to);
                    //Console.WriteLine("APPCALL:"+level);
                    if(op["subscript"] != null)
                        execOps(op["subscript"]["ops"] as JArray, txid, blockIndex, froms, ref index, ref level, sender);
                }
                else if ( op["op"].ToString() == "CALL" || op["op"].ToString() == "CALL_I" || op["op"].ToString() == "CALL_E")
                {
                    froms.Add(froms[(int)level]);
                    level++;
                    //Console.WriteLine("CALL:"+level);
                }
                else if (op["op"].ToString() == "RET")
                {
                    if (level == 0)
                        return;
                    froms.RemoveAt((int)level);
                    level--;
                    //Console.WriteLine("RET:" + level);
                }
                else if (op["op"].ToString() == "SYSCALL" && op["param"] != null &&"Neo.Contract.Create" == System.Text.Encoding.UTF8.GetString(ThinNeo.Helper.HexString2Bytes(op["param"].ToString())))
                {
                    var data = JObject.Parse(ops[n - 1]["result"].ToString())["ByteArray"].ToString();
                    var bytes_data = ThinNeo.Helper.HexString2Bytes(data);
                    ThinNeo.Hash160 scriptHash = ThinNeo.Helper_NEO.GetScriptHashFromScript(bytes_data);
                    var l = (int)level > froms.Count ? froms.Count - 1 : (int)level;
                    InvoeInfo info = new InvoeInfo() { from = froms[l], txid = txid, to = scriptHash.ToString(), type = InvokeType.Create, index = index, level = level, blockIndex = blockIndex };
                    MongoDBHelper.InsertOne(Program.mongodbConnStr, Program.mongodbDatabase, "contract_exec_detail", info);
                    index++;
                }
                else if (op["op"].ToString() == "SYSCALL" && op["param"] != null && "Neo.Contract.Migrate" == System.Text.Encoding.UTF8.GetString(ThinNeo.Helper.HexString2Bytes(op["param"].ToString())))
                {
                    var _to = "";
                    try
                    {
                        var data = JObject.Parse(ops[n - 1]["result"].ToString())["ByteArray"].ToString();
                        var bytes_data = ThinNeo.Helper.HexString2Bytes(data);
                        ThinNeo.Hash160 scriptHash = ThinNeo.Helper_NEO.GetScriptHashFromScript(bytes_data);
                        _to = scriptHash.ToString();
                    }
                    catch
                    {
                        
                    }
                    var l = (int)level > froms.Count ? froms.Count - 1 : (int)level;
                    InvoeInfo info = new InvoeInfo() { from = froms[l], txid = txid, to = _to, type = InvokeType.Update, index = index, level = level, blockIndex = blockIndex };
                    MongoDBHelper.InsertOne(Program.mongodbConnStr, Program.mongodbDatabase, "contract_exec_detail", info);
                    index++;
                }
                else if (op["op"].ToString() == "SYSCALL" && op["param"] != null && "Neo.Contract.Destroy" == System.Text.Encoding.UTF8.GetString(ThinNeo.Helper.HexString2Bytes(op["param"].ToString())))
                {
                    var l = (int)level > froms.Count ? froms.Count - 1 : (int)level;
                    InvoeInfo info = new InvoeInfo() { from = froms[l], txid = txid, to = "", type = InvokeType.Destroy, index = index, level = level, blockIndex = blockIndex };
                    MongoDBHelper.InsertOne(Program.mongodbConnStr, Program.mongodbDatabase, "contract_exec_detail", info);
                    index++;
                }
            }
        }

        string Verification2Hash(string verification)
        {
            var bts = ThinNeo.Helper.HexString2Bytes(verification);
            if (bts.Length == 22)//这种情况一般是正常的地址
            {
                var pubKey = bts.Skip(1).Take(20).ToArray();
                return ThinNeo.Helper_NEO.GetScriptHash_FromPublicKey(pubKey).ToString();
            }
            else//鉴权构造
            {
                RIPEMD160Managed ripemd160 = new RIPEMD160Managed();
                var scripthash = ThinNeo.Helper.Sha256.ComputeHash(ThinNeo.Helper.HexString2Bytes(verification));
                scripthash = ripemd160.ComputeHash(scripthash);
                return new ThinNeo.Hash160(scripthash).ToString();
            }
        }
    }

    class InvoeInfo
    {
        public InvokeType type;
        public string txid;
        public string from;
        public string to;
        public uint level;
        public uint index;
        public int blockIndex;
    }
    enum InvokeType
    {
        Call = 1,
        Create = 2,
        Update = 3,
        Destroy = 4
    }
}

