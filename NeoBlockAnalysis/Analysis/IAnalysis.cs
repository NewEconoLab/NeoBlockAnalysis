using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoBlockAnalysis
{
    interface IAnalysis
    {
        void StartTask();
    }



    class Detail
    {
        public string assetId;
        public string assetType;
        public string assetName;
        public string assetSymbol;
        public string assetDecimals;
        public BsonDecimal128 value;
        public bool isSender;
        public string[] from;
        public string[] to;
        /*
        public MyJson.JsonNode_Object toMyJson()
        {
            MyJson.JsonNode_Object jo = new MyJson.JsonNode_Object();
            jo["assetType"] = new MyJson.JsonNode_ValueString(assetType);
            jo["assetName"] = new MyJson.JsonNode_ValueString(assetName);
            jo["assetSymbol"] = new MyJson.JsonNode_ValueString(assetSymbol);
            jo["assetDecimals"] = new MyJson.JsonNode_ValueString(assetDecimals);
            jo["value"] = new MyJson.JsonNode_ValueString(value);
            jo["isSender"] = new MyJson.JsonNode_ValueNumber(isSender);
            return jo;
        }
        */
    }
    class Address_Tx
    {
        public string addr;
        public string txid;
        public string txType;
        public bool isNep5;
        public Utxo[] vin;
        public Utxo[] vout;
        public Detail detail;
        public string netfee;
        public string sysfee;
        public int blockindex;
        public string blocktime;

        public Address_Tx()
        {

        }
        /*
        public MyJson.JsonNode_Object toMyJson()
        {
            MyJson.JsonNode_Object jo = new MyJson.JsonNode_Object();
            jo["addr"] = new MyJson.JsonNode_ValueString(addr);
            jo["txid"] = new MyJson.JsonNode_ValueString(txid);
            jo["txType"] = new MyJson.JsonNode_ValueString(txType);
            jo["vin"] = vin;
            jo["vout"] = vout;
            jo["detail"] = detail;
            jo["netfee"] = new MyJson.JsonNode_ValueString(netfee);
            jo["sysfee"] = new MyJson.JsonNode_ValueString(sysfee);
            jo["blockindex"] = new MyJson.JsonNode_ValueNumber(blockindex);
            jo["blocktime"] = new MyJson.JsonNode_ValueString(blocktime);
            jo["isNep5"] = new MyJson.JsonNode_ValueNumber(isNep5);

            return jo;
        }
        */
    }
    class Utxo
    {
        public uint n;
        public string asset;
        public decimal value;
        public string address;
    }
}
