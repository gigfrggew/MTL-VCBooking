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
            // 1. Load the Service Account Key
            string jsonPath = HttpContext.Current.Server.MapPath("~/App_Data/google-key.json");
            if (!File.Exists(jsonPath))
                throw new Exception("Google key file missing in App_Data");

            string json = File.ReadAllText(jsonPath);
            dynamic keyData = JsonConvert.DeserializeObject(json);

            _serviceAccountEmail = keyData.client_email;
            _privateKey = keyData.private_key;

            // 2. Fetch the specific Gmail address for this account from DB
            string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;
            using (System.Data.SqlClient.SqlConnection conn = new System.Data.SqlClient.SqlConnection(connStr))
            {
                conn.Open();
                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand(
                    "SELECT VC_Email FROM VC_Account_Master WHERE VCAccountId = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", vcAccountId);
                object result = cmd.ExecuteScalar();
                
                if (result != null && !string.IsNullOrEmpty(result.ToString()))
                    _calendarId = result.ToString();
                else
                    throw new Exception("Google Account Email not found in VC_Account_Master for ID: " + vcAccountId);
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

            // Note: Service accounts on standard Gmail cannot auto-generate Meet links via API.
            // We create a plain calendar event and use the pre-configured room URL stored in the DB.
            string meetLink = string.IsNullOrEmpty(roomUrl) ? "" : roomUrl;

            var eventData = new
            {
                summary = topic,
                description = string.Format("VC Booking: {0}\nJoin: {1}", topic, meetLink),
                start = new { dateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ"), timeZone = "UTC" },
                end = new { dateTime = startTime.AddMinutes(durationMinutes).ToString("yyyy-MM-ddTHH:mm:ssZ"), timeZone = "UTC" }
            };

            var json = JsonConvert.SerializeObject(eventData);
            string url = string.Format("https://www.googleapis.com/calendar/v3/calendars/{0}/events", _calendarId);

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
            string url = string.Format("https://www.googleapis.com/calendar/v3/calendars/{0}/events/{1}", _calendarId, eventId);

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
            string url = string.Format("https://www.googleapis.com/calendar/v3/calendars/{0}/events/{1}", _calendarId, eventId);

            using (var request = new HttpRequestMessage(HttpMethod.Delete, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound && response.StatusCode != System.Net.HttpStatusCode.Gone)
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

            // Manual PKCS#8 parsing for .NET 4.8
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
                byte bt = reader.ReadByte();
                if (bt != 0x30) throw new Exception("Invalid PKCS#8 data");
                ReadLength(reader); // Outer SEQUENCE length

                // Read version INTEGER (tag=0x02, length=0x01, value=0x00)
                bt = reader.ReadByte();
                if (bt != 0x02) throw new Exception("Invalid PKCS#8 version tag");
                int versionLen = ReadLength(reader);
                reader.ReadBytes(versionLen); // Skip version value bytes

                // Algorithm identifier SEQUENCE
                bt = reader.ReadByte();
                if (bt != 0x30) throw new Exception("Invalid AlgorithmIdentifier");
                int algLen = ReadLength(reader);
                reader.ReadBytes(algLen); // Skip entire AlgorithmIdentifier contents (OID + params)

                // PrivateKey OCTET STRING
                bt = reader.ReadByte();
                if (bt != 0x04) throw new Exception("Invalid PrivateKey octet string");
                ReadLength(reader);

                // Now we are at the RSAPrivateKey (PKCS#1) inner SEQUENCE
                bt = reader.ReadByte();
                if (bt != 0x30) throw new Exception("Invalid RSAPrivateKey");
                ReadLength(reader);

                // RSAPrivateKey version INTEGER
                bt = reader.ReadByte();
                if (bt != 0x02) throw new Exception("Invalid RSAPrivateKey version tag");
                int rsaVersionLen = ReadLength(reader);
                reader.ReadBytes(rsaVersionLen); // Skip version value

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
            byte bt = reader.ReadByte();
            if (bt != 0x02) throw new Exception("Expected Integer");
            int length = ReadLength(reader);
            byte[] data = reader.ReadBytes(length);
            if (data[0] == 0x00) // Skip leading zero byte if present (ASN.1 sign bit)
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
