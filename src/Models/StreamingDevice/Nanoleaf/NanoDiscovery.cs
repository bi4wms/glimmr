﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DeviceDiscovery.Models;
using Glimmr.Models.Util;
using ISocketLite.PCL.Exceptions;
using Makaretu.Dns;
using Nanoleaf.Client.Discovery;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.StreamingDevice.Nanoleaf {
    public static class NanoDiscovery {
        private static ServiceDiscovery sd;
        private static MulticastService mDns;
        private static List<NanoleafData> output;
        public static bool Discovering;

        static NanoDiscovery() {
            sd = new ServiceDiscovery();
            mDns = new MulticastService();
            output = new List<NanoleafData>();
            
            mDns.NetworkInterfaceDiscovered += (s, e) => {
                // Ask for the name of all services.
                sd.QueryServiceInstances("_nanoleafapi._tcp");
            };
            
            sd.ServiceDiscovered += (s, serviceName) => { mDns.SendQuery(serviceName, type: DnsType.PTR); };
            sd.ServiceInstanceDiscovered += ParseInstance;

        }

        private static void ParseInstance(object? o, ServiceInstanceDiscoveryEventArgs e) {
            
                var name = e.ServiceInstanceName.ToString();
                var nData = new NanoleafData {IpAddress = string.Empty};
                if (!name.Contains("nanoleafapi", StringComparison.InvariantCulture)) return;
                foreach (var msg in e.Message.AdditionalRecords) {
                    switch (msg.Type) {
                        case DnsType.A:
                            var aString = msg.ToString();
                            var aValues = aString.Split(" ");
                            nData.IpAddress = aValues[4];
                            nData.Name = aValues[0].Split(".")[0];
                            break;
                        case DnsType.TXT:
                            var txtString = msg.ToString();
                            var txtValues = txtString.Split(" ");
                            nData.Version = txtValues[5]
                                .Replace("srcvers=", string.Empty, StringComparison.InvariantCulture);
                            nData.Type = txtValues[4].Replace("md=", string.Empty, StringComparison.InvariantCulture);
                            nData.Id = txtValues[3].Replace("id=", string.Empty, StringComparison.InvariantCulture);
                            break;
                        case DnsType.AAAA:
                            var mString = msg.ToString();
                            var mValues = mString.Split(" ");
                            nData.IpV6Address = mValues[4];
                            // Remove rest of FQDN
                            nData.Name = mValues[0].Split(".")[0];
                            break;
                        case DnsType.SRV:
                            var sString = msg.ToString();
                            var sValues = sString.Split(" ");
                            nData.Port = int.Parse(sValues[6], CultureInfo.InvariantCulture);
                            nData.Hostname = sValues[7];
                            break;
                    }
                }

                if (string.IsNullOrEmpty(nData.IpAddress) && !string.IsNullOrEmpty(nData.Hostname)) {
                    nData.IpAddress = nData.Hostname;
                }

                if (!string.IsNullOrEmpty(nData.IpAddress) && !string.IsNullOrEmpty(nData.Id)) {
                    output.Add(nData);
                    Log.Debug("ADDING NANOLEAF: " + JsonConvert.SerializeObject(nData));
                }
            
        }
        
        public static async Task<List<NanoleafData>> Discover(int timeout = 5) {
            output = new List<NanoleafData>();
            mDns.Start();
            Log.Debug("Nano: Discovery Started.");
            await Task.Delay(timeout * 1000).ConfigureAwait(false);
            mDns.Stop();
            Log.Debug($"Nano: Discovery complete, found {output.Count} devices.");
            return output;
        }

        
        public static async Task<List<NanoleafData>> Refresh(CancellationToken ct) {
            var foo = Task.Run(() => Discover(), ct);
            var output2 = new List<NanoleafData>();
            var newLeaves = await foo;
            foreach (var nl in newLeaves) {
                var ex = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nanoleaf", nl.Id);
                var cp = nl;
                if (ex != null) {
                    Log.Debug("Copying existing leaf stuff...");
                    cp = nl.CopyExisting(ex);
                }
                DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", cp);
                output2.Add(cp);
            }
            return output2;
        }
    }
}