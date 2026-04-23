using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Configuration;
using System.Data.SqlClient;

namespace VCBooking.Services
{
    public class GoogleMeetService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private string _calendarId;
        private readonly string _serviceAccountEmail;
        private readonly string _privateKey;

        public GoogleMeetService(string vcAccountId)
        {
            // ✅ Load credentials from DB instead of JSON
            string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(
                    "SELECT GoogleClientEmail, GooglePrivateKey, VC_Email FROM VC_Account_Master WHERE VCAccountId = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", vcAccountId);

                SqlDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    _serviceAccountEmail = reader["GoogleClientEmail"].ToString();
                    _privateKey = reader["GooglePrivateKey"].ToString();

                    // 🔥 IMPORTANT: Fix newline formatting
                    _privateKey = _privateKey.Replace("\\n", "\n");

                    _calendarId = reader["VC_Email"].ToString();
                }
                else
                {
                    throw new Exception("Google credentials not found in VC_Account_Master");
                }
            }
        }

        public async Task<string> GetAccessTokenAsync()
        {
            var header = new { alg = "RS256", typ = "JWT" };
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var claimSet = new
            {
                iss = _serviceAccountEmail,
                scope = "https://www.googleapis.com/auth/calendar",
                aud = "https://oauth2.googleapis.com/token",
                exp = now + 3600,
                iat = now
            };

            string headerBase64 = Base64UrlEncode(JsonConvert.SerializeObject(header));
            string claimSetBase64 = Base64UrlEncode(JsonConvert.SerializeObject(claimSet));
            string input = headerBase64 + "." + claimSetBase64;

            string signature = SignWithRsa(input, _privateKey);
            string jwt = input + "." + signature;

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", jwt)
            });

            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception("Google Auth Failed: " + responseJson);

            dynamic tokenData = JsonConvert.DeserializeObject(responseJson);
            return tokenData.access_token;
        }

        public async Task<MeetingResponse> CreateMeetingAsync(string topic, DateTime startTime, int durationMinutes, string roomUrl)
        {
            string token = await GetAccessTokenAsync();

            string meetLink = string.IsNullOrEmpty(roomUrl) ? "" : roomUrl;

            var eventData = new
            {
                summary = topic,
                description = $"VC Booking: {topic}\nJoin: {meetLink}",
                start = new { dateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ"), timeZone = "UTC" },
                end = new { dateTime = startTime.AddMinutes(durationMinutes).ToString("yyyy-MM-ddTHH:mm:ssZ"), timeZone = "UTC" }
            };

            var json = JsonConvert.SerializeObject(eventData);
            string url = $"https://www.googleapis.com/calendar/v3/calendars/{_calendarId}/events";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Google Meet Creation Failed: " + responseContent);

                dynamic result = JsonConvert.DeserializeObject(responseContent);

                return new MeetingResponse
                {
                    id = result.id,
                    join_url = meetLink,
                    password = ""
                };
            }
        }

        public async Task UpdateMeetingAsync(string eventId, string topic, DateTime startTime, int durationMinutes)
        {
            string token = await GetAccessTokenAsync();

            var eventData = new
            {
                summary = topic,
                start = new { dateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ"), timeZone = "UTC" },
                end = new { dateTime = startTime.AddMinutes(durationMinutes).ToString("yyyy-MM-ddTHH:mm:ssZ"), timeZone = "UTC" }
            };

            var json = JsonConvert.SerializeObject(eventData);
            string url = $"https://www.googleapis.com/calendar/v3/calendars/{_calendarId}/events/{eventId}";

            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception("Google Meet Update Failed: " + error);
                }
            }
        }

        public async Task DeleteMeetingAsync(string eventId)
        {
            string token = await GetAccessTokenAsync();
            string url = $"https://www.googleapis.com/calendar/v3/calendars/{_calendarId}/events/{eventId}";

            using (var request = new HttpRequestMessage(HttpMethod.Delete, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode &&
                    response.StatusCode != System.Net.HttpStatusCode.NotFound &&
                    response.StatusCode != System.Net.HttpStatusCode.Gone)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception("Google Meet Delete Failed: " + error);
                }
            }
        }

        private string SignWithRsa(string input, string pemKey)
        {
            byte[] data = Encoding.UTF8.GetBytes(input);

            using (var rsa = CreateRsaProviderFromPem(pemKey))
            {
                byte[] signature = rsa.SignData(data, CryptoConfig.MapNameToOID("SHA256"));
                return Base64UrlEncode(signature);
            }
        }

        private RSACryptoServiceProvider CreateRsaProviderFromPem(string pem)
        {
            var base64 = pem.Replace("-----BEGIN PRIVATE KEY-----", "")
                            .Replace("-----END PRIVATE KEY-----", "")
                            .Replace("\n", "")
                            .Replace("\r", "")
                            .Trim();

            byte[] keyBuffer = Convert.FromBase64String(base64);

            var rsa = new RSACryptoServiceProvider();
            var parameters = DecodePkcs8PrivateKey(keyBuffer);
            rsa.ImportParameters(parameters);

            return rsa;
        }

        private RSAParameters DecodePkcs8PrivateKey(byte[] pkcs8)
        {
            using (var ms = new MemoryStream(pkcs8))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); ReadLength(reader);
                reader.ReadByte(); reader.ReadBytes(ReadLength(reader));
                reader.ReadByte(); reader.ReadBytes(ReadLength(reader));
                reader.ReadByte(); ReadLength(reader);
                reader.ReadByte(); ReadLength(reader);
                reader.ReadByte(); reader.ReadBytes(ReadLength(reader));

                return new RSAParameters
                {
                    Modulus = ReadInteger(reader),
                    Exponent = ReadInteger(reader),
                    D = ReadInteger(reader),
                    P = ReadInteger(reader),
                    Q = ReadInteger(reader),
                    DP = ReadInteger(reader),
                    DQ = ReadInteger(reader),
                    InverseQ = ReadInteger(reader)
                };
            }
        }

        private int ReadLength(BinaryReader reader)
        {
            int length = reader.ReadByte();
            if (length > 0x80)
            {
                int count = length & 0x0f;
                length = 0;
                for (int i = 0; i < count; i++)
                    length = (length << 8) | reader.ReadByte();
            }
            return length;
        }

        private byte[] ReadInteger(BinaryReader reader)
        {
            reader.ReadByte();
            int length = ReadLength(reader);
            byte[] data = reader.ReadBytes(length);

            if (data[0] == 0x00)
            {
                byte[] temp = new byte[data.Length - 1];
                Buffer.BlockCopy(data, 1, temp, 0, temp.Length);
                return temp;
            }
            return data;
        }

        private string Base64UrlEncode(string input)
        {
            return Base64UrlEncode(Encoding.UTF8.GetBytes(input));
        }

        private string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input).Split('=')[0].Replace('+', '-').Replace('/', '_');
        }

        public class MeetingResponse
        {
            public string id { get; set; }
            public string join_url { get; set; }
            public string password { get; set; }
        }
    }
}