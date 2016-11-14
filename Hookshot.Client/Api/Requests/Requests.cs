using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Collections.Specialized;

namespace Hookshot.Client.Api.Requests
{
    public class RequestInfo
    {
        public HttpMethod Method { get; set; }
        public string Path { get; set; }
        public NameValueCollection Query { get; set; } = new NameValueCollection();
        public HttpContent Content { get; set; } = new StringContent("");

        public string GetUrl(string address)
        {
            var uri = new StringBuilder($"http://{address}/{Path}");
            if (Query.HasKeys())
            {
                var kvps = Query
                    .AllKeys
                    .SelectMany(k =>
                    {
                        var values = Query.GetValues(k) ?? new string[0];
                        return values
                            .Where(v => v != null)  // Don't add items like name=null.
                            .Select(v => new
                            {
                                Name = k,
                                Value = v
                            });
                    });
                if (kvps.Any())
                    uri
                        .Append("?")
                        .Append(string.Join(
                            "&",
                            kvps.Select(kv => $"{Uri.EscapeUriString(kv.Name)}={Uri.EscapeUriString(kv.Value)}"))
                        );
            }
            return uri.ToString();
        }
    }

    public interface IRequest
    {
        RequestInfo GetRequestInfo();
    }

    public class Api : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "api",
                Query = new NameValueCollection() { { "format", "json" } },
            };
        }
    }

    public class Ping : IRequest
    {
        public string Msg { get; set; }

        public Ping() { }

        public Ping(string msg)
        {
            Msg = msg;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "ping",
                Query = new NameValueCollection() { { "msg", Msg } },
            };
        }
    }

    public class Screen : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "screen/now",
            };
        }
    }

    public class ScreenInfo : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "screen/info",
            };
        }
    }

    public class OsSchema : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "os/schema",
            };
        }
    }

    public class Os : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "os/query",
            };
        }
    }

    public class ServiceSchema : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "service/schema",
            };
        }
    }

    public class Service : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "service/query",
            };
        }
    }

    public class Sleep : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/sleep",
            };
        }
    }

    public class PowerOff : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/power-off",
            };
        }
    }

    public class DatasetsSchema : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "datasets/schema",
            };
        }
    }

    public class Dataset : IRequest
    {
        public string Name { get; set; }
        public long? From { get; set; }
        public long? To { get; set; }

        public Dataset() { }

        public Dataset(string name, long? from, long? to)
        {
            Name = name;
            From = from;
            To = to;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = $"datasets/dataset/{Name}",
                Query = new NameValueCollection()
                {
                    { "from", From?.ToString() },
                    { "to", To?.ToString() }
                }
            };
        }
    }

    public class Processes : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "processes",
            };
        }
    }

    public class KillProcess : IRequest
    {
        public long Pid { get; set; }

        public KillProcess() { }

        public KillProcess(long pid)
        {
            Pid = pid;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Delete,
                Path = $"processes/{Pid}",
            };
        }
    }

    public class Drives : IRequest
    {
        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "filesystem/drives",
            };
        }
    }

    public class Files : IRequest
    {
        public string Path { get; set; }

        public Files() { }

        public Files(string path)
        {
            Path = path;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Get,
                Path = "filesystem/files",
                Query = new NameValueCollection()
                {
                    { "path", Path }
                },
            };
        }
    }

    public class RunFile : IRequest
    {
        public string Path { get; set; }
        public string[] Args { get; set; }

        public RunFile() { }

        public RunFile(string path, string[] args)
        {
            Path = path;
            Args = args;
        }

        public RequestInfo GetRequestInfo()
        {
            var q = new NameValueCollection();
            q.Add("path", Path);
            foreach (var arg in Args ?? new string[0])
                q.Add("args", arg);

            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "filesystem/files/run",
                Query = q,
            };
        }
    }

    public class Beep : IRequest
    {
        public int? Frequency { get; set; }
        public int? Duration { get; set; }

        public Beep() { }

        public Beep(int? frequency, int? duration)
        {
            Frequency = frequency;
            Duration = duration;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/beep",
                Query = new NameValueCollection()
                {
                    { "frequency", Frequency?.ToString() },
                    { "duration", Duration?.ToString() },
                },
            };
        }
    }

    public class Speak : IRequest
    {
        public string Text { get; set; }
        public int? Rate { get; set; }
        public int? Volume { get; set; }

        public Speak() { }

        public Speak(string text, int? rate, int? volume)
        {
            Text = text;
            Rate = rate;
            Volume = volume;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/speak",
                Query = new NameValueCollection()
                {
                    { "text", Text },
                    { "rate", Rate?.ToString() },
                    { "volume", Volume?.ToString() },
                },
            };
        }
    }

    public class CdDrive : IRequest
    {
        public bool Open { get; set; }

        public CdDrive() { }

        public CdDrive(bool open)
        {
            Open = open;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/cdrom",
                Query = new NameValueCollection()
                {
                    { "action", Open ? "open" : "close" },
                },
            };
        }
    }

    public class Monitor : IRequest
    {
        public bool On { get; set; }

        public Monitor() { }

        public Monitor(bool on)
        {
            On = on;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/monitor",
                Query = new NameValueCollection()
                {
                    { "action", On ? "on" : "off" },
                },
            };
        }
    }

    public class ChangeSystemVolume : IRequest
    {
        public int VolumeChange { get; set; }
        public string Component { get; set; }
        public string Device { get; set; }

        public ChangeSystemVolume() { }

        public ChangeSystemVolume(int volumeChange, string component, string device)
        {
            VolumeChange = volumeChange;
            Component = component;
            Device = device;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/changesysvolume",
                Query = new NameValueCollection()
                {
                    { "volumeChange", VolumeChange.ToString() },
                    { "component", Component },
                    { "deviceIndex", Device },
                },
            };
        }
    }

    public class MuteSystemVolume : IRequest
    {
        public bool Mute { get; set; }
        public string Component { get; set; }
        public string Device { get; set; }

        public MuteSystemVolume() { }

        public MuteSystemVolume(bool mute, string component, string device)
        {
            Mute = mute;
            Component = component;
            Device = device;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/mutesysvolume",
                Query = new NameValueCollection()
                {
                    { "action", Mute ? "1" : "0" },
                    { "component", Component },
                    { "deviceIndex", Device },
                },
            };
        }
    }

    public class ChangeAppVolume : IRequest
    {
        public string Process { get; set; }
        public float VolumeLevel { get; set; }
        public string Device { get; set; }

        public ChangeAppVolume() { }

        public ChangeAppVolume(string process, float volumeLevel, string device)
        {
            Process = process;
            VolumeLevel = volumeLevel;
            Device = device;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/changeappvolume",
                Query = new NameValueCollection()
                {
                    { "process", Process },
                    { "volumeLevel", VolumeLevel.ToString() },
                    { "deviceIndex", Device },
                },
            };
        }
    }

    public class MuteAppVolume : IRequest
    {
        public string Process { get; set; }
        public bool Mute { get; set; }
        public string Device { get; set; }

        public MuteAppVolume() { }

        public MuteAppVolume(string process, bool mute, string device)
        {
            Process = process;
            Mute = mute;
            Device = device;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/muteappvolume",
                Query = new NameValueCollection()
                {
                    { "process", Process },
                    { "action", Mute ? "1" : "0" },
                    { "deviceIndex", Device },
                },
            };
        }
    }

    public class SetSystemVolume : IRequest
    {
        public int VolumeLevel { get; set; }
        public string Component { get; set; }
        public string Device { get; set; }

        public static int VOLUME_MAX = 65535;
        public static int VOLUME_MIN = 0;

        public SetSystemVolume() { }

        public SetSystemVolume(int volumeLevel, string component, string device)
        {
            VolumeLevel = volumeLevel;
            Component = component;
            Device = device;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/setsysvolume",
                Query = new NameValueCollection()
                {
                    { "volumeLevel", VolumeLevel.ToString() },
                    { "component", Component },
                    { "deviceIndex", Device },
                },
            };
        }
    }

    public class SetAppVolume : IRequest
    {
        public string Process { get; set; }
        public float VolumeLevel { get; set; }
        public string Device { get; set; }

        public SetAppVolume() { }

        public SetAppVolume(string process, float volumeLevel, string device)
        {
            Process = process;
            VolumeLevel = volumeLevel;
            Device = device;
        }

        public RequestInfo GetRequestInfo()
        {
            return new RequestInfo
            {
                Method = HttpMethod.Post,
                Path = "os/setappvolume",
                Query = new NameValueCollection()
                {
                    { "process", Process },
                    { "volumeLevel", VolumeLevel.ToString() },
                    { "deviceIndex", Device },
                },
            };
        }
    }

}