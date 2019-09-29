using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MeowWatcherSharp
{
    class Network
    {
        WebProxy ProxyClient = new WebProxy();
        
        public Network(string ProxyAddr)
        {
            if (ProxyAddr != "")
            {
                ProxyClient.Address = new Uri(ProxyAddr);
            }
        }
        public void Post(string URLAddr,string PostArgs)
        {
            HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(URLAddr);
            Request.ContentType = "application/x-www-form-urlencoded";
            Request.Method = "POST";
            Request.Proxy = this.ProxyClient;

            byte[] PostData = Encoding.UTF8.GetBytes(PostArgs);
            Request.ContentLength = PostData.Length;

            Stream RequestStream = Request.GetRequestStream();
            RequestStream.Write(PostData, 0, PostData.Length);
            RequestStream.Close();

            HttpWebResponse resp = (HttpWebResponse)Request.GetResponse();
            Stream ResponseStream = resp.GetResponseStream();
            StreamReader ResponseStreamReader = new StreamReader(ResponseStream, Encoding.UTF8);
            var Result = ResponseStreamReader.ReadToEnd();
        }
    }
}
