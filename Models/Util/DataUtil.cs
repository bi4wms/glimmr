﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.DreamScreen;
using HueDream.Models.DreamScreen.Devices;
using HueDream.Models.LED;
using HueDream.Models.StreamingDevice;
using HueDream.Models.StreamingDevice.Hue;
using HueDream.Models.StreamingDevice.LIFX;
using HueDream.Models.StreamingDevice.Nanoleaf;
using HueDream.Models.StreamingDevice.WLed;
using LifxNet;
using LiteDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.X509.Qualified;
using ZedGraph;

namespace HueDream.Models.Util {
    [Serializable]
    public static class DataUtil {
        public static bool scanning { get; set; }
        private static LiteDatabase _db;
        
        public static LiteDatabase GetDb() {
            if (_db == null) _db = new LiteDatabase(@"./store.db");
            return _db;
        }

        public static void Dispose() {
            LogUtil.Write("DISPOSING DATABASE.");
            _db?.Commit();
            _db?.Dispose();
        }

        public static void DbDefaults(LifxClient lc) {
            var db = GetDb();
            // Check to see if we have our system data object
            var defaultSet = GetItem("DefaultSet");
            if (defaultSet == null || defaultSet == false) {
                LogUtil.Write("Starting to create defaults.");
                // If not, create it
                var sd = new SystemData(true);
                foreach (var v in sd.GetType().GetProperties()) {
                    LogUtil.Write("Setting: " + v.Name);
                    SetItem(v.Name, v.GetValue(sd));
                }

                var dsIp = GetItem("DsIp");
                var ledData = new LedData(true);
                var myDevice = new DreamScreen4K(dsIp);
                myDevice.SetDefaults();
                myDevice.Id = dsIp;
                SetObject("LedData", ledData);
                SetObject("MyDevice", myDevice);
                LogUtil.Write("Loading first...");
                // Get/create our collection of Dream devices
                var d = db.GetCollection<BaseDevice>("devices");
                // Create our default device
                // Save it
                d.Upsert(myDevice.Id, myDevice);
                d.EnsureIndex(x => x.Id);
                db.Commit();
                // Scan for devices
                ScanDevices(lc).ConfigureAwait(false);
            } else {
                LogUtil.Write("Defaults are already set.");
            }
        }
       
        //fixed
        public static List<dynamic> GetCollection(string key) {
            try {
                var db = GetDb();
                var coll = db.GetCollection(key);
                var output = new List<dynamic>();
                if (coll == null) return output;
                output.AddRange(coll.FindAll());
                return output;
            } catch (Exception e) {
                LogUtil.Write($@"Get exception for {key}: {e.Message}");
                return null;
            }
        }
        //fixed
        public static List<T> GetCollection<T>() where T : class {
            try {
                var db = GetDb();
                var coll = db.GetCollection<T>();
                var output = new List<T>();
                if (coll == null) return output;
                output.AddRange(coll.FindAll());
                return output;
            } catch (Exception e) {
                LogUtil.Write($@"Get exception for {typeof(T)}: {e.Message}");
                return null;
            }
        }
        //fixed
        public static List<T> GetCollection<T>(string key) where T : class {

            var db = GetDb();
            var coll = db.GetCollection<T>(key);
            var output = new List<T>();
            if (coll == null) return output;
            output.AddRange(coll.FindAll());
            return output;
            
        }
        //fixed
        public static dynamic GetCollectionItem<T>(string key, string value) where T : new() {
            try {
                var db = GetDb();
                var coll = db.GetCollection<T>(key);
                    var r = coll.FindById(value);
                    return r;
                
            } catch (Exception e) {
                LogUtil.Write($@"Get exception for {typeof(T)}: {e.Message}");
                return null;
            }
        }
        //fixed
        public static void InsertCollection<T>(string key, dynamic value) where T: class {
            var db = GetDb();
            var coll = db.GetCollection<T>(key);
            coll.Upsert(value.Id, value);
            db.Commit();
        }
        //fixed
        public static void InsertCollection(string key, dynamic value) {
                var db = GetDb();
                var coll = db.GetCollection(key);
                coll.Upsert(value.Id, value);
                db.Commit();
        }

        public static void InsertDsDevice(BaseDevice dev) {
            if (dev == null) throw new ArgumentException("Invalid device.");
            var db = GetDb();
            var ex = db.GetCollection<BaseDevice>("devices");
            ex.Upsert(dev);
            db.Commit();
        }
        

        public static void CheckDefaults(LifxClient lc) {
            var db = GetDb();
            var sc = db.GetCollection<SystemData>("system");
            LogUtil.Write("SC: " + JsonConvert.SerializeObject(sc.ToString()));
            try {
                sc.Query().First();
                LogUtil.Write("We should be set up already.");
            } catch (InvalidOperationException) {
                LogUtil.Write("Creating defaults.");
                DbDefaults(lc);
            } catch (NullReferenceException) {
                LogUtil.Write("Creating defaults.");
                DbDefaults(lc);
            }
        }

        
        public static string GetDeviceSerial() {
            var serial = string.Empty;
            try {
                serial = GetItem("Serial");
            } catch (KeyNotFoundException) {

            }

            if (string.IsNullOrEmpty(serial)) {
                Random rd = new Random();
                serial = "12091" + rd.Next(0, 9) + rd.Next(0, 9) + rd.Next(0, 9);
                SetItem("Serial", serial);
            }

            return serial;
        }

        
        public static string GetStoreSerialized() {
            var db = GetDb();
            var cols = db.GetCollectionNames();
            var output = new Dictionary<string, List<dynamic>>();
            foreach (var col in cols) {
                var collection = db.GetCollection(col);
                var list = collection.FindAll().ToList();
                var lList = new List<dynamic>();
                foreach (var l in list) {
                    var jObj = LiteDB.JsonSerializer.Serialize(l);
                    var json = JObject.Parse(jObj);
                    lList.Add(json);
                }
                output[col] = lList;
            }

            return JsonConvert.SerializeObject(output);
        }

        public static BaseDevice GetDeviceData() {
            var dd = GetDb();
            BaseDevice dev;
            var devs = dd.GetCollection<BaseDevice>("devices");
            var devType = GetItem("DevType");
            string dsIp = GetItem("DsIp");
            if (devType == "SideKick") {
                dev = (SideKick) devs.FindOne(x => x.IpAddress == dsIp);
            } else if (devType == "DreamScreen4K") {
                dev = (DreamScreen4K) devs.FindOne(x => x.IpAddress == dsIp);
            } else {
                dev = (Connect) devs.FindOne(x => x.IpAddress == dsIp);
            }

            if (string.IsNullOrEmpty(dev.AmbientColor)) {
                dev.AmbientColor = "FFFFFF";
            }
            return dev;
        }

        public static void SetItem<T>(string key, dynamic value) {
            SetItem(key, value);
        }

        
        public static dynamic GetItem<T>(string key) {
            return (T) GetItem(key);
        }
        
        public static void SetItem(string key, dynamic value) {
            var db = GetDb();
            var col = db.GetCollection(key);
            col.Insert(new BsonDocument { ["value"] = value });
            db.Commit();
        }
        
        public static void SetObject(string key, dynamic value) {
            var db = GetDb();
            var doc = BsonMapper.Global.ToDocument(value);
            var col = db.GetCollection(key);
            col.Upsert(0, doc);
            db.Commit();
        }

        public static dynamic GetItem(string key) {
            var db = GetDb();
            var col = db.GetCollection(key);
            foreach(var doc in col.FindAll()) {
                return doc["value"];
            }

            return null;
        }
        
        public static dynamic GetObject<T>(string key) {
            var db = GetDb();
            var col = db.GetCollection<T>(key);
            foreach(var doc in col.FindAll()) {
                return doc;
            }

            return null;
        }

        
        
        
        
        public static List<BaseDevice> GetDreamDevices() {
            var dd = GetDb();
            var output = new List<BaseDevice>();
            var devs = dd.GetCollection<BaseDevice>("devices");
            var dl = devs.FindAll();
            if (dl == null) return output;
            foreach (var dev in dl) {
                var tag = dev.Tag;
                switch (tag) {
                    case "SideKick":
                        output.Add((SideKick) dev);
                        break;
                    case "Connect":
                        output.Add((Connect) dev);
                        break;
                    case "DreamScreen":
                        output.Add((DreamScreenHd) dev);
                        break;
                    case "DreamScreen4K":
                        output.Add((DreamScreen4K) dev);
                        break;
                    case "DreamScreenSolo":
                        output.Add((DreamScreenSolo) dev);
                        break;
                }
            }

            return output;
        }

        public static BaseDevice GetDreamDevice(string id) {
            return GetDreamDevices().FirstOrDefault(dev => dev.Id == id);
        }

        public static (int, int) GetTargetLights() {
            var db = GetDb();
            var dsIp = GetItem("DsIp");
            var devices = db.GetCollection<BaseDevice>("devices").FindAll();
            foreach (var dev in devices) {
                var tsIp = dev.IpAddress;
                LogUtil.Write("Device IP: " + tsIp);
                if (tsIp != dsIp) continue;
                LogUtil.Write("We have a matching IP");
                var fs = dev.flexSetup;
                var dX = fs[0];
                var dY = fs[1];
                LogUtil.Write($@"DX, DY: {dX} {dY}");
                return (dX, dY);
            }

            return (0, 0);
        }

        /// <summary>
        ///     Determine if config path is local, or docker
        /// </summary>
        /// <param name="filePath">Config file to check</param>
        /// <returns>Modified path to config file</returns>
        private static string GetConfigPath(string filePath) {
            // If no etc dir, return normal path
            if (!Directory.Exists("/etc/glimmr")) return filePath;
            // Make our etc path for docker
            var newPath = "/etc/glimmr/" + filePath;
            // If the config file doesn't exist locally, we're done
            if (!File.Exists(filePath)) return newPath;
            // Otherwise, move the config to etc
            LogUtil.Write($@"Moving file from {filePath} to {newPath}");
            File.Copy(filePath, newPath);
            File.Delete(filePath);
            return newPath;
        }


        public static async void RefreshDevices(LifxClient c) {
            var cs = new CancellationTokenSource();
            cs.CancelAfter(30000);
            LogUtil.Write("Starting scan.");
            scanning = true;
            // Get dream devices
            var ld = new LifxDiscovery(c);
            var nanoTask = NanoDiscovery.Refresh(cs.Token);
            var bridgeTask = HueDiscovery.Refresh(cs.Token);
            var dreamTask = DreamDiscovery.Discover();
            var wLedTask = WledDiscovery.Discover();
            var bulbTask = ld.Refresh(cs.Token);
            try {
                await Task.WhenAll(nanoTask, bridgeTask, dreamTask, bulbTask, wLedTask);
            } catch (TaskCanceledException e) {
                LogUtil.Write("Discovery task was canceled before completion: " + e.Message, "WARN");
            } catch (SocketException f) {
                LogUtil.Write("Socket Exception during discovery: " + f.Message, "WARN");
            }
				
            LogUtil.Write("Refresh complete.");
            try {
                var leaves = nanoTask.Result;
                var bridges = bridgeTask.Result;
                var dreamDevices = dreamTask.Result;
                var bulbs = bulbTask.Result;
                var wleds = wLedTask.Result;
                var db = GetDb();
                var bridgeCol = db.GetCollection<BridgeData>("Dev_Hue");
                var nanoCol = db.GetCollection<NanoData>("Dev_Nanoleaf");
                var devCol = db.GetCollection<BaseDevice>("Dev_DreamScreen");
                var lifxCol = db.GetCollection<LifxData>("Dev_Lifx");
                var wledCol = db.GetCollection<WLedData>("Dev_Wled");
                foreach (var b in bridges) {
                    var nb = b;
                    if (b.Key !=  null && b.User != null) {
                        var n = new HueBridge(b);
                        nb = n.RefreshData(5).Result;
                        n.Dispose();
                    }
                    bridgeCol.Upsert(nb);
                }

                foreach (var b in bridgeCol.FindAll()) {
                    var nb = b;
                    if (b.Key !=  null && b.User != null) {
                        var n = new HueBridge(b);
                        nb = n.RefreshData(5).Result;
                        LogUtil.Write("Got me a bridge to update: " + nb.IpAddress);
                        bridgeCol.Upsert(nb);
                        n.Dispose();
                    }
                }
                foreach (var n in leaves) nanoCol.Upsert(n);
                foreach (var dd in dreamDevices) devCol.Upsert(dd);
                foreach (var b in bulbs) lifxCol.Upsert(b);
                foreach (var w in wleds) wledCol.Upsert(w);
                bridgeCol.EnsureIndex(x => x.Id);
                nanoCol.EnsureIndex(x => x.Id);
                devCol.EnsureIndex(x => x.Id);
                lifxCol.EnsureIndex(x => x.Id);
                wledCol.EnsureIndex(x => x.Id);
            } catch (TaskCanceledException) {

            } catch (AggregateException) {
                
            }

            scanning = false;
            cs.Dispose();
        }

        public static async Task ScanDevices(LifxClient lc) {
            if (scanning) return;
            scanning = true;
            var db = GetDb();
                // Get dream devices
                var ld = new LifxDiscovery(lc);
                var nanoTask = NanoDiscovery.Discover();
                var hueTask = HueDiscovery.Discover();
                var dreamTask = DreamDiscovery.Discover();
                var wLedTask = WledDiscovery.Discover();
                var bulbTask = ld.Discover(5);
                await Task.WhenAll(nanoTask, hueTask, dreamTask, bulbTask).ConfigureAwait(false);
                var leaves = await nanoTask.ConfigureAwait(false);
                var bridges = await hueTask.ConfigureAwait(false);
                var dreamDevices = await dreamTask.ConfigureAwait(false);
                var bulbs = await bulbTask.ConfigureAwait(false);
                var wleds = await wLedTask.ConfigureAwait(false);
                var bridgeCol = db.GetCollection<BridgeData>("Dev_Hue");
                var nanoCol = db.GetCollection<NanoData>("Dev_Nanoleaf");
                var devCol = db.GetCollection<BaseDevice>("Dev_DreamScreen");
                var lifxCol = db.GetCollection<LifxData>("Dev_Lifx");
                var wledCol = db.GetCollection<WLedData>("Dev_Wled");
                foreach (var b in bridges) bridgeCol.Upsert(b);
                foreach (var n in leaves) nanoCol.Upsert(n);
                foreach (var dd in dreamDevices) devCol.Upsert(dd);
                foreach (var b in bulbs) lifxCol.Upsert(b);
                foreach (var w in wleds) wledCol.Upsert(w);
                bridgeCol.EnsureIndex(x => x.Id);
                nanoCol.EnsureIndex(x => x.Id);
                devCol.EnsureIndex(x => x.Id);
                lifxCol.EnsureIndex(x => x.Id);
                wledCol.EnsureIndex(x => x.Id);
                scanning = false;
            
        }

        public static void RefreshPublicIp() {
            var myIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
            LogUtil.Write("My IP Address is :" + myIp);
        }
    }
}