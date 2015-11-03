// Copyright (c) IxMilia.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// The contents of this file are automatically generated by a tool, and should not be directly modified.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IxMilia.Dxf.Objects
{

    /// <summary>
    /// DxfRasterVariables class
    /// </summary>
    public partial class DxfRasterVariables : DxfObject
    {
        public override DxfObjectType ObjectType { get { return DxfObjectType.RasterVariables; } }

        public int ClassVersion { get; set; }
        public bool IsDisplayFrameImage { get; set; }
        public bool IsHighDisplayQuality { get; set; }
        public DxfRasterImageUnits ImageUnits { get; set; }

        public DxfRasterVariables()
            : base()
        {
        }

        protected override void Initialize()
        {
            base.Initialize();
            this.ClassVersion = 0;
            this.IsDisplayFrameImage = false;
            this.IsHighDisplayQuality = false;
            this.ImageUnits = DxfRasterImageUnits.None;
        }

        protected override void AddValuePairs(List<DxfCodePair> pairs, DxfAcadVersion version, bool outputHandles)
        {
            base.AddValuePairs(pairs, version, outputHandles);
            pairs.Add(new DxfCodePair(100, "AcDbRasterVariables"));
            pairs.Add(new DxfCodePair(90, (this.ClassVersion)));
            pairs.Add(new DxfCodePair(70, BoolShort(this.IsDisplayFrameImage)));
            pairs.Add(new DxfCodePair(71, BoolShort(this.IsHighDisplayQuality)));
            pairs.Add(new DxfCodePair(72, (short)(this.ImageUnits)));
        }

        internal override bool TrySetPair(DxfCodePair pair)
        {
            switch (pair.Code)
            {
                case 70:
                    this.IsDisplayFrameImage = BoolShort(pair.ShortValue);
                    break;
                case 71:
                    this.IsHighDisplayQuality = BoolShort(pair.ShortValue);
                    break;
                case 72:
                    this.ImageUnits = (DxfRasterImageUnits)(pair.ShortValue);
                    break;
                case 90:
                    this.ClassVersion = (pair.IntegerValue);
                    break;
                default:
                    return base.TrySetPair(pair);
            }

            return true;
        }
    }

}