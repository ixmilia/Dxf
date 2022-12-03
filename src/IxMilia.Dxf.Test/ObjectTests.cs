using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IxMilia.Dxf.Entities;
using IxMilia.Dxf.Objects;
using IxMilia.Dxf.Sections;
using Xunit;

namespace IxMilia.Dxf.Test
{
    public class ObjectTests : AbstractDxfTests
    {
        private DxfObject GenObject(string typeString, params (int code, object value)[] codePairs)
        {
            var prePairs = new[]
            {
                (0, (object)typeString)
            };
            return Section("OBJECTS", prePairs.Concat(codePairs)).Objects.Last();
        }

        private static void EnsureFileContainsObject(DxfObject obj, DxfAcadVersion version, params (int code, object value)[] codePairs)
        {
            var file = new DxfFile();
            file.Clear();
            file.Header.Version = version;
            file.Objects.Add(obj);
            VerifyFileContains(file, DxfSectionType.Objects, codePairs);
        }

        [Fact]
        public void ReadSimpleObjectTest()
        {
            var proxyObject = GenObject("ACAD_PROXY_OBJECT");
            Assert.IsType<DxfAcadProxyObject>(proxyObject);
        }

        [Fact]
        public void WriteSimpleObjectTest()
        {
            var file = new DxfFile();
            file.Header.Version = DxfAcadVersion.R2000;
            file.Objects.Add(new DxfAcadProxyObject());
            VerifyFileContains(file,
                DxfSectionType.Objects,
                (0, "ACAD_PROXY_OBJECT"),
                (5, "#"),
                (100, "AcDbProxyObject"),
                (90, 499)
            );
        }

        [Fact]
        public void ReadAllObjectsWithTrailingXDataTest()
        {
            foreach (var objectType in GetAllObjectTypes())
            {
                var o = Activator.CreateInstance(objectType, nonPublic: true);
                if (o is DxfObject obj)
                {
                    var file = Section("OBJECTS",
                        (0, obj.ObjectTypeString),
                        (5, "424241"),
                        (1001, "ACAD"),
                        (1000, "sample-xdata"),
                        (0, "ACAD_PROXY_OBJECT"), // sentinel to ensure we didn't read too far
                        (5, "424242")
                    );
                    Assert.Equal(2, file.Objects.Count);

                    var readObject = file.Objects.First();
                    Assert.IsType(objectType, readObject);
                    Assert.Equal((ulong)0x424241, ((IDxfItemInternal)readObject).Handle.Value);
                    var xdata = readObject.XData;
                    var kvp = xdata.Single();
                    Assert.Equal("ACAD", kvp.Key);
                    Assert.Equal("sample-xdata", ((DxfXDataString)kvp.Value.Single()).Value);

                    var sentinel = file.Objects.Last();
                    Assert.IsType<DxfAcadProxyObject>(sentinel);
                    Assert.Equal((ulong)0x424242, ((IDxfItemInternal)sentinel).Handle.Value);
                }
            }
        }

        [Fact]
        public void ReadDataTableTest()
        {
            var table = (DxfDataTable)GenObject("DATATABLE",
                (330, "0"),
                (100, "AcDbDataTable"),
                (70, 2),
                (90, 3), // 3 columns
                (91, 2), // 2 rows per column
                (1, "table-name"),
                (92, 10), // column 1
                (2, "column-of-points"),
                (10, 1.0),
                (20, 2.0),
                (30, 3.0),
                (10, 4.0),
                (20, 5.0),
                (30, 6.0),
                (92, 3), // column 2
                (2, "column-of-strings"),
                (3, "string 1"),
                (3, "string 2"),
                (92, 331), // column 3
                (2, "column-of-handles"),
                (331, "ABCD"),
                (331, "1234")
            );
            Assert.Equal(3, table.ColumnCount);
            Assert.Equal(2, table.RowCount);
            Assert.Equal("table-name", table.Name);

            Assert.Equal("column-of-points", table.ColumnNames[0]);
            Assert.Equal(new DxfPoint(1, 2, 3), (DxfPoint)table[0, 0]);
            Assert.Equal(new DxfPoint(4, 5, 6), (DxfPoint)table[1, 0]);

            Assert.Equal("column-of-strings", table.ColumnNames[1]);
            Assert.Equal("string 1", (string)table[0, 1]);
            Assert.Equal("string 2", (string)table[1, 1]);

            Assert.Equal("column-of-handles", table.ColumnNames[2]);
            Assert.Equal(new DxfHandle(0xABCD), (DxfHandle)table[0, 2]);
            Assert.Equal(new DxfHandle(0x1234), (DxfHandle)table[1, 2]);
        }

        [Fact]
        public void WriteDataTableTest()
        {
            var table = new DxfDataTable();
            table.Name = "table-name";
            table.SetSize(2, 2);
            table.ColumnNames.Add("column-of-points");
            table.ColumnNames.Add("column-of-strings");
            table[0, 0] = new DxfPoint(1, 2, 3);
            table[1, 0] = new DxfPoint(4, 5, 6);
            table[0, 1] = "string 1";
            table[1, 1] = "string 2";
            var file = new DxfFile();
            file.Header.Version = DxfAcadVersion.R2007;
            file.Objects.Add(table);
            VerifyFileContains(file,
                (100, "AcDbDataTable"),
                (70, 0),
                (90, 2),
                (91, 2),
                (1, "table-name"),
                (92, 10),
                (2, "column-of-points"),
                (10, 1.0),
                (20, 2.0),
                (30, 3.0),
                (10, 4.0),
                (20, 5.0),
                (30, 6.0),
                (92, 3),
                (2, "column-of-strings"),
                (3, "string 1"),
                (3, "string 2")
            );
        }

        [Fact]
        public void ReadDictionaryTest1()
        {
            // dictionary with simple DICTIONARYVAR values
            var file = Section("OBJECTS",
                (0, "DICTIONARY"),
                (3, "key-1"),
                (360, "111"), // pointer to value-1 below
                (3, "key-2"),
                (360, "222"), // pointer to value-2 below
                (0, "DICTIONARYVAR"),
                (5, "111"),
                (280, 0),
                (1, "value-1"),
                (0, "DICTIONARYVAR"),
                (5, "222"),
                (280, 0),
                (1, "value-2")
            );
            var dict = file.Objects.OfType<DxfDictionary>().Single();
            Assert.Equal(dict, dict["key-1"].Owner);
            Assert.Equal(dict, dict["key-2"].Owner);
            Assert.Equal("value-1", ((DxfDictionaryVariable)dict["key-1"]).Value);
            Assert.Equal("value-2", ((DxfDictionaryVariable)dict["key-2"]).Value);
        }

        [Fact]
        public void ReadDictionaryTest2()
        {
            // dictionary with sub-dictionary with DICTIONARYVAR value
            var file = Section("OBJECTS",
                (0, "DICTIONARY"),
                (3, "key-1"),
                (360, "1000"), // pointer to dictionary below
                (0, "DICTIONARY"),
                (5, "1000"),
                (3, "key-2"),
                (360, "2000"), // pointer to value-2 below
                (0, "DICTIONARYVAR"),
                (5, "2000"),
                (280, 0),
                (1, "value-2")
            );
            var dict1 = file.Objects.OfType<DxfDictionary>().First();
            var dict2 = (DxfDictionary)dict1["key-1"];
            Assert.Equal(dict1, dict2.Owner);
            Assert.Equal(dict2, dict2["key-2"].Owner);
            Assert.Equal("value-2", ((DxfDictionaryVariable)dict2["key-2"]).Value);
        }

        [Fact]
        public void ReadDictionaryTest3()
        {
            // dictionary with default with simple DICTIONARYVAR values
            var file = Section("OBJECTS",
                (0, "DICTIONARYVAR"),
                (5, "1"),
                (280, 0),
                (1, "default-value"),
                (0, "ACDBDICTIONARYWDFLT"),
                (5, "2"),
                (340, "1"),
                (3, "key-1"),
                (350, "111"),
                (3, "key-2"),
                (360, "222"),
                (0, "DICTIONARYVAR"),
                (5, "111"),
                (280, 0),
                (1, "value-1"),
                (0, "DICTIONARYVAR"),
                (5, "222"),
                (280, 0),
                (1, "value-2")
            );
            var dict = file.Objects.OfType<DxfDictionaryWithDefault>().Single();
            Assert.Equal(dict, dict["key-1"].Owner);
            Assert.Equal(dict, dict["key-2"].Owner);
            Assert.Equal("value-1", ((DxfDictionaryVariable)dict["key-1"]).Value);
            Assert.Equal("value-2", ((DxfDictionaryVariable)dict["key-2"]).Value);
            Assert.Equal("default-value", ((DxfDictionaryVariable)dict["key-that-isn't-present"]).Value);
        }

        [Fact]
        public void ReadDictionaryTest4()
        {
            // dictionary with default with sub-dictionary with DICTIONARYVAR value
            var file = Section("OBJECTS",
                (0, "DICTIONARYVAR"),
                (5, "1"),
                (280, 0),
                (1, "default-value"),
                (0, "ACDBDICTIONARYWDFLT"),
                (340, "1"),
                (3, "key-1"),
                (350, "111"),
                (0, "DICTIONARY"),
                (5, "111"),
                (3, "key-2"),
                (360, "1000"),
                (0, "DICTIONARYVAR"),
                (5, "1000"),
                (280, 0),
                (1, "value-2")
            );
            var dict1 = file.Objects.OfType<DxfDictionaryWithDefault>().Single();
            var dict2 = (DxfDictionary)dict1["key-1"];
            Assert.Equal(dict1, dict2.Owner);
            Assert.Equal(dict2, dict2["key-2"].Owner);
            Assert.Equal("value-2", ((DxfDictionaryVariable)dict2["key-2"]).Value);
        }

        [Fact]
        public void ReadDictionaryTest5()
        {
            // dictionary with MLINESTYLE value
            var file = Section("OBJECTS",
                (0, "DICTIONARY"),
                (5, "42"),
                (3, "Standard"),
                (350, "43"),
                (0, "MLINESTYLE"),
                (5, "43"),
                (330, "42"),
                (2, "Standard")
            );
            var dict = (DxfDictionary)file.Objects.First();
            var mlineStyle = (DxfMLineStyle)dict["Standard"];
            Assert.Equal("Standard", mlineStyle.StyleName);

            // now round-trip it
            using (var ms = new MemoryStream())
            {
                file.Save(ms);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                var file2 = DxfFile.Load(ms);
                var dict2 = (DxfDictionary)file.Objects.First();
                var mlineStyle2 = (DxfMLineStyle)dict["Standard"];
                Assert.Equal("Standard", mlineStyle2.StyleName);
            }
        }

        [Fact]
        public void DictionaryWithUnsupportedObjectTest()
        {
            var file = Section("OBJECTS",
                (0, "DICTIONARY"),
                (5, "42"),
                (3, "key"),
                (350, "43"),
                (0, "UNSUPPORTED_OBJECT"),
                (5, "43"),
                (330, "42")
            );
            var dict = (DxfDictionary)file.Objects.Single();
            Assert.Equal(1, dict.Keys.Count);
            Assert.Null(dict["key"]);
        }

        [Fact]
        public void WriteDictionaryTest1()
        {
            // dictionary with simple DICTIONARYVAR values
            var dict = new DxfDictionary();
            dict["key-1"] = new DxfDictionaryVariable() { Value = "value-1" };
            dict["key-2"] = new DxfDictionaryVariable() { Value = "value-2" };
            EnsureFileContainsObject(dict,
                DxfAcadVersion.R2000,
                (0, "DICTIONARY"),
                (5, "#"),
                (100, "AcDbDictionary"),
                (281, 0),
                (3, "key-1"),
                (350, "#"),
                (3, "key-2"),
                (350, "#"),
                (0, "DICTIONARYVAR"),
                (5, "#"),
                (330, "#"),
                (100, "DictionaryVariables"),
                (280, 0),
                (1, "value-1"),
                (0, "DICTIONARYVAR"),
                (5, "#"),
                (330, "#"),
                (100, "DictionaryVariables"),
                (280, 0),
                (1, "value-2")
            );
        }

        [Fact]
        public void WriteDictionaryTest2()
        {
            // dictionary with sub-dictionary with DICTIONARYVAR value
            var dict1 = new DxfDictionary();
            var dict2 = new DxfDictionary();
            dict1["key-1"] = dict2;
            dict2["key-2"] = new DxfDictionaryVariable() { Value = "value-2" };
            EnsureFileContainsObject(dict1,
                DxfAcadVersion.R2000,
                (0, "DICTIONARY"),
                (5, "#"),
                (100, "AcDbDictionary"),
                (281, 0),
                (3, "key-1"),
                (350, "#"),
                (0, "DICTIONARY"),
                (5, "#"),
                (330, "#"),
                (100, "AcDbDictionary"),
                (281, 0),
                (3, "key-2"),
                (350, "#"),
                (0, "DICTIONARYVAR"),
                (5, "#"),
                (330, "#"),
                (100, "DictionaryVariables"),
                (280, 0),
                (1, "value-2")
            );
        }

        [Fact]
        public void WriteDictionaryTest3()
        {
            // dictionary with default with DICTIONARYVAR value
            var dict = new DxfDictionaryWithDefault();
            dict.DefaultObject = new DxfDictionaryVariable() { Value = "default-value" };
            dict["key-1"] = new DxfDictionaryVariable() { Value = "value-1" };
            EnsureFileContainsObject(dict,
                DxfAcadVersion.R2000,
                (0, "ACDBDICTIONARYWDFLT"),
                (5, "#"),
                (100, "AcDbDictionary"),
                (281, 0),
                (3, "key-1"),
                (350, "#"),
                (100, "AcDbDictionaryWithDefault"),
                (340, "#"),
                (0, "DICTIONARYVAR"),
                (5, "#"),
                (330, "#"),
                (100, "DictionaryVariables"),
                (280, 0),
                (1, "default-value"),
                (0, "DICTIONARYVAR"),
                (5, "#"),
                (330, "#"),
                (100, "DictionaryVariables"),
                (280, 0),
                (1, "value-1")
            );
        }

        [Fact]
        public void WriteDictionaryWithNullValueTest()
        {
            var dict = new DxfDictionary();
            dict["key"] = null;

            var objectsSection = new DxfObjectsSection();
            objectsSection.Objects.Add(dict);

            var _pairs = objectsSection.GetValuePairs(DxfAcadVersion.R2000, outputHandles: true, writtenItems: new HashSet<IDxfItem>());
        }

        [Fact]
        public void DictionaryRoundTripTest1()
        {
            // dictionary with DICTIONARYVAR values
            var dict = new DxfDictionary();
            dict["key-1"] = new DxfDictionaryVariable() { Value = "value-1" };
            dict["key-2"] = new DxfDictionaryVariable() { Value = "value-2" };

            var file = new DxfFile();
            file.Clear();
            file.Header.Version = DxfAcadVersion.R2000;
            file.Objects.Add(dict);

            var roundTrippedFile = RoundTrip(file);
            var roundTrippedDict = roundTrippedFile.Objects.OfType<DxfDictionary>().Single(d => d.Keys.Count == 2);
            Assert.Equal("value-1", ((DxfDictionaryVariable)roundTrippedDict["key-1"]).Value);
            Assert.Equal("value-2", ((DxfDictionaryVariable)roundTrippedDict["key-2"]).Value);
        }

        [Fact]
        public void DictionaryRoundTripTest2()
        {
            // dictionary with sub-dictionary wit DICTIONARYVAR value
            var dict1 = new DxfDictionary();
            var dict2 = new DxfDictionary();
            dict1["key-1"] = dict2;
            dict2["key-2"] = new DxfDictionaryVariable() { Value = "value-2" };

            var file = new DxfFile();
            file.Clear();
            file.Header.Version = DxfAcadVersion.R2000;
            file.Objects.Add(dict1);

            var roundTrippedFile = RoundTrip(file);
            var roundTrippedDict1 = roundTrippedFile.Objects.OfType<DxfDictionary>().First(d => d.ContainsKey("key-1"));
            var roundTrippedDict2 = (DxfDictionary)roundTrippedDict1["key-1"];
            Assert.Equal("value-2", ((DxfDictionaryVariable)roundTrippedDict2["key-2"]).Value);
        }

        [Fact]
        public void DictionaryRoundTripTest3()
        {
            // dictionary with default with DICTIONARYVAR values
            var dict = new DxfDictionaryWithDefault();
            dict.DefaultObject = new DxfDictionaryVariable() { Value = "default-value" };
            dict["key-1"] = new DxfDictionaryVariable() { Value = "value-1" };

            var file = new DxfFile();
            file.Clear();
            file.Header.Version = DxfAcadVersion.R2000;
            file.Objects.Add(dict);

            var roundTrippedFile = RoundTrip(file);
            var roundTrippedDict = roundTrippedFile.Objects.OfType<DxfDictionaryWithDefault>().Single();
            Assert.Equal("value-1", ((DxfDictionaryVariable)roundTrippedDict["key-1"]).Value);
            Assert.Equal("default-value", ((DxfDictionaryVariable)roundTrippedDict.DefaultObject).Value);
        }

        [Fact]
        public void ReadDimensionAssociativityTest()
        {
            var file = Parse(
                (0, "SECTION"),
                (2, "ENTITIES"),
                    (0, "DIMENSION"),
                    (5, "1"),
                    (1, "dimension-text"),
                    (70, 1),
                (0, "ENDSEC"),
                (0, "SECTION"),
                (2, "OBJECTS"),
                    (0, "DIMASSOC"),
                    (330, "1"),
                    (1, "class-name"),
                (0, "ENDSEC"),
                (0, "EOF")
            );
            var dimassoc = (Objects.DxfDimensionAssociativity)file.Objects.Last();
            Assert.Equal("class-name", dimassoc.ClassName);
            var dim = (DxfAlignedDimension)dimassoc.Dimension;
            Assert.Equal(dimassoc, dim.Owner);
            Assert.Equal("dimension-text", dim.Text);
        }

        [Fact]
        public void ReadLayoutTest()
        {
            var layout = (DxfLayout)GenObject("LAYOUT",
                (1, "page-setup-name"),
                (100, "AcDbLayout"),
                (1, "layout-name")
            );
            Assert.Equal("page-setup-name", layout.PageSetupName);
            Assert.Equal("layout-name", layout.LayoutName);
        }

        [Fact]
        public void ReadLayoutWithEmptyNameTest()
        {
            var layout = (DxfLayout)GenObject("LAYOUT",
                (1, "page-setup-name"),
                (100, "AcDbLayout"),
                (1, ""),
                (999, "comment line to ensure the value for the `1` code is an empty string")
            );
            Assert.Equal("page-setup-name", layout.PageSetupName);
            Assert.Equal("", layout.LayoutName);
        }

        [Fact]
        public void WriteLayoutTest()
        {
            var layout = new DxfLayout();
            layout.PageSetupName = "page-setup-name";
            layout.LayoutName = "layout-name";
            var file = new DxfFile();
            file.Header.Version = DxfAcadVersion.R2000;
            file.Objects.Add(layout);
            var pairs = file.GetCodePairs();

            // verify the plot settings were written
            var plotSettingsOffset = IndexOf(pairs,
                (100, "AcDbPlotSettings"),
                (1, "page-setup-name")
            );
            Assert.True(plotSettingsOffset > 0);

            // verify the layout settings were written
            var layoutOffset = IndexOf(pairs,
                (100, "AcDbLayout"),
                (1, "layout-name")
            );
            Assert.True(layoutOffset > 0);

            // verify that the layout settings were written after the plot settings
            Assert.True(layoutOffset > plotSettingsOffset);
        }

        [Fact]
        public void PlotSettingsPlotViewName()
        {
            // ensure invalid values aren't allowed
            Assert.Throws<InvalidOperationException>(() => new DxfPlotSettings(null));
            Assert.Throws<InvalidOperationException>(() => new DxfPlotSettings(""));

            // ensure valid values are allowed
            _ = new DxfPlotSettings("some value");
        }

        [Fact]
        public void LayoutName()
        {
            // ensure invalid values aren't allowed
            Assert.Throws<InvalidOperationException>(() => new DxfLayout(null, "layout name"));
            Assert.Throws<InvalidOperationException>(() => new DxfLayout("", "layout name"));
            Assert.Throws<InvalidOperationException>(() => new DxfLayout("plot view name", null));
            Assert.Throws<InvalidOperationException>(() => new DxfLayout("plot view name", ""));

            // ensure valid values are allowed
            _ = new DxfLayout("plot view name", "layout name");
        }

        [Fact]
        public void LayoutNameOverridesPlotViewName()
        {
            // artificially create a layout with a null PlotViewName, but valid LayoutName
            var layout = new DxfLayout();
            layout.LayoutName = "layout name";
            Assert.Equal("layout name", layout.PlotViewName);

            // ensure the base plot view name is honored
            layout = new DxfLayout("plot view name", "layout name");
            Assert.Equal("layout name", layout.LayoutName);
            Assert.Equal("plot view name", layout.PlotViewName);
        }

        [Fact]
        public void ReadLightListTest()
        {
            var file = Parse(
                (0, "SECTION"),
                (2, "ENTITIES"),
                    (0, "LIGHT"),
                    (5, "42"),
                    (1, "light-name"),
                (0, "ENDSEC"),
                (0, "SECTION"),
                (2, "OBJECTS"),
                    (0, "LIGHTLIST"),
                    (5, "DEADBEEF"),
                    (90, 43),
                    (90, 1),
                    (5, "42"),
                    (1, "can-be-anything"),
                (0, "ENDSEC"),
                (0, "EOF")
            );
            var lightList = (DxfLightList)file.Objects.Last();
            Assert.Equal(new DxfHandle(0xDEADBEEF), ((IDxfItemInternal)lightList).Handle);
            Assert.Equal(43, lightList.Version);
            Assert.Equal("light-name", lightList.Lights.Single().Name);
        }

        [Fact]
        public void WriteLightListTest()
        {
            var file = new DxfFile();
            file.Clear();
            file.Header.Version = DxfAcadVersion.R2007;
            file.Entities.Add(new DxfLight() { Name = "light-name" });
            var lightList = new DxfLightList();
            lightList.Version = 42;
            lightList.Lights.Add((DxfLight)file.Entities.Single());
            file.Objects.Add(lightList);
            VerifyFileContains(file,
                (0, "LIGHTLIST"),
                (5, "#"),
                (100, "AcDbLightList"),
                (90, 42),
                (90, 1),
                (5, "#"),
                (1, "light-name")
            );
        }

        [Fact]
        public void ReadMaterialTest()
        {
            var material = (DxfMaterial)GenObject("MATERIAL",
                (75, 1),
                (43, 1.0),
                (43, 2.0),
                (43, 3.0),
                (43, 4.0),
                (43, 5.0),
                (43, 6.0),
                (43, 7.0),
                (43, 8.0),
                (43, 9.0),
                (43, 10.0),
                (43, 11.0),
                (43, 12.0),
                (43, 13.0),
                (43, 14.0),
                (43, 15.0),
                (43, 16.0),
                (75, 2),
                (43, 10.0),
                (43, 20.0),
                (43, 30.0),
                (43, 40.0),
                (43, 50.0),
                (43, 60.0),
                (43, 70.0),
                (43, 80.0),
                (43, 90.0),
                (43, 100.0),
                (43, 110.0),
                (43, 120.0),
                (43, 130.0),
                (43, 140.0),
                (43, 150.0),
                (43, 160.0)
            );
            Assert.Equal(DxfMapAutoTransformMethod.NoAutoTransform, material.DiffuseMapAutoTransformMethod);
            Assert.Equal(1.0, material.DiffuseMapTransformMatrix.M11);
            Assert.Equal(2.0, material.DiffuseMapTransformMatrix.M12);
            Assert.Equal(3.0, material.DiffuseMapTransformMatrix.M13);
            Assert.Equal(4.0, material.DiffuseMapTransformMatrix.M14);
            Assert.Equal(5.0, material.DiffuseMapTransformMatrix.M21);
            Assert.Equal(6.0, material.DiffuseMapTransformMatrix.M22);
            Assert.Equal(7.0, material.DiffuseMapTransformMatrix.M23);
            Assert.Equal(8.0, material.DiffuseMapTransformMatrix.M24);
            Assert.Equal(9.0, material.DiffuseMapTransformMatrix.M31);
            Assert.Equal(10.0, material.DiffuseMapTransformMatrix.M32);
            Assert.Equal(11.0, material.DiffuseMapTransformMatrix.M33);
            Assert.Equal(12.0, material.DiffuseMapTransformMatrix.M34);
            Assert.Equal(13.0, material.DiffuseMapTransformMatrix.M41);
            Assert.Equal(14.0, material.DiffuseMapTransformMatrix.M42);
            Assert.Equal(15.0, material.DiffuseMapTransformMatrix.M43);
            Assert.Equal(16.0, material.DiffuseMapTransformMatrix.M44);

            Assert.Equal(DxfMapAutoTransformMethod.ScaleToCurrentEntity, material.NormalMapAutoTransformMethod);
            Assert.Equal(10.0, material.NormalMapTransformMatrix.M11);
            Assert.Equal(20.0, material.NormalMapTransformMatrix.M12);
            Assert.Equal(30.0, material.NormalMapTransformMatrix.M13);
            Assert.Equal(40.0, material.NormalMapTransformMatrix.M14);
            Assert.Equal(50.0, material.NormalMapTransformMatrix.M21);
            Assert.Equal(60.0, material.NormalMapTransformMatrix.M22);
            Assert.Equal(70.0, material.NormalMapTransformMatrix.M23);
            Assert.Equal(80.0, material.NormalMapTransformMatrix.M24);
            Assert.Equal(90.0, material.NormalMapTransformMatrix.M31);
            Assert.Equal(100.0, material.NormalMapTransformMatrix.M32);
            Assert.Equal(110.0, material.NormalMapTransformMatrix.M33);
            Assert.Equal(120.0, material.NormalMapTransformMatrix.M34);
            Assert.Equal(130.0, material.NormalMapTransformMatrix.M41);
            Assert.Equal(140.0, material.NormalMapTransformMatrix.M42);
            Assert.Equal(150.0, material.NormalMapTransformMatrix.M43);
            Assert.Equal(160.0, material.NormalMapTransformMatrix.M44);
        }

        [Fact]
        public void ReadMentalRayRenderSettingsTest()
        {
            var mentalRay = (DxfMentalRayRenderSettings)GenObject("MENTALRAYRENDERSETTINGS",
                (100, "AcDbRenderSettings"),
                (90, 2),
                (1, ""),
                (290, 1),
                (290, 1),
                (290, 1),
                (290, 1),
                (1, ""),
                (1, ""),
                (90, 0),
                (100, "AcDbMentalRayRenderSettings"),
                (90, 2),
                (90, 0),
                (90, 1),
                (70, 2),
                (40, 1.0),
                (40, 1.0),
                (40, 0.5),
                (40, 0.5),
                (40, 0.5),
                (40, 0.5),
                (70, 0),
                (290, 0),
                (290, 1),
                (90, 5),
                (90, 5),
                (90, 5),
                (290, 0),
                (90, 500),
                (290, 0),
                (40, 1.0),
                (90, 10000),
                (90, 5),
                (90, 5),
                (90, 5),
                (290, 0),
                (90, 200),
                (290, 0),
                (290, 0),
                (290, 0),
                (40, 1.0),
                (40, 1.0),
                (40, 1500.0),
                (70, 0),
                (70, 0),
                (40, 10.0),
                (70, 0),
                (70, 0),
                (290, 0),
                (1, ""),
                (90, 32),
                (70, 0),
                (90, 1048),
                (290, 1), // these last two values aren't in the spec, but can appear in the file
                (40, 42.0));
            Assert.True(mentalRay._unknown_code_290);
            Assert.Equal(42.0, mentalRay._unknown_code_40);
        }

        [Fact]
        public void ReadRenderSettingsVersion1Test()
        {
            var mentalRay = (DxfMentalRayRenderSettings)GenObject("MENTALRAYRENDERSETTINGS",
                (100, "AcDbRenderSettings"),
                (90, 1), // render settings version 1
                (1, "render preset name"),
                (290, 1), // render materials flag
                (90, 2), // texture sampling quality
                (290, 1), // render back faces flag
                (290, 0), // render shadows flag
                (1, "preview image file name"),
                (100, "AcDbMentalRayRenderSettings"));
            var renderSettings = mentalRay.RenderSettings;
            Assert.Equal(1, renderSettings.Version);
            Assert.Equal("render preset name", renderSettings.PresetName);
            Assert.True(renderSettings.RenderMaterials);
            Assert.Equal(2, renderSettings.TextureSamplingQuality);
            Assert.True(renderSettings.RenderBackFaces);
            Assert.False(renderSettings.RenderShadows);
            Assert.Equal("preview image file name", renderSettings.PreviewImageFileName);
            Assert.Null(renderSettings.PresetDescription);
            Assert.Equal(0, renderSettings.DisplayIndex);
            Assert.False(renderSettings.IsPredefined);
        }

        [Fact]
        public void WriteRenderSettingsVersion1Test()
        {
            var mentalRay = new DxfMentalRayRenderSettings();
            mentalRay.RenderSettings.Version = 1;
            mentalRay.RenderSettings.PresetName = "render preset name";
            mentalRay.RenderSettings.RenderMaterials = true;
            mentalRay.RenderSettings.TextureSamplingQuality = 2;
            mentalRay.RenderSettings.RenderBackFaces = true;
            mentalRay.RenderSettings.RenderShadows = false;
            mentalRay.RenderSettings.PreviewImageFileName = "preview image file name";
            EnsureFileContainsObject(mentalRay,
                DxfAcadVersion.R2010,
                (100, "AcDbRenderSettings"),
                (90, 1), // render settings version 1
                (1, "render preset name"),
                (290, 1), // render materials flag
                (90, 2), // texture sampling quality
                (290, 1), // render back faces flag
                (290, 0), // render shadows flag
                (1, "preview image file name"),
                (100, "AcDbMentalRayRenderSettings"));
        }

        [Fact]
        public void ReadRenderSettingsVersion2Test()
        {
            var mentalRay = (DxfMentalRayRenderSettings)GenObject("MENTALRAYRENDERSETTINGS",
                (100, "AcDbRenderSettings"),
                (90, 2), // render settings version 2
                (1, "render preset name"),
                (290, 1), // render materials flag
                (290, 0), // texture sampling flag
                (290, 1), // render back faces flag
                (290, 0), // render shadows flag
                (1, "preview image file name"),
                (1, "render preset description"),
                (90, 2), // display index
                (290, 1), // is predefined
                (100, "AcDbMentalRayRenderSettings"));
            var renderSettings = mentalRay.RenderSettings;
            Assert.Equal(2, renderSettings.Version);
            Assert.Equal("render preset name", renderSettings.PresetName);
            Assert.True(renderSettings.RenderMaterials);
            Assert.Equal(0, renderSettings.TextureSamplingQuality);
            Assert.True(renderSettings.RenderBackFaces);
            Assert.False(renderSettings.RenderShadows);
            Assert.Equal("preview image file name", renderSettings.PreviewImageFileName);
            Assert.Equal("render preset description", renderSettings.PresetDescription);
            Assert.Equal(2, renderSettings.DisplayIndex);
            Assert.True(renderSettings.IsPredefined);
        }

        [Fact]
        public void WriteRenderSettingsVersion2Test()
        {
            var mentalRay = new DxfMentalRayRenderSettings();
            mentalRay.RenderSettings.Version = 2;
            mentalRay.RenderSettings.PresetName = "render preset name";
            mentalRay.RenderSettings.RenderMaterials = true;
            mentalRay.RenderSettings.TextureSamplingQuality = 2;
            mentalRay.RenderSettings.RenderBackFaces = true;
            mentalRay.RenderSettings.RenderShadows = false;
            mentalRay.RenderSettings.PreviewImageFileName = "preview image file name";
            mentalRay.RenderSettings.PresetDescription = "render preset description";
            mentalRay.RenderSettings.DisplayIndex = 2;
            mentalRay.RenderSettings.IsPredefined = true;
            EnsureFileContainsObject(mentalRay,
                DxfAcadVersion.R2010,
                (100, "AcDbRenderSettings"),
                (90, 2), // render settings version 2
                (1, "render preset name"),
                (290, 1), // render materials flag
                (290, 1), // texture sampling flag
                (290, 1), // render back faces flag
                (290, 0), // render shadows flag
                (1, "preview image file name"),
                (1, "render preset description"),
                (90, 2), // display index
                (290, 1), // is predefined
                (100, "AcDbMentalRayRenderSettings"));
        }

        [Fact]
        public void ReadMLineStyleTest()
        {
            var mlineStyle = (DxfMLineStyle)GenObject("MLINESTYLE",
                (2, "<name>"),
                (3, "<description>"),
                (62, 1),
                (51, 99.0),
                (52, 100.0),
                (71, 2),
                (49, 3.0),
                (62, 3),
                (49, 4.0),
                (62, 4),
                (6, "quatro")
            );
            Assert.Equal("<name>", mlineStyle.StyleName);
            Assert.Equal("<description>", mlineStyle.Description);
            Assert.Equal(1, mlineStyle.FillColor.RawValue);
            Assert.Equal(99.0, mlineStyle.StartAngle);
            Assert.Equal(100.0, mlineStyle.EndAngle);
            Assert.Equal(2, mlineStyle.Elements.Count);

            Assert.Equal(3.0, mlineStyle.Elements[0].Offset);
            Assert.Equal(3, mlineStyle.Elements[0].Color.RawValue);
            Assert.Null(mlineStyle.Elements[0].LineType);

            Assert.Equal(4.0, mlineStyle.Elements[1].Offset);
            Assert.Equal(4, mlineStyle.Elements[1].Color.RawValue);
            Assert.Equal("quatro", mlineStyle.Elements[1].LineType);
        }

        [Fact]
        public void WriteMLineStyleTest()
        {
            var mlineStyle = new DxfMLineStyle();
            mlineStyle.StyleName = "<name>";
            mlineStyle.Description = "<description>";
            mlineStyle.FillColor = DxfColor.FromRawValue(1);
            mlineStyle.StartAngle = 99.9;
            mlineStyle.EndAngle = 100.0;
            mlineStyle.Elements.Add(new DxfMLineStyle.DxfMLineStyleElement() { Offset = 3.0, Color = DxfColor.FromRawValue(3), LineType = "tres" });
            mlineStyle.Elements.Add(new DxfMLineStyle.DxfMLineStyleElement() { Offset = 4.0, Color = DxfColor.FromRawValue(4), LineType = "quatro" });
            var file = new DxfFile();
            file.Header.Version = DxfAcadVersion.R14;
            file.Objects.Add(mlineStyle);
            VerifyFileContains(file,
                (2, "<name>"),
                (70, 0),
                (3, "<description>"),
                (62, 1),
                (51, 99.9),
                (52, 100.0),
                (71, 2),
                (49, 3.0),
                (62, 3),
                (6, "tres"),
                (49, 4.0),
                (62, 4),
                (6, "quatro")
            );
        }

        [Fact]
        public void ReadSectionSettingsTest()
        {
            var settings = (DxfSectionSettings)GenObject("SECTIONSETTINGS",
                (5, "ABC"),
                (100, "AcDbSectionSettings"),
                (90, 42),
                (91, 1),
                (1, "SectionTypeSettings"),
                (90, 43),
                (91, 1),
                (92, 2),
                (330, "100"),
                (330, "101"),
                (331, "FF"),
                (1, "file-name"),
                (93, 2),
                (2, "SectionGeometrySettings"),
                (90, 1001),
                (91, 0),
                (92, 0),
                (63, 0),
                (8, ""),
                (6, ""),
                (40, 1.0),
                (1, ""),
                (370, 0),
                (70, 0),
                (71, 0),
                (72, 0),
                (2, ""),
                (41, 0.0),
                (42, 1.0),
                (43, 0.0),
                (3, "SectionGeometrySettingsEnd"),
                (90, 1002),
                (91, 0),
                (92, 0),
                (63, 0),
                (8, ""),
                (6, ""),
                (40, 1.0),
                (1, ""),
                (370, 0),
                (70, 0),
                (71, 0),
                (72, 0),
                (2, ""),
                (41, 0.0),
                (42, 1.0),
                (43, 0.0),
                (3, "SectionGeometrySettingsEnd"),
                (3, "SectionTypeSettingsEnd")
            );
            Assert.Equal(42, settings.SectionType);
            var typeSettings = settings.SectionTypeSettings.Single();
            Assert.Equal(43, typeSettings.SectionType);
            Assert.True(typeSettings.IsGenerationOption);
            Assert.Equal(new DxfHandle(0xFF), typeSettings.DestinationObjectHandle);
            Assert.Equal("file-name", typeSettings.DestinationFileName);

            Assert.Equal(2, typeSettings.SourceObjectHandles.Count);
            Assert.Equal(new DxfHandle(0x100), typeSettings.SourceObjectHandles[0]);
            Assert.Equal(new DxfHandle(0x101), typeSettings.SourceObjectHandles[1]);

            Assert.Equal(2, typeSettings.GeometrySettings.Count);
            Assert.Equal(1001, typeSettings.GeometrySettings[0].SectionType);
            Assert.Equal(1002, typeSettings.GeometrySettings[1].SectionType);
        }

        [Fact]
        public void WriteSectionSettingsTest()
        {
            var settings = new DxfSectionSettings();
            settings.SectionType = 42;
            var typeSettings = new DxfSectionTypeSettings()
            {
                SectionType = 43,
                IsGenerationOption = true,
                DestinationObjectHandle = new DxfHandle(0xFF),
                DestinationFileName = "file-name",
            };
            typeSettings.SourceObjectHandles.Add(new DxfHandle(0x100));
            typeSettings.SourceObjectHandles.Add(new DxfHandle(0x101));
            typeSettings.GeometrySettings.Add(new DxfSectionGeometrySettings() { SectionType = 1001 });
            typeSettings.GeometrySettings.Add(new DxfSectionGeometrySettings() { SectionType = 1002 });
            settings.SectionTypeSettings.Add(typeSettings);
            var file = new DxfFile();
            file.Header.Version = DxfAcadVersion.R2007;
            file.Objects.Add(settings);
            VerifyFileContains(file,
                (100, "AcDbSectionSettings"),
                (90, 42),
                (91, 1),
                (1, "SectionTypeSettings"),
                (90, 43),
                (91, 1),
                (92, 2),
                (330, "100"),
                (330, "101"),
                (331, "FF"),
                (1, "file-name"),
                (93, 2),
                (2, "SectionGeometrySettings"),
                (90, 1001),
                (91, 0),
                (92, 0),
                (63, 0),
                (8, ""),
                (6, ""),
                (40, 1.0),
                (1, ""),
                (370, 0),
                (70, 0),
                (71, 0),
                (72, 0),
                (2, ""),
                (41, 0.0),
                (42, 1.0),
                (43, 0.0),
                (3, "SectionGeometrySettingsEnd"),
                (90, 1002),
                (91, 0),
                (92, 0),
                (63, 0),
                (8, ""),
                (6, ""),
                (40, 1.0),
                (1, ""),
                (370, 0),
                (70, 0),
                (71, 0),
                (72, 0),
                (2, ""),
                (41, 0.0),
                (42, 1.0),
                (43, 0.0),
                (3, "SectionGeometrySettingsEnd"),
                (3, "SectionTypeSettingsEnd")
            );
        }

        [Fact]
        public void ReadSortentsTableTest()
        {
            var file = Parse(
                (0, "SECTION"),
                (2, "ENTITIES"),
                (0, "POINT"),
                (5, "42"),
                (10, 1.0),
                (20, 2.0),
                (30, 3.0),
                (0, "POINT"),
                (5, "43"),
                (10, 4.0),
                (20, 5.0),
                (30, 6.0),
                (0, "ENDSEC"),
                (0, "SECTION"),
                (2, "OBJECTS"),
                (0, "SORTENTSTABLE"),
                (331, "42"),
                (5, "43"),
                (0, "ENDSEC"),
                (0, "EOF")
            );
            var sortents = (DxfSortentsTable)file.Objects.Last();
            Assert.Equal(new DxfPoint(1, 2, 3), ((DxfModelPoint)sortents.Entities.Single()).Location);
            Assert.Equal(new DxfPoint(4, 5, 6), ((DxfModelPoint)sortents.SortItems.Single()).Location);
        }

        [Fact]
        public void WriteSortentsTableTest()
        {
            var file = new DxfFile();
            file.Clear();
            file.Header.Version = DxfAcadVersion.R14;
            file.Entities.Add(new DxfModelPoint(new DxfPoint(1, 2, 3)));
            file.Entities.Add(new DxfModelPoint(new DxfPoint(4, 5, 6)));
            var sortents = new DxfSortentsTable();
            sortents.Entities.Add(file.Entities.First());
            sortents.SortItems.Add(file.Entities.Skip(1).First());
            file.Objects.Add(sortents);
            VerifyFileContains(file,
                (0, "POINT"),
                (5, "#")
            );
            VerifyFileContains(file,
                (0, "SORTENTSTABLE"),
                (5, "#"),
                (100, "AcDbSortentsTable"),
                (331, "#"),
                (5, "#")
            );
        }

        [Fact]
        public void ReadSpatialIndexTest()
        {
            var si = (DxfSpatialIndex)GenObject("SPATIAL_INDEX",
                (100, "AcDbIndex"),
                (40, 2451544.91568287), // from Autodesk spec: 2451544.91568287 = December 31, 1999, 9:58:35 pm.
                (100, "AcDbSpatialIndex"),
                (40, 1.0),
                (40, 2.0),
                (40, 3.0)
            );
            Assert.Equal(new DateTime(1999, 12, 31, 21, 58, 35), si.Timestamp);
            AssertArrayEqual(new[] { 1.0, 2.0, 3.0 }, si.Values.ToArray());
        }

        [Fact]
        public void WriteSpatialIndexTest()
        {
            var si = new DxfSpatialIndex();
            si.Timestamp = new DateTime(1999, 12, 31, 21, 58, 35);
            si.Values.Add(1.0);
            si.Values.Add(2.0);
            si.Values.Add(3.0);
            EnsureFileContainsObject(si, DxfAcadVersion.R2000,
                (100, "AcDbIndex"),
                (40, 2451544.9156828704), // from Autodesk spec: 2451544.91568287(04) = December 31, 1999, 9:58:35 pm.
                (100, "AcDbSpatialIndex"),
                (40, 1.0),
                (40, 2.0),
                (40, 3.0)
            );
        }

        [Fact]
        public void ReadSunStudyTest()
        {
            // with subset
            var sun = (DxfSunStudy)GenObject("SUNSTUDY",
                (290, 1),
                (73, 3),
                (290, 42),
                (290, 43),
                (290, 44)
            );
            Assert.True(sun.UseSubset);
            Assert.Equal(3, sun.Hours.Count);
            Assert.Equal(42, sun.Hours[0]);
            Assert.Equal(43, sun.Hours[1]);
            Assert.Equal(44, sun.Hours[2]);

            // without subset
            sun = (DxfSunStudy)GenObject("SUNSTUDY",
                (73, 3),
                (290, 42),
                (290, 43),
                (290, 44)
            );
            Assert.False(sun.UseSubset);
            Assert.Equal(3, sun.Hours.Count);
            Assert.Equal(42, sun.Hours[0]);
            Assert.Equal(43, sun.Hours[1]);
            Assert.Equal(44, sun.Hours[2]);
        }

        [Fact]
        public void WriteSunStudyTest()
        {
            var sun = new DxfSunStudy();
            sun.Hours.Add(42);
            sun.Hours.Add(43);
            sun.Hours.Add(44);
            var file = new DxfFile();
            file.Header.Version = DxfAcadVersion.R2013;
            file.Objects.Add(sun);
            VerifyFileContains(file,
                (0, "SUNSTUDY"),
                (5, "#"),
                (100, "AcDbSunStudy"),
                (90, 0),
                (1, ""),
                (2, ""),
                (70, 0),
                (3, ""),
                (290, false),
                (4, ""),
                (291, false),
                (91, 0),
                (292, false),
                (73, 3),
                (290, 42),
                (290, 43),
                (290, 44),
                (74, 0),
                (75, 0),
                (76, 0),
                (77, 0),
                (40, 0.0),
                (293, false),
                (294, false)
            );

            // verify writing as binary doesn't crash
            using (var ms = new MemoryStream())
            {
                file.Save(ms, asText: false);
            }
        }

        [Fact]
        public void ReadTableStyleTest()
        {
            var table = (DxfTableStyle)GenObject("TABLESTYLE",
                (5, "123"),
                (100, "AcDbTableStyle"),
                (280, 0),
                (3, ""),
                (70, 0),
                (71, 0),
                (40, 0.06),
                (41, 0.06),
                (280, 0),
                (281, 0),
                (7, "one"),
                (140, 0.0),
                (170, 0),
                (62, 0),
                (63, 7),
                (283, 0),
                (90, 0),
                (91, 0),
                (274, 0),
                (275, 0),
                (276, 0),
                (277, 0),
                (278, 0),
                (279, 0),
                (284, 1),
                (285, 1),
                (286, 1),
                (287, 1),
                (288, 1),
                (289, 1),
                (64, 0),
                (65, 0),
                (66, 0),
                (67, 0),
                (68, 0),
                (69, 0),
                (7, "two"),
                (140, 0.0),
                (170, 0),
                (62, 0),
                (63, 7),
                (283, 0),
                (90, 0),
                (91, 0),
                (274, 0),
                (275, 0),
                (276, 0),
                (277, 0),
                (278, 0),
                (279, 0),
                (284, 1),
                (285, 1),
                (286, 1),
                (287, 1),
                (288, 1),
                (289, 1),
                (64, 0),
                (65, 0),
                (66, 0),
                (67, 0),
                (68, 0),
                (69, 0)
            );
            Assert.Equal(2, table.CellStyles.Count);
            Assert.Equal("one", table.CellStyles[0].Name);
            Assert.Equal("two", table.CellStyles[1].Name);
        }

        [Fact]
        public void WriteTableStyleTest()
        {
            var table = new DxfTableStyle();
            table.CellStyles.Add(new DxfTableCellStyle() { Name = "one" });
            table.CellStyles.Add(new DxfTableCellStyle() { Name = "two" });
            var file = new DxfFile();
            file.Header.Version = DxfAcadVersion.R2004;
            file.Objects.Add(table);
            VerifyFileContains(file,
                (0, "TABLESTYLE"),
                (5, "#"),
                (100, "AcDbTableStyle"),
                (3, ""),
                (70, 0),
                (71, 0),
                (40, 0.06),
                (41, 0.06),
                (280, 0),
                (281, 0),
                (7, "one"),
                (140, 0.0),
                (170, 0),
                (62, 0),
                (63, 7),
                (283, 0),
                (90, 0),
                (91, 0),
                (274, 0),
                (275, 0),
                (276, 0),
                (277, 0),
                (278, 0),
                (279, 0),
                (284, 1),
                (285, 1),
                (286, 1),
                (287, 1),
                (288, 1),
                (289, 1),
                (64, 0),
                (65, 0),
                (66, 0),
                (67, 0),
                (68, 0),
                (69, 0),
                (7, "two"),
                (140, 0.0),
                (170, 0),
                (62, 0),
                (63, 7),
                (283, 0),
                (90, 0),
                (91, 0),
                (274, 0),
                (275, 0),
                (276, 0),
                (277, 0),
                (278, 0),
                (279, 0),
                (284, 1),
                (285, 1),
                (286, 1),
                (287, 1),
                (288, 1),
                (289, 1),
                (64, 0),
                (65, 0),
                (66, 0),
                (67, 0),
                (68, 0),
                (69, 0)
            );
        }

        [Fact]
        public void ReadObjectWithUnterminatedXData()
        {
            // dictionary value with XDATA
            var file = Section("OBJECTS",
                (0, "ACDBPLACEHOLDER"),
                (1001, "IxMilia.Dxf"),
                (0, "ACDBPLACEHOLDER"),
                (1001, "IxMilia.Dxf")
            );
            Assert.Equal(2, file.Objects.Count);
            Assert.IsType<DxfPlaceHolder>(file.Objects[0]);
            Assert.IsType<DxfPlaceHolder>(file.Objects[1]);
        }

        [Fact]
        public void ReadXRecordWithMultipleXDataTest1()
        {
            var xrecord = (DxfXRecordObject)GenObject("XRECORD",
                (102, "{ACAD_REACTORS_1"),
                (330, "111"),
                (102, "}"),
                (102, "{ACAD_REACTORS_2"),
                (330, "222"),
                (102, "}"),
                (102, "{ACAD_REACTORS_3"),
                (330, "333"),
                (102, "}"),
                (100, "AcDbXrecord"),
                (280, 1),
                (102, "VTR_0.000_0.000_1.000_1.000_VISUALSTYLE"),
                (340, "195"),
                (102, "VTR_0.000_0.000_1.000_1.000_GRIDDISPLAY"),
                (70, 3),
                (102, "VTR_0.000_0.000_1.000_1.000_GRIDMAJOR"),
                (70, 5),
                (102, "VTR_0.000_0.000_1.000_1.000_DEFAULTLIGHTING"),
                (280, 1),
                (102, "VTR_0.000_0.000_1.000_1.000_DEFAULTLIGHTINGTYPE"),
                (70, 1),
                (102, "VTR_0.000_0.000_1.000_1.000_BRIGHTNESS"),
                (141, 0.0),
                (102, "VTR_0.000_0.000_1.000_1.000_CONTRAST"),
                (142, 0.0)
            );
            Assert.Equal(3, xrecord.ExtensionDataGroups.Count);
            Assert.Equal("ACAD_REACTORS_1", xrecord.ExtensionDataGroups[0].GroupName);
            Assert.Equal("ACAD_REACTORS_2", xrecord.ExtensionDataGroups[1].GroupName);
            Assert.Equal("ACAD_REACTORS_3", xrecord.ExtensionDataGroups[2].GroupName);
            Assert.Equal(DxfDictionaryDuplicateRecordHandling.KeepExisting, xrecord.DuplicateRecordHandling);
            Assert.Equal(14, xrecord.DataPairs.Count);
            Assert.Equal(102, xrecord.DataPairs[0].Code);
            Assert.Equal("VTR_0.000_0.000_1.000_1.000_VISUALSTYLE", xrecord.DataPairs[0].StringValue);
        }

        [Fact]
        public void ReadXRecordWithMultipleXDataTest2()
        {
            // reads an XRECORD object that hasn't specified it's 280 code pair for duplicate record handling
            var xrecord = (DxfXRecordObject)GenObject("XRECORD",
                (100, "AcDbXrecord"),
                (102, "VTR_0.000_0.000_1.000_1.000_VISUALSTYLE"),
                (340, "195"),
                (102, "VTR_0.000_0.000_1.000_1.000_GRIDDISPLAY"),
                (70, 3),
                (102, "VTR_0.000_0.000_1.000_1.000_GRIDMAJOR"),
                (70, 5),
                (102, "VTR_0.000_0.000_1.000_1.000_DEFAULTLIGHTING"),
                (280, 1),
                (102, "VTR_0.000_0.000_1.000_1.000_DEFAULTLIGHTINGTYPE"),
                (70, 1),
                (102, "VTR_0.000_0.000_1.000_1.000_BRIGHTNESS"),
                (141, 0.0),
                (102, "VTR_0.000_0.000_1.000_1.000_CONTRAST"),
                (142, 0.0)
            );
            Assert.Equal(0, xrecord.ExtensionDataGroups.Count);
            Assert.Equal(14, xrecord.DataPairs.Count);
            Assert.Equal(102, xrecord.DataPairs[6].Code);
            Assert.Equal("VTR_0.000_0.000_1.000_1.000_DEFAULTLIGHTING", xrecord.DataPairs[6].StringValue);
            Assert.Equal(1, xrecord.DataPairs[7].ShortValue);
        }

        [Fact]
        public void ReadDictionaryWithEveryEntityTest()
        {
            // read a dictionary with a reference to every entity
            var assembly = typeof(DxfFile).GetTypeInfo().Assembly;
            foreach (var type in assembly.GetTypes())
            {
                // dimensions are hard
                if (IsDxfEntityOrDerived(type) && type.GetTypeInfo().BaseType != typeof(DxfDimensionBase))
                {
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        var ent = (DxfEntity)ctor.Invoke(new object[0]);
                        var file = Parse(
                            (0, "SECTION"),
                            (2, "ENTITIES"),
                            (0, ent.EntityTypeString),
                            (5, "AAAA"),
                            (0, "ENDSEC"),
                            (0, "SECTION"),
                            (2, "OBJECTS"),
                            (0, "DICTIONARY"),
                            (3, "the-entity"),
                            (350, "AAAA"),
                            (0, "ENDSEC"),
                            (0, "EOF")
                        );
                        var dict = (DxfDictionary)file.Objects.Single();
                        var parsedEntity = dict["the-entity"] as DxfEntity;
                        Assert.Equal(type, parsedEntity.GetType());
                    }
                }
            }
        }

        [Fact]
        public void WriteAllDefaultObjectsTest()
        {
            var file = new DxfFile();
            var assembly = typeof(DxfFile).GetTypeInfo().Assembly;
            foreach (var type in assembly.GetTypes())
            {
                if (IsDxfObjectOrDerived(type))
                {
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        // add the object with its default initialized values
                        var obj = (DxfObject)ctor.Invoke(new object[0]);
                        file.Objects.Add(obj);

                        // add the entity with its default values and 2 items added to each List<T> collection
                        obj = (DxfObject)ctor.Invoke(new object[0]);
                        foreach (var property in type.GetProperties().Where(p => IsListOfT(p.PropertyType)))
                        {
                            var itemType = property.PropertyType.GenericTypeArguments.Single();
                            var itemValue = itemType.GetTypeInfo().IsValueType
                                ? Activator.CreateInstance(itemType)
                                : null;
                            var addMethod = property.PropertyType.GetMethod("Add");
                            var propertyValue = property.GetValue(obj);
                            addMethod.Invoke(propertyValue, new object[] { itemValue });
                            addMethod.Invoke(propertyValue, new object[] { itemValue });
                        }

                        // add an object with all non-indexer properties set to `default(T)`
                        obj = (DxfObject)SetAllPropertiesToDefault(ctor.Invoke(new object[0]));
                        file.Objects.Add(obj);
                    }
                }
            }

            // write each version of the objects with default versions
            foreach (var version in new[] { DxfAcadVersion.R10, DxfAcadVersion.R11, DxfAcadVersion.R12, DxfAcadVersion.R13, DxfAcadVersion.R14, DxfAcadVersion.R2000, DxfAcadVersion.R2004, DxfAcadVersion.R2007, DxfAcadVersion.R2010, DxfAcadVersion.R2013 })
            {
                file.Header.Version = version;
                using (var ms = new MemoryStream())
                {
                    file.Save(ms);
                }
            }
        }

        private static bool IsDxfEntityOrDerived(Type type)
        {
            return IsTypeOrDerived(type, typeof(DxfEntity));
        }

        private static bool IsDxfObjectOrDerived(Type type)
        {
            return IsTypeOrDerived(type, typeof(DxfObject));
        }

        private static bool IsTypeOrDerived(Type type, Type expectedType)
        {
            if (type == expectedType)
            {
                return true;
            }

            if (type.GetTypeInfo().BaseType != null)
            {
                return IsTypeOrDerived(type.GetTypeInfo().BaseType, expectedType);
            }

            return false;
        }
    }
}
