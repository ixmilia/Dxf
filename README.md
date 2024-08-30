﻿IxMilia.Dxf
===========

A .NET library for reading and writing DXF and DXB files.  Clone and
build locally or directly consume the
[NuGet package](http://www.nuget.org/packages/IxMilia.Dxf/).

## Usage

Open a DXF file:

``` C#
using System.IO;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
// ...

DxfFile dxfFile = DxfFile.Load(@"C:\Path\To\File.dxf");

foreach (DxfEntity entity in dxfFile.Entities)
{
    switch (entity.EntityType)
    {
        case DxfEntityType.Line:
            DxfLine line = (DxfLine)entity;
            // ...
            break;
        // ...
    }
}
```

Save a DXF file:

``` C#
using System.IO;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
// ...

DxfFile dxfFile = new DxfFile();
dxfFile.Entities.Add(new DxfLine(new DxfPoint(0, 0, 0), new DxfPoint(50, 50, 0)));
// ...

dxfFile.Save(@"C:\Path\To\File.dxf");
```

## Compatibility

### Reading Files

This library should be able to open any valid DXF file, including files produced by AutoCAD or anything using the
ODA libraries from the [Open Design Alliance](https://opendesign.com), including Microsoft Visio which uses older
Open Design libraries.

### Open Design Alliance (ODA)

The ODA libraries should be able to open anything produced by this library.

### AutoCAD

AutoCAD is rather fussy with what it will accept as valid DXF, even though the official spec is rather loose.  If you
use this library to write a file that AutoCAD can't open, file an issue with the drawing (or a sample) that was
produced by this library.  I've found that AutoCAD compatibility can be greatly improved by doing the following:

``` C#
// assuming `dxfFile` is a valid `DxfFile` object
dxfFile.Header.SetDefaults();
dxfFile.ViewPorts.Clear();
dxfFile.Save(...);
```

There are also some entity types that AutoCAD might not open when written by this library, specifically:

- 3DSOLID (`Dxf3DSolid`)
- ACAD_PROXY_ENTITY (`DxfProxyEntity`)
- ATTRIB (`DxfAttribute`)
- ATTDEF (`DxfAttributeDefinition`)
- BODY (`DxfBody`)
- DIMENSION (`DxfAlignedDimension`, `DxfAngularThreePointDimension`, `DxfDiameterDimension`, `DxfOrdinateDimension`,
  `DxfRadialDimension`, `DxfRotatedDimension`)
- HELIX (`DxfHelix`)
- LIGHT (`DxfLight`)
- MTEXT (`DxfMText`)
- REGION (`DxfRegion`)
- SHAPE (`DxfShape`)
- TOLERANCE (`DxfTolerance`)

And the following entities might not open in AutoCAD if written with missing information, e.g., a LEADER (`DxfLeader`)
requires at least 2 vertices.

- INSERT (`DxfInsert`)
- LEADER (`DxfLeader`)
- MLINE (`DxfMLine`)
- DGNUNDERLAY (`DxfDgnUnderlay`)
- DWFUNDERLAY (`DxfDwfUnderlay`)
- PDFUNDERLAY (`DxfPdfUnderlay`)
- SPLINE (`DxfSpline`)
- VERTEX (`DxfVertex`)

Also note that AutoCAD doesn't seem to like R13 files written by IxMilia.  For the greatest chance of compatibility,
save the file as either R12 or the newest version possible (e.g., R2013 or R2010.)

## Status

Support for DXF files is complete from versions R10 through R2014 _EXCEPT_ for the following entities:
- MESH
- MLEADER
- SURFACE
- TABLE
- VIEWPORT

## Building locally

To build locally, install the [latest .NET 8.0 SDK](https://dotnet.microsoft.com/download).

## DXF Reference

Since I don't want to fall afoul of Autodesk's lawyers, this repo can't include the actual DXF documentation.  It can,
however contain links to the official documents that I've been able to scrape together.  For most scenarios the latest
documentation should suffice, but all other versions are included here for backwards compatibility and reference
between versions.

[2020](http://help.autodesk.com/cloudhelp/2020/ENU/AutoCAD-DXF/files/index.htm)

[2019](http://help.autodesk.com/cloudhelp/2019/ENU/AutoCAD-DXF/files/index.htm)

[2018](http://help.autodesk.com/cloudhelp/2018/ENU/AutoCAD-DXF/files/index.htm)

[2017](http://help.autodesk.com/cloudhelp/2017/ENU/AutoCAD-DXF/files/index.htm)

[2016](http://help.autodesk.com/cloudhelp/2016/ENU/AutoCAD-DXF/files/index.htm)

[2015](http://help.autodesk.com/cloudhelp/2015/ENU/AutoCAD-DXF/files/index.htm)

[2014](http://images.autodesk.com/adsk/files/autocad_2014_pdf_dxf_reference_enu.pdf)

[2013](http://images.autodesk.com/adsk/files/autocad_2013_pdf_dxf_reference_enu.pdf)

[2012](http://images.autodesk.com/adsk/files/autocad_2012_pdf_dxf-reference_enu.pdf)

[2011](http://images.autodesk.com/adsk/files/acad_dxf2.pdf)

[2010](http://images.autodesk.com/adsk/files/acad_dxf1.pdf)

[2009](http://images.autodesk.com/adsk/files/acad_dxf.pdf)

[2008](http://images.autodesk.com/adsk/files/acad_dxf0.pdf)

2007 (Autodesk's link erroneously points to the R2008 documentation)

[2006](http://images.autodesk.com/adsk/files/dxf_format.pdf)

[2005](http://download.autodesk.com/prodsupp/downloads/acad_dxf.pdf)

[2004](http://download.autodesk.com/prodsupp/downloads/dxf.pdf)

[2002](http://www.autodesk.com/techpubs/autocad/dxf/dxf2002.pdf)

[2000](http://www.autodesk.com/techpubs/autocad/acad2000/dxf/index.htm)

[R14](http://www.autodesk.com/techpubs/autocad/acadr14/dxf/index.htm)

[R13 (self-extracting 16-bit executable)](http://www.autodesk.com/techpubs/autocad/dxf/dxf13_hlp.exe)

[R12 (non-Autodesk source)](http://www.martinreddy.net/gfx/3d/DXF12.spec)

[R11 (differences between R10 and R11)](http://autodesk.blogs.com/between_the_lines/ACAD_R11.html)

[R10 (non-Autodesk source)](http://www.martinreddy.net/gfx/3d/DXF10.spec)

The 2015-2020 spec can be downloaded for offline use via the [ReferenceCollector](src/IxMilia.Dxf.ReferenceCollector/README.md).

Many earlier links were compiled from an archive.org [May 9, 2013 snapshot](https://web.archive.org/web/20130509144333/http://usa.autodesk.com/adsk/servlet/item?siteID=123112&id=12272454&linkID=10809853)
