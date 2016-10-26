using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SampleDX;
using System.Diagnostics;

namespace raw_streams.cs
{
    public partial class MainForm : Form
    {
        private PXCMSession session;
        private volatile bool closing = false;
        private int current_device_iuid = 0;
        private Dictionary<ToolStripMenuItem, PXCMCapture.DeviceInfo> devices = new Dictionary<ToolStripMenuItem, PXCMCapture.DeviceInfo>();
        private Dictionary<ToolStripMenuItem, int> devices_iuid = new Dictionary<ToolStripMenuItem, int>();
        private Dictionary<ToolStripMenuItem, PXCMCapture.Device.StreamProfile> profiles = new Dictionary<ToolStripMenuItem, PXCMCapture.Device.StreamProfile>();
        private ToolStripMenuItem[] streamMenus = new ToolStripMenuItem[PXCMCapture.STREAM_LIMIT];
        private RadioButton[] streamButtons = new RadioButton[PXCMCapture.STREAM_LIMIT];
        private ToolStripMenuItem[] streamNone = new ToolStripMenuItem[PXCMCapture.STREAM_LIMIT];
        private int[] default_menu = new int[PXCMCapture.STREAM_LIMIT];
        private PictureBox[] streamPanels = new PictureBox[PXCMCapture.STREAM_LIMIT];
        private D2D1Render[] renders = new D2D1Render[5] { new D2D1Render(), new D2D1Render(), new D2D1Render(), new D2D1Render(), new D2D1Render() };
        private RenderStreams streaming = new RenderStreams();
        private string[] default_config = new string[]{"RGB24 640x480x60", "DEPTH 640x480x60", "Y8 640x480x60", "Y8 640x480x60", "Y8 640x480x60", "Y8 640x480x60", "Y8 640x480x60", "Y8 640x480x60" };

        public MainForm(PXCMSession session)
        {
            InitializeComponent();
            /* Put stream menu items to array */
            streamMenus[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_COLOR)] = ColorMenu;
            streamMenus[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_DEPTH)] = DepthMenu;
            streamMenus[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_IR)] = IRMenu;
            streamMenus[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_LEFT)] = LeftMenu;
            streamMenus[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_RIGHT)] = RightMenu;
            /* Put stream buttons to array */
            streamButtons[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_COLOR)] = Color;
            streamButtons[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_DEPTH)] = Depth;
            streamButtons[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_IR)] = IR;
            streamButtons[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_LEFT)] = IRLeft;
            streamButtons[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_RIGHT)] = IRRight;

            /* Put panels buttons to array */
            streamPanels[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_COLOR)] = ColorPanel;
            streamPanels[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_DEPTH)] = DepthPanel;
            streamPanels[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_IR)] = IRPanel;
            streamPanels[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_LEFT)] = IRLeftPanel;
            streamPanels[PXCMCapture.StreamTypeToIndex(PXCMCapture.StreamType.STREAM_TYPE_RIGHT)] = IRRightPanel;

            this.session = session;
            streaming.UpdateStatus += new EventHandler<UpdateStatusEventArgs>(UpdateStatusHandler);
            streaming.RenderFrame += new EventHandler<RenderFrameEventArgs>(RenderFrameHandler);
            FormClosing += new FormClosingEventHandler(FormClosingHandler);

            ColorPanel.Paint += new PaintEventHandler(PaintHandler);
            DepthPanel.Paint += new PaintEventHandler(PaintHandler);
            IRPanel.Paint += new PaintEventHandler(PaintHandler);
            IRLeftPanel.Paint += new PaintEventHandler(PaintHandler);
            IRRightPanel.Paint += new PaintEventHandler(PaintHandler);
            ColorPanel.Resize += new EventHandler(ResizeHandler);
            DepthPanel.Resize += new EventHandler(ResizeHandler);
            IRPanel.Resize += new EventHandler(ResizeHandler);
            IRLeftPanel.Resize += new EventHandler(ResizeHandler);
            IRRightPanel.Resize += new EventHandler(ResizeHandler);

            ResetStreamTypes();
            PopulateDeviceMenu();

            Scale2.CheckedChanged += new EventHandler(Scale_Checked);
            Mirror.CheckedChanged += new EventHandler(Mirror_Checked);

            foreach (RadioButton button in streamButtons)
                if(button != null) button.Click += new EventHandler(Stream_Click);
            
            renders[0].SetHWND(ColorPanel);
            renders[1].SetHWND(DepthPanel);
            renders[2].SetHWND(IRPanel);
            renders[3].SetHWND(IRLeftPanel);
            renders[4].SetHWND(IRRightPanel);
        }

        private void PopulateDeviceMenu()
        {
            devices.Clear();
            devices_iuid.Clear();

            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
            desc.group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR;
            desc.subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE;

            DeviceMenu.DropDownItems.Clear();

            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                PXCMCapture capture;
                if (session.CreateImpl<PXCMCapture>(desc1, out capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;
                for (int j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo dinfo;
                    if (capture.QueryDeviceInfo(j, out dinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                    ToolStripMenuItem sm1 = new ToolStripMenuItem(dinfo.name, null, new EventHandler(Device_Item_Click));
                    devices[sm1] = dinfo;
                    devices_iuid[sm1] = desc1.iuid;
                    DeviceMenu.DropDownItems.Add(sm1);
                }
                capture.Dispose();
            }
            if (DeviceMenu.DropDownItems.Count > 0)
            {
                (DeviceMenu.DropDownItems[0] as ToolStripMenuItem).Checked = true;
                PopulateColorDepthMenus(DeviceMenu.DropDownItems[0] as ToolStripMenuItem);
            }
            else
            {
                ModeLive.Visible = false;
                ModeRecord.Visible = false;
                Start.Enabled = false;
                for (int s = 0; s < PXCMCapture.STREAM_LIMIT; s++)
                {
                    if (streamMenus[s] != null)
                    {
                        streamMenus[s].Visible = false;
                        streamButtons[s].Visible = false;
                    }
                }
            }
        }

        private bool PopulateDeviceFromFileMenu()
        {
            devices.Clear();
            devices_iuid.Clear();

            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
            desc.group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR;
            desc.subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE;

            PXCMSession.ImplDesc desc1;
            PXCMCapture.DeviceInfo dinfo;
            PXCMSenseManager pp = PXCMSenseManager.CreateInstance();
            if (pp == null)
            {
                SetStatus("Init Failed");
                return false;
            }
            try
            {
                if (session.QueryImpl(desc, 0, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) throw null;
                if (pp.captureManager.SetFileName(streaming.File, false) < pxcmStatus.PXCM_STATUS_NO_ERROR) throw null;
                if (pp.captureManager.LocateStreams() < pxcmStatus.PXCM_STATUS_NO_ERROR) throw null;
                pp.captureManager.device.QueryDeviceInfo(out dinfo);
            }
            catch
            {
                pp.Dispose();
                SetStatus("Init Failed");
                return false;
            }
            DeviceMenu.DropDownItems.Clear();
            ToolStripMenuItem sm1 = new ToolStripMenuItem(dinfo.name, null, new EventHandler(Device_Item_Click));
            devices[sm1] = dinfo;
            devices_iuid[sm1] = desc1.iuid;
            DeviceMenu.DropDownItems.Add(sm1);

            sm1 = new ToolStripMenuItem("playback from the file : ", null);
            sm1.Enabled = false;
            DeviceMenu.DropDownItems.Add(sm1);
            sm1 = new ToolStripMenuItem(streaming.File, null);
            sm1.Enabled = false;
            DeviceMenu.DropDownItems.Add(sm1);
            if (DeviceMenu.DropDownItems.Count > 0)
                (DeviceMenu.DropDownItems[0] as ToolStripMenuItem).Checked = true;

            /* populate profile menus from the file */
            profiles.Clear();
            foreach (ToolStripMenuItem menu in streamMenus)
            {
                if (menu != null)
                    menu.DropDownItems.Clear();
            }

            PXCMCapture.Device device = pp.captureManager.device;
            if (device == null)
            {
                pp.Dispose();
                SetStatus("Init Failed");
                return false;
            }
            
            PXCMCapture.Device.StreamProfileSet profile = new PXCMCapture.Device.StreamProfileSet();

            for (int s = 0; s < PXCMCapture.STREAM_LIMIT; s++)
            {
                PXCMCapture.StreamType st = PXCMCapture.StreamTypeFromIndex(s);
                if (((int)dinfo.streams & (int)st) != 0 && streamMenus[s] != null)
                {
                    streamMenus[s].Visible = true;
                    streamButtons[s].Visible = true;
                    streamPanels[s].Visible = true;
                    int num = device.QueryStreamProfileSetNum(st);
                    for (int p = 0; p < num; p++)
                    {
                        if (device.QueryStreamProfileSet(st, p, out profile) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                        PXCMCapture.Device.StreamProfile sprofile = profile[st];
                        sm1 = new ToolStripMenuItem(ProfileToString(sprofile), null, new EventHandler(Stream_Item_Click));
                        profiles[sm1] = sprofile;
                        streamMenus[s].DropDownItems.Add(sm1);
                        Debug.WriteLine("1: " + ProfileToString(sprofile));
                    }
                }
                else if (((int)dinfo.streams & (int)st) == 0 && streamMenus[s] != null)
                {
                    streamMenus[s].Visible = false;
                    streamButtons[s].Visible = false;
                    streamPanels[s].Visible = false;
                }
            }

            for (int i = 0; i < PXCMCapture.STREAM_LIMIT; i++)
            {
                ToolStripMenuItem menu = streamMenus[i];
                if (menu != null)
                {
                    streamNone[i] = new ToolStripMenuItem("None", null, new EventHandler(Stream_Item_Click));
                    profiles[streamNone[i]] = new PXCMCapture.Device.StreamProfile();
                    menu.DropDownItems.Add(streamNone[i]);
                    (menu.DropDownItems[0] as ToolStripMenuItem).Checked = true;
                }
            }
            Start.Enabled = true;
            
            CheckSelection();
            pp.Close();
            pp.Dispose();

            StatusLabel.Text = "Ok";
            return true;
        }

        private void PopulateColorDepthMenus(ToolStripMenuItem device_item)
        {
            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
            desc.group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR;
            desc.subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE;
            desc.iuid = devices_iuid[device_item];
            current_device_iuid = desc.iuid;
            desc.cuids[0] = PXCMCapture.CUID;

            profiles.Clear();
            foreach (ToolStripMenuItem menu in streamMenus)
            {
                if (menu != null)
                    menu.DropDownItems.Clear();
            }
            
            PXCMCapture capture;
            PXCMCapture.DeviceInfo dinfo2 = GetCheckedDevice(); 
            if (session.CreateImpl<PXCMCapture>(desc, out capture) >= pxcmStatus.PXCM_STATUS_NO_ERROR) {
                PXCMCapture.Device device=capture.CreateDevice(dinfo2.didx);
                if (device!=null) {
                    PXCMCapture.Device.StreamProfileSet profile = new PXCMCapture.Device.StreamProfileSet();

                    for (int s = 0; s < PXCMCapture.STREAM_LIMIT; s++)
                    {
                        PXCMCapture.StreamType st = PXCMCapture.StreamTypeFromIndex(s);
                        if (((int)dinfo2.streams & (int)st) != 0 && streamMenus[s] != null)
                        {
                            streamMenus[s].Visible = true;
                            streamButtons[s].Visible = true;
                            streamPanels[s].Visible = true;

                            int num = device.QueryStreamProfileSetNum(st);
                            for (int p = 0; p < num; p++)
                            {
                                if (device.QueryStreamProfileSet(st, p, out profile) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                                PXCMCapture.Device.StreamProfile sprofile = profile[st];
                                ToolStripMenuItem sm1 = new ToolStripMenuItem(ProfileToString(sprofile), null, new EventHandler(Stream_Item_Click));
                                profiles[sm1] = sprofile;
                                streamMenus[s].DropDownItems.Add(sm1);
                                
                                if (sm1.Text.Contains(default_config[s]))
                                {
                                    default_menu[s] = p;
                                    Debug.WriteLine(s + " : " + p + " : " + ProfileToString(sprofile));
                                }
                            }
                        }
                        else if (((int)dinfo2.streams & (int)st) == 0 && streamMenus[s] != null)
                        {
                            streamMenus[s].Visible = false;
                            streamButtons[s].Visible = false;
                            streamPanels[s].Visible = false;
                        }
                    }
                    
                    device.Dispose();
                }
                capture.Dispose();
            }
            for (int i = 0; i < PXCMCapture.STREAM_LIMIT; i++)
            {
                ToolStripMenuItem menu = streamMenus[i];
                if (menu != null)
                {
                    streamNone[i] = new ToolStripMenuItem("None", null, new EventHandler(Stream_Item_Click));
                    profiles[streamNone[i]] = new PXCMCapture.Device.StreamProfile();
                    menu.DropDownItems.Add(streamNone[i]);
                    if (default_menu[i] != 0)
                        (menu.DropDownItems[default_menu[i]] as ToolStripMenuItem).Checked = true;
                    else
                        streamNone[i].Checked = true;
                }
            }
            
            CheckSelection();
        }

        private string StreamOptionToString(PXCMCapture.Device.StreamOption streamOption)
        {
	        switch (streamOption)
	        {
	        case PXCMCapture.Device.StreamOption.STREAM_OPTION_UNRECTIFIED:
		        return " RAW";
            case (PXCMCapture.Device.StreamOption)0x20000: // Depth Confidence
                return " + Confidence";
	        case PXCMCapture.Device.StreamOption.STREAM_OPTION_DEPTH_PRECALCULATE_UVMAP:
	        case PXCMCapture.Device.StreamOption.STREAM_OPTION_STRONG_STREAM_SYNC:
	        case PXCMCapture.Device.StreamOption.STREAM_OPTION_ANY:
		        return "";
	        default:
		        return " (" + streamOption.ToString() + ")";
	        }
        }

        private string ProfileToString(PXCMCapture.Device.StreamProfile pinfo)
        {
            string line = "Unknown ";
            if (Enum.IsDefined(typeof(PXCMImage.PixelFormat), pinfo.imageInfo.format))
                line = pinfo.imageInfo.format.ToString().Substring(13)+" "+pinfo.imageInfo.width+"x"+pinfo.imageInfo.height+"x";
            else
                line += pinfo.imageInfo.width + "x" + pinfo.imageInfo.height + "x";
            if (pinfo.frameRate.min != pinfo.frameRate.max) {
                line += (float)pinfo.frameRate.min + "-" +
                      (float)pinfo.frameRate.max;
            } else {
                float fps = (pinfo.frameRate.min!=0)?pinfo.frameRate.min:pinfo.frameRate.max;
                line += fps;
            }
            line += StreamOptionToString(pinfo.options);
            return line;
        }

        private void ResetStreamTypes()
        {
            streaming.ColorPanel=streaming.DepthPanel=PXCMCapture.StreamType.STREAM_TYPE_ANY;
            streaming.IRPanel = streaming.IRLeftPanel = streaming.IRRightPanel = PXCMCapture.StreamType.STREAM_TYPE_ANY;
        }

        private void Device_Item_Click(object sender, EventArgs e)
        {
            foreach (ToolStripMenuItem e1 in DeviceMenu.DropDownItems)
                e1.Checked = (sender == e1);
            PopulateColorDepthMenus(sender as ToolStripMenuItem);
        }
        
        private void Stream_Item_Click(object sender, EventArgs e)
        {
            foreach (ToolStripMenuItem menu in streamMenus)
            {
                if (menu != null && menu.DropDownItems.Contains(sender as ToolStripMenuItem))
                {
                    foreach (ToolStripMenuItem e1 in menu.DropDownItems)
                        e1.Checked = (sender == e1);
                }
            }
            ResetStreamTypes();
            CheckSelection();
        }

        private void Start_Click(object sender, EventArgs e)
        {
            streaming.ColorPanel = GetStreamAvailable(PXCMCapture.StreamType.STREAM_TYPE_COLOR);
            streaming.DepthPanel = GetStreamAvailable(PXCMCapture.StreamType.STREAM_TYPE_DEPTH);
            streaming.IRPanel = GetStreamAvailable(PXCMCapture.StreamType.STREAM_TYPE_IR);
            streaming.IRLeftPanel = GetStreamAvailable(PXCMCapture.StreamType.STREAM_TYPE_LEFT);
            streaming.IRRightPanel = GetStreamAvailable(PXCMCapture.StreamType.STREAM_TYPE_RIGHT);

            MainMenu.Enabled = false;
            Start.Enabled = false;
            Stop.Enabled = true;
            PIP.Checked = true;
            streaming.StreamProfileSet = GetStreamSetConfiguration();
            streaming.DeviceInfo = GetCheckedDevice();
            streaming.Stop = false;
            System.Threading.Thread thread = new System.Threading.Thread(DoStreaming);
            thread.Start();
            System.Threading.Thread.Sleep(5);
        }

        delegate void DoStreamingEnd();
        private void DoStreaming()
        {
            streaming.StreamColorDepth();
            Invoke(new DoStreamingEnd(
                delegate
                {
                    Start.Enabled = true;
                    Stop.Enabled = false;
                    MainMenu.Enabled = true;
                    if (closing) Close();
                }
            ));
        }

        private PXCMCapture.DeviceInfo GetCheckedDevice()
        {
            foreach (ToolStripMenuItem e in DeviceMenu.DropDownItems)
            {
                if (devices.ContainsKey(e))
                {
                    if (e.Checked) return devices[e];
                }
            }
            return new PXCMCapture.DeviceInfo();
        }

        private PXCMCapture.Device.StreamProfile GetConfiguration(ToolStripMenuItem m)
        {
            foreach (ToolStripMenuItem e in m.DropDownItems)
                if (e.Checked) return profiles[e];
            return new PXCMCapture.Device.StreamProfile();
        }

        private ToolStripMenuItem GetMenuItem(PXCMCapture.StreamType st, PXCMCapture.Device.StreamProfile profile)
        {
            ToolStripMenuItem parent = streamMenus[PXCMCapture.StreamTypeToIndex(st)];
            if (parent == null)
                return null;
            foreach (ToolStripMenuItem key1 in parent.DropDownItems)
            {
                PXCMCapture.Device.StreamProfile profile1 = profiles[key1];
                if (ProfileToString(profile1) == ProfileToString(profile)) return key1;
            }
            return null;
        }

        private PXCMCapture.Device.StreamProfile GetStreamConfiguration(PXCMCapture.StreamType st)
        {
            ToolStripMenuItem menu = streamMenus[PXCMCapture.StreamTypeToIndex(st)];
            if(menu != null)
                return GetConfiguration(menu);
            else
                return new PXCMCapture.Device.StreamProfile();
        }

        private PXCMCapture.Device.StreamProfileSet GetStreamSetConfiguration()
        {
            PXCMCapture.Device.StreamProfileSet profiles = new PXCMCapture.Device.StreamProfileSet();
            for (int s=0; s<PXCMCapture.STREAM_LIMIT; s++)
            {
                PXCMCapture.StreamType st=PXCMCapture.StreamTypeFromIndex(s);
                profiles[st]=GetStreamConfiguration(st);
            }
            return profiles;
        }

        private void SetStatus(String text) {
            StatusLabel.Text = text;
        }

        private delegate void SetStatusDelegate(String status);
        private void UpdateStatusHandler(Object sender, UpdateStatusEventArgs e)
        {
            Status2.Invoke(new SetStatusDelegate(SetStatus), new object[] { e.text });
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            streaming.Stop = true;
        }

        private void RenderFrameHandler(Object sender, RenderFrameEventArgs e)
        {
            if (e.image == null) return;
            renders[e.index].UpdatePanel(e.image);
        }

        /* Redirect to DirectX Update */
        private void PaintHandler(object sender, PaintEventArgs e)
        {
            int render_index = 0;

            if (sender == DepthPanel)
                render_index = 1;
            else if (sender == IRPanel)
                render_index = 2;
            else if (sender == IRLeftPanel)
                render_index = 3;
            else if (sender == IRRightPanel)
                render_index = 4;

            renders[render_index].UpdatePanel();
        }

        /* Redirect to DirectX Resize */
        private void ResizeHandler(object sender, EventArgs e)
        {
            int render_index = 0;

            if (sender == DepthPanel)
                render_index = 1;
            else if (sender == IRPanel)
                render_index = 2;
            else if (sender == IRLeftPanel)
                render_index = 3;
            else if (sender == IRRightPanel)
                render_index = 4;

            renders[render_index].ResizePanel();
        }

        private void FormClosingHandler(object sender, FormClosingEventArgs e)
        {
            streaming.Stop = true;
            e.Cancel = Stop.Enabled;
            closing = true;
        }

        private void Scale_Checked(object sender, EventArgs e)
        {
            renders[0].SetScale(Scale2.Checked);
            renders[1].SetScale(Scale2.Checked);
            renders[2].SetScale(Scale2.Checked);
            renders[3].SetScale(Scale2.Checked);
            renders[4].SetScale(Scale2.Checked);
        }

        private void Mirror_Checked(object sender, EventArgs e)
        {
            streaming.Mirror = Mirror.Checked;
        }

        private void CheckSelection()
        {
            if (!ModePlayback.Checked)
            {
                PXCMCapture.Device.StreamProfileSet allProfile = new PXCMCapture.Device.StreamProfileSet();
                for (int s = 0; s < PXCMCapture.STREAM_LIMIT; s++)
                {
                    PXCMCapture.StreamType st = PXCMCapture.StreamTypeFromIndex(s);
                    allProfile[st] = GetStreamConfiguration(st);
                }

                PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
                desc.group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR;
                desc.subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE;
                desc.iuid = current_device_iuid;
                desc.cuids[0] = PXCMCapture.CUID;
                PXCMCapture capture;
                PXCMCapture.DeviceInfo dinfo2 = GetCheckedDevice();
                if (session.CreateImpl<PXCMCapture>(desc, out capture) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    PXCMCapture.Device device = capture.CreateDevice(dinfo2.didx);
                    if (device != null)
                    {
                        PXCMCapture.Device.StreamProfileSet profile = new PXCMCapture.Device.StreamProfileSet();
                        PXCMCapture.Device.StreamProfileSet test = new PXCMCapture.Device.StreamProfileSet();

                        /* Loop over all stream types and profiles and enable only compatible in menu */
                        for (int s = 0; s < PXCMCapture.STREAM_LIMIT; s++)
                        {
                            PXCMCapture.StreamType st = PXCMCapture.StreamTypeFromIndex(s);
                            if (((int)dinfo2.streams & (int)st) != 0)
                            {
                                for (int s1 = 0; s1 < PXCMCapture.STREAM_LIMIT; s1++)
                                {
                                    test[PXCMCapture.StreamTypeFromIndex(s1)] = allProfile[PXCMCapture.StreamTypeFromIndex(s1)];
                                }
                                int num = device.QueryStreamProfileSetNum(st);
                                for (int p = 0; p < num; p++)
                                {
                                    if (device.QueryStreamProfileSet(st, p, out profile) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                                    PXCMCapture.Device.StreamProfile sprofile = profile[st];
                                    ToolStripMenuItem sm1 = GetMenuItem(st, sprofile);
                                    if (sm1 != null)
                                    {
                                        test[st] = sprofile;
                                        sm1.Enabled = device.IsStreamProfileSetValid(test);
                                    }
                                }
                            }
                        }
                        Start.Enabled = device.IsStreamProfileSetValid(allProfile);
                        device.Dispose();
                    }
                    capture.Dispose();
                }
            }

            int sumEnabled = 0;
            for (int s = 0; s < PXCMCapture.STREAM_LIMIT; s++)
            {
                if (streamButtons[s] != null && streamNone[s] != null)
                {
                    streamButtons[s].Enabled = !streamNone[s].Checked;
                    sumEnabled += streamButtons[s].Enabled?1:0;
                }
            }
            PIP.Enabled = (sumEnabled >= 2);
            Mirror.Enabled = !ModePlayback.Checked;

            PXCMCapture.StreamType selectedStream = GetSelectedStream();
            if (selectedStream != PXCMCapture.StreamType.STREAM_TYPE_ANY && !streamButtons[PXCMCapture.StreamTypeToIndex(selectedStream)].Enabled)
            {
                PXCMCapture.StreamType st = GetUnselectedStream();
                streamButtons[PXCMCapture.StreamTypeToIndex(st)].Checked = true;
                streaming.ColorPanel = st;
            }

            //if (PIP.Enabled && streaming.DepthPanel == PXCMCapture.StreamType.STREAM_TYPE_ANY)
            //{
            //    streaming.DepthPanel = GetUnselectedStream();
            //}
        }

        private PXCMCapture.StreamType GetStreamAvailable(PXCMCapture.StreamType stream)
        {
            if (streamMenus[PXCMCapture.StreamTypeToIndex(stream)] != null 
                && streamMenus[PXCMCapture.StreamTypeToIndex(stream)].Available == true)
                return stream;

            return PXCMCapture.StreamType.STREAM_TYPE_ANY;
        }
        private PXCMCapture.StreamType GetSelectedStream()
        {
            for (int s = 0; s < PXCMCapture.STREAM_LIMIT; s++)
            {
                if (streamButtons[s] != null && streamButtons[s].Checked)
                    return PXCMCapture.StreamTypeFromIndex(s);
            }
            return PXCMCapture.StreamType.STREAM_TYPE_ANY;
        }

        private PXCMCapture.StreamType GetUnselectedStream()
        {
            for (int s = 0; s < PXCMCapture.STREAM_LIMIT; s++)
            {
                if (streamButtons[s] != null && !streamButtons[s].Checked && streamButtons[s].Enabled)
                    return PXCMCapture.StreamTypeFromIndex(s);
            }
            return PXCMCapture.StreamType.STREAM_TYPE_ANY;
        }

        private void Stream_Click(object sender, EventArgs e)
        {
            //PXCMCapture.StreamType selected_stream = GetSelectedStream();
            //if (selected_stream != streaming.ColorPanel)
            //{
            //    streaming.DepthPanel = streaming.ColorPanel;
            //    streaming.ColorPanel = selected_stream;
            //}
        }

        private void ColorDepthSync_Click(object sender, EventArgs e)
        {
            ColorDepthSync.Checked = streaming.Synced = true;
            ColorDepthAsync.Checked = false;
            CheckSelection();
        }

        private void ColorDepthAsync_Click(object sender, EventArgs e)
        {
            ColorDepthSync.Checked = streaming.Synced = false;
            ColorDepthAsync.Checked = true;
            CheckSelection();
        }

        private void ModeLive_Click(object sender, EventArgs e)
        {
            ModeLive.Checked = true;
            streaming.Playback = streaming.Record = false;
            ModeRecord.Checked = false;
            if (ModePlayback.Checked)
            {
                /* rescan streams after playback */
                ModePlayback.Checked = false;
                PopulateDeviceMenu();
            }
        }

        private void ModePlayback_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "RSSDK clip|*.rssdk|Old format clip|*.pcsdk|All files|*.*";
            ofd.CheckFileExists = true;
            ofd.CheckPathExists = true;
            streaming.File=(ofd.ShowDialog() == DialogResult.OK)?ofd.FileName:null;
            if (streaming.File == null)
            {
                ModeLive.Checked = true;
                ModePlayback.Checked = ModeRecord.Checked = false;
                streaming.Playback = streaming.Record = false;
                PopulateDeviceMenu();
            } 
            else 
            {
                ModePlayback.Checked = streaming.Playback=true;
                ModeLive.Checked = ModeRecord.Checked = streaming.Record = false;
                if (PopulateDeviceFromFileMenu() == false)
                {
                    ModeLive.Checked = true;
                    ModePlayback.Checked = ModeRecord.Checked = streaming.Playback = false;
                    MessageBox.Show("Incorrect file format, switching to live mode");
                }
            }
        }

        private void ModeRecord_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "RSSDK clip|*.rssdk|All files|*.*";
            sfd.CheckPathExists = true;
            sfd.OverwritePrompt = true;
            sfd.AddExtension    = true;
            streaming.File = (sfd.ShowDialog() == DialogResult.OK) ? sfd.FileName : null;
            if (streaming.File == null)
            {
                ModeLive.Checked = true;
                streaming.Playback = streaming.Record = false;
                ModeRecord.Checked = false;
                if (ModePlayback.Checked)
                {
                    /* rescan streams after playback */
                    PopulateDeviceMenu();
                    ModePlayback.Checked = false;
                }
            }
            else
            {
                ModeRecord.Checked = streaming.Record = true;
                ModeLive.Checked = streaming.Playback = false;
                if (ModePlayback.Checked)
                {
                    PopulateDeviceMenu();
                    ModePlayback.Checked = false;
                }
            }
        }

        private Rectangle SetHalfSize(Rectangle rc)
        {
            rc.Width = rc.Width / 2;
            rc.X = rc.X + rc.Width;
            rc.Height = rc.Height / 2;
            rc.Y = rc.Y + rc.Height;
            return rc;
        }

        private Rectangle SetQuarterSize(Rectangle rc)
        {
            rc.X = rc.X + rc.Width * 3 / 4;
            rc.Y = rc.Y + rc.Height * 3 / 4;
            rc.Width = rc.Width / 4;
            rc.Height = rc.Height / 4;
            return rc;
        }


        private void PIP_CheckStateChanged(object sender, EventArgs e)
        {
            switch (PIP.CheckState)
            {
                case CheckState.Checked:
                    //DepthPanel.Bounds = SetHalfSize(ColorPanel.Bounds);
                    DepthPanel.Show();
                    break;
                case CheckState.Indeterminate:
                    //DepthPanel.Bounds = SetQuarterSize(ColorPanel.Bounds);
                    DepthPanel.Show();
                    break;
                case CheckState.Unchecked:
                    DepthPanel.Hide();
                    break;
            }
        }
    }
}
