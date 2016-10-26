using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;

namespace raw_streams.cs
{
    class RenderStreams
    {
        public event EventHandler<UpdateStatusEventArgs> UpdateStatus=null;
        public event EventHandler<RenderFrameEventArgs> RenderFrame = null;
        public bool Playback { get; set; }
        public bool Record { get; set; }
        public String File { get; set; }
        public bool Mirror { get; set; }
        public bool Stop { get; set; }

        public PXCMCapture.DeviceInfo DeviceInfo { get; set; }

        public PXCMCapture.Device.StreamProfileSet StreamProfileSet { get; set; }
        public PXCMCapture.StreamType ColorPanel { get; set; }
        public PXCMCapture.StreamType DepthPanel { get; set; }
        public PXCMCapture.StreamType IRPanel { get; set; }
        public PXCMCapture.StreamType IRLeftPanel { get; set; }
        public PXCMCapture.StreamType IRRightPanel { get; set; }

        public bool Synced { get; set; }

        public RenderStreams()
        {
            Playback = false;
            Record = false;
            File = null;
            DeviceInfo=null;
            StreamProfileSet = null;
            Mirror = true;
            Stop = false;
            ColorPanel = PXCMCapture.StreamType.STREAM_TYPE_ANY;
            DepthPanel = PXCMCapture.StreamType.STREAM_TYPE_ANY;
            IRPanel = PXCMCapture.StreamType.STREAM_TYPE_ANY;
            IRLeftPanel = PXCMCapture.StreamType.STREAM_TYPE_ANY;
            IRRightPanel = PXCMCapture.StreamType.STREAM_TYPE_ANY;
            Synced = true;
        }

        private void SetStatus(String text) {
            EventHandler<UpdateStatusEventArgs> handler=UpdateStatus;
            if (handler!=null) {
                handler(this, new UpdateStatusEventArgs(text));
            }
        }

        public void StreamColorDepth() /* Stream Color and Depth Synchronously or Asynchronously */
        {
            try
            {
                bool sts = true;

                /* Create an instance of the PXCMSenseManager interface */
                PXCMSenseManager sm = PXCMSenseManager.CreateInstance();

                if (sm == null)
                {
                    SetStatus("Failed to create an SDK pipeline object");
                    return;
                }

                /* Optional: if playback or recoridng */
                if ((Playback || Record) && File != null)
                    sm.captureManager.SetFileName(File, Record);

                /* Optional: Set Input Source */
                if (!Playback && DeviceInfo != null)
                    sm.captureManager.FilterByDeviceInfo(DeviceInfo);

                /* Set Color & Depth Resolution and enable streams */
                if (StreamProfileSet != null)
                {
                    /* Optional: Filter the data based on the request */
                    sm.captureManager.FilterByStreamProfiles(StreamProfileSet);

                    /* Enable raw data streaming for specific stream types */
                    for (int s=0; s<PXCMCapture.STREAM_LIMIT; s++) 
                    {
                        PXCMCapture.StreamType st = PXCMCapture.StreamTypeFromIndex(s);
                        PXCMCapture.Device.StreamProfile info = StreamProfileSet[st];
                        if (info.imageInfo.format != 0)
                        {
                            /* For simple request, you can also use sm.EnableStream(...) */
                            PXCMVideoModule.DataDesc desc = new PXCMVideoModule.DataDesc();
                            desc.streams[st].frameRate.min = desc.streams[st].frameRate.max = info.frameRate.max;
                            desc.streams[st].sizeMin.height = desc.streams[st].sizeMax.height = info.imageInfo.height;
                            desc.streams[st].sizeMin.width = desc.streams[st].sizeMax.width = info.imageInfo.width;
                            desc.streams[st].options = info.options;
                            sm.EnableStreams(desc);
                        }
                    }
                }

                /* Initialization */
                Timer timer = new Timer();
                timer.UpdateStatus += UpdateStatus;

                SetStatus("Init Started");
                if (sm.Init() >= pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    /* Reset all properties */
                    sm.captureManager.device.ResetProperties(PXCMCapture.StreamType.STREAM_TYPE_ANY);

                    /* Set mirror mode */
                    PXCMCapture.Device.MirrorMode mirror = Mirror ? PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL : PXCMCapture.Device.MirrorMode.MIRROR_MODE_DISABLED;
                    sm.captureManager.device.SetMirrorMode(mirror);

                    SetStatus("Streaming");
                    while (!Stop)
                    {
                        /* Wait until a frame is ready: Synchronized or Asynchronous */
                        if (sm.AcquireFrame(Synced).IsError()) break;

                        /* Display images */
                        PXCMCapture.Sample sample = sm.QuerySample();

                        /* Render streams */
                        EventHandler<RenderFrameEventArgs> render = RenderFrame;
                        PXCMImage image = null;
                        if (ColorPanel != PXCMCapture.StreamType.STREAM_TYPE_ANY && render != null)
                        {
                            image = sample[ColorPanel];
                            render(this, new RenderFrameEventArgs(0, image));
                        }

                        if (DepthPanel != PXCMCapture.StreamType.STREAM_TYPE_ANY && render != null)
                            render(this, new RenderFrameEventArgs(1, sample[DepthPanel]));

                        if (IRPanel != PXCMCapture.StreamType.STREAM_TYPE_ANY && render != null)
                            render(this, new RenderFrameEventArgs(2, sample[IRPanel]));

                        if (IRLeftPanel != PXCMCapture.StreamType.STREAM_TYPE_ANY && render != null)
                            render(this, new RenderFrameEventArgs(3, sample[IRLeftPanel]));

                        if (IRRightPanel != PXCMCapture.StreamType.STREAM_TYPE_ANY && render != null)
                            render(this, new RenderFrameEventArgs(4, sample[IRRightPanel]));

                        /* Optional: Set Mirror State */
                        mirror = Mirror ? PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL : PXCMCapture.Device.MirrorMode.MIRROR_MODE_DISABLED;
                        if (mirror != sm.captureManager.device.QueryMirrorMode())
                            sm.captureManager.device.SetMirrorMode(mirror);

                        /* Optional: Show performance tick */
                        if (image!=null) timer.Tick(PXCMImage.PixelFormatToString(image.info.format)+" "+image.info.width+"x"+image.info.height);

                        sm.ReleaseFrame();
                    }
                }
                else
                {
                    SetStatus("Init Failed");
                    sts = false;
                }

                sm.Dispose();
                if (sts) SetStatus("Stopped");
            }
            catch (Exception e) {
                SetStatus(e.GetType().ToString());
            }
        }
    }
}
