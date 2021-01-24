﻿using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace Glimmr.Models.ColorTarget {
	public interface IStreamingDevice {
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
        
		public StreamingData Data { get; set; }
        
		public void StartStream(CancellationToken ct);

		public void StopStream();

		public void SetColor(List<Color> leds, List<Color> sectors, double fadeTime);

		public void FlashColor(Color color);

		public bool IsEnabled() {
			return Enable;
		}

		public void ReloadData();

		public void Dispose();
	}
}