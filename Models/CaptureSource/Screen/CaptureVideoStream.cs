﻿using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;

namespace HueDream.Models.CaptureSource.Screen {
    public class CaptureVideoStream : IVideoStream {
        Mat IVideoStream.Frame { get; }

        public Task Start(CancellationToken ct) {
            throw new System.NotImplementedException();
        }
    }
}