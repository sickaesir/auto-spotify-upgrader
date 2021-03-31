using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Console = Colorful.Console;

namespace AutoSpotifyUpgrader
{
    class Program
    {
        public static readonly string SpotifyCookieDomain = "spotify.com";
        public static readonly string SpotifyUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.110 Safari/537.36";
        public static readonly string SpotifyAcceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
        public static readonly string SpotifyOverviewRegex = @"spweb\.account\.spa\[\'renderOverview\'\]\((.+)\)\;";
        public static readonly List<string> SpotifyConstCookies = new List<string>()
        {
            "__bon=MHwwfC0xMDUxMTA1MjczfC00NDE0NjQyMTQ2NnwxfDF8MXwx",
            "_fbp=fb.1.1543639395460.995421210",
            "_ga=GA1.2.591153806.1543634608",
            "_gac_UA-5784146-31=1.1543639397.EAIaIQobChMIxai67Oj93gIVBrTtCh1FlgPKEAAYASAAEgJrRvD_BwE",
            "_gaexp=GAX1.2.9QU0SF7bQMaY82V1bqIhEw.17956.0",
            "_gat_UA-5784146-31=1",
            "_gat=1",
            "_gcl_au=1.1.684062264.1543639395",
            "_gcl_aw=GCL.1543639395.EAIaIQobChMIxai67Oj93gIVBrTtCh1FlgPKEAAYASAAEgJrRvD_BwE",
            "_gcl_dc=GCL.1543639395.EAIaIQobChMIxai67Oj93gIVBrTtCh1FlgPKEAAYASAAEgJrRvD_BwE",
            "_gid=GA1.2.1563318671.1543634608",
            "fb_continue=https%3A%2F%2Fwww.spotify.com%2Fuk%2Faccount%2Foverview%2F",
            "sp_last_utm==%7B%22utm_source%22%3A%22uk-en_brand_contextual-desktop_text%22%2C%22utm_medium%22%3A%22paidsearch%22%2C%22utm_campaign%22%3A%22alwayson_eu_uk_performancemarketing_core_brand%20contextual-desktop%20text%20exact%20uk-en%20google%22%7D",
            "sp_new=1",
            "sp_usid=d8f02d9c-7a12-4ca3-aedd-7e9d47749734"
        };


        public static readonly string SpotifyCsrfEndpoint = "https://accounts.spotify.com/en/login?continue=https:%2F%2Fwww.spotify.com%2Fint%2Faccount%2Foverview%2F";
        public static readonly string SpotifyAuthEndpoint = "https://accounts.spotify.com/api/login";
        public static readonly string SpotifyOverviewEndpoint = "https://www.spotify.com/uk/account/overview/";
        public static readonly string SpotifyProfileEndpoint = "https://www.spotify.com/uk/account/profile/";
        public static readonly string SpotifyChangePasswordEndpoint = "https://www.spotify.com/uk/account/change-password/";
        public static readonly string SpotifyFamilyInfoEndpoint = "https://www.spotify.com/uk/home-hub/api/v1/family/home/";
        public static readonly string SpotifyFamilyVerifyAddressEndpoint = "https://www.spotify.com/uk/home-hub/api/v1/family/address/verify/";
        public static readonly string SpotifyFamilyOnboardEndpoint = "https://www.spotify.com/uk/home-hub/api/v1/family/home/onboard/";
        public static readonly string SpotifyFamilyMemberEndpoint = "https://www.spotify.com/it/home-hub/api/v1/family/member/";
        public static readonly string SpotifyFamilyRandomAddressEndpoint = "https://www.fakeaddressgenerator.com/World_Address/get_us_address";

        static void SetCookie(ref HttpWebRequest request, string cookieString)
        {
            string[] cookiePart = cookieString.Split("=");

            request.CookieContainer.Add(new Cookie(cookiePart[0], cookiePart[1], "/", SpotifyCookieDomain));
        }


        static async Task<CookieCollection> LoginAccount(string username, string password, WebProxy proxy = null)
        {
            CookieContainer container = new CookieContainer();
            HttpWebRequest csrfRequest = (HttpWebRequest)WebRequest.Create("https://accounts.spotify.com/en/login?continue=https:%2F%2Fwww.spotify.com%2Fint%2Faccount%2Foverview%2F");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://accounts.spotify.com/api/login");
            request.CookieContainer = container;

            if (proxy != null)
                csrfRequest.Proxy = proxy;

            //csrfRequest.CookieContainer = container;
            csrfRequest.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            csrfRequest.Accept = SpotifyAcceptHeader;
            csrfRequest.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
            csrfRequest.UserAgent = SpotifyUserAgent;

            if (proxy != null)
                csrfRequest.Proxy = proxy;

            string csrfToken = "";
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)await csrfRequest.GetResponseAsync())
                {
                    response.Cookies.Clear();
                    string cookieHeader = response.Headers["set-cookie"];

                    string[] cookiesString = cookieHeader.Split(',');

                    foreach (var cookieString in cookiesString)
                    {
                        Regex regex = new Regex(@"(.+?)=(.+?);");

                        Match match = regex.Match(cookieString);

                        Cookie cookie = new Cookie(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim(), "/", "spotify.com");

                        if (cookie.Name == "csrf_token")
                            csrfToken = cookie.Value;

                        response.Cookies.Add(cookie);
                    }

                    request.CookieContainer.Add(response.Cookies);
                }
            }
            catch (Exception)
            {
                await Task.Delay(1000);
                return await LoginAccount(username, password, proxy);
            }


            request.Method = "POST";
            request.UserAgent = csrfRequest.UserAgent;
            request.Accept = csrfRequest.Accept;
            request.AutomaticDecompression = DecompressionMethods.All;
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");

            foreach (var cookie in SpotifyConstCookies)
                SetCookie(ref request, cookie);

            SetCookie(ref request, $"remember={username}");


            if (proxy != null)
                request.Proxy = proxy;


            using (var stream = await request.GetRequestStreamAsync())
            {
                Dictionary<string, string> requestParams = new Dictionary<string, string>()
                {
                    { "remember", "true" },
                    { "username", username },
                    { "password", password },
                    { "csrf_token", csrfToken }
                };


                await new FormUrlEncodedContent(requestParams).CopyToAsync(stream);
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseContent = await reader.ReadToEndAsync();

                    response.Cookies.Clear();
                    string cookieHeader = response.Headers["set-cookie"];

                    string[] cookiesString = cookieHeader.Split(',');

                    foreach (var cookieString in cookiesString)
                    {
                        Regex regex = new Regex(@"(.+?)=(.+?);");

                        Match match = regex.Match(cookieString);

                        if (!match.Success) continue;

                        Cookie cookie = new Cookie(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim(), "/", "spotify.com");

                        if (cookie.Name == "csrf_token")
                            csrfToken = cookie.Value;

                        response.Cookies.Add(cookie);
                    }


                    return response.Cookies;
                }
            }
            catch(Exception)
            {
                return null;
            }
        }

        static async Task<KeyValuePair<string, string>?> VerifyAddress(CookieCollection cookies, string address, bool master, WebProxy proxy = null)
        {

            HttpWebRequest verifyAddressRequest = (HttpWebRequest)WebRequest.CreateHttp(SpotifyFamilyVerifyAddressEndpoint);
            verifyAddressRequest.CookieContainer = new CookieContainer();
            verifyAddressRequest.CookieContainer.Add(cookies);
            verifyAddressRequest.ContentType = "application/json;charset=utf-8";
            verifyAddressRequest.UserAgent = SpotifyUserAgent;
            verifyAddressRequest.Accept = "application/json, text/plain, */*";
            verifyAddressRequest.Headers.Add("Origin", "https://www.spotify.com");
            verifyAddressRequest.Referer = "https://www.spotify.com/uk/family/setup/";
            verifyAddressRequest.Method = "POST";

            if (proxy != null)
                verifyAddressRequest.Proxy = proxy;

            using (var requestStream = await verifyAddressRequest.GetRequestStreamAsync())
            using (var requestWriter = new StreamWriter(requestStream))
            {
                await requestWriter.WriteAsync(JsonConvert.SerializeObject(new
                {
                    address = address,
                    isMaster = master,
                    placeId = (string)null
                }));
            }

            try
            {
                using (var response = await verifyAddressRequest.GetResponseAsync())
                using (var responseReader = new StreamReader(response.GetResponseStream()))
                {
                    string responseText = await responseReader.ReadToEndAsync();

                    dynamic data = JsonConvert.DeserializeObject<dynamic>(responseText);


                    return new KeyValuePair<string, string>((string)data["placeId"], (string)data["address"]);
                }
            }
            catch (WebException)
            {
                return null;
            }
        }

        static async Task<bool?> AcceptSpotifyFamilyInvite(CookieCollection cookies, string inviteToken, KeyValuePair<string, string> addressRes, string country, WebProxy proxy = null)
        {

            string endpoint = SpotifyFamilyMemberEndpoint.Replace("/it/", $"/{country.ToLower()}/");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(cookies);
            request.Method = "POST";
            request.ContentType = "application/json;charset=utf-8";

            if (proxy != null)
                request.Proxy = proxy;

            using (var requestStream = await request.GetRequestStreamAsync())
            using (var requestWriter = new StreamWriter(requestStream))
            {
                await requestWriter.WriteAsync(JsonConvert.SerializeObject(new
                {
                    address = addressRes.Value,
                    placeId = addressRes.Key,
                    inviteToken = inviteToken
                }));
            }

            try
            {
                using (var response = await request.GetResponseAsync())
                {
                    return true;
                }
            }
            catch (WebException ex)
            {
                using(var response = ex.Response.GetResponseStream())
                using(var responseReader = new StreamReader(response))
                {
                    string responseText = await responseReader.ReadToEndAsync();

                    if(responseText.Contains("PLAN_IS_FULL"))
                    {
                        return null;
                    }

                    return false;
                }
            }

        }


        static async Task<KeyValuePair<string, string>> GetPlanName(CookieCollection cookies, WebProxy proxy = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(SpotifyOverviewEndpoint);

            if (proxy != null)
                request.Proxy = proxy;

            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(cookies);

            using (var response = await request.GetResponseAsync())
            using (var responseStream = new StreamReader(response.GetResponseStream()))
            {
                string responseString = await responseStream.ReadToEndAsync();

                Regex infoRegex = new Regex(SpotifyOverviewRegex);

                Match match = infoRegex.Match(responseString);

                string infoData = match.Groups[1].Value;

                dynamic data = JsonConvert.DeserializeObject<dynamic>(infoData);

                string plan = "";
                if (data["plan"] == null) plan = "Spotify Free";
                else plan = (string)data["plan"]["plan"]["name"];

                string username = "N/A";

                try
                {
                    foreach(var entry in data.profile.fields)
                    {
                        if((string)entry.label == "Username")
                        {
                            username = (string)entry.value;
                            break;
                        }
                    }
                }
                catch(Exception)
                { }

                return new KeyValuePair<string, string>(plan, username);
            }
        }

        static Queue<KeyValuePair<string, string>> accounts = new Queue<KeyValuePair<string, string>>();

        static void Main(string[] args)
        {
            Console.WriteLine($"Spotify Family Assigner - by Aesir - [ Nulled: SickAesir | Telegramm: @sickaesir | Discord: Aesir#1337 ]", Color.Cyan);

            if(!File.Exists("free_accounts.txt"))
            {
                Console.WriteLine($"[Error] Failed to find free_accounts.txt file!", Color.Red);

                Console.ReadLine();
                return;

            }


            {
                string[] accountLines = File.ReadAllLines("free_accounts.txt");


                foreach(var line in accountLines)
                {
                    string[] accountSplit = line.Split(":");

                    if (accountSplit.Length < 2) continue;

                    accounts.Enqueue(new KeyValuePair<string, string>(accountSplit[0], accountSplit[1]));
                }
            }

            Console.WriteLine($"[Info] Loaded {accounts.Count} accounts!", Color.Green);

            while(true)
            {
                Console.Write($"[Spotify] Input the invite link/invite token: ", Color.Orange);
                string token = Console.ReadLine();


                Console.Write($"[Spotify] Input the associated address: ", Color.Orange);
                string address = Console.ReadLine();

                Console.Write($"[Spotify] Input the target country ISO code (e.g. UK, US, AU etc.): ", Color.Orange);
                string country = Console.ReadLine();

                Regex linkRegex = new Regex(@"invite\/(.+?)$");

                if (linkRegex.IsMatch(token))
                    token = linkRegex.Match(token).Groups[1].Value;

                Console.WriteLine($"[Spotify] Loaded token {token}", Color.Green);

                Console.WriteLine($"[Spotify] Loaded address {address}, upgrading accounts...", Color.Green);

                KeyValuePair<string, string>? addressInfo = null;

                while(true)
                {
                    if(!accounts.TryDequeue(out var accountInfo))
                    {
                        Console.WriteLine($"[Spotify] No more accounts left!", Color.Yellow);
                        Console.ReadLine();


                        return;
                    }

                    Console.WriteLine($"[Spotify] Logging in {accountInfo.Key} account...", Color.Yellow);

                    var loginCookies = LoginAccount(accountInfo.Key, accountInfo.Value).Result;

                    if(loginCookies == null)
                    {
                        Console.WriteLine($"[Spotify] Account {accountInfo.Key} skipped (invalid credentials)", Color.Red);
                        continue;
                    }

                    Console.WriteLine($"[Spotify] Account {accountInfo.Key} logged in successfully!", Color.Green);

                    Console.WriteLine($"[Spotify] Verifying account subscription...", Color.Yellow);
                    string username = "";
                    {
                        var overview = GetPlanName(loginCookies).Result;
                        string subscription = overview.Key;
                        username = overview.Value;

                        if(subscription != "Spotify Free")
                        {
                            Console.WriteLine($"[Spotify] Account {accountInfo.Key} skipped (already premium, subscription: {subscription})", Color.Red);
                            continue;
                        }
                    }
                    Console.WriteLine($"[Spotify] Account subscription verified, username: {username}!", Color.Green);

                    if (!addressInfo.HasValue)
                    {
                        Console.WriteLine($"[Spotify] Verifying address {address}...", Color.Yellow);

                        addressInfo = VerifyAddress(loginCookies, address, false).Result;

                        if(!addressInfo.HasValue)
                        {
                            Console.WriteLine($"[Spotify] The specified address {address} is not recognized by Spotify!", Color.Red);
                            accounts.Enqueue(accountInfo);
                            continue;
                        }


                        Console.WriteLine($"[Spotify] Address verified, id: {addressInfo.Value.Key}", Color.Magenta);

                    }

                    Console.WriteLine($"[Spotify] Attempting upgrade...", Color.Yellow);

                    var res = AcceptSpotifyFamilyInvite(loginCookies, token, addressInfo.Value, country).Result;

                    if(res.HasValue && res.Value)
                    {
                        Console.WriteLine($"[Spotify] Account {accountInfo.Key} upgraded!", Color.Magenta);
                        File.AppendAllText("upgraded_accounts.txt", $"{username}:{accountInfo.Key}:{accountInfo.Value}\n");
                    }
                    else if(res.HasValue)
                    {
                        Console.WriteLine($"[Spotify] Failed to upgrade account {accountInfo.Key}, skipped", Color.Red);
                        accounts.Enqueue(accountInfo);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"[Spotify] Failed to upgrade account {accountInfo.Key}, plan filled up!", Color.Yellow);
                        accounts.Enqueue(accountInfo);
                        break;
                    }
                }
            }
        }
    }
}
