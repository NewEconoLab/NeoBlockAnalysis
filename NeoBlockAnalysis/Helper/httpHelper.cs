using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NeoBlockAnalysis
{
    class httpHelper
    {
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

        public static async Task<string> HttpGetAsyncy(string url)
        {
            WebClient wc = new WebClient();
            return await wc.DownloadStringTaskAsync(url);
        }
        public static async Task<string> HttpPostAsyncy(string url, byte[] data)
        {
            WebClient wc = new WebClient();
            wc.Headers["content-type"] = "text/plain;charset=UTF-8";
            byte[] retdata = await wc.UploadDataTaskAsync(url, "POST", data);
            return System.Text.Encoding.UTF8.GetString(retdata);
        }
    }
}
