using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Json;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;

namespace Hookshot.Client
{
    class DiscoveryClient
    {
        public static readonly string TAG = "DiscoveryClient";

        public class DiscoveredArgs
        {
            public string App { get; set; }
            public string Hostname { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }
        }

        public delegate void DiscoveredDelegate(DiscoveredArgs args);
        public event DiscoveredDelegate Discovered;

        int Port;
        string App;

        public DiscoveryClient(int port, string app)
        {
            Port = port;
            App = app;
        }

        Thread thread;

        public void Run()
        {
            thread = new Thread(() => {
                try
                {
                    var ep = new IPEndPoint(IPAddress.Any, Port);
                    var client = new UdpClient(ep);
                    while (true)
                    {
                        try
                        {
                            var r = client.Receive(ref ep);
                            var eventArgs = Parse(r);

                            // If the discovery packet is from a different app, then ignore it.
                            if (!string.Equals(eventArgs.App, App, StringComparison.OrdinalIgnoreCase))
                                continue;

                            Discovered?.Invoke(eventArgs);
                        }
                        catch (Exception e)
                        {
                            Log.Warn(TAG, $"Failed to handle udp message with error {e}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(TAG, $"Failed to listen for udp on port {Port} with error {e}");
                }
            });
            thread.Start();
        }

        DiscoveredArgs Parse(byte[] msg)
        {
            var str = Encoding.UTF8.GetString(msg);
            var j = JsonObject.Parse(str);
            return new DiscoveredArgs
            {
                App = j["app"],
                Hostname = j["hostname"],
                Host = j["host"],
                Port = j["port"],
            };
        }
    }
}