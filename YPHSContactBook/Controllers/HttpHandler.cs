using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Threading.Tasks;

using System.IO;
using System.IO.Compression;
using System.Text;

namespace YPHSContactBook {
    class HttpHandler {
        //HttpClient, one for each program
        private static HttpClientHandler handler;
        private static HttpClient client;
        private static CookieContainer cookieJar = new CookieContainer();
        private static bool initialized = false;
        private static readonly bool debugMode = false;
        private static string sessionID = "";

        string url;
        string eventValidation = "";
        string viewState = "";
        string viewStateGenerator = "";
        StringContent content;

        private HttpRequestMessage postRequest = new HttpRequestMessage();


        private string UpperUrlEncode(string str) {
            if (str == null) return "";
            //upper case for hex numbers
            char[] result = HttpUtility.UrlEncode(str).ToCharArray();
            for(int i = 0; i < result.Length; i++) {
                if (result[i] == '%') {
                    result[i + 1] = char.ToUpper(result[i + 1]);
                    result[i + 2] = char.ToUpper(result[i + 2]);
                }
            }
            return new string(result);
        }

        private async Task<string> DecodeResponseHTML(HttpResponseMessage response) {
            string result = "";
            using (Stream receiveStream = await response.Content.ReadAsStreamAsync()) {
                using (GZipStream gzip = new GZipStream(receiveStream, CompressionMode.Decompress)) {
                    using (StreamReader sr = new StreamReader(gzip, Encoding.UTF8)) {
                        result += sr.ReadToEnd();
                    }
                }
            }
            return result;
        }

        private string CreateLoginForm(string account,  string password, string classID) {
            GetViewstate();
            string result = "";

            result += "__VIEWSTATE=" + UpperUrlEncode(viewState) + "&";
            result += "__VIEWSTATEGENERATOR=" + UpperUrlEncode(viewStateGenerator) + "&";
            result += "__EVENTVALIDATION=" + UpperUrlEncode(eventValidation) + "&";
            result += "tbox_acc=" + account + "&";
            result += "tbox_pwd=" + password + "&";
            result += "tbox_cls=" + classID + "&";
            result += "but_login=" + "%E7%99%BB%E3%80%80%E3%80%80%E5%85%A5";
            if(debugMode) Console.WriteLine("Login form: {0}\n", result);

            return result;
        }

        private string CreateAddContactBookForm() {
            GetViewstate();
            string result = "";
            result += "__EVENTTARGET=&";
            result += "__EVENTARGUMENT=&";
            result += "__VIEWSTATE=" + UpperUrlEncode(viewState) + "&";
            result += "__VIEWSTATEGENERATOR=" + UpperUrlEncode(viewStateGenerator) + "&";
            result += "__EVENTVALIDATION=" + UpperUrlEncode(eventValidation) + "&";
            result += "but_add=%E6%96%B0%E5%A2%9E";

            if (debugMode) Console.WriteLine("Add contact book form: {0}\n", result);
            return result;
        }   

        private string CreateContactBookForm(string title, string contactBookContent, string externalLink, string currentWeb) {
            GetViewstate(currentWeb);
            string result = "";

            result += "__VIEWSTATE=" + UpperUrlEncode(viewState) + "&";
            result += "__VIEWSTATEGENERATOR=" + UpperUrlEncode(viewStateGenerator) + "&";
            result += "__EVENTVALIDATION=" + UpperUrlEncode(eventValidation) + "&";
            result += "but_save=%E5%84%B2%E5%AD%98&";
            result += "tbox_purport=" + UpperUrlEncode(title) + "&";
            result += "tbox_content=" + UpperUrlEncode(contactBookContent) + "&";
            result += "tbox_link=" + UpperUrlEncode(externalLink);

            if (debugMode) Console.WriteLine("Contact book form: {0}\n", result);

            return result;
        }

        private void SetHttpPostRequest() {
            postRequest = new HttpRequestMessage {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url)
            };
            
            //set up others
            postRequest.Headers.Host = "www.yphs.tp.edu.tw";
            postRequest.Headers.Connection.ParseAdd("keep-alive");
            postRequest.Headers.Add("Origin", "http://www.yphs.tp.edu.tw");
            postRequest.Headers.Accept.ParseAdd("text/html");
            postRequest.Headers.Accept.ParseAdd("application/xhtml+xml");
            postRequest.Headers.Accept.ParseAdd("application/xml;q=0.9");
            postRequest.Headers.Accept.ParseAdd("image/webp,image/apng");
            postRequest.Headers.Accept.ParseAdd("*/*;q=0.8");
            postRequest.Headers.Accept.ParseAdd("application/signed-exchange;v=b3");
            postRequest.Headers.Referrer = new Uri(url);
            postRequest.Headers.AcceptEncoding.ParseAdd("gzip");
            postRequest.Headers.AcceptEncoding.ParseAdd("deflate");
            postRequest.Headers.AcceptLanguage.ParseAdd("zh-TW");
            postRequest.Headers.AcceptLanguage.ParseAdd("zh;q=0.9");
            postRequest.Headers.AcceptLanguage.ParseAdd("en-US;q=0.8");
            postRequest.Headers.AcceptLanguage.ParseAdd("en-US;q=0.7");
            CacheControlHeaderValue cacheControl = new CacheControlHeaderValue();
            cacheControl.MaxAge = new TimeSpan(0);
            postRequest.Headers.CacheControl = cacheControl;
            postRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.108 Safari/537.36");
            postRequest.Headers.Add("Upgrade-Insecure-Requests", "1");
        }

        private void GetViewstate() {
            if (!initialized) Console.WriteLine("HttpHandler not initalized");
            HttpResponseMessage response = client.GetAsync(url).Result;
            if (response.StatusCode != HttpStatusCode.OK) {
                Console.WriteLine("GetViewstateAsync failed to GET . Status code: {0}", response.StatusCode.ToString());
                return;
            }

            string content = response.Content.ReadAsStringAsync().Result;

            string searchKey = "id=\"__EVENTVALIDATION\" value=\"";
            int begin = content.IndexOf(searchKey) + searchKey.Length; 
            int end = content.IndexOf('\"', begin);
            eventValidation = content.Substring(begin, end - begin);
            if (debugMode) Console.WriteLine("__EVENTVALIDATION value: {0}", eventValidation);

            searchKey = "id=\"__VIEWSTATE\" value=\"";
            begin = content.IndexOf(searchKey) + searchKey.Length;
            end = content.IndexOf('\"', begin);
            viewState = content.Substring(begin, end - begin);
            if (debugMode) Console.WriteLine("__VIEWSTATE value: {0}", viewState);

            searchKey = "id=\"__VIEWSTATEGENERATOR\" value=\"";
            begin = content.IndexOf(searchKey) + searchKey.Length;
            end = content.IndexOf('\"', begin);
            viewStateGenerator = content.Substring(begin, end - begin);
            if (debugMode) Console.WriteLine("__VIEWSTATEGENERATOR value: {0}", viewStateGenerator);

            Console.WriteLine("GetViewstateAsync() completed");
            if (debugMode) Console.WriteLine();
        }

        private void GetViewstate(string html) {
            string searchKey = "id=\"__EVENTVALIDATION\" value=\"";
            int begin = html.IndexOf(searchKey) + searchKey.Length;
            int end = html.IndexOf('\"', begin);
            eventValidation = html.Substring(begin, end - begin);
            if (debugMode) Console.WriteLine("__EVENTVALIDATION value: {0}", eventValidation);

            searchKey = "id=\"__VIEWSTATE\" value=\"";
            begin = html.IndexOf(searchKey) + searchKey.Length;
            end = html.IndexOf('\"', begin);
            viewState = html.Substring(begin, end - begin);
            if (debugMode) Console.WriteLine("__VIEWSTATE value: {0}", viewState);

            searchKey = "id=\"__VIEWSTATEGENERATOR\" value=\"";
            begin = html.IndexOf(searchKey) + searchKey.Length;
            end = html.IndexOf('\"', begin);
            viewStateGenerator = html.Substring(begin, end - begin);
            if (debugMode) Console.WriteLine("__VIEWSTATEGENERATOR value: {0}", viewStateGenerator);

            Console.WriteLine("GetViewstateAsync() completed");
            if (debugMode) Console.WriteLine();
        }

        public bool Login(string account, string password, string classID) {  //return true when logged in successfully
            if (!initialized) Console.WriteLine("HttpHandler not initalized");
            //set up request
            SetHttpPostRequest();
            SetHttpPostRequest();
            string contentString = CreateLoginForm(account, password, classID);
            content = new StringContent(contentString);
            content.Headers.ContentLength = contentString.Length;
            content.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";
            postRequest.Content = content;
            //print result
            if(debugMode) Console.WriteLine(postRequest.ToString());
            HttpResponseMessage response = client.SendAsync(postRequest).Result;
            Console.WriteLine(response.StatusCode);

            foreach (Cookie cookie in cookieJar.GetCookies(new Uri("http://www.yphs.tp.edu.tw"))) {
                Console.WriteLine("{0}: {1}", cookie.Name, cookie.Value);
                sessionID = cookie.Value;
            }

            return (cookieJar.GetCookies(new Uri("http://www.yphs.tp.edu.tw")).Count != 0);
        }


        public void PostContactBook(string title, string contactBookContent = "", string externalLink = "") {
            if (!initialized) Console.WriteLine("HttpHandler not initalized");
            SetHttpPostRequest();
            //imitate the "新增" action
            string contentString = CreateAddContactBookForm();
            content = new StringContent(contentString);
            content.Headers.ContentLength = contentString.Length;
            content.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";
            postRequest.Content = content;
            postRequest.Headers.Add("Cookie", "ASP.NET_SessionId=" + sessionID);
            HttpResponseMessage response = client.SendAsync(postRequest).Result;
            if(debugMode)  Console.WriteLine(response.StatusCode);
            //new VIEWSTATE after "clicking" the "新增" button
            string newWeb = DecodeResponseHTML(response).Result;
            if(debugMode) Console.WriteLine(newWeb);
            //create the contact book
            SetHttpPostRequest();
            contentString = CreateContactBookForm(title, contactBookContent, externalLink, newWeb);     
            content = new StringContent(contentString);
            content.Headers.ContentLength = contentString.Length;
            content.Headers.ContentType.MediaType = "application/x-www-form-urlencoded";
            postRequest.Content = content;
            postRequest.Headers.Add("Cookie", "ASP.NET_SessionId=" + sessionID);
            if(debugMode) Console.WriteLine(postRequest.ToString());
            response = client.SendAsync(postRequest).Result;
            if(debugMode) Console.WriteLine(response.StatusCode);
            if(debugMode) Console.WriteLine(response.ToString());
            Console.WriteLine("Contact book posted");
        }

        public static void Initialize() {
            if(!initialized) {
                handler = new HttpClientHandler {
                    UseCookies = true,
                    CookieContainer = cookieJar
                };
                client = new HttpClient(handler);
                initialized = true;
            } else {
                Console.WriteLine("HttpHandler class had already been initialized");
            }
        }

        public HttpHandler(string url) {
            this.url = url;
        }
    }
}
