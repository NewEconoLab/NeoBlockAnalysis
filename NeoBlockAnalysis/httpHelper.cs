using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace NeoBlockAnalysis
{
    class httpHelper
    {
        public static string MakeRpcUrlPost(string url, string method, out byte[] data, params MyJson.IJsonNode[] _params)
        {
            if (url.Last() != '/')
                url = url + "/";
            var json = new MyJson.JsonNode_Object();
            json["id"] = new MyJson.JsonNode_ValueNumber(1);
            json["jsonrpc"] = new MyJson.JsonNode_ValueString("2.0");
            json["method"] = new MyJson.JsonNode_ValueString(method);
            StringBuilder sb = new StringBuilder();
            var array = new MyJson.JsonNode_Array();
            for (var i = 0; i < _params.Length; i++)
            {

                array.Add(_params[i]);
            }
            json["params"] = array;
            data = System.Text.Encoding.UTF8.GetBytes(json.ToString());
            return url;
        }
        public static string HttpGet(string url)
        {
            WebClient wc = new WebClient();
            return wc.DownloadString(url);
        }
        public static string HttpPost(string url, byte[] data)
        {
            WebClient wc = new WebClient();
            wc.Headers["content-type"] = "text/plain;charset=UTF-8";
            byte[] retdata;
            try
            {
                retdata = wc.UploadData(url, "POST", data);
            }
            catch (Exception e)
            {
                System.Threading.Thread.Sleep(100);
                return HttpPost(url, data);
            }
            return System.Text.Encoding.UTF8.GetString(retdata);
        }
    }
}
