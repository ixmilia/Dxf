// Copyright (c) IxMilia.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// The contents of this file are automatically generated by a tool, and should not be directly modified.

using System.Linq;
using System.Collections.Generic;
using IxMilia.Dxf.Collections;
using IxMilia.Dxf.Sections;

namespace IxMilia.Dxf.Tables
{
    public partial class DxfLayerTable : DxfTable
    {
        internal override DxfTableType TableType { get { return DxfTableType.Layer; } }

        public IList<DxfLayer> Items { get; private set; }

        protected override IEnumerable<DxfSymbolTableFlags> GetSymbolItems()
        {
            return Items.Cast<DxfSymbolTableFlags>();
        }

        public DxfLayerTable()
        {
            Items = new ListNonNull<DxfLayer>();
            Normalize();
        }

        internal static DxfTable ReadFromBuffer(DxfCodePairBufferReader buffer)
        {
            var table = new DxfLayerTable();
            table.Items.Clear();
            while (buffer.ItemsRemain)
            {
                var pair = buffer.Peek();
                buffer.Advance();
                if (DxfTablesSection.IsTableEnd(pair))
                {
                    break;
                }

                if (pair.Code == 0 && pair.StringValue == DxfTable.LayerText)
                {
                    var item = DxfLayer.FromBuffer(buffer);
                    table.Items.Add(item);
                }
            }

            return table;
        }
    }
}
