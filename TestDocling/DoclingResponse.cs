using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text.Json.Serialization;

namespace TestDocling;
public class DoclingResponse
{
    [JsonPropertyName("document")]
    public Document Document { get; set; }

    [JsonPropertyName("processing_time")]
    public double process_time { get; set; }
}

public class Document
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; }

    [JsonPropertyName("md_content")]
    public string MdContent { get; set; }

    [JsonPropertyName("json_content")]
    public JsonContent DoclingJsonContent { get; set; }
}

public class JsonContent
{
    [JsonPropertyName("pages")]
    public Dictionary<string, PageDetail> Pages { get; set; }

    [JsonPropertyName("body")]
    public DoclingBody Body { get; set; }

    [JsonPropertyName("groups")]
    public List<GroupItem>? Groups { get; set; }

    [JsonPropertyName("texts")]
    public List<TextItem>? Texts { get; set; }

    [JsonPropertyName("tables")]
    public List<TableItem>? Tables { get; set; }

    [JsonPropertyName("pictures")] 
    public List<PictureItem>? Pictures { get; set; }
}
public class DoclingBody
{
    [JsonPropertyName("self_ref")]
    public string SelfRef { get; set; }

    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    [JsonPropertyName("children")]
    public List<DoclingRef>? Children { get; set; }

    [JsonPropertyName("content_layer")]
    public string? ContentLayer { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}
public class GroupItem
{
    [JsonPropertyName("self_ref")]
    public string SelfRef { get; set; }

    [JsonPropertyName("parent")]
    public DoclingRef? Parent { get; set; }

    [JsonPropertyName("children")]
    public List<DoclingRef>? Children { get; set; }

    [JsonPropertyName("content_layer")]
    public string? ContentLayer { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}
public class TextItem
{
    [JsonPropertyName("self_ref")]
    public string SelfRef { get; set; }

    [JsonPropertyName("parent")]
    public DoclingRef? Parent { get; set; }

    [JsonPropertyName("children")]
    public List<DoclingRef>? Children { get; set; }

    [JsonPropertyName("content_layer")]
    public string? ContentLayer { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("prov")]
    public List<ProvItem>? Prov { get; set; } = new List<ProvItem>();

    [JsonPropertyName("orig")]
    public string? Orig { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("hyperlink")]
    public string? Hyperlink { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("enumerated")]
    public bool? Enumerated { get; set; }

    [JsonPropertyName("marker")]
    public string? Marker { get; set; }
}
public class ProvItem
{
    [JsonPropertyName("page_no")]
    public int? PageNo { get; set; }

    [JsonPropertyName("bbox")]
    public BoundingBox? Bbox { get; set; }

    [JsonPropertyName("charspan")]
    public List<int>? Charspan { get; set; } = new List<int>();
}
public class TableItem
{
    [JsonPropertyName("self_ref")]
    public string SelfRef { get; set; }

    [JsonPropertyName("parent")]
    public DoclingRef? Parent { get; set; }

    [JsonPropertyName("children")]
    public List<DoclingRef>? Children { get; set; }

    [JsonPropertyName("content_layer")]
    public string? ContentLayer { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("prov")]
    public List<ProvItem>? Prov { get; set; } = new List<ProvItem>();

    [JsonPropertyName("data")]
    public TableData? Data { get; set; }
}
public class PictureItem
{
    [JsonPropertyName("self_ref")]
    public string SelfRef { get; set; }

    [JsonPropertyName("parent")]
    public DoclingRef? Parent { get; set; }

    [JsonPropertyName("children")]
    public List<DoclingRef>? Children { get; set; }

    [JsonPropertyName("content_layer")]
    public string? ContentLayer { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("prov")]
    public List<ProvItem>? Prov { get; set; } = new List<ProvItem>();

    [JsonPropertyName("image")]
    public ImageRef? Image { get; set; }
}


public class TableData
{
    [JsonPropertyName("bbox")]
    public BoundingBox? Bbox { get; set; }

    [JsonPropertyName("table_cells")]
    public List<TableCell>? TableCells { get; set; }

    [JsonPropertyName("num_rows")]
    public int? NumRows { get; set; }

    [JsonPropertyName("num_cols")]
    public int? NumCols { get; set; }

    [JsonPropertyName("grid")]
    public List<List<GridItem>>? Grid { get; set; } = new List<List<GridItem>>();
}
public class TableCell
{
    [JsonPropertyName("row_span")]
    public int? RowSpan { get; set; }

    [JsonPropertyName("col_span")]
    public int? ColSpan { get; set; }

    [JsonPropertyName("start_row_offset_idx")]
    public int? StartRowOffsetIDX { get; set; }

    [JsonPropertyName("start_col_offset_idx")]
    public int? StartColOffsetIDX { get; set; }

    [JsonPropertyName("end_row_offset_idx")]
    public int? EndRowOffsetIDX { get; set; }

    [JsonPropertyName("end_col_offset_idx")]
    public int? EndColOffsetIDX { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("column_header")]
    public bool? ColumnHeader { get; set; }

    [JsonPropertyName("row_header")]
    public bool? RowHeader { get; set; }

    [JsonPropertyName("row_section")]
    public bool? RowSection { get; set; }
}
public class GridItem
{
    [JsonPropertyName("row_span")]
    public int? RowSpan { get; set; }

    [JsonPropertyName("col_span")]
    public int? ColSpan { get; set; }

    [JsonPropertyName("start_row_offset_idx")]
    public int? StartRowOffsetIDX { get; set; }

    [JsonPropertyName("start_col_offset_idx")]
    public int? StartColOffsetIDX { get; set; }

    [JsonPropertyName("end_row_offset_idx")]
    public int? EndRowOffsetIDX { get; set; }

    [JsonPropertyName("end_col_offset_idx")]
    public int? EndColOffsetIDX { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("column_header")]
    public bool? ColumnHeader { get; set; }

    [JsonPropertyName("row_header")]
    public bool? RowHeader { get; set; }

    [JsonPropertyName("row_section")]
    public bool? RowSection { get; set; }
}

public class BoundingBox
{
    [JsonPropertyName("l")]
    public double? L { get; set; }
    [JsonPropertyName("t")]
    public double? T { get; set; }
    [JsonPropertyName("r")]
    public double? R { get; set; }
    [JsonPropertyName("b")]
    public double? B { get; set; }
    [JsonPropertyName("coord_origin")]
    public string? CoordOrigin { get; set; }
}


public class DoclingRef
{
    [JsonPropertyName("$ref")]
    public string? Ref { get; set; }
}

public class PageDetail
{
    [JsonPropertyName("page_no")]
    public int? PageNo { get; set; }

    [JsonPropertyName("image")]
    public ImageRef? Image { get; set; }
}
public class ImageRef
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("mimetype")]
    public string? MimeType { get; set; }
}
