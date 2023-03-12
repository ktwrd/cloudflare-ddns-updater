using CloudFlare.Client;
using CloudFlare.Client.Api.Zones;
using CloudFlare.Client.Api.Zones.DnsRecord;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudflareDDNS
{
    public static class Program
    {
        public static HttpClient HttpClient = new HttpClient();
        public static DDNSConfig Config = new DDNSConfig();
        public const string ConfigLocation = "config.json";
        public static JsonSerializerOptions serializerOptions = new JsonSerializerOptions()
        {
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            IncludeFields = true,
            WriteIndented = true
        };
        public static void Main(string[] args)
        {
            HttpClient = new HttpClient();
            if (!File.Exists(ConfigLocation))
            {
                SaveConfig();
            }
            LoadConfig();
            SaveConfig();
            if (Config.Token.Length < 1)
            {
                Console.Error.WriteLine($"Token not set in {ConfigLocation}");
                Environment.Exit(1);
            }

            Client = new CloudFlareClient(Config.Token);
            Update().Wait();
        }
        public static CloudFlareClient Client;
        public static async Task Update()
        {
            var currentIpAddress = await FetchIpAddress();
            Config.PreviousIPAddress = currentIpAddress;
            SaveConfig();
            var zones = await Client.Zones.GetAsync();
            if (!zones.Success)
            {
                Console.Error.WriteLine("Failed to fetch zones");
                foreach (var e in zones.Errors)
                {
                    Console.Error.WriteLine($"- {e.Message}");
                }
                Environment.Exit(1);
            }
            foreach (var configZone in Config.Zones)
            {
                var filteredZones = new List<Zone>();
                foreach (var cloudflareZone in zones.Result)
                {
                    if (configZone.Id != null && cloudflareZone.Id == configZone.Id)
                    {
                        filteredZones.Add(cloudflareZone);
                        continue;
                    }

                    if (configZone.Name == cloudflareZone.Name)
                    {
                        filteredZones.Add(cloudflareZone);
                        continue;
                    }
                }

                foreach (var zone in filteredZones)
                {
                    Console.WriteLine($"{zone.Name} running");
                    var cloudflareRecords = await Client.Zones.DnsRecords.GetAsync(zone.Id);
                    if (!cloudflareRecords.Success)
                    {
                        Console.Error.WriteLine($"{zone.Name} Failed to fetch records for {zone.Id}");
                        foreach (var e in cloudflareRecords.Errors)
                        {
                            Console.Error.WriteLine($"- {e.Message}");
                        }
                        Environment.Exit(1);
                    }
                    foreach (var configRecord in configZone.Records)
                    {
                        var filteredDnsRecords = new List<DnsRecord>();
                        foreach (var cloudflareRecord in cloudflareRecords.Result)
                        {
                            if (configRecord.Id != null && cloudflareRecord.Id == configRecord.Id)
                            {
                                filteredDnsRecords.Add(cloudflareRecord);
                                continue;
                            }

                            if (configRecord.Name == cloudflareRecord.Name)
                            {
                                if (configRecord.Type.ToString().ToUpper() == cloudflareRecord.Type.ToString().ToUpper())
                                {
                                    filteredDnsRecords.Add(cloudflareRecord);
                                    continue;
                                }
                            }
                        }

                        foreach (var i in filteredDnsRecords)
                        {
                            var content = configRecord.DDNSUpdateValue.Replace("%1", currentIpAddress);
                            if (i.Content == content)
                            {
                                Console.WriteLine($"{i.Type} {i.Name} Ignored, content the same");
                                continue;
                            }
                            var mod = new ModifiedDnsRecord()
                            {
                                Type = i.Type,
                                Name = i.Name,
                                Content = content,
                                Proxied = configRecord.Proxy
                            };
                            var res = await Client.Zones.DnsRecords.UpdateAsync(zone.Id, i.Id, mod);
                            if (res.Success)
                            {
                                Console.WriteLine($"{i.Type} {i.Name} Record Updated to \"{content}\"");
                            }
                            else
                            {
                                Console.Error.WriteLine($"{i.Type} {i.Name} Failed to update record");
                                foreach (var e in res.Errors)
                                {
                                    Console.Error.WriteLine($"- {e.Message}");
                                }
                                Environment.Exit(1);
                            }
                        }
                    }
                }
            }
        }
        public static void SaveConfig()
        {
            File.WriteAllText(ConfigLocation, JsonSerializer.Serialize(Config, serializerOptions));
        }
        public static void LoadConfig()
        {
            var content = File.ReadAllText(ConfigLocation);
            Config = JsonSerializer.Deserialize<DDNSConfig>(content, serializerOptions);
        }
        public static async Task<string> FetchIpAddress()
        {
            var response = await HttpClient.GetAsync("https://myip.wtf/text");
            var stringContent = response.Content.ReadAsStringAsync().Result;
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return stringContent.Split("\n")[0].Split("\r")[0];
            }
            else
            {
                throw new Exception(stringContent);
            }
        }
    }
}