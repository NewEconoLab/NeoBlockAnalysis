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
        public string assetType;
        public string assetName;
        public string assetSymbol;
        public string assetDecimals;
        public string value;
        public string fromOrTo;

        public MyJson.JsonNode_Object toMyJson()
        {
            MyJson.JsonNode_Object jo = new MyJson.JsonNode_Object();
            jo["assetType"] = new MyJson.JsonNode_ValueString(assetType);
            jo["assetName"] = new MyJson.JsonNode_ValueString(assetName);
            jo["assetSymbol"] = new MyJson.JsonNode_ValueString(assetSymbol);
            jo["assetDecimals"] = new MyJson.JsonNode_ValueString(assetDecimals);
            jo["value"] = new MyJson.JsonNode_ValueString(value);
            jo["fromOrTo"] = new MyJson.JsonNode_ValueString(fromOrTo);
            return jo;
        }
    }
    class Address_Tx
    {
        public string addr;
        public string txid;
        public string txType;
        public bool isNep5;
        public MyJson.JsonNode_Array vin;
        public MyJson.JsonNode_Array vout;
        public MyJson.JsonNode_Object detail = new MyJson.JsonNode_Object();
        public string netfee;
        public string sysfee;
        public int blockindex;
        public string blocktime;

        public Address_Tx()
        {

        }
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
    }
}
