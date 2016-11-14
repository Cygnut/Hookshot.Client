using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Json;

namespace Hookshot.Client.Api.Responses
{
    public interface IResponse
    {
        Task ParseAsync(HttpContent content);
    }

    public abstract class Empty : IResponse
    {
        public Task ParseAsync(HttpContent content)
        {
            return Task.CompletedTask;
        }
    }

    public class Api : IResponse
    {
        public class Endpoint
        {
            public string path { get; set; }
            public string method { get; set; }

            public static Endpoint Parse(JsonValue json)
            {
                return new Endpoint
                {
                    path = json["path"],
                    method = json["method"],
                };
            }
        }

        public Endpoint[] endpoints { get; set; } = new Endpoint[0];

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonObject.Parse(await content.ReadAsStringAsync())["api"] as JsonArray;
            endpoints = j == null ? new Endpoint[0] : j.Select(s => Endpoint.Parse(s)).ToArray();
        }
    }

    public class Ping : IResponse
    {
        public string msg { get; set; }

        public async Task ParseAsync(HttpContent content)
        {
            msg = JsonValue.Parse(await content.ReadAsStringAsync())["msg"];
        }
    }

    public class Screen : IResponse
    {
        public byte[] Image { get; set; } = new byte[0];

        public async Task ParseAsync(HttpContent content)
        {
            Image = await content.ReadAsByteArrayAsync();
        }
    }

    public class ScreenInfo : IResponse
    {
        public string imagePath { get; set; }
        public string whenCaptured { get; set; }
        public int period { get; set; }

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonValue.Parse(await content.ReadAsStringAsync());
            imagePath = j["imagePath"];
            whenCaptured = j["whenCaptured"];
            period = j["period"];
        }
    }

    public abstract class Schema : IResponse
    {
        string[] fields { get; set; } = new string[0];

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonValue.Parse(await content.ReadAsStringAsync())["fields"];
            fields = j.OfType<JsonValue>().Select(i => (string)i).ToArray();
        }
    }

    public class OsSchema : Schema { }

    public class Os : IResponse
    {
        public class Cpu
        {
            public class Times
            {
                public long user { get; set; }
                public long nice { get; set; }
                public long sys { get; set; }
                public long idle { get; set; }
                public long irq { get; set; }

                public static Times Parse(JsonValue json)
                {
                    return new Times
                    {
                        user = json["user"],
                        nice = json["nice"],
                        sys = json["sys"],
                        idle = json["idle"],
                        irq = json["irq"],
                    };
                }
            }

            public string model { get; set; }
            public long speed { get; set; }
            public Times times { get; set; }

            public static Cpu Parse(JsonValue json)
            {
                return new Cpu
                {
                    model = json["model"],
                    speed = json["speed"],
                    times = Times.Parse(json["times"]),
                };
            }
        }

        public string eol { get; set; }
        public string arch { get; set; }
        public string release { get; set; }
        public Cpu[] cpus { get; set; } = new Cpu[0];
        public long freemem { get; set; }
        public long usedmem { get; set; }
        public long totalmem { get; set; }
        public string uptime { get; set; }

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonValue.Parse(await content.ReadAsStringAsync())["result"];
            eol = j["eol"];
            arch = j["arch"];
            release = j["release"];
            cpus = j["cpus"].OfType<JsonValue>().Select(i => Cpu.Parse(i)).ToArray();
            freemem = j["freemem"];
            usedmem = j["usedmem"];
            totalmem = j["totalmem"];
            uptime = j["uptime"];
        }
    }

    public class ServiceSchema : Schema { }

    public class Service : IResponse
    {
        public class Memory
        {
            public long residentSetSize { get; set; }
            public long heapTotal { get; set; }
            public long heapUsed { get; set; }

            public static Memory Parse(JsonValue json)
            {
                return new Memory
                {
                    residentSetSize = json["residentSetSize"],
                    heapTotal = json["heapTotal"],
                    heapUsed = json["heapUsed"],
                };
            }
        }

        public class RuntimeVersion
        {
            public string Name { get; set; }
            public string Version { get; set; }
        }

        static RuntimeVersion[] ParseVersions(JsonValue versions)
        {
            var vs = versions as JsonObject;
            return
                vs == null
                ?
                new RuntimeVersion[0]
                :
                vs.Select(v => new RuntimeVersion { Name = v.Key, Version = v.Value }).ToArray();
        }

        public Memory memory { get; set; }
        public string version { get; set; }
        public RuntimeVersion[] versions { get; set; }

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonValue.Parse(await content.ReadAsStringAsync())["result"];
            memory = Memory.Parse(j["memory"]);
            version = j["version"];
            versions = ParseVersions(j["versions"]);
        }
    }

    public abstract class Error : IResponse
    {
        public string error { get; set; }

        public async Task ParseAsync(HttpContent content)
        {
            error = JsonValue.Parse(await content.ReadAsStringAsync())["error"];
        }
    }

    public class Sleep : Error { }

    public class PowerOff : Error { }

    public class DatasetsSchema : IResponse
    {
        public class DatasetSchema
        {
            public string name { get; set; }
            public long timestampOffset { get; set; }
            public int period { get; set; }
            public int limit { get; set; }

            public static DatasetSchema Parse(JsonValue json)
            {
                return new DatasetSchema
                {
                    name = json["name"],
                    timestampOffset = json["timestampOffset"],
                    period = json["period"],
                    limit = json["limit"],
                };
            }
        }

        public DatasetSchema[] datasets { get; set; } = new DatasetSchema[0];

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonValue.Parse(await content.ReadAsStringAsync())["datasets"];
            datasets = j.OfType<JsonValue>().Select(s => DatasetSchema.Parse(s)).ToArray();
        }
    }

    public class Dataset : IResponse
    {
        public class DataPoint
        {
            public long Timestamp { get; set; }
            public float Value { get; set; }

            public static DataPoint Parse(JsonValue json)
            {
                return new DataPoint
                {
                    Timestamp = json["timestamp"],
                    Value = (long)json["value"],
                };
            }
        }

        public DataPoint[] dataset { get; set; } = new DataPoint[0];

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonValue.Parse(await content.ReadAsStringAsync())["dataset"] as JsonArray;
            dataset = j == null ? new DataPoint[0] : j.Select(s => DataPoint.Parse(s)).ToArray();
        }
    }

    public class Processes : IResponse
    {
        public class Process
        {
            public string imageName { get; set; }
            public int pid { get; set; }
            public string sessionName { get; set; }
            public int sessionNumber { get; set; }
            public long memUsage { get; set; }
            public string status { get; set; }
            public string username { get; set; }
            public long cpuTime { get; set; }
            public string windowTitle { get; set; }

            public static Process Parse(JsonValue json)
            {
                return new Process
                {
                    imageName = json["imageName"],
                    pid = json["pid"],
                    sessionName = json["sessionName"],
                    sessionNumber = json["sessionNumber"],
                    memUsage = json["memUsage"],
                    status = json["status"],
                    username = json["username"],
                    cpuTime = json["cpuTime"],
                    windowTitle = json["windowTitle"],
                };
            }
        }

        public long lastUpdated { get; set; }
        public Process[] processes { get; set; } = new Process[0];

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonObject.Parse(await content.ReadAsStringAsync());
            lastUpdated = j["lastUpdated"];
            processes = j["processes"].OfType<JsonValue>().Select(i => Process.Parse(i)).ToArray();
        }
    }

    public class KillProcess : IResponse
    {
        public bool result { get; set; }
        public string error { get; set; }

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonObject.Parse(await content.ReadAsStringAsync());
            result = j["result"];
            error = j["error"];
        }
    }

    public class Drives : IResponse
    {
        public class Drive
        {
            public string name { get; set; }
            public string volumeName { get; set; }

            public static Drive Parse(JsonValue j)
            {
                return new Drive
                {
                    name = j["name"],
                    volumeName = j["volumeName"],
                };
            }
        }

        public Drive[] drives { get; set; } = new Drive[0];

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonValue.Parse(await content.ReadAsStringAsync())["drives"] as JsonArray;
            drives = j == null ? new Drive[0] : j.Select(s => Drive.Parse(s)).ToArray();
        }
    }

    public class Files : IResponse
    {
        public class File
        {
            public string name { get; set; }
            public string path { get; set; }
            public string ext { get; set; }
            public string type { get; set; }

            public static File Parse(JsonValue j)
            {
                return new File
                {
                    name = j["name"],
                    path = j["path"],
                    ext = j["ext"],
                    type = j["type"],
                };
            }
        }

        public File[] files { get; set; } = new File[0];

        public async Task ParseAsync(HttpContent content)
        {
            var j = JsonObject.Parse(await content.ReadAsStringAsync())["files"] as JsonArray;
            files = j == null ? new File[0] : j.Select(f => File.Parse(f)).ToArray();
        }
    }

    public class RunFile : Empty { }

    public class Beep : Error { }

    public class Speak : Error { }

    public class CdDrive : Error { }

    public class Monitor : Error { }

    public class ChangeSystemVolume : Error { }

    public class MuteSystemVolume : Error { }

    public class ChangeAppVolume : Error { }

    public class MuteAppVolume : Error { }

    public class SetSystemVolume : Error { }

    public class SetAppVolume : Error { }
}