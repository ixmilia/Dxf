﻿// Copyright (c) IxMilia.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace IxMilia.Dxf.Objects
{
    public abstract partial class DxfObject
    {
        protected List<DxfCodePair> ExcessCodePairs = new List<DxfCodePair>();
        protected DxfXData XDataProtected { get; set; }
        public List<DxfCodePairGroup> ExtensionDataGroups { get; private set; }

        public abstract DxfObjectType ObjectType { get; }

        protected virtual DxfAcadVersion MinVersion
        {
            get { return DxfAcadVersion.Min; }
        }

        protected virtual DxfAcadVersion MaxVersion
        {
            get { return DxfAcadVersion.Max; }
        }

        protected DxfObject()
        {
            Initialize();
            ExtensionDataGroups = new List<DxfCodePairGroup>();
        }

        protected virtual void AddTrailingCodePairs(List<DxfCodePair> pairs, DxfAcadVersion version, bool outputHandles)
        {
        }

        protected virtual DxfObject PostParse()
        {
            return this;
        }

        public IEnumerable<DxfCodePair> GetValuePairs(DxfAcadVersion version, bool outputHandles)
        {
            var pairs = new List<DxfCodePair>();
            if (version >= MinVersion && version <= MaxVersion)
            {
                AddValuePairs(pairs, version, outputHandles);
                AddTrailingCodePairs(pairs, version, outputHandles);
            }

            return pairs;
        }

        private void AddExtensionValuePairs(List<DxfCodePair> pairs, DxfAcadVersion version, bool outputHandles)
        {
            foreach (var group in ExtensionDataGroups)
            {
                group.AddValuePairs(pairs, version, outputHandles);
            }
        }

        internal virtual DxfObject PopulateFromBuffer(DxfCodePairBufferReader buffer)
        {
            while (buffer.ItemsRemain)
            {
                var pair = buffer.Peek();
                if (pair.Code == 0)
                {
                    break;
                }
                else if (pair.Code == DxfCodePairGroup.GroupCodeNumber)
                {
                    buffer.Advance();
                    var groupName = DxfCodePairGroup.GetGroupName(pair.StringValue);
                    ExtensionDataGroups.Add(DxfCodePairGroup.FromBuffer(buffer, groupName));
                }
                else if (pair.Code == (int)DxfXDataType.ApplicationName)
                {
                    XDataProtected = DxfXData.FromBuffer(buffer, pair.StringValue);
                }

                if (!TrySetPair(pair))
                {
                    ExcessCodePairs.Add(pair);
                }

                buffer.Advance();
            }

            return PostParse();
        }

        protected static bool BoolShort(short s)
        {
            return DxfCommonConverters.BoolShort(s);
        }

        protected static short BoolShort(bool b)
        {
            return DxfCommonConverters.BoolShort(b);
        }

        protected static uint UIntHandle(string s)
        {
            return DxfCommonConverters.UIntHandle(s);
        }

        protected static string UIntHandle(uint u)
        {
            return DxfCommonConverters.UIntHandle(u);
        }

        private static void SwallowObject(DxfCodePairBufferReader buffer)
        {
            while (buffer.ItemsRemain)
            {
                var pair = buffer.Peek();
                if (pair.Code == 0)
                    break;
                buffer.Advance();
            }
        }
    }

    public partial class DxfAcadProxyObject
    {
        public List<string> ObjectIds { get; } = new List<string>();

        public uint DrawingVersion
        {
            get { return ObjectDrawingFormat | 0x0000FFFF; }
            set { ObjectDrawingFormat |= value & 0x0000FFFF; }
        }

        public uint MaintenenceReleaseVersion
        {
            get { return (ObjectDrawingFormat | 0xFFFF0000) >> 16; }
            set { ObjectDrawingFormat |= (value & 0xFFFF0000) << 16; }
        }

        protected override DxfObject PostParse()
        {
            ObjectIds.AddRange(ObjectIdsA);
            ObjectIds.AddRange(ObjectIdsB);
            ObjectIds.AddRange(ObjectIdsC);
            ObjectIds.AddRange(ObjectIdsD);
            ObjectIdsA.Clear();
            ObjectIdsB.Clear();
            ObjectIdsC.Clear();
            ObjectIdsD.Clear();

            return this;
        }
    }
}