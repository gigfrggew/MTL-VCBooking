using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace VCBooking.Services
{
    public class ZoomService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly string _accountId;
        private readonly string _clientId;
        private readonly string _clientSecret;

        public ZoomService()
        {
            _accountId = ConfigurationManager.AppSettings["ZoomAccountId"];
            _clientId = ConfigurationManager.AppSettings["Zoom:ClientId"];
            _clientSecret = ConfigurationManager.AppSettings["Zoom:ClientSecret"];
        }

        /// <summary>
        /// Get Zoom Access Token (Server-to-Server OAuth)
        /// </summary>
        public async Task<string> GetAccessTokenAsync()
        {
            var authHeader = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(string.Format("{0}:{1}", _clientId, _clientSecret))
            );

            var url = string.Format("https://zoom.us/oauth/token?grant_type=account_credentials&account_id={0}", _accountId);

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Zoom Auth Failed: " + content);

                dynamic tokenData = JsonConvert.DeserializeObject(content);
                return tokenData.access_token;
            }
        }

        /// <summary>
        /// Create Zoom Meeting
        /// </summary>
        public async Task<MeetingResponse> CreateMeetingAsync(
            string topic,
            DateTime startTime,
            int durationMinutes)
        {
            string token = await GetAccessTokenAsync();

            var meetingData = new
            {
                topic = topic,
                type = 2,
                start_time = startTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                duration = durationMinutes,
                timezone = "Asia/Kolkata",
                settings = new
                {
                    host_video = true,
                    participant_video = true,
                    join_before_host = false,
                    mute_upon_entry = true,
                    waiting_room = true
                }
            };

            var json = JsonConvert.SerializeObject(meetingData);

            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.zoom.us/v2/users/me/meetings"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Zoom Meeting Creation Failed: " + responseContent);

                return JsonConvert.DeserializeObject<MeetingResponse>(responseContent);
            }
        }

        /// <summary>
        /// Update Zoom Meeting
        /// </summary>
        public async Task UpdateMeetingAsync(
            string meetingId,
            string topic,
            DateTime startTime,
            int durationMinutes)
        {
            string token = await GetAccessTokenAsync();

            var meetingData = new
            {
                topic = topic,
                start_time = startTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                duration = durationMinutes,
                timezone = "Asia/Kolkata"
            };

            var json = JsonConvert.SerializeObject(meetingData);

            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), string.Format("https://api.zoom.us/v2/meetings/{0}", meetingId)))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception("Zoom Meeting Update Failed: " + error);
                }
            }
        }

        /// <summary>
        /// Delete Zoom Meeting
        /// </summary>
        public async Task DeleteMeetingAsync(string meetingId)
        {
            string token = await GetAccessTokenAsync();

            using (var request = new HttpRequestMessage(HttpMethod.Delete, string.Format("https://api.zoom.us/v2/meetings/{0}", meetingId)))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        try
                        {
                            dynamic errorObj = JsonConvert.DeserializeObject(error);
                            if (errorObj != null && errorObj.code != null && errorObj.code.ToString() == "3001")
                            {
                                // Meeting is already deleted or does not exist, safe to ignore
                                return;
                            }
                        }
                        catch
                        {
                            // Ignore if parse fails, fall through to throw
                        }
                    }
                    throw new Exception("Zoom Delete Failed: " + error);
                }
            }
        }

        /// <summary>
        /// Get Zoom Meeting
        /// </summary>
        public async Task<MeetingResponse> GetMeetingAsync(string meetingId)
        {
            string token = await GetAccessTokenAsync();

            using (var request = new HttpRequestMessage(HttpMethod.Get, string.Format("https://api.zoom.us/v2/meetings/{0}", meetingId)))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Zoom Get Meeting Failed: " + content);

                return JsonConvert.DeserializeObject<MeetingResponse>(content);
            }
        }

        public class MeetingResponse
        {
            public string id { get; set; }
            public string join_url { get; set; }
            public string start_url { get; set; }
            public string password { get; set; }
        }
    }
}