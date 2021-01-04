﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using LifxNet;
using Serilog;

namespace Glimmr.Models.StreamingDevice.LIFX {
    public class LifxDiscovery {
        private readonly LifxClient _client;
        private List<LightBulb> _bulbs;

        public LifxDiscovery(LifxClient client) {
            _client = client;
        }

        public async Task<List<LifxData>> Discover(int timeOut) {
            if (_client == null) return new List<LifxData>();
            _bulbs = new List<LightBulb>();
            _client.DeviceDiscovered += Client_DeviceDiscovered;
            _client.StartDeviceDiscovery();
            Log.Debug("Lifx: Discovery started.");
            await Task.Delay(timeOut * 1000);
            Log.Debug("Discovery completed.");
            _client.StopDeviceDiscovery();
            return _bulbs.Select(GetBulbInfo).ToList();
        }

        public async Task<List<LifxData>> Refresh(CancellationToken ct) {
            var foo = Task.Run(() => Discover(5), ct);
            var b = await foo;
            foreach (var bulb in b) {
                var existing = DataUtil.GetCollectionItem<LifxData>("Dev_Lifx", bulb.MacAddressString);
                if (existing != null) {
                    bulb.TargetSector = existing.TargetSector;
                    bulb.Brightness = existing.Brightness;
                }
                DataUtil.InsertCollection<LifxData>("Dev_Lifx", bulb);
            }
            return DataUtil.GetCollection<LifxData>("Dev_Lifx");
        }

        private void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
            var bulb = e.Device as LightBulb;
            Log.Debug("Bulb discovered?");
            _bulbs.Add(bulb);
        }

        private LifxData GetBulbInfo(LightBulb b) {
            var state = _client.GetLightStateAsync(b).Result;
            var d = new LifxData(b) {
                Power = _client.GetLightPowerAsync(b).Result,
                Hue = state.Hue,
                Saturation = state.Saturation,
                Brightness = state.Brightness,
                Kelvin = state.Kelvin,
                TargetSector = -1
            };
            return d;
        }
    }
}