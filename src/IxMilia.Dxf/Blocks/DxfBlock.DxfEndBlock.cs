﻿// Copyright (c) IxMilia.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace IxMilia.Dxf.Blocks
{
    public partial class DxfBlock
    {
        private class DxfEndBlock : IDxfHasHandle
        {
            public DxfBlock Parent { get; }
            public uint Handle { get; set; }
            public List<DxfCodePairGroup> ExtensionDataGroups { get; private set; }

            public DxfEndBlock(DxfBlock parent)
            {
                Parent = parent;
                ExtensionDataGroups = new List<DxfCodePairGroup>();
            }

            public void ApplyCodePairs(DxfCodePairBufferReader buffer, DxfAcadVersion version)
            {
                var pair = buffer.Peek();
                buffer.Advance();
                switch (pair.Code)
                {
                    case 5:
                        Handle = DxfCommonConverters.UIntHandle(pair.StringValue);
                        break;
                    case 8:
                        // just a re-iteration of the layer
                        break;
                    case 67:
                        // just a re-iteration of the paper space setting
                        break;
                    case 100:
                        Debug.Assert(pair.StringValue == AcDbEntityText || pair.StringValue == AcDbBlockEndText);
                        break;
                    case DxfCodePairGroup.GroupCodeNumber:
                        var groupName = DxfCodePairGroup.GetGroupName(pair.StringValue);
                        ExtensionDataGroups.Add(DxfCodePairGroup.FromBuffer(buffer, groupName));
                        break;
                }
            }

            public IEnumerable<DxfCodePair> GetValuePairs(DxfAcadVersion version, bool outputHandles)
            {
                var list = new List<DxfCodePair>();
                list.Add(new DxfCodePair(0, EndBlockText));
                if (outputHandles && Handle != 0u)
                {
                    list.Add(new DxfCodePair(5, DxfCommonConverters.UIntHandle(Handle)));
                }

                if (Parent.XData != null)
                {
                    Parent.XData.AddValuePairs(list, version, outputHandles);
                }

                if (version >= DxfAcadVersion.R14)
                {
                    foreach (var group in ExtensionDataGroups)
                    {
                        group.AddValuePairs(list, version, outputHandles);
                    }
                }

                if (version >= DxfAcadVersion.R2000)
                {
                    list.Add(new DxfCodePair(330, DxfCommonConverters.UIntHandle(Parent.OwnerHandle)));
                }

                if (version >= DxfAcadVersion.R13)
                {
                    list.Add(new DxfCodePair(100, AcDbEntityText));
                }

                if (Parent.IsInPaperSpace)
                {
                    list.Add(new DxfCodePair(67, DxfCommonConverters.BoolShort(Parent.IsInPaperSpace)));
                }

                list.Add(new DxfCodePair(8, Parent.Layer));

                if (version >= DxfAcadVersion.R13)
                {
                    list.Add(new DxfCodePair(100, AcDbBlockEndText));
                }
                return list;
            }
        }
    }
}