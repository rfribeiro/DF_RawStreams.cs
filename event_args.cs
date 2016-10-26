/*******************************************************************************

INTEL CORPORATION PROPRIETARY INFORMATION
This software is supplied under the terms of a license agreement or nondisclosure
agreement with Intel Corporation and may not be copied or disclosed except in
accordance with the terms of that agreement
Copyright(c) 2015 Intel Corporation. All Rights Reserved.

*******************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace raw_streams.cs
{
    class UpdateStatusEventArgs : EventArgs
    {
        public String text { get; set; }

        public UpdateStatusEventArgs(String text)
        {
            this.text = text;
        }
    }

    class RenderFrameEventArgs : EventArgs
    {
        public int index { get; set; }
        public PXCMImage image { get; set; }

        public RenderFrameEventArgs(int index, PXCMImage image)
        {
            this.index = index;
            this.image = image;
        }
    }
}
