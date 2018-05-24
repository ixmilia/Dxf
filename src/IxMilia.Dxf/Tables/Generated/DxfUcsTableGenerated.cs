// Copyright (c) IxMilia.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// The contents of this file are automatically generated by a tool, and should not be directly modified.

using System.Linq;
using System.Collections.Generic;
using IxMilia.Dxf.Collections;
using IxMilia.Dxf.Sections;

namespace IxMilia.Dxf.Tables
{
    public partial class DxfUcsTable : DxfTable
    {
        internal override DxfTableType TableType { get { return DxfTableType.Ucs; } }

        public IList<DxfUcs> Items { get; private set; }

        protected override IEnumerable<DxfSymbolTableFlags> GetSymbolItems()
        {
            return Items.Cast<DxfSymbolTableFlags>();
        }

        public DxfUcsTable()
        {
            Items = new ListNonNull<DxfUcs>();
            Normalize();
        }

        internal static DxfTable ReadFromBuffer(DxfCodePairBufferReader buffer)
        {
            var table = new DxfUcsTable();
            table.Items.Clear();
            while (buffer.ItemsRemain)
            {
                var pair = buffer.Peek();
                buffer.Advance();
                if (DxfTablesSection.IsTableEnd(pair))
                {
                    break;
                }

                if (pair.Code == 0 && pair.StringValue == DxfTable.UcsText)
                {
                    var item = DxfUcs.FromBuffer(buffer);
                    table.Items.Add(item);
                }
            }

            return table;
        }
    }
}
