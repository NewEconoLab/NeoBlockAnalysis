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
    }
    class Utxo
    {
        public uint n;
        public string asset;
        public decimal value;
        public string address;
    }
}
