using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KR_VU1_Server
{
    public class Backlight
    {
        public int red { get; set; }
        public int green { get; set; }
        public int blue { get; set; }
    }

    public class DialInfo
    {
        public string uid { get; set; } = string.Empty;
        public string dial_name { get; set; } = string.Empty;
        public int value { get; set; }
        public Backlight backlight { get; set; } = new Backlight();
        public object? image_file { get; set; }
    }

    public class VU1JSONDialListResponse
    {
        public string? status { get; set; }
        public List<DialInfo>? data { get; set; }
    }

    public class VU1JSONStandardResponse
    {
        public string? status { get; set; }
    }

    public class VU1_Server
    {
        private readonly String _server_ip = "localhost";
        private readonly int _server_port = 5340;
        private readonly String _api_key = "";
        private List<DialInfo> gDialInfo = new List<DialInfo>();
        private readonly object _dialInfoLock = new object();
        private readonly HttpClient _httpClient;

        public VU1_Server(String API_Key) :
            this("localhost", 5340, API_Key)
        { }

        public VU1_Server(String ServerIP = "localhost", int ServerPort = 5340, string API_Key = "", HttpClient? httpClient = null)
        {
            _server_ip = ServerIP;
            _server_port = ServerPort;
            _api_key = API_Key;

            _httpClient = httpClient ?? new HttpClient();
        }

        public String get_api_url()
        {
            return String.Format("http://{0}:{1}/api/v0", _server_ip, _server_port);
        }

        public String get_api_key()
        {
            return _api_key;
        }


        public bool RefreshDialList()
        {
            return RefreshDialListAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> RefreshDialListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string url = String.Format(
                    "{0}/dial/list?key={1}",
                    get_api_url(),
                    Uri.EscapeDataString(_api_key));
                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (String.IsNullOrWhiteSpace(content)) return false;

                    VU1JSONDialListResponse? dialData = JsonConvert.DeserializeObject<VU1JSONDialListResponse>(content);
                    if (dialData == null)
                    {
                        return false;
                    }

                    lock (_dialInfoLock)
                    {
                        gDialInfo = dialData.data ?? new List<DialInfo>();
                    }

                    return true;

                }
                else
                {
                    Trace.WriteLine($"HTTP Code: {response.StatusCode}");
                    return false;
                }


            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (HttpRequestException e)
            {
                Trace.WriteLine(e.ToString());
                return false;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
                return false;
            }
        }


        public List<DialInfo> GetDialList()
        {
            lock (_dialInfoLock)
            {
                return new List<DialInfo>(gDialInfo);
            }
        }


        public bool UpdateDialName(string uid, string dialName)
        {
            return UpdateDialNameAsync(uid, dialName).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateDialNameAsync(string uid, string dialName, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(dialName)) return false;
            if (String.IsNullOrWhiteSpace(uid)) return false;

            string url = String.Format(
                "{0}/dial/{1}/name?name={2}&key={3}",
                get_api_url(),
                Uri.EscapeDataString(uid),
                Uri.EscapeDataString(dialName),
                Uri.EscapeDataString(_api_key));

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }


        public bool UpdateDialValue(string uid, int val)
        {
            return UpdateDialValueAsync(uid, val).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateDialValueAsync(string uid, int val, CancellationToken cancellationToken = default)
        {
            if (uid == null || uid == "") return false;

            val = Math.Clamp(val, 0, 100);
            string url = String.Format(
                "{0}/dial/{1}/set?value={2}&key={3}",
                get_api_url(),
                Uri.EscapeDataString(uid),
                val,
                Uri.EscapeDataString(_api_key));

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }


        public bool UpdateDialBacklight(string uid, int red, int green, int blue, bool values_as_percent=false)
        {
            return UpdateDialBacklightAsync(uid, red, green, blue, values_as_percent).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateDialBacklightAsync(string uid, int red, int green, int blue, bool values_as_percent = false, CancellationToken cancellationToken = default)
        {
            if (uid == null || uid == "") return false;

            if(values_as_percent)
            {
                red = Math.Clamp(red, 0, 100);
                green = Math.Clamp(green, 0, 100);
                blue = Math.Clamp(blue, 0, 100);
            }
            else
            {
                red = Math.Clamp(red, 0, 255);
                green = Math.Clamp(green, 0, 255);
                blue = Math.Clamp(blue, 0, 255);

                red = (int)Math.Ceiling((decimal)(red * 100 / 255));
                green = (int)Math.Ceiling((decimal)(green * 100 / 255));
                blue = (int)Math.Ceiling((decimal)(blue * 100 / 255));
            }

            string url = String.Format(
                "{0}/dial/{1}/backlight?red={2}&green={3}&blue={4}&white=0&key={5}",
                get_api_url(),
                Uri.EscapeDataString(uid),
                red,
                green,
                blue,
                Uri.EscapeDataString(_api_key));

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }


        public bool UpdateDialBackgroundImage(String uid, string filepath)
        {
            return UpdateDialBackgroundImageAsync(uid, filepath).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateDialBackgroundImageAsync(String uid, string filepath, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(uid) || String.IsNullOrWhiteSpace(filepath))
            {
                return false;
            }

            if (!File.Exists(filepath))
            {
                return false;
            }

            try
            {
                string url = String.Format("{0}/dial/{1}/image/set", get_api_url(), Uri.EscapeDataString(uid));
                using var stream = File.OpenRead(filepath);
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(get_api_key()), "key");
                content.Add(new StreamContent(stream), "imgfile", Path.GetFileName(filepath));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (HttpRequestException err)
            {
                Trace.WriteLine(err.Message);
                return false;
            }
            catch (IOException err)
            {
                Trace.WriteLine(err.Message);
                return false;
            }
            catch (Exception err)
            {
                Trace.WriteLine(err.Message);
                return false;
            }
        }
    }


}

